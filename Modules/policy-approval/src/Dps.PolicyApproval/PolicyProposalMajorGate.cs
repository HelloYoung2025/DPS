using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dps.Planner.Contracts;

namespace Dps.PolicyApproval;

/// <summary>
/// Bounded, hash-only proof that one structurally valid <c>action.proposal/v1</c>
/// wire was quarantined at the Policy entry. It never contains payload bytes and
/// is never persisted: the wire arrived unauthenticated.
/// </summary>
public sealed record PolicyProposalQuarantineProof(
    string ContractId,
    int Major,
    string Mode,
    string Disposition,
    string WireSha256,
    int WireBytes)
{
    public const string QuarantineMode = "quarantine-only";
    public const string QuarantineDisposition = "QUARANTINE";
}

public sealed class PolicyProposalQuarantinedException : NotSupportedException
{
    internal PolicyProposalQuarantinedException(PolicyProposalQuarantineProof proof)
        : base("action.proposal/v1 is quarantine-only and never reaches policy evaluation.")
        => Proof = proof;

    public PolicyProposalQuarantineProof Proof { get; }
}

/// <summary>
/// The single entry gate that dispatches <c>action.proposal</c> wire by contract
/// major before authentication or database access: v2 goes to the Planner owner
/// codec, v1 is quarantined, and anything else fails closed.
/// </summary>
internal static class PolicyProposalMajorGate
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static ActionProposalV2 Admit(ReadOnlySpan<byte> wire)
    {
        if (wire.IsEmpty || wire.Length > ActionProposalV2Json.MaximumWireBytes)
            throw new ArgumentException(
                $"action.proposal wire must contain between 1 and {ActionProposalV2Json.MaximumWireBytes} bytes.",
                nameof(wire));
        try { _ = StrictUtf8.GetCharCount(wire); }
        catch (DecoderFallbackException exception)
        {
            throw new ArgumentException("action.proposal wire must be strict UTF-8.", nameof(wire), exception);
        }

        switch (ReadContractId(wire))
        {
            case ActionProposalV2.CurrentContractId:
                return ActionProposalV2Json.Deserialize(wire);
            case ActionProposalV1.CurrentContractId:
                // ponytail: mirrors executor-gateway's LegacyNativeStopProofV1QuarantineClassifier
                // because modules cannot reference each other's classifiers; promote to a shared
                // quarantine-proof contract if a third consumer appears.
                _ = ActionProposalV1Json.Deserialize(wire);
                throw new PolicyProposalQuarantinedException(new PolicyProposalQuarantineProof(
                    ActionProposalV1.CurrentContractId,
                    1,
                    PolicyProposalQuarantineProof.QuarantineMode,
                    PolicyProposalQuarantineProof.QuarantineDisposition,
                    Convert.ToHexStringLower(SHA256.HashData(wire)),
                    wire.Length));
            default:
                throw new NotSupportedException("Unknown action.proposal contract major fails closed.");
        }
    }

    private static string ReadContractId(ReadOnlySpan<byte> wire)
    {
        using var document = JsonDocument.Parse(
            wire.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("action.proposal wire must be one JSON object.");
        string? contractId = null;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!string.Equals(property.Name, "contract_id", StringComparison.Ordinal)) continue;
            if (contractId is not null || property.Value.ValueKind != JsonValueKind.String)
                throw new JsonException("action.proposal wire must carry exactly one string contract_id.");
            contractId = property.Value.GetString()!;
        }
        return contractId
            ?? throw new JsonException("action.proposal wire must carry exactly one string contract_id.");
    }
}
