using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dps.SoulRegistry.Contracts;
using Xunit;

namespace Dps.SoulRegistry.Tests;

public sealed class SoulIdentityContractTests
{
    private static JsonElement Corpus()
    {
        using var stream = typeof(SoulIdentityContractTests).Assembly.GetManifestResourceStream("soul.identity.v1.corpus.json")
            ?? throw new InvalidOperationException("The complete owner corpus is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var item in Corpus().GetProperty("cases").EnumerateArray())
            yield return new object[] { item.GetProperty("id").GetString()!, item.GetRawText() };
    }

    private static (byte[] Wire, byte[] Frame, string Hash) Decode(string contract, byte[] bytes)
    {
        switch (contract)
        {
            case "soul.alias.verify.request/v1":
                var request = SoulAliasVerificationRequestV1.FromUtf8Json(bytes);
                return (request.ToUtf8Json(), request.CanonicalBytes(), request.ComputeSha256());
            case "soul.alias.verification/v1":
                var proof = SoulAliasVerificationV1.FromUtf8Json(bytes);
                return (proof.ToUtf8Json(), proof.CanonicalBytes(), proof.ComputeSha256());
            case "soul.current.query/v1":
                var query = SoulCurrentQueryV1.FromUtf8Json(bytes);
                return (query.ToUtf8Json(), query.CanonicalBytes(), query.ComputeSha256());
            case "soul.current.resolution/v1":
                var current = SoulCurrentResolutionV1.FromUtf8Json(bytes);
                return (current.ToUtf8Json(), current.CanonicalBytes(), current.ComputeSha256());
            default: throw new InvalidOperationException("Unknown corpus family.");
        }
    }

    private static byte[] Wire(JsonElement item) => item.TryGetProperty("raw_base64", out var raw)
        ? Convert.FromBase64String(raw.GetString()!) : Encoding.UTF8.GetBytes(item.GetProperty("payload").GetRawText());

    private static JsonObject Sample(string contract)
    {
        var item = Corpus().GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == contract.Replace("/v1", "", StringComparison.Ordinal) + ".valid");
        return JsonNode.Parse(item.GetProperty("payload").GetRawText())!.AsObject();
    }

    [Theory, MemberData(nameof(Cases)), Trait("Category", "Contract")]
    public void SharedCorpusStrictCodecAndCommitments(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        var item = document.RootElement;
        Assert.Equal(id, item.GetProperty("id").GetString());
        var contract = item.GetProperty("contract_id").GetString()!;
        var bytes = Wire(item);
        if (!item.GetProperty("codec_expected").GetBoolean())
        {
            Assert.ThrowsAny<Exception>(() => Decode(contract, bytes));
            return;
        }
        var actual = Decode(contract, bytes);
        var second = Decode(contract, actual.Wire);
        Assert.Equal(actual.Wire, second.Wire);
        Assert.Equal(actual.Frame, second.Frame);
        Assert.Equal(actual.Hash, second.Hash);
        if (item.TryGetProperty("frame_sha256", out var digest))
        {
            Assert.Equal(item.GetProperty("frame_bytes").GetInt32(), actual.Frame.Length);
            Assert.Equal(digest.GetString(), actual.Hash);
        }
    }

    [Fact, Trait("Category", "Contract")]
    public void CorpusInventoryIsCompleteAndUnique()
    {
        var corpus = Corpus();
        var ids = corpus.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("id").GetString()!).ToArray();
        Assert.Equal(corpus.GetProperty("inventory").EnumerateArray().Select(c => c.GetString()!), ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, corpus.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("contract_id").GetString()).Distinct().Count());
    }

    [Fact, Trait("Category", "Contract")]
    public void PropertyOrderDoesNotChangeFrameButEveryCommittedScalarDoes()
    {
        foreach (var contract in new[] { "soul.alias.verify.request/v1", "soul.alias.verification/v1", "soul.current.query/v1", "soul.current.resolution/v1" })
        {
            var original = Sample(contract);
            var initial = Decode(contract, Encoding.UTF8.GetBytes(original.ToJsonString()));
            var reversed = new JsonObject();
            foreach (var pair in original.Reverse()) reversed.Add(pair.Key, pair.Value?.DeepClone());
            Assert.Equal(initial.Frame, Decode(contract, Encoding.UTF8.GetBytes(reversed.ToJsonString())).Frame);
            foreach (var pair in original)
            {
                if (pair.Key is "signature_base64" or "resolution") continue;
                var changed = (JsonObject)original.DeepClone();
                if (pair.Value is JsonValue scalar && scalar.TryGetValue<long>(out var integer)) changed[pair.Key] = integer + 1;
                else if (pair.Value is null) changed[pair.Key] = 1;
                else
                {
                    var text = pair.Value!.GetValue<string>();
                    changed[pair.Key] = text[..^1] + (text[^1] == 'a' ? 'b' : 'a');
                }
                (byte[] Wire, byte[] Frame, string Hash) decoded;
                try { decoded = Decode(contract, Encoding.UTF8.GetBytes(changed.ToJsonString())); }
                catch (Exception error) when (error is ArgumentException or FormatException or JsonException or InvalidOperationException) { continue; }
                Assert.NotEqual(initial.Hash, decoded.Hash);
            }
        }
    }

    [Fact, Trait("Category", "Contract")]
    public void ProofCommitsToExactRequestAndCurrentQuery()
    {
        var request = SoulAliasVerificationRequestV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.alias.verify.request/v1").ToJsonString()));
        var proof = SoulAliasVerificationV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.alias.verification/v1").ToJsonString())) with { RequestSha256 = request.ComputeSha256() };
        proof.RequireMatches(request);
        Assert.Throws<ArgumentException>(() => proof.RequireMatches(request with { ProofRef = "proof_" + new string('a', 64) }));
        Assert.Throws<ArgumentException>(() => proof.RequireMatches(request with { Nonce = "nonce_" + new string('a', 64) }));
        var query = SoulCurrentQueryV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.current.query/v1").ToJsonString()));
        var current = SoulCurrentResolutionV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.current.resolution/v1").ToJsonString()));
        current.RequireMatches(query);
        Assert.Throws<ArgumentException>(() => current.RequireMatches(query with { ExpectedResolutionRevision = 2 }));
        Assert.Throws<ArgumentException>(() => current.RequireMatches(query with { Nonce = "nonce_" + new string('a', 64) }));
        Assert.Throws<ArgumentException>(() => current.RequireMatches(query with { ProducerModule = "binding", Purpose = "binding-activate" }));
    }

    [Fact, Trait("Category", "Contract")]
    public void HistoricalStructureAndStrictExpiryAreSeparate()
    {
        var proof = SoulAliasVerificationV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.alias.verification/v1").ToJsonString()));
        proof.RequireValidWindow(proof.IssuedAt);
        proof.RequireValidWindow(proof.ExpiresAt.AddTicks(-1));
        Assert.Throws<ArgumentException>(() => proof.RequireValidWindow(proof.IssuedAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => proof.RequireValidWindow(proof.ExpiresAt));
        Assert.Equal(proof, SoulAliasVerificationV1.FromUtf8Json(proof.ToUtf8Json()));
        var current = SoulCurrentResolutionV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.current.resolution/v1").ToJsonString())) with { Status = "REVOKED" };
        Assert.Equal("REVOKED", SoulCurrentResolutionV1.FromUtf8Json(current.ToUtf8Json()).Status);
        Assert.Throws<ArgumentException>(() => current.RequireValidWindow(current.ExpiresAt));
    }

    [Fact, Trait("Category", "Contract")]
    public void SignatureBytesAreExcludedButAlgorithmAndFrameAreBound()
    {
        // Synthetic mathematical fixture only. This key is not an issuer or production root.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = SoulAliasVerificationV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.alias.verification/v1").ToJsonString()));
        var bytes = proof.CanonicalBytes();
        Assert.False(key.VerifyData(bytes, Convert.FromBase64String(proof.SignatureBase64), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var signature = key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        Assert.Equal(64, signature.Length);
        var signed = proof with { SignatureBase64 = Convert.ToBase64String(signature) };
        Assert.Equal(bytes, signed.CanonicalBytes());
        Assert.True(key.VerifyData(bytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.False(key.VerifyData((signed with { TrustEpoch = 2 }).CanonicalBytes(), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact, Trait("Category", "Contract")]
    public void FrameUsesLengthPrefixesAndExplicitNullablePresence()
    {
        var query = SoulCurrentQueryV1.FromUtf8Json(Encoding.UTF8.GetBytes(Sample("soul.current.query/v1").ToJsonString()));
        var fields = new List<string>();
        var bytes = query.CanonicalBytes();
        for (var offset = 0; offset < bytes.Length;)
        {
            var length = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4; fields.Add(new UTF8Encoding(false, true).GetString(bytes, offset, length)); offset += length;
        }
        Assert.Equal("dps.soul-registry.current-query/v1", fields[0]);
        Assert.Contains(query.RequestId.ToString("N"), fields);
        Assert.Equal("true", fields[^3]); Assert.Equal("1", fields[^2]);
        Assert.NotEqual(query.ComputeSha256(), (query with { ExpectedResolutionRevision = null }).ComputeSha256());
        Assert.NotEqual(query.ComputeSha256(), (query with { ExpectedResolutionRevision = long.MaxValue }).ComputeSha256());
    }
}
