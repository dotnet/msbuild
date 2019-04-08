using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolManifest
{
    internal interface IToolManifestEditor
    {
        void Add(FilePath to, PackageId packageId, NuGetVersion nuGetVersion, ToolCommandName[] toolCommandNames);
        void Remove(FilePath fromFilePath, PackageId packageId);
        void Edit(FilePath to, PackageId packageId, NuGetVersion newNuGetVersion, ToolCommandName[] newToolCommandNames);
    }
}
