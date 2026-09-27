using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dps.PolicyApproval.Contracts;
using Xunit;

namespace Dps.ControlPlaneHost.Tests;

public sealed class PolicyShadowChainContractTests
{
    [Fact, Trait("Category", "Contract")]
    public void HostSrcComposesOnlyContractPacksAndProductStartupNeverReachesTheShadowChain()
    {
        var source = Path.Combine(RepositoryRoot(), "Modules/control-plane-host/src/Dps.ControlPlaneHost");
        // 1. No ProjectReference from Host src to a Planner or Policy implementation project.
        var references = XDocument.Load(Path.Combine(source, "Dps.ControlPlaneHost.csproj"))
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")!.Value.Replace('\\', '/'))
            .ToArray();
        Assert.Contains("../../../planner/contracts/provided/Dps.Planner.Contracts/Dps.Planner.Contracts.csproj", references);
        Assert.DoesNotContain(references, reference =>
            reference.Contains("/planner/src/", StringComparison.Ordinal)
            || reference.Contains("/policy-approval/src/", StringComparison.Ordinal));
        // 2. No Host src code names an implementation type or namespace; only the
        //    composition file names the composition.
        var implementation = new Regex(@"\bDps\.(Planner|PolicyApproval)\b(?!\.Contracts\b)", RegexOptions.CultureInvariant);
        foreach (var file in Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            Assert.False(implementation.IsMatch(text), file);
            if (Path.GetFileName(file) != "ControlPlanePolicyShadowChain.cs")
                Assert.DoesNotContain("ControlPlanePolicyShadowChain", text, StringComparison.Ordinal);
        }
        Assert.DoesNotContain(typeof(ControlPlanePolicyShadowChain).Assembly.GetReferencedAssemblies(), name =>
            name.Name is "Dps.Planner" or "Dps.PolicyApproval");
        // 3. Product startup still accepts only --self-check.
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, HostStartup.Run(["--self-check"], output, error));
        foreach (var args in new[] { Array.Empty<string>(), ["--shadow-chain"], ["--self-check", "--shadow-chain"] })
            Assert.Equal(64, HostStartup.Run(args, output, error));

        Assert.True(new ControlPlaneShadowChainOptions(new string('b', 64)).KillControlPlaneIngest);
        // The public surface cannot carry a mode, a promotion, or a caller-built request.
        var parameterTypes = typeof(ControlPlanePolicyShadowChain)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Assert.DoesNotContain(typeof(ActionExecutionPromotionV1), parameterTypes);
        Assert.DoesNotContain(typeof(ApprovalEvaluationRequestV1), parameterTypes);
        Assert.DoesNotContain(typeof(string), parameterTypes);
    }

    [Fact, Trait("Category", "Contract")]
    public void PolicyOwnedReadbackCorpusIsAcceptedOnlyWhereTheOwnerCodecAccepts()
    {
        using var stream = typeof(ApprovalReadbackV1).Assembly.GetManifestResourceStream(
                               "Dps.PolicyApproval.Contracts.approval.readback.v1.corpus.json")
                           ?? throw new InvalidOperationException("Owner readback corpus is missing.");
        var corpus = JsonNode.Parse(stream)!.AsObject();
        Assert.Equal("approval.readback/v1", (string?)corpus["contractId"]);
        var cases = corpus["cases"]!.AsArray();
        Assert.Equal(28, cases.Count);
        foreach (var node in cases)
        {
            var instance = corpus["base"]!.DeepClone().AsObject();
            foreach (var pair in node!["patch"]!.AsObject()) instance[pair.Key] = pair.Value?.DeepClone();
            foreach (var field in node["remove"]!.AsArray()) instance.Remove((string)field!);
            var payload = Encoding.UTF8.GetBytes(instance.ToJsonString());
            if ((bool)node["codecValid"]!)
            {
                var readback = ApprovalReadbackV1Codec.Deserialize(payload);
                Assert.False(readback.ProposalContractMajor == 1
                             && readback.Status == ApprovalReadbackV1.Active
                             && readback.Approval.Decision == ApprovalDecisionV1.Approved
                             && !readback.Approval.ShadowOnly);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => ApprovalReadbackV1Codec.Deserialize(payload));
            }
        }
    }

    [Fact, Trait("Category", "Contract")]
    public void HostIsTheDeclaredProducerPreservingRelayForPlannerProposalsAndProducesTheEvaluationRequest()
    {
        var host = Manifest("control-plane-host");
        var planner = Manifest("planner");
        var policy = Manifest("policy-approval");

        var relayOut = Edge(host, "outbound", "policy-approval", "action.proposal", 2);
        Assert.True(relayOut.GetProperty("preserveProducer").GetBoolean());
        Edge(host, "inbound", "planner", "action.proposal", 2);
        Assert.Contains(Consumed(host), pair => pair == ("action.proposal", 2));
        Assert.Contains(Consumed(host), pair => pair == ("approval.evaluation.request", 1));
        Assert.Contains(Consumed(host), pair => pair == ("approval.readback", 1));
        Edge(host, "outbound", "policy-approval", "approval.evaluation.request", 1);
        Edge(host, "inbound", "policy-approval", "approval.readback", 1);

        Edge(planner, "outbound", "control-plane-host", "action.proposal", 2);
        Assert.DoesNotContain(Edges(planner, "outbound"), edge => edge.GetProperty("peerModule").GetString() == "policy-approval");
        Edge(policy, "inbound", "control-plane-host", "action.proposal", 2);
        Assert.DoesNotContain(Edges(policy, "inbound"), edge => edge.GetProperty("peerModule").GetString() == "planner");
        Edge(policy, "outbound", "control-plane-host", "approval.readback", 1);
    }

    private static JsonElement Manifest(string module)
        => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepositoryRoot(), "Modules", module, "module.yaml"))).RootElement;

    private static IEnumerable<JsonElement> Edges(JsonElement manifest, string direction)
        => manifest.GetProperty("communication").GetProperty(direction).EnumerateArray();

    private static JsonElement Edge(JsonElement manifest, string direction, string peer, string contract, int major)
        => Assert.Single(Edges(manifest, direction), edge =>
            edge.GetProperty("peerModule").GetString() == peer
            && edge.GetProperty("contractId").GetString() == contract
            && edge.GetProperty("major").GetInt32() == major);

    private static IEnumerable<(string, int)> Consumed(JsonElement manifest)
        => manifest.GetProperty("contracts").GetProperty("consumed").EnumerateArray()
            .Where(entry => entry.GetProperty("mode").GetString() == "active")
            .Select(entry => (entry.GetProperty("contractId").GetString()!, entry.GetProperty("major").GetInt32()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
    }
}
