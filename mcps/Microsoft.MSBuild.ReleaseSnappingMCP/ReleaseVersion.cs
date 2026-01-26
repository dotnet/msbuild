namespace Microsoft.MSBuild.ReleaseSnappingMCP;

/// <summary>
/// Represents MSBuild release version information with computed related versions.
/// </summary>
public sealed class ReleaseVersion
{
    public string Current { get; }
    public string Previous { get; }
    public string Next { get; }
    public string BranchName { get; }
    public string PreviousBranchName { get; }
    public string NextBranchName { get; }
    public string DarcChannel { get; }
    public string NextDarcChannel { get; }
    public string VsRelBranch { get; }
    public string NextVsRelBranch { get; }

    public ReleaseVersion(string version)
    {
        Current = version;

        // Parse version to compute previous and next
        var parts = version.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor))
        {
            throw new ArgumentException($"Invalid version format: {version}. Expected format like '17.13' or '18.0'");
        }

        // Compute previous version
        if (minor == 0)
        {
            Previous = $"{major - 1}.12"; // Assuming 12 is max minor for previous major
        }
        else
        {
            Previous = $"{major}.{minor - 1}";
        }

        // Compute next version
        Next = $"{major}.{minor + 1}";

        // Branch names
        BranchName = $"vs{version}";
        PreviousBranchName = $"vs{Previous}";
        NextBranchName = $"vs{Next}";

        // DARC channels
        DarcChannel = $"VS {version}";
        NextDarcChannel = $"VS {Next}";

        // VS rel branches
        VsRelBranch = $"rel/d{version}";
        NextVsRelBranch = $"rel/d{Next}";
    }

    public override string ToString() => Current;
}
