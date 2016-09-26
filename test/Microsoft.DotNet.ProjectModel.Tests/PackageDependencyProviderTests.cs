using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class PackageDependencyProviderTests : TestBase
    {
        [Fact]
        public void GetDescriptionShouldLeavePackageLibraryPathAlone()
        {
            // Arrange
            var provider = new PackageDependencyProvider(
                NuGetPathContext.Create("/foo/packages"),
                new FrameworkReferenceResolver("/foo/references"));
            var package = new LockFileLibrary();
            package.Name = "Something";
            package.Version = NuGetVersion.Parse("1.0.0");
            package.Files.Add("lib/dotnet/_._");
            package.Files.Add("runtimes/any/native/Microsoft.CSharp.CurrentVersion.targets");
            package.Path = "SomePath";

            var target = new LockFileTargetLibrary();
            target.Name = "Something";
            target.Version = package.Version;

            target.RuntimeAssemblies.Add("lib/dotnet/_._");
            target.CompileTimeAssemblies.Add("lib/dotnet/_._");
            target.NativeLibraries.Add("runtimes/any/native/Microsoft.CSharp.CurrentVersion.targets");

            // Act
            var p = provider.GetDescription(NuGetFramework.Parse("netcoreapp1.0"), package, target);

            // Assert
            p.PackageLibrary.Path.Should().Be("SomePath");
        }

        [Fact]
        public void GetDescriptionShouldGenerateHashFileName()
        {
            // Arrange
            var provider = new PackageDependencyProvider(
                NuGetPathContext.Create("/foo/packages"),
                new FrameworkReferenceResolver("/foo/references"));
            var package = new LockFileLibrary();
            package.Name = "Something";
            package.Version = NuGetVersion.Parse("1.0.0-Beta");
            package.Files.Add("lib/dotnet/_._");
            package.Files.Add("runtimes/any/native/Microsoft.CSharp.CurrentVersion.targets");
            package.Path = "SomePath";

            var target = new LockFileTargetLibrary();
            target.Name = "Something";
            target.Version = package.Version;

            target.RuntimeAssemblies.Add("lib/dotnet/_._");
            target.CompileTimeAssemblies.Add("lib/dotnet/_._");
            target.NativeLibraries.Add("runtimes/any/native/Microsoft.CSharp.CurrentVersion.targets");

            // Act
            var p = provider.GetDescription(NuGetFramework.Parse("netcoreapp1.0"), package, target);

            // Assert
            p.PackageLibrary.Path.Should().Be("SomePath");
            p.HashPath.Should().Be("something.1.0.0-beta.nupkg.sha512");
        }

        [Fact]
        public void GetDescriptionShouldNotModifyTarget()
        {
            var provider = new PackageDependencyProvider(
                NuGetPathContext.Create("/foo/packages"),
                new FrameworkReferenceResolver("/foo/references"));
            var package = new LockFileLibrary();
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

            var p1 = provider.GetDescription(NuGetFramework.Parse("netcoreapp1.0"), package, target);
            var p2 = provider.GetDescription(NuGetFramework.Parse("netcoreapp1.0"), package, target);

            Assert.True(p1.Compatible);
            Assert.True(p2.Compatible);

            Assert.Empty(p1.CompileTimeAssemblies);
            Assert.Empty(p1.RuntimeAssemblies);

            Assert.Empty(p2.CompileTimeAssemblies);
            Assert.Empty(p2.RuntimeAssemblies);
        }

        [Fact]
        public void HasCompileTimePlaceholderChecksAllCompileTimeAssets()
        {
            var provider = new PackageDependencyProvider(
                NuGetPathContext.Create("/foo/packages"),
                new FrameworkReferenceResolver("/foo/references"));
            var package = new LockFileLibrary();
            package.Name = "Something";
            package.Version = NuGetVersion.Parse("1.0.0");
            package.Files.Add("lib/net46/_._");
            package.Files.Add("lib/net46/Something.dll");

            var target = new LockFileTargetLibrary();
            target.Name = "Something";
            target.Version = package.Version;

            target.RuntimeAssemblies.Add("lib/net46/_._");
            target.RuntimeAssemblies.Add("lib/net46/Something.dll");
            target.CompileTimeAssemblies.Add("lib/net46/_._");
            target.CompileTimeAssemblies.Add("lib/net46/Something.dll");

            var p1 = provider.GetDescription(NuGetFramework.Parse("net46"), package, target);
            
            Assert.False(p1.HasCompileTimePlaceholder);
            Assert.Equal(1, p1.CompileTimeAssemblies.Count());
            Assert.Equal(1, p1.RuntimeAssemblies.Count());
            Assert.Equal("lib/net46/Something.dll", p1.CompileTimeAssemblies.First().Path);
            Assert.Equal("lib/net46/Something.dll", p1.RuntimeAssemblies.First().Path);
        }
        
        [Fact]
        public void HasCompileTimePlaceholderReturnsFalseIfEmpty()
        {
            var provider = new PackageDependencyProvider(
                NuGetPathContext.Create("/foo/packages"),
                new FrameworkReferenceResolver("/foo/references"));
            var package = new LockFileLibrary();
            package.Name = "Something";
            package.Version = NuGetVersion.Parse("1.0.0");

            var target = new LockFileTargetLibrary();
            target.Name = "Something";
            target.Version = package.Version;

            var p1 = provider.GetDescription(NuGetFramework.Parse("net46"), package, target);
            
            Assert.False(p1.HasCompileTimePlaceholder);
            Assert.Equal(0, p1.CompileTimeAssemblies.Count());
            Assert.Equal(0, p1.RuntimeAssemblies.Count());
        }

        [Theory]
        [InlineData("TestMscorlibReference", true)]
        [InlineData("TestMscorlibReference", false)]
        [InlineData("TestMicrosoftCSharpReference", true)]
        [InlineData("TestMicrosoftCSharpReference", false)]
        [InlineData("TestSystemReference", true)]
        [InlineData("TestSystemReference", false)]
        [InlineData("TestSystemCoreReference", true)]
        [InlineData("TestSystemCoreReference", false)]
        public void TestDuplicateDefaultDesktopReferences(string sampleName, bool withLockFile)
        {
            var instance = TestAssetsManager.CreateTestInstance(sampleName);
            if (withLockFile)
            {
                instance = instance.WithLockFiles();
            }

            var context = new ProjectContextBuilder().WithProjectDirectory(instance.TestRoot)
                                                     .WithTargetFramework("net451")
                                                     .Build();

            Assert.Equal(4, context.RootProject.Dependencies.Count());
        }

        [Fact]
        public void NoDuplicateReferencesWhenFrameworkMissing()
        {
            var instance = TestAssetsManager.CreateTestInstance("TestMicrosoftCSharpReferenceMissingFramework")
                                            .WithLockFiles();

            var context = new ProjectContextBuilder().WithProjectDirectory(instance.TestRoot)
                                                     .WithTargetFramework("net99")
                                                     .Build();

            // Will fail with dupes if any
            context.LibraryManager.GetLibraries().ToDictionary(l => l.Identity.Name, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void NetCore50ShouldNotResolveFrameworkAssemblies()
        {
            var instance = TestAssetsManager.CreateTestInstance("TestMicrosoftCSharpReferenceMissingFramework")
                                            .WithLockFiles();

            var context = new ProjectContextBuilder().WithProjectDirectory(instance.TestRoot)
                                                     .WithTargetFramework("netcore50")
                                                     .Build();

            var diagnostics = context.LibraryManager.GetAllDiagnostics();
            Assert.False(diagnostics.Any(d => d.ErrorCode == ErrorCodes.DOTNET1011));
        }

        [Fact]
        public void NoDuplicatesWithProjectAndReferenceAssemblyWithSameName()
        {
            var instance = TestAssetsManager.CreateTestInstance("DuplicatedReferenceAssembly")
                                            .WithLockFiles();
            var context = new ProjectContextBuilder().WithProjectDirectory(Path.Combine(instance.TestRoot, "TestApp"))
                                                     .WithTargetFramework("net461")
                                                     .Build();

            // Will fail with dupes if any
            context.LibraryManager.GetLibraries().ToDictionary(l => l.Identity.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}
