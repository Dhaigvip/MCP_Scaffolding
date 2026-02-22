namespace Mcp.Governance.Exposure;

public interface IExposureService
{
    ExposureManifest Current { get; }
    bool IsEnabled(string toolName);
    bool TryGetToolPolicy(string toolName, out Policy.ToolPolicy policy);
}

public sealed class ExposureService : IExposureService
{
    private readonly ExposureManifest _manifest;

    public ExposureService(ExposureManifest manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    public ExposureManifest Current => _manifest;

    public bool IsEnabled(string toolName)
    {
        if (!_manifest.GlobalEnabled) return false;
        if (!_manifest.Tools.TryGetValue(toolName, out var policy)) return false;
        return policy.Enabled;
    }

    public bool TryGetToolPolicy(string toolName, out Policy.ToolPolicy policy)
    {
        if (_manifest.Tools.TryGetValue(toolName, out var found))
        {
            policy = found;
            return true;
        }

        policy = default!;
        return false;
    }
}