using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class PackageDependencyProviderTests : TestBase
    {
        [Fact]
        public void GetDescriptionShouldNotModifyTarget()
        {
            var provider = new PackageDependencyProvider("/foo/packages", new FrameworkReferenceResolver("/foo/references"));
            var package = new LockFilePackageLibrary();
            package.Name = "Something";
            package.Version = NuGetVersion.Parse("1.0.0");
            package.Files.Add("lib/dotnet/_._");
            package.Files.Add("runtimes/any/native/Microsoft.CSharp.CurrentVersion.targets");

            var target = new LockFileTargetLibrary();
            target.Name = "Something";
            target.Version = package.Version;

            target.RuntimeAssemblies.Add("lib/dotnet/_._");
            target.CompileTimeAssemblies.Add("lib/dotnet/_._");
            target.NativeLibraries.Add("runtimes/any/native/Microsoft.CSharp.CurrentVersion.targets");

            var p1 = provider.GetDescription(NuGetFramework.Parse("netstandardapp1.5"), package, target);
            var p2 = provider.GetDescription(NuGetFramework.Parse("netstandardapp1.5"), package, target);

            Assert.True(p1.Compatible);
            Assert.True(p2.Compatible);

            Assert.Empty(p1.CompileTimeAssemblies);
            Assert.Empty(p1.RuntimeAssemblies);

            Assert.Empty(p2.CompileTimeAssemblies);
            Assert.Empty(p2.RuntimeAssemblies);
        }

        [Fact]
        public void SingleMicrosoftCSharpReference()
        {
            // https://github.com/dotnet/cli/issues/1602
            var instance = TestAssetsManager.CreateTestInstance("TestMicrosoftCSharpReference")
                                            .WithLockFiles();

            var context = new ProjectContextBuilder().WithProjectDirectory(instance.TestRoot)
                                                     .WithTargetFramework("dnx451")
                                                     .Build();

            Assert.Equal(4, context.RootProject.Dependencies.Count());
        }
    }
}
