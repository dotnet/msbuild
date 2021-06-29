using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class ResolveTargetingPackAssetsTests
    {
        [Fact]
        public void Given_ResolvedTargetingPacks_with_valid_PATH_in_PlatformManifest_It_resolves_TargetingPack()
        {
            string mockPackageDirectory = Path.Combine(Path.GetTempPath(), "dotnetSdkTests", Path.GetRandomFileName());

            string dataDir = Path.Combine(mockPackageDirectory, "data");
            Directory.CreateDirectory(dataDir);

            File.WriteAllText(Path.Combine(dataDir, "FrameworkList.xml"), _frameworkList);
            File.WriteAllText(Path.Combine(dataDir, "PlatformManifest.txt"), "");

            var task = new ResolveTargetingPackAssets();

            task.FrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
            };

            task.ResolvedTargetingPacks = new[]
            {
                new MockTaskItem("Microsoft.Windows.SDK.NET.Ref",
                    new Dictionary<string, string>()
                    {
                        {MetadataKeys.NuGetPackageId, "Microsoft.Windows.SDK.NET.Ref"},
                        {MetadataKeys.NuGetPackageVersion, "5.0.0-preview1"},
                        {MetadataKeys.PackageConflictPreferredPackages, "Microsoft.Windows.SDK.NET.Ref;"},
                        {MetadataKeys.PackageDirectory, mockPackageDirectory},
                        {MetadataKeys.Path, mockPackageDirectory},
                        {"TargetFramework", "net5.0"}
                    })
            };

            task.ProjectLanguage = "C#";

            task.Execute().Should().BeTrue();

            task.ReferencesToAdd[0].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, "lib/Microsoft.Windows.SDK.NET.dll"));
            task.PlatformManifests[0].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, $"data{Path.DirectorySeparatorChar}PlatformManifest.txt"));
            task.AnalyzersToAdd.Length.Should().Be(2);
            task.AnalyzersToAdd[0].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, "analyzers/dotnet/anyAnalyzer.dll"));
            task.AnalyzersToAdd[1].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, "analyzers/dotnet/cs/csAnalyzer.dll"));
        }

        private readonly string _frameworkList =
@"<FileList Name=""cswinrt .NET Core 5.0"">
  <File Type=""Managed"" Path=""lib/Microsoft.Windows.SDK.NET.dll"" PublicKeyToken=""null"" AssemblyName=""Microsoft.Windows.SDK.NET"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Path=""analyzers/dotnet/anyAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""anyAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""cs"" Path=""analyzers/dotnet/cs/csAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""csAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""vb"" Path=""analyzers/dotnet/vb/vbAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""vbAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
</FileList>";
    }
}
