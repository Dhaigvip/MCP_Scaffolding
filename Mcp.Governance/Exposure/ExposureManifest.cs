using Mcp.Core.Governance;
using Mcp.Governance.Policy;

namespace Mcp.Governance.Exposure;

public sealed class ExposureManifest
{
    public int Version { get; init; } = 1;

    public bool GlobalEnabled { get; init; } = true;

    public RiskLevel GlobalMaxRisk { get; init; } = RiskLevel.ReadOnly;

    public Dictionary<string, ToolPolicy> Tools { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}