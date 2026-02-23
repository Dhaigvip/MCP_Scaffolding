// GovernanceExecutorTests.cs
using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;

public class PolicyEngineTests
{
    private readonly PolicyEngine _engine = new();

    [Fact]
    public void Risk_above_global_ceiling_throws()
    {
        var policy = new ToolPolicy { Enabled = true, Risk = RiskLevel.High };

        var ex = Assert.Throws<GovernanceException>(() =>
            _engine.EnforceToolPolicy("my.tool", policy, RiskLevel.ReadOnly, tenant: null));

        Assert.Equal("risk_gate", ex.Code);
    }

    [Fact]
    public void Tenant_not_in_allowlist_throws()
    {
        var policy = new ToolPolicy
        {
            Enabled = true,
            Risk = RiskLevel.ReadOnly,
            AllowedTenants = ["tenant-a"]
        };

        var ex = Assert.Throws<GovernanceException>(() =>
            _engine.EnforceToolPolicy("my.tool", policy, RiskLevel.High, tenant: "tenant-b"));

        Assert.Equal("tenant_not_allowed", ex.Code);
    }

    [Fact]
    public void Valid_tenant_passes()
    {
        var policy = new ToolPolicy
        {
            Enabled = true,
            Risk = RiskLevel.ReadOnly,
            AllowedTenants = ["tenant-a"]
        };

        // Should not throw
        _engine.EnforceToolPolicy("my.tool", policy, RiskLevel.High, tenant: "tenant-a");
    }
}

public class ExposureServiceTests
{
    [Fact]
    public void Disabled_tool_returns_false()
    {
        var manifest = new ExposureManifest
        {
            GlobalEnabled = true,
            Tools = new() { ["hello.say"] = new ToolPolicy { Enabled = false } }
        };
        var svc = new ExposureService(manifest);

        Assert.False(svc.TryGetEnabledPolicy("hello.say", out _));
    }

    [Fact]
    public void GlobalEnabled_false_blocks_everything()
    {
        var manifest = new ExposureManifest
        {
            GlobalEnabled = false,
            Tools = new() { ["hello.say"] = new ToolPolicy { Enabled = true } }
        };
        var svc = new ExposureService(manifest);

        Assert.False(svc.TryGetEnabledPolicy("hello.say", out _));
    }

    [Fact]
    public void Unknown_tool_returns_false()
    {
        var manifest = new ExposureManifest { GlobalEnabled = true };
        var svc = new ExposureService(manifest);

        Assert.False(svc.TryGetEnabledPolicy("does.not.exist", out _));
    }
}