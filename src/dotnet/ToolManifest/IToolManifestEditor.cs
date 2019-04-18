using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolManifest
{
    internal interface IToolManifestEditor
    {
        void Add(FilePath manifest, PackageId packageId, NuGetVersion nuGetVersion, ToolCommandName[] toolCommandNames);
        void Remove(FilePath manifest, PackageId packageId);
        void Edit(FilePath manifest, PackageId packageId, NuGetVersion newNuGetVersion, ToolCommandName[] newToolCommandNames);
    }
}
