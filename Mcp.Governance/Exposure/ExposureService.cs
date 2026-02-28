using Mcp.Governance.Policy;
using System;

namespace Mcp.Governance.Exposure;

public interface IExposureService
{
    ExposureManifest Current { get; }

    /// <summary>
    /// Checks enabled AND returns policy in one dictionary lookup.
    /// Replaces the old pattern of calling IsEnabled() then TryGetToolPolicy() separately.
    /// </summary>
    bool TryGetEnabledPolicy(string toolName, out ToolPolicy policy);
}

public sealed class ExposureService : IExposureService
{
    private readonly ExposureManifest _manifest;

    public ExposureService(ExposureManifest manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    public ExposureManifest Current => _manifest;

    public bool TryGetEnabledPolicy(string toolName, out ToolPolicy policy)
    {
        if (!_manifest.GlobalEnabled)
        {
            policy = default!;
            return false;
        }

        if (!_manifest.Tools.TryGetValue(toolName, out var found) || !found.Enabled)
        {
            policy = default!;
            return false;
        }

        policy = found;
        return true;
    }
}