using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dps.Planner.Contracts;
using Dps.PolicyApproval.Contracts;
using Xunit;

namespace Dps.PolicyApproval.Tests;

public sealed class PolicyProposalMajorGateTests
{
    private const string Soul = "soul_cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string Device = "db_cccccccccccccccccccccccccccccccc";
    private const string Account = "pa_cccccccccccccccccccccccccccccccc";
    private const string Trace = "trace_cccccccccccccccccccccccccccccccc";
    private const string Idempotency = "idem_cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact, Trait("Category", "Contract")]
    public void OwnerV2CorpusAgreesWithPolicyMajorGateAndIsPinnedBySchema()
    {
        var corpusBytes = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "planner-contracts",
            "action-proposal-contract-cases.v2.json"));
        using (var schemaStream = typeof(ActionProposalV2).Assembly.GetManifestResourceStream(
                   "Dps.Planner.Contracts.action.proposal.v2.schema.json")
               ?? throw new InvalidOperationException("Embedded action.proposal/v2 schema is missing."))
        using (var schema = JsonDocument.Parse(schemaStream))
        {
            Assert.Equal(
                schema.RootElement.GetProperty("x-dps-adversarial-corpus-sha256").GetString(),
                Convert.ToHexStringLower(SHA256.HashData(corpusBytes)));
        }

        var corpus = JsonNode.Parse(corpusBytes)!.AsObject();
        var baseline = corpus["base"]!.AsObject();
        var accepted = 0;
        var rejected = 0;
        foreach (var caseNode in corpus["cases"]!.AsArray())
        {
            var testCase = caseNode!.AsObject();
            var name = testCase["name"]!.GetValue<string>();
            var instance = baseline.DeepClone().AsObject();
            if (testCase["overrides"] is JsonObject overrides)
            {
                foreach (var pair in overrides) instance[pair.Key] = pair.Value?.DeepClone();
            }
            if (testCase["remove"] is JsonArray removals)
            {
                foreach (var propertyName in removals) instance.Remove(propertyName!.GetValue<string>());
            }
            var wire = Encoding.UTF8.GetBytes(instance.ToJsonString());

            ActionProposalV2? owner = null;
            try { owner = ActionProposalV2Json.Deserialize(wire); }
            catch (Exception) { owner = null; }
            ActionProposalV2? admitted = null;
            Exception? gateFailure = null;
            try { admitted = PolicyProposalMajorGate.Admit(wire); }
            catch (Exception exception) { gateFailure = exception; }

            Assert.True(
                testCase["valid"]!.GetValue<bool>() == (owner is not null),
                $"Owner codec disagrees with corpus case '{name}'.");
            Assert.True(
                (owner is not null) == (admitted is not null),
                $"Policy major gate disagrees with the owner codec on corpus case '{name}'.");
            if (owner is not null)
            {
                Assert.Equal(
                    ActionProposalV2Canonical.ComputeSha256(owner),
                    ActionProposalV2Canonical.ComputeSha256(admitted!));
                accepted++;
            }
            else
            {
                Assert.IsNotType<PolicyProposalQuarantinedException>(gateFailure);
                rejected++;
            }
        }
        Assert.True(accepted > 0 && rejected > 0);
    }

    [Fact, Trait("Category", "Contract")]
    public void V1WireIsQuarantinedWithBoundedHashOnlyProof()
    {
        var wire = ActionProposalV1Json.Serialize(V1Proposal());
        var exception = Assert.Throws<PolicyProposalQuarantinedException>(
            () => PolicyProposalMajorGate.Admit(wire));
        Assert.IsAssignableFrom<NotSupportedException>(exception);
        var proof = exception.Proof;
        Assert.Equal(ActionProposalV1.CurrentContractId, proof.ContractId);
        Assert.Equal(1, proof.Major);
        Assert.Equal(PolicyProposalQuarantineProof.QuarantineMode, proof.Mode);
        Assert.Equal(PolicyProposalQuarantineProof.QuarantineDisposition, proof.Disposition);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(wire)), proof.WireSha256);
        Assert.Equal(wire.Length, proof.WireBytes);
        Assert.Equal(
            ["ContractId", "Disposition", "Major", "Mode", "WireBytes", "WireSha256"],
            typeof(PolicyProposalQuarantineProof)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(Soul, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Idempotency, exception.ToString(), StringComparison.Ordinal);

        var invalidV1 = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(wire)[..^1] + ",\"requested_mode\":\"EXECUTE\"}");
        var invalid = Assert.ThrowsAny<Exception>(() => PolicyProposalMajorGate.Admit(invalidV1));
        Assert.IsNotType<PolicyProposalQuarantinedException>(invalid);
    }

    [Fact, Trait("Category", "Contract")]
    public void UnknownMajorMalformedContractIdAndUnboundedWireFailClosed()
    {
        var v2 = Encoding.UTF8.GetString(ActionProposalV2Json.Serialize(V2Proposal()));
        const string contract = "\"contract_id\":\"action.proposal/v2\"";
        Assert.Contains(contract, v2, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => PolicyProposalMajorGate.Admit(Encoding.UTF8.GetBytes(
            v2.Replace(contract, "\"contract_id\":\"action.proposal/v3\"", StringComparison.Ordinal))));
        foreach (var malformed in new[]
                 {
                     v2.Replace(contract + ",", string.Empty, StringComparison.Ordinal),
                     v2.Replace(contract, contract + "," + contract, StringComparison.Ordinal),
                     v2.Replace(contract, "\"contract_id\":2", StringComparison.Ordinal),
                     "[" + v2 + "]"
                 })
        {
            Assert.Throws<JsonException>(
                () => PolicyProposalMajorGate.Admit(Encoding.UTF8.GetBytes(malformed)));
        }
        Assert.Throws<ArgumentException>(() => PolicyProposalMajorGate.Admit([]));
        Assert.Throws<ArgumentException>(() => PolicyProposalMajorGate.Admit(
            new byte[ActionProposalV2Json.MaximumWireBytes + 1]));
        Assert.Throws<ArgumentException>(() => PolicyProposalMajorGate.Admit([0x7b, 0x80, 0x7d]));
    }

    [Fact, Trait("Category", "Contract")]
    public void PolicyApiAdmitsProposalsOnlyAsWireAndExposesNoV1TypedEntry()
    {
        var assembly = typeof(PostgresPolicyApprovalService).Assembly;
        Assert.Null(assembly.GetType("Dps.PolicyApproval.PolicyEvaluationEnvelope"));
        Assert.Null(assembly.GetType("Dps.PolicyApproval.PolicyAuthorizationBinding"));
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Assert.Empty(assembly.GetTypes()
            .SelectMany(type => type.GetMembers(declared))
            .Where(member => member switch
            {
                FieldInfo field => field.FieldType == typeof(ActionProposalV1),
                PropertyInfo property => property.PropertyType == typeof(ActionProposalV1),
                MethodBase method => method.GetParameters().Any(
                    parameter => parameter.ParameterType == typeof(ActionProposalV1)),
                _ => false
            })
            .Select(member => member.DeclaringType + "." + member.Name));

        Assert.Equal(
            ["CountAsync", "Dispose", "EvaluateAsync", "ReadAuthoritativeAsync", "RevokeAsync"],
            typeof(PostgresPolicyApprovalService)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            [typeof(ReadOnlyMemory<byte>), typeof(ReadOnlyMemory<byte>), typeof(ActionExecutionPromotionV1), typeof(CancellationToken)],
            typeof(PostgresPolicyApprovalService).GetMethod(nameof(PostgresPolicyApprovalService.EvaluateAsync))!
                .GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(
            [typeof(ActionProposalV2), typeof(ApprovalEvaluationRequestV1), typeof(ActionExecutionPromotionV1), typeof(CancellationToken)],
            typeof(DeterministicPolicyEvaluator).GetMethod(nameof(DeterministicPolicyEvaluator.EvaluateAsync))!
                .GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact, Trait("Category", "Contract")]
    public void Migration005IsRegisteredEmbeddedAndOnlyExpandsWithoutGrants()
    {
        var migrationField = typeof(PostgresPolicyApprovalMigrator).GetField(
            "MigrationResourceSuffixes",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Product migration registry is missing.");
        Assert.Equal(
            [
                "001_create_policy_approval.sql",
                "002_configure_policy_runtime_role.sql",
                "003_create_submission_lifecycle.sql",
                "005_add_proposal_contract_major.sql"
            ],
            Assert.IsType<string[]>(migrationField.GetValue(null)));
        var assembly = typeof(PostgresPolicyApprovalMigrator).Assembly;
        var resourceName = Assert.Single(
            assembly.GetManifestResourceNames(),
            static name => name.EndsWith("005_add_proposal_contract_major.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var sql = reader.ReadToEnd();

        foreach (var required in new[]
                 {
                     "ADD COLUMN IF NOT EXISTS proposal_contract_major smallint NOT NULL DEFAULT 1",
                     "ADD COLUMN IF NOT EXISTS request_signature text",
                     "CHECK (proposal_contract_major IN (1, 2))",
                     "(proposal_contract_major = 1 AND request_signature IS NULL)",
                     "SET search_path = pg_catalog, __SCHEMA__",
                     "AND decision.proposal_contract_major = 2)",
                     "ERRCODE = '42501'",
                     "BEFORE INSERT ON __SCHEMA__.approval_submission_attempts",
                     "REVOKE ALL ON FUNCTION __SCHEMA__.reject_non_v2_submission_attempt() FROM PUBLIC, __RUNTIME_ROLE__, __SUBMISSION_EXECUTOR_ROLE__, __SUBMISSION_RECONCILIATION_ROLE__, __SUBMISSION_RECOVERY_ROLE__;"
                 })
            Assert.Contains(required, sql, StringComparison.Ordinal);
        foreach (var forbidden in new[]
                 {
                     "GRANT ", "SECURITY DEFINER", "DROP COLUMN", "DROP TABLE", "ALTER COLUMN",
                     "UPDATE __SCHEMA__", "DELETE FROM", "TRUNCATE"
                 })
            Assert.DoesNotContain(forbidden, sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ActionProposalV1 V1Proposal() => new(
        ActionProposalV1.CurrentSchemaVersion, ActionProposalV1.CurrentContractId, ActionProposalV1.CurrentProducerModule,
        ActionProposalIdentity.Create(Soul, Device, Account, Idempotency), Soul, Device, Account, Trace, Idempotency, Now,
        "internal", "fixture.tap", true, true,
        new Dictionary<string, string> { ["selector_ref"] = "fixture.button" },
        ["evidence:synthetic"]);

    private static ActionProposalV2 V2Proposal() => new(
        ActionProposalV2.CurrentSchemaVersion, ActionProposalV2.CurrentContractId, ActionProposalV2.CurrentProducerModule,
        ActionProposalIdentity.Create(Soul, Device, Account, Idempotency), Soul, Device, Account, Trace, Idempotency, Now,
        "internal", "fixture.tap", true, true,
        new Dictionary<string, string> { ["selector_ref"] = "selector_" + new string('a', 64) },
        ["evidence_" + new string('e', 64)]);
}
