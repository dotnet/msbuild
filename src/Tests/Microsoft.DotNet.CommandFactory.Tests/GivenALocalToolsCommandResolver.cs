// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tests
{
    public class GivenALocalToolsCommandResolver : SdkTest
    {
        private const string ManifestFilename = "dotnet-tools.json";
        private readonly string _testDirectoryRoot;
        private DirectoryPath _nugetGlobalPackagesFolder;
        private readonly LocalToolsResolverCache _localToolsResolverCache;
        private readonly IFileSystem _fileSystem;

        public GivenALocalToolsCommandResolver(ITestOutputHelper log) : base(log)
        {
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _nugetGlobalPackagesFolder = new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation());
            string temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _testDirectoryRoot = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _localToolsResolverCache = new LocalToolsResolverCache(
                _fileSystem,
                new DirectoryPath(Path.Combine(temporaryDirectory, "cache")));
        }

        [Fact]
        public void WhenResolveStrictItCanFindToolExecutable()
        {
            (FilePath fakeExecutable, LocalToolsCommandResolver localToolsCommandResolver) = DefaultSetup(toolCommand: "a");

            var result = localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
            {
                CommandName = "dotnet-a",
            });

            result.Should().NotBeNull();

            var commandPath = result.Args.Trim('"');
            _fileSystem.File.Exists(commandPath).Should().BeTrue("the following path exists: " + commandPath);
            commandPath.Should().Be(fakeExecutable.Value);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("dotnet-a")]
        public void WhenResolveItCanFindToolExecutable(string toolCommand)
        {
            (FilePath fakeExecutable, LocalToolsCommandResolver localToolsCommandResolver) = DefaultSetup(toolCommand);

            var result = localToolsCommandResolver.Resolve(new CommandResolverArguments()
            {
                CommandName = "dotnet-a",
            });

            result.Should().NotBeNull();

            var commandPath = result.Args.Trim('"');
            _fileSystem.File.Exists(commandPath).Should().BeTrue("the following path exists: " + commandPath);
            commandPath.Should().Be(fakeExecutable.Value);
        }

        [Fact]
        public void WhenResolveWithNoArgumentsItReturnsNull()
        {
            (FilePath fakeExecutable, LocalToolsCommandResolver localToolsCommandResolver) = DefaultSetup("-d");

            var result = localToolsCommandResolver.Resolve(new CommandResolverArguments()
            {
                CommandName = "-d",
            });

            result.Should().BeNull();
        }

        private (FilePath, LocalToolsCommandResolver) DefaultSetup(string toolCommand)
        {
            NuGetVersion packageVersionA = NuGetVersion.Parse("1.0.4");
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, ManifestFilename),
                _jsonContent.Replace("$TOOLCOMMAND$", toolCommand));
            ToolManifestFinder toolManifest =
                new(new DirectoryPath(_testDirectoryRoot), _fileSystem, new FakeDangerousFileDetector());
            ToolCommandName toolCommandNameA = new(toolCommand);
            FilePath fakeExecutable = _nugetGlobalPackagesFolder.WithFile("fakeExecutable.dll");
            _fileSystem.Directory.CreateDirectory(_nugetGlobalPackagesFolder.Value);
            _fileSystem.File.CreateEmptyFile(fakeExecutable.Value);
            _localToolsResolverCache.Save(
                new Dictionary<RestoredCommandIdentifier, RestoredCommand>
                {
                    [new RestoredCommandIdentifier(
                            new PackageId("local.tool.console.a"),
                            packageVersionA,
                            NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                            Constants.AnyRid,
                            toolCommandNameA)]
                        = new RestoredCommand(toolCommandNameA, "dotnet", fakeExecutable)
                });

            var localToolsCommandResolver = new LocalToolsCommandResolver(
                toolManifest,
                _localToolsResolverCache,
                _fileSystem);

            return (fakeExecutable, localToolsCommandResolver);
        }

        [Fact]
        public void WhenNuGetGlobalPackageLocationIsCleanedAfterRestoreItShowError()
        {
            ToolCommandName toolCommandNameA = new("a");
            NuGetVersion packageVersionA = NuGetVersion.Parse("1.0.4");
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, ManifestFilename),
                _jsonContent.Replace("$TOOLCOMMAND$", toolCommandNameA.Value));
            ToolManifestFinder toolManifest =
                new(new DirectoryPath(_testDirectoryRoot), _fileSystem, new FakeDangerousFileDetector());

            var fakeExecutable = _nugetGlobalPackagesFolder.WithFile("fakeExecutable.dll");
            _fileSystem.Directory.CreateDirectory(_nugetGlobalPackagesFolder.Value);
            _fileSystem.File.CreateEmptyFile(fakeExecutable.Value);
            _localToolsResolverCache.Save(
                new Dictionary<RestoredCommandIdentifier, RestoredCommand>
                {
                    [new RestoredCommandIdentifier(
                            new PackageId("local.tool.console.a"),
                            packageVersionA,
                            NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                            Constants.AnyRid,
                            toolCommandNameA)]
                        = new RestoredCommand(toolCommandNameA, "dotnet", fakeExecutable)
                });

            var localToolsCommandResolver = new LocalToolsCommandResolver(
                toolManifest,
                _localToolsResolverCache,
                _fileSystem);

            _fileSystem.File.Delete(fakeExecutable.Value);

            Action action = () => localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
            {
                CommandName = $"dotnet-{toolCommandNameA.ToString()}",
            });

            action.Should().Throw<GracefulException>(string.Format(CommandFactory.LocalizableStrings.NeedRunToolRestore,
                toolCommandNameA.ToString()));
        }

        [Fact]
        public void WhenNuGetGlobalPackageLocationIsNotRestoredItThrowsGracefulException()
        {
            ToolCommandName toolCommandNameA = new("a");
            NuGetVersion packageVersionA = NuGetVersion.Parse("1.0.4");
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, ManifestFilename),
                _jsonContent.Replace("$TOOLCOMMAND$", toolCommandNameA.Value));
            ToolManifestFinder toolManifest =
                new(new DirectoryPath(_testDirectoryRoot), _fileSystem);

            var localToolsCommandResolver = new LocalToolsCommandResolver(
                toolManifest,
                _localToolsResolverCache,
                _fileSystem);

            Action action = () => localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
            {
                CommandName = $"dotnet-{toolCommandNameA.ToString()}",
            });

            action.Should().Throw<GracefulException>(string.Format(CommandFactory.LocalizableStrings.NeedRunToolRestore,
                toolCommandNameA.ToString()));
        }

        [Fact]
        public void ItCanResolveAmbiguityCausedByPrefixDotnetDash()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, ManifestFilename),
                _jsonContentWithDotnetDash);
            ToolManifestFinder toolManifest =
                new(new DirectoryPath(_testDirectoryRoot), _fileSystem);

            var fakeExecutableA = _nugetGlobalPackagesFolder.WithFile("fakeExecutable-a.dll");
            var fakeExecutableDotnetA = _nugetGlobalPackagesFolder.WithFile("fakeExecutable-a.dll");
            _fileSystem.Directory.CreateDirectory(_nugetGlobalPackagesFolder.Value);
            _fileSystem.File.CreateEmptyFile(fakeExecutableA.Value);
            _fileSystem.File.CreateEmptyFile(fakeExecutableDotnetA.Value);
            _localToolsResolverCache.Save(
                new Dictionary<RestoredCommandIdentifier, RestoredCommand>
                {
                    [new RestoredCommandIdentifier(
                            new PackageId("local.tool.console.a"),
                            NuGetVersion.Parse("1.0.4"),
                            NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                            Constants.AnyRid,
                            new ToolCommandName("a"))]
                        = new RestoredCommand(new ToolCommandName("a"), "dotnet", fakeExecutableA),
                    [new RestoredCommandIdentifier(
                            new PackageId("local.tool.console.dotnet.a"),
                            NuGetVersion.Parse("1.0.4"),
                            NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                            Constants.AnyRid,
                            new ToolCommandName("dotnet-a"))]
                        = new RestoredCommand(new ToolCommandName("dotnet-a"), "dotnet", fakeExecutableDotnetA)
                });

            var localToolsCommandResolver = new LocalToolsCommandResolver(
               toolManifest,
               _localToolsResolverCache,
               _fileSystem);

            localToolsCommandResolver.Resolve(new CommandResolverArguments()
            {
                CommandName = "dotnet-a",
            }).Args.Trim('"').Should().Be(fakeExecutableA.Value);

            localToolsCommandResolver.Resolve(new CommandResolverArguments()
            {
                CommandName = "dotnet-dotnet-a",
            }).Args.Trim('"').Should().Be(fakeExecutableDotnetA.Value);
        }

        private string _jsonContent =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""local.tool.console.a"":{
         ""version"":""1.0.4"",
         ""commands"":[
            ""$TOOLCOMMAND$""
         ]
      }
   }
}";


        private string _jsonContentWithDotnetDash =
                @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""local.tool.console.a"":{
         ""version"":""1.0.4"",
         ""commands"":[
            ""a""
         ]
      },
      ""local.tool.console.dotnet.a"":{
         ""version"":""1.0.4"",
         ""commands"":[
            ""dotnet-a""
         ]
      }
   }
}";
    }

    internal class FakeDangerousFileDetector : IDangerousFileDetector
    {
        public bool IsDangerous(string filePath)
        {
            return false;
        }
    }
}
