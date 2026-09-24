using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dps.SoulRegistry.Contracts;
using Npgsql;

namespace Dps.SoulRegistry;

// This port has no production composition. The external evidence verifier,
// protected signing key and release trust root are deliberately absent.
internal interface ISoulAliasEvidenceAuthority
{
    Task<VerifiedAliasEvidence> AuthenticateAndVerifyAsync(
        SoulAliasVerificationRequestV1 request, CancellationToken cancellationToken);
}

internal sealed record VerifiedAliasEvidence(
    string RequestSha256,
    string RawAlias,
    string? TargetSoulId,
    string? TargetControlSha256);

internal sealed record SoulAuthorityBindings(
    ISoulAliasEvidenceAuthority Evidence,
    ECDsa VerificationKey,
    Func<byte[], byte[]> Sign,
    string KeyId,
    long TrustEpoch,
    long AliasKeyEpoch,
    string ReleaseBomSha256,
    long ReleaseGeneration,
    TimeProvider Clock);

internal sealed class SoulCommitUnknownException() : SoulRegistryException(
    "The Soul authority transaction outcome is unknown; reconcile the exact idempotency key before retrying.");

/// <summary>
/// Soul-owner decision core. Internal construction keeps production routing closed
/// until a separate trusted issuer and fixed composition root are supplied.
/// </summary>
internal sealed class SoulIdentityAuthority : ISoulAliasClientV1
{
    private readonly PostgresSoulRegistry _registry;
    private readonly SoulAuthorityBindings _bindings;
    private string Schema => _registry.Options.SchemaName;

    internal SoulIdentityAuthority(PostgresSoulRegistry registry, SoulAuthorityBindings bindings)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        if (bindings.Evidence is null || bindings.VerificationKey is null || bindings.Sign is null || bindings.Clock is null ||
            string.IsNullOrWhiteSpace(bindings.KeyId) || bindings.TrustEpoch < 1 || bindings.AliasKeyEpoch < 1 ||
            bindings.ReleaseGeneration < 1 || bindings.ReleaseBomSha256.Length != 64 ||
            bindings.ReleaseBomSha256.AsSpan().ContainsAnyExcept("0123456789abcdef"))
            throw new ArgumentException("A fixed Soul authority trust binding is required.", nameof(bindings));
    }

    public async Task<SoulAliasVerificationV1> VerifyAliasAsync(
        SoulAliasVerificationRequestV1 request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.ToUtf8Json();
        var evidence = await _bindings.Evidence.AuthenticateAndVerifyAsync(request, cancellationToken);
        if (evidence is null || !SecretComparison.EqualsHex(evidence.RequestSha256, request.ComputeSha256()) ||
            evidence.TargetSoulId != request.TargetSoulId ||
            (request.TargetSoulId is not null && !IsDigest(evidence.TargetControlSha256)) ||
            (request.TargetSoulId is null && evidence.TargetControlSha256 is not null))
            throw new SoulRegistryException("The alias evidence is not bound to the authenticated request.");

        var kind = ParseKind(request.AliasKind);
        var references = _registry.Digester.AllReferences(request.TenantRef, kind, evidence.RawAlias);
        var current = _registry.Digester.CurrentReference(request.TenantRef, kind, evidence.RawAlias);
        var requestSha = request.ComputeSha256();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockIdempotencyAsync(connection, transaction, request.TenantRef, request.IdempotencyKey, cancellationToken);
        await PostgresSoulRegistry.AcquireLockAsync(connection, transaction,
            $"soul-identity-nonce:{request.TenantRef}:{request.Nonce}", cancellationToken);
        var existing = await ReadVerificationAsync(connection, transaction, request.TenantRef,
            request.Nonce, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!SecretComparison.EqualsHex(existing.RequestSha256, requestSha) ||
                existing.Nonce != request.Nonce || existing.IdempotencyKey != request.IdempotencyKey)
                throw new IdempotencyConflictException();
            var replay = SoulAliasVerificationV1.FromUtf8Json(existing.ProofWire);
            ValidateProof(replay);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        await PostgresSoulRegistry.AcquireAliasLocksAsync(connection, transaction, request.TenantRef,
            references, cancellationToken);
        long revocationEpoch = 0;
        if (request.TargetSoulId is not null)
        {
            await LockSoulAsync(connection, transaction, request.TenantRef, request.TargetSoulId, cancellationToken);
            var state = await ReadStateAsync(connection, transaction, request.TenantRef, request.TargetSoulId,
                cancellationToken);
            if (state is null || state.Status != "ACTIVE") throw new AliasNotFoundException();
            revocationEpoch = state.RevocationEpoch;
        }

        var now = _bindings.Clock.GetUtcNow();
        if (request.OccurredAt > now || now - request.OccurredAt > TimeSpan.FromMinutes(5))
            throw new SoulRegistryException("The alias request is outside the trusted time window.");
        var unsigned = new SoulAliasVerificationV1
        {
            ProofId = Guid.NewGuid(), TenantRef = request.TenantRef, Operation = request.Operation,
            AliasKind = request.AliasKind, AliasDigest = current.Digest, AliasKeyId = current.KeyId,
            AliasKeyEpoch = _bindings.AliasKeyEpoch, TargetSoulId = request.TargetSoulId,
            DeviceBindingId = request.DeviceBindingId, PlatformAccountId = request.PlatformAccountId,
            TraceId = request.TraceId, IdempotencyKey = request.IdempotencyKey, OccurredAt = request.OccurredAt,
            RequestSha256 = requestSha, KeyId = _bindings.KeyId, TrustEpoch = _bindings.TrustEpoch,
            RevocationEpoch = revocationEpoch, ReleaseBomSha256 = _bindings.ReleaseBomSha256,
            ReleaseGeneration = _bindings.ReleaseGeneration, IssuedAt = now,
            ExpiresAt = now.AddMinutes(5), Nonce = request.Nonce,
            SignatureBase64 = Convert.ToBase64String(new byte[64])
        };
        var signature = _bindings.Sign(unsigned.CanonicalBytes());
        if (signature.Length != 64) throw new SoulRegistryException("The authority signer returned an invalid signature.");
        var proof = unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
        ValidateProof(proof);
        proof.RequireMatches(request);
        var proofWire = proof.ToUtf8Json();
        var referencesJson = JsonSerializer.Serialize(references);
        var referencesSignature = _bindings.Sign(ReferenceCommitment(proof, referencesJson));
        if (referencesSignature.Length != 64 || !_bindings.VerificationKey.VerifyData(
                ReferenceCommitment(proof, referencesJson), referencesSignature,
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new SoulRegistryException("The alias reference set was not signed by the owner.");
        await using (var command = new NpgsqlCommand($"""
            INSERT INTO {Schema}.identity_verifications
                (tenant_ref, nonce, idempotency_key, request_sha256, proof_id, proof_wire,
                 alias_references, references_signature, operation, target_soul_id, expires_at)
            VALUES (@tenant, @nonce, @idem, @request_sha, @proof_id, @proof_wire,
                    @references::jsonb, @references_signature, @operation, @target, @expires)
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("tenant", request.TenantRef);
            command.Parameters.AddWithValue("nonce", request.Nonce);
            command.Parameters.AddWithValue("idem", request.IdempotencyKey);
            command.Parameters.AddWithValue("request_sha", requestSha);
            command.Parameters.AddWithValue("proof_id", proof.ProofId);
            command.Parameters.AddWithValue("proof_wire", proofWire);
            command.Parameters.AddWithValue("references", referencesJson);
            command.Parameters.AddWithValue("references_signature", referencesSignature);
            command.Parameters.AddWithValue("operation", request.Operation);
            command.Parameters.AddWithValue("target", (object?)request.TargetSoulId ?? DBNull.Value);
            command.Parameters.AddWithValue("expires", proof.ExpiresAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        try { await transaction.CommitAsync(cancellationToken); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            return await ReconcileVerificationAsync(request, requestSha, error);
        }
        return proof;
    }

    public async Task<SoulResolved> ApplyVerifiedAliasAsync(
        SoulAliasVerificationV1 proof, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ValidateProof(proof);
        var proofWire = proof.ToUtf8Json();
        var proofSha = proof.ComputeSha256();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockIdempotencyAsync(connection, transaction, proof.TenantRef, proof.IdempotencyKey, cancellationToken);
        var verification = await ReadVerificationByProofAsync(connection, transaction, proof.ProofId,
            cancellationToken);
        if (verification is null || verification.RequestSha256 != proof.RequestSha256 ||
            !CryptographicOperations.FixedTimeEquals(verification.ProofWire, proofWire) ||
            verification.Nonce != proof.Nonce || verification.IdempotencyKey != proof.IdempotencyKey)
            throw new SoulRegistryException("The signed proof was not issued by this Soul owner.");
        var existing = await ReadAuthorityReceiptAsync(connection, transaction, proof.TenantRef,
            proof.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!SecretComparison.EqualsHex(existing.ProofSha256, proofSha) || existing.ProofId != proof.ProofId)
                throw new IdempotencyConflictException();
            await transaction.CommitAsync(cancellationToken);
            return SoulResolvedCanonicalV1.FromUtf8Json(existing.ResultWire);
        }
        if (verification.ConsumedAt is not null)
            throw new IdempotencyConflictException();
        var references = JsonSerializer.Deserialize<AliasReference[]>(verification.AliasReferences)
            ?? throw new SoulRegistryException("The stored alias reference set is invalid.");
        if (!_bindings.VerificationKey.VerifyData(
                ReferenceCommitment(proof, JsonSerializer.Serialize(references)),
                verification.ReferencesSignature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new SoulRegistryException("The persisted alias keyring reference set was changed.");
        if (references.Length != _registry.Options.AliasKeys.Count ||
            !references.Select(static item => item.KeyId).Order(StringComparer.Ordinal)
                .SequenceEqual(_registry.Options.AliasKeys.Select(static key => key.KeyId).Order(StringComparer.Ordinal)) ||
            !references.Any(item => item.KeyId == proof.AliasKeyId && SecretComparison.EqualsHex(item.Digest, proof.AliasDigest)))
            throw new SoulRegistryException("The signed alias reference is not in the stored keyring set.");
        await PostgresSoulRegistry.AcquireAliasLocksAsync(connection, transaction, proof.TenantRef,
            references, cancellationToken);
        var legacy = await ReadAliasOwnersAsync(connection, transaction, "identity_aliases", "tenant_id",
            proof.TenantRef, references, cancellationToken);
        if (legacy.Count != 0) throw new AliasConflictException();
        var matches = await ReadAliasOwnersAsync(connection, transaction, "identity_authority_aliases", "tenant_ref",
            proof.TenantRef, references, cancellationToken);
        var active = matches.Where(static item => item.RevokedAt is null).ToArray();
        var owners = active.Select(static item => item.SoulId).Distinct(StringComparer.Ordinal).ToArray();
        if (owners.Length > 1) throw new AmbiguousAliasException();
        if (matches.Any(static item => item.RevokedAt is not null)) throw new AliasRevokedException();

        string soulId;
        switch (proof.Operation)
        {
            case "create":
                if (active.Length != 0) throw new AliasConflictException();
                soulId = NewSoulId();
                await LockSoulAsync(connection, transaction, proof.TenantRef, soulId, cancellationToken);
                await InsertSoulAsync(connection, transaction, proof.TenantRef, soulId, proof.OccurredAt, cancellationToken);
                await InsertAuthorityAliasesAsync(connection, transaction, proof, soulId, references, cancellationToken);
                await InsertStateAsync(connection, transaction, proof.TenantRef, soulId, proof.OccurredAt, cancellationToken);
                break;
            case "attach":
                soulId = proof.TargetSoulId!;
                if (owners.Length != 0 && owners[0] != soulId) throw new AliasConflictException();
                await LockSoulAsync(connection, transaction, proof.TenantRef, soulId, cancellationToken);
                await RequireActiveStateAsync(connection, transaction, proof, soulId, cancellationToken);
                if (active.Length == 0)
                {
                    await InsertAuthorityAliasesAsync(connection, transaction, proof, soulId, references, cancellationToken);
                    await AdvanceStateAsync(connection, transaction, proof.TenantRef, soulId, false, proof.OccurredAt, cancellationToken);
                }
                break;
            case "resolve":
                soulId = proof.TargetSoulId!;
                if (owners.Length != 1 || owners[0] != soulId) throw new AliasNotFoundException();
                await LockSoulAsync(connection, transaction, proof.TenantRef, soulId, cancellationToken);
                await RequireActiveStateAsync(connection, transaction, proof, soulId, cancellationToken);
                break;
            case "revoke":
                soulId = proof.TargetSoulId!;
                if (owners.Length != 1 || owners[0] != soulId) throw new AliasNotFoundException();
                await LockSoulAsync(connection, transaction, proof.TenantRef, soulId, cancellationToken);
                await RequireActiveStateAsync(connection, transaction, proof, soulId, cancellationToken);
                await RevokeAuthorityAliasesAsync(connection, transaction, proof, references, cancellationToken);
                await AdvanceStateAsync(connection, transaction, proof.TenantRef, soulId, true, proof.OccurredAt, cancellationToken);
                break;
            default: throw new SoulRegistryException("Unknown alias operation.");
        }
        var result = new SoulResolved(soulId, proof.DeviceBindingId, proof.PlatformAccountId,
            proof.TraceId, proof.IdempotencyKey, proof.OccurredAt, proof.AliasKind,
            proof.AliasDigest, proof.AliasKeyId);
        result.Validate();
        await StoreAuthorityResultAsync(connection, transaction, proof, proofSha, result, cancellationToken);
        try { await transaction.CommitAsync(cancellationToken); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            return await ReconcileApplyAsync(proof, proofSha, error);
        }
        return result;
    }

    private void ValidateProof(SoulAliasVerificationV1 proof)
    {
        _ = proof.ToUtf8Json();
        proof.RequireValidWindow(_bindings.Clock.GetUtcNow());
        if (proof.KeyId != _bindings.KeyId || proof.TrustEpoch != _bindings.TrustEpoch ||
            proof.AliasKeyEpoch != _bindings.AliasKeyEpoch ||
            (proof.Operation == "create" && proof.RevocationEpoch != 0) ||
            proof.ReleaseGeneration != _bindings.ReleaseGeneration ||
            !SecretComparison.EqualsHex(proof.ReleaseBomSha256, _bindings.ReleaseBomSha256) ||
            !_registry.Options.AliasKeys.Any(key => key.KeyId == proof.AliasKeyId) ||
            !_bindings.VerificationKey.VerifyData(proof.CanonicalBytes(),
                Convert.FromBase64String(proof.SignatureBase64), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new SoulRegistryException("The alias authority proof failed fixed trust verification.");
    }

    private static bool IsDigest(string? value) => value is { Length: 64 } &&
        !value.AsSpan().ContainsAnyExcept("0123456789abcdef");

    private static IdentityAliasKind ParseKind(string value) => value switch
    {
        "email" => IdentityAliasKind.Email,
        "phone" => IdentityAliasKind.Phone,
        "platform_id" => IdentityAliasKind.PlatformId,
        _ => throw new SoulRegistryException("Unknown alias kind.")
    };

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_registry.Options.ConnectionString);
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static Task LockIdempotencyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string tenant, string idem, CancellationToken cancellationToken) =>
        PostgresSoulRegistry.AcquireLockAsync(connection, transaction,
            $"soul-identity-idempotency:{tenant}:{idem}", cancellationToken);

    private static Task LockSoulAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string tenant, string soul, CancellationToken cancellationToken) =>
        PostgresSoulRegistry.AcquireLockAsync(connection, transaction,
            SoulLockName(tenant, soul), cancellationToken);

    private static string SoulLockName(string tenant, string soul) => $"soul-current:{tenant}:{soul}";

    private static string NewSoulId() => "soul_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static byte[] ReferenceCommitment(SoulAliasVerificationV1 proof, string referencesJson) =>
        System.Text.Encoding.UTF8.GetBytes("dps.soul-registry.alias-refs/v1\n" +
            proof.ProofId.ToString("N") + "\n" + proof.RequestSha256 + "\n" + referencesJson);

    private async Task<VerificationRow?> ReadVerificationAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string tenant, string nonce, string idem, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT tenant_ref, nonce, idempotency_key, request_sha256, proof_id, proof_wire,
                   alias_references::text, references_signature, consumed_at
            FROM {Schema}.identity_verifications
            WHERE tenant_ref = @tenant AND (nonce = @nonce OR idempotency_key = @idem)
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("nonce", nonce);
        command.Parameters.AddWithValue("idem", idem);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        VerificationRow? result = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (result is not null) throw new IdempotencyConflictException();
            result = ReadVerificationRow(reader);
        }
        return result;
    }

    private async Task<VerificationRow?> ReadVerificationByProofAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid proofId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT tenant_ref, nonce, idempotency_key, request_sha256, proof_id, proof_wire,
                   alias_references::text, references_signature, consumed_at
            FROM {Schema}.identity_verifications WHERE proof_id = @proof_id
            """, connection, transaction);
        command.Parameters.AddWithValue("proof_id", proofId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadVerificationRow(reader) : null;
    }

    private static VerificationRow ReadVerificationRow(NpgsqlDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.GetGuid(4), reader.GetFieldValue<byte[]>(5), reader.GetString(6),
        reader.GetFieldValue<byte[]>(7),
        reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8).ToUniversalTime());

    private async Task<AuthorityReceipt?> ReadAuthorityReceiptAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string tenant, string idem, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT proof_id, proof_sha256, result_wire FROM {Schema}.identity_authority_receipts
            WHERE tenant_ref = @tenant AND idempotency_key = @idem
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("idem", idem);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AuthorityReceipt(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<byte[]>(2))
            : null;
    }

    private async Task<IReadOnlyList<OwnerRow>> ReadAliasOwnersAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string table, string tenantColumn, string tenant,
        IReadOnlyList<AliasReference> references, CancellationToken cancellationToken)
    {
        var predicates = references.Select((_, index) =>
            $"(alias_kind = @kind_{index} AND alias_key_id = @key_{index} AND alias_digest = @digest_{index})");
        await using var command = new NpgsqlCommand($"""
            SELECT soul_id, revoked_at FROM {Schema}.{table}
            WHERE {tenantColumn} = @tenant AND ({string.Join(" OR ", predicates)})
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant);
        for (var index = 0; index < references.Count; index++)
        {
            command.Parameters.AddWithValue($"kind_{index}", references[index].Kind);
            command.Parameters.AddWithValue($"key_{index}", references[index].KeyId);
            command.Parameters.AddWithValue($"digest_{index}", references[index].Digest);
        }
        var rows = new List<OwnerRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new OwnerRow(reader.GetString(0), reader.IsDBNull(1) ? null :
                reader.GetFieldValue<DateTimeOffset>(1).ToUniversalTime()));
        return rows;
    }

    private async Task InsertSoulAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string tenant, string soul, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO {Schema}.souls (soul_id, tenant_id, created_at)
            VALUES (@soul, @tenant, @occurred)
            """, connection, transaction);
        command.Parameters.AddWithValue("soul", soul);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("occurred", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertAuthorityAliasesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SoulAliasVerificationV1 proof, string soul, IReadOnlyList<AliasReference> references,
        CancellationToken cancellationToken)
    {
        foreach (var reference in references)
        {
            await using var command = new NpgsqlCommand($"""
                INSERT INTO {Schema}.identity_authority_aliases
                    (tenant_ref, alias_kind, alias_key_id, alias_digest, soul_id, proof_id, verified_at)
                VALUES (@tenant, @kind, @key, @digest, @soul, @proof_id, @verified)
                """, connection, transaction);
            command.Parameters.AddWithValue("tenant", proof.TenantRef);
            command.Parameters.AddWithValue("kind", reference.Kind);
            command.Parameters.AddWithValue("key", reference.KeyId);
            command.Parameters.AddWithValue("digest", reference.Digest);
            command.Parameters.AddWithValue("soul", soul);
            command.Parameters.AddWithValue("proof_id", proof.ProofId);
            command.Parameters.AddWithValue("verified", proof.IssuedAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task RevokeAuthorityAliasesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SoulAliasVerificationV1 proof, IReadOnlyList<AliasReference> references,
        CancellationToken cancellationToken)
    {
        foreach (var reference in references)
        {
            await using var command = new NpgsqlCommand($"""
                UPDATE {Schema}.identity_authority_aliases SET revoked_at = @revoked
                WHERE tenant_ref = @tenant AND alias_kind = @kind AND alias_key_id = @key
                  AND alias_digest = @digest AND soul_id = @soul AND revoked_at IS NULL
                """, connection, transaction);
            command.Parameters.AddWithValue("revoked", proof.OccurredAt);
            command.Parameters.AddWithValue("tenant", proof.TenantRef);
            command.Parameters.AddWithValue("kind", reference.Kind);
            command.Parameters.AddWithValue("key", reference.KeyId);
            command.Parameters.AddWithValue("digest", reference.Digest);
            command.Parameters.AddWithValue("soul", proof.TargetSoulId!);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new AliasRevokedException();
        }
    }

    private async Task InsertStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string tenant, string soul, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO {Schema}.soul_current_state
                (tenant_ref, soul_id, resolution_revision, revocation_epoch, status, updated_at)
            VALUES (@tenant, @soul, 1, 0, 'ACTIVE', @occurred)
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("soul", soul);
        command.Parameters.AddWithValue("occurred", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<OwnerState?> ReadStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string tenant, string soul, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT s.resolution_revision, s.revocation_epoch, s.status
            FROM {Schema}.soul_current_state s JOIN {Schema}.souls i
              ON s.tenant_ref = i.tenant_id AND s.soul_id = i.soul_id
            WHERE s.tenant_ref = @tenant AND s.soul_id = @soul AND i.tombstoned_at IS NULL
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("soul", soul);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OwnerState(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2))
            : null;
    }

    private async Task RequireActiveStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SoulAliasVerificationV1 proof, string soul, CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(connection, transaction, proof.TenantRef, soul, cancellationToken);
        if (state is null || state.Status != "ACTIVE") throw new AliasNotFoundException();
        if (state.RevocationEpoch != proof.RevocationEpoch)
            throw new SoulRegistryException("The Soul identity epoch changed after verification.");
    }

    private async Task AdvanceStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string tenant, string soul, bool revoked, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var status = "ACTIVE";
        if (revoked)
        {
            await using var countCommand = new NpgsqlCommand($"""
                SELECT count(*) FROM {Schema}.identity_authority_aliases
                WHERE tenant_ref = @tenant AND soul_id = @soul AND revoked_at IS NULL
                """, connection, transaction);
            countCommand.Parameters.AddWithValue("tenant", tenant);
            countCommand.Parameters.AddWithValue("soul", soul);
            if ((long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L) == 0)
                status = "REVOKED";
        }
        await using var command = new NpgsqlCommand($"""
            UPDATE {Schema}.soul_current_state
            SET resolution_revision = resolution_revision + 1,
                revocation_epoch = revocation_epoch + @epoch_increment,
                status = @status, updated_at = @occurred
            WHERE tenant_ref = @tenant AND soul_id = @soul
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("soul", soul);
        command.Parameters.AddWithValue("epoch_increment", revoked ? 1L : 0L);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("occurred", occurredAt);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new AliasNotFoundException();
    }

    private async Task StoreAuthorityResultAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SoulAliasVerificationV1 proof, string proofSha, SoulResolved result, CancellationToken cancellationToken)
    {
        await using (var consume = new NpgsqlCommand($"""
            UPDATE {Schema}.identity_verifications SET consumed_at = @consumed
            WHERE proof_id = @proof_id AND consumed_at IS NULL
            """, connection, transaction))
        {
            consume.Parameters.AddWithValue("consumed", _bindings.Clock.GetUtcNow());
            consume.Parameters.AddWithValue("proof_id", proof.ProofId);
            if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new IdempotencyConflictException();
        }
        await using (var receipt = new NpgsqlCommand($"""
            INSERT INTO {Schema}.identity_authority_receipts
                (tenant_ref, idempotency_key, proof_id, proof_sha256, soul_id, result_wire, created_at)
            VALUES (@tenant, @idem, @proof_id, @proof_sha, @soul, @wire, @created)
            """, connection, transaction))
        {
            receipt.Parameters.AddWithValue("tenant", proof.TenantRef);
            receipt.Parameters.AddWithValue("idem", proof.IdempotencyKey);
            receipt.Parameters.AddWithValue("proof_id", proof.ProofId);
            receipt.Parameters.AddWithValue("proof_sha", proofSha);
            receipt.Parameters.AddWithValue("soul", result.SoulId);
            receipt.Parameters.AddWithValue("wire", SoulResolvedCanonicalV1.SerializeWire(result));
            receipt.Parameters.AddWithValue("created", _bindings.Clock.GetUtcNow());
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }
        var state = await ReadStateAsync(connection, transaction, proof.TenantRef, result.SoulId, cancellationToken)
            ?? throw new AliasNotFoundException();
        await using var audit = new NpgsqlCommand($"""
            INSERT INTO {Schema}.identity_authority_events
                (event_id, tenant_ref, soul_id, proof_id, event_kind, proof_sha256,
                 resolution_revision, revocation_epoch, occurred_at)
            VALUES (@event_id, @tenant, @soul, @proof_id, @kind, @proof_sha,
                    @revision, @epoch, @occurred)
            """, connection, transaction);
        audit.Parameters.AddWithValue("event_id", Guid.NewGuid());
        audit.Parameters.AddWithValue("tenant", proof.TenantRef);
        audit.Parameters.AddWithValue("soul", result.SoulId);
        audit.Parameters.AddWithValue("proof_id", proof.ProofId);
        audit.Parameters.AddWithValue("kind", proof.Operation);
        audit.Parameters.AddWithValue("proof_sha", proofSha);
        audit.Parameters.AddWithValue("revision", state.Revision);
        audit.Parameters.AddWithValue("epoch", state.RevocationEpoch);
        audit.Parameters.AddWithValue("occurred", _bindings.Clock.GetUtcNow());
        await audit.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SoulAliasVerificationV1> ReconcileVerificationAsync(
        SoulAliasVerificationRequestV1 request, string requestSha, Exception commitError)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var connection = await OpenAsync(timeout.Token);
            await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
            await LockIdempotencyAsync(connection, transaction, request.TenantRef, request.IdempotencyKey, timeout.Token);
            var row = await ReadVerificationAsync(connection, transaction, request.TenantRef,
                request.Nonce, request.IdempotencyKey, timeout.Token);
            if (row is not null && row.Nonce == request.Nonce && row.IdempotencyKey == request.IdempotencyKey &&
                SecretComparison.EqualsHex(row.RequestSha256, requestSha))
            {
                var proof = SoulAliasVerificationV1.FromUtf8Json(row.ProofWire);
                ValidateProof(proof);
                await transaction.CommitAsync(timeout.Token);
                return proof;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _ = error;
        }
        _ = commitError;
        throw new SoulCommitUnknownException();
    }

    private async Task<SoulResolved> ReconcileApplyAsync(
        SoulAliasVerificationV1 proof, string proofSha, Exception commitError)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var connection = await OpenAsync(timeout.Token);
            await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
            await LockIdempotencyAsync(connection, transaction, proof.TenantRef, proof.IdempotencyKey, timeout.Token);
            var receipt = await ReadAuthorityReceiptAsync(connection, transaction, proof.TenantRef,
                proof.IdempotencyKey, timeout.Token);
            if (receipt is not null && receipt.ProofId == proof.ProofId &&
                SecretComparison.EqualsHex(receipt.ProofSha256, proofSha))
            {
                var result = SoulResolvedCanonicalV1.FromUtf8Json(receipt.ResultWire);
                await transaction.CommitAsync(timeout.Token);
                return result;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _ = error;
        }
        _ = commitError;
        throw new SoulCommitUnknownException();
    }

    internal async Task<SoulOwnerLease> AcquireOwnerLeaseAsync(string tenant, string soul,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(soul))
            throw new ArgumentException("A scoped Soul identity is required.");
        var connectionString = new NpgsqlConnectionStringBuilder(_registry.Options.ConnectionString)
        {
            Pooling = false
        }.ConnectionString;
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var lockName = SoulLockName(tenant, soul);
            await using (var command = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@lock_name, 0))", connection))
            {
                command.CommandTimeout = 5;
                command.Parameters.AddWithValue("lock_name", lockName);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            var state = await ReadOwnerStateWithoutTransactionAsync(connection, tenant, soul, cancellationToken);
            if (state is null || state.Status != "ACTIVE") throw new AliasNotFoundException();
            return new SoulOwnerLease(this, connection, tenant, soul, lockName, state);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<OwnerState?> ReadOwnerStateWithoutTransactionAsync(NpgsqlConnection connection,
        string tenant, string soul, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT s.resolution_revision, s.revocation_epoch, s.status
            FROM {Schema}.soul_current_state s JOIN {Schema}.souls i
              ON s.tenant_ref = i.tenant_id AND s.soul_id = i.soul_id
            WHERE s.tenant_ref = @tenant AND s.soul_id = @soul AND i.tombstoned_at IS NULL
            """, connection);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("soul", soul);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OwnerState(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)) : null;
    }

    internal sealed class SoulOwnerLease : IAsyncDisposable
    {
        private readonly SoulIdentityAuthority _owner;
        private readonly NpgsqlConnection _connection;
        private readonly string _tenant;
        private readonly string _soul;
        private readonly string _lockName;
        private bool _disposed;
        internal OwnerState Snapshot { get; }
        internal int BackendProcessId => _connection.ProcessID;

        internal SoulOwnerLease(SoulIdentityAuthority owner, NpgsqlConnection connection,
            string tenant, string soul, string lockName, OwnerState snapshot)
        {
            _owner = owner; _connection = connection; _tenant = tenant; _soul = soul;
            _lockName = lockName; Snapshot = snapshot;
        }

        internal async Task RevalidateAsync(CancellationToken cancellationToken)
        {
            if (_disposed || _connection.State != ConnectionState.Open)
                throw new SoulRegistryException("Soul owner lease was lost.");
            var state = await _owner.ReadOwnerStateWithoutTransactionAsync(_connection, _tenant, _soul,
                cancellationToken);
            if (state != Snapshot) throw new SoulRegistryException("Soul owner state changed during lease.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await using var command = new NpgsqlCommand(
                        "SELECT pg_advisory_unlock(hashtextextended(@lock_name, 0))", _connection);
                    command.Parameters.AddWithValue("lock_name", _lockName);
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            finally { await _connection.DisposeAsync(); }
        }
    }

    private sealed record VerificationRow(string TenantRef, string Nonce, string IdempotencyKey,
        string RequestSha256, Guid ProofId, byte[] ProofWire, string AliasReferences,
        byte[] ReferencesSignature, DateTimeOffset? ConsumedAt);
    private sealed record AuthorityReceipt(Guid ProofId, string ProofSha256, byte[] ResultWire);
    private sealed record OwnerRow(string SoulId, DateTimeOffset? RevokedAt);
    internal sealed record OwnerState(long Revision, long RevocationEpoch, string Status);
}
