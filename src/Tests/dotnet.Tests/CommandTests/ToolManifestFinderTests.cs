// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.ToolManifest.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolManifestFinderTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly List<ToolManifestPackage> _defaultExpectedResult;
        private readonly string _testDirectoryRoot;
        private const string _manifestFilename = "dotnet-tools.json";

        public ToolManifestFinderTests()
        {
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _testDirectoryRoot = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            _defaultExpectedResult = new List<ToolManifestPackage>
            {
                new ToolManifestPackage(
                    new PackageId("t-rex"),
                    NuGetVersion.Parse("1.0.53"),
                    new[] {new ToolCommandName("t-rex")},
                    new DirectoryPath(_testDirectoryRoot)),
                new ToolManifestPackage(
                    new PackageId("dotnetsay"),
                    NuGetVersion.Parse("2.1.4"),
                    new[] {new ToolCommandName("dotnetsay")},
                    new DirectoryPath(_testDirectoryRoot))
            };
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryItGetContent()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult = toolManifest.Find();

            AssertToolManifestPackageListEqual(_defaultExpectedResult, manifestResult);
        }

        [Fact]
        public void GivenManifestFileOnParentDirectoryItGetContent()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult = toolManifest.Find();

            AssertToolManifestPackageListEqual(_defaultExpectedResult, manifestResult);
        }

        [Fact]
        public void GivenManifestFileInDotConfigDirectoryItGetContent()
        {
            var dotnetconfigDirectory = Path.Combine(_testDirectoryRoot, ".config");
            _fileSystem.Directory.CreateDirectory(dotnetconfigDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(dotnetconfigDirectory, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult = toolManifest.Find();

            AssertToolManifestPackageListEqual(_defaultExpectedResult, manifestResult);
        }

        [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX)]
        public void GivenManifestFileInRootDirectoryForLinuxMacOSItGetsContent()
        {
            var rootDirectory = new DirectoryPath(_testDirectoryRoot);
            while (rootDirectory.GetParentPathNullable() != null)
            {
                rootDirectory = (DirectoryPath)rootDirectory.GetParentPathNullable();
            }
            var dotnetconfigDirectory = Path.Combine(rootDirectory.Value, ".config");
            _fileSystem.Directory.CreateDirectory(dotnetconfigDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(dotnetconfigDirectory, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult = toolManifest.Find();

            var expectedResult = new List<ToolManifestPackage>
            {
                new ToolManifestPackage(
                    new PackageId("t-rex"),
                    NuGetVersion.Parse("1.0.53"),
                    new[] {new ToolCommandName("t-rex")},
                    new DirectoryPath(rootDirectory.Value)),
                new ToolManifestPackage(
                    new PackageId("dotnetsay"),
                    NuGetVersion.Parse("2.1.4"),
                    new[] {new ToolCommandName("dotnetsay")},
                    new DirectoryPath(rootDirectory.Value))
            };

            AssertToolManifestPackageListEqual(expectedResult, manifestResult);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public void GivenManifestFileInRootDirectoryItThrowsError()
        {
            var rootDirectory = new DirectoryPath(_testDirectoryRoot);
            while (rootDirectory.GetParentPathNullable() != null)
            {
                rootDirectory = (DirectoryPath)rootDirectory.GetParentPathNullable();
            }
            var dotnetconfigDirectory = Path.Combine(rootDirectory.Value, ".config");
            _fileSystem.Directory.CreateDirectory(dotnetconfigDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(dotnetconfigDirectory, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestCannotBeFoundException>().And.Message.Should()
                .Contain(LocalizableStrings.CannotFindAManifestFile);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public void GivenManifestFileInRootDirectoryWithEnvVariableCHECK_MANIFEST_IN_ROOTToTrueItGetsContent()
        {
            var rootDirectory = new DirectoryPath(_testDirectoryRoot);
            while (rootDirectory.GetParentPathNullable() != null)
            {
                rootDirectory = (DirectoryPath)rootDirectory.GetParentPathNullable();
            }
            var dotnetconfigDirectory = Path.Combine(rootDirectory.Value, ".config");
            _fileSystem.Directory.CreateDirectory(dotnetconfigDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(dotnetconfigDirectory, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector(),
                    getEnvironmentVariable: name =>
                    {
                        if (name.Equals(EnvironmentVariableNames.DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT, StringComparison.OrdinalIgnoreCase))
                        {
                            return "true";
                        }
                        throw new ArgumentException($"Didn't expect environment query for {name}");
                    });

            var manifestResult = toolManifest.Find();

            var expectedResult = new List<ToolManifestPackage>
            {
                new ToolManifestPackage(
                    new PackageId("t-rex"),
                    NuGetVersion.Parse("1.0.53"),
                    new[] {new ToolCommandName("t-rex")},
                    new DirectoryPath(rootDirectory.Value)),
                new ToolManifestPackage(
                    new PackageId("dotnetsay"),
                    NuGetVersion.Parse("2.1.4"),
                    new[] {new ToolCommandName("dotnetsay")},
                    new DirectoryPath(rootDirectory.Value))
            };

            AssertToolManifestPackageListEqual(expectedResult, manifestResult);
        }

        [Fact]
        public void GivenManifestWithDuplicatedPackageIdItReturnsTheLastValue()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonWithDuplicatedPackagedId);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestException>().And.Message.Should()
                .Contain(string.Format(string.Format(LocalizableStrings.MultipleSamePackageId,
                    string.Join(", ", "t-rex")), ""));
        }

        [Fact]
        public void WhenCalledWithFilePathItGetContent()
        {
            string customFileName = "customname.file";
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, customFileName), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult =
                toolManifest.Find(new FilePath(Path.Combine(_testDirectoryRoot, customFileName)));

            AssertToolManifestPackageListEqual(_defaultExpectedResult, manifestResult);
        }

        private void AssertToolManifestPackageListEqual(
            IReadOnlyCollection<ToolManifestPackage> expect,
            IReadOnlyCollection<ToolManifestPackage> result)
        {
            result.Count().Should().Be(expect.Count());
            foreach (var package in expect)
            {
                result.Should().Contain(package);
            }
        }

        [Fact]
        public void WhenCalledWithNonExistsFilePathItThrows()
        {
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find(new FilePath(Path.Combine(_testDirectoryRoot, "non-exists")));
            a.Should().Throw<ToolManifestCannotBeFoundException>().And.Message.Should()
                .Contain(LocalizableStrings.CannotFindAManifestFile);

            a.Should().Throw<ToolManifestCannotBeFoundException>().And.VerboseMessage.Should()
                .Contain(string.Format(LocalizableStrings.ListOfSearched, ""))
                .And.Contain(
                    Path.Combine(_testDirectoryRoot, "non-exists"),
                    "the specificied manifest file name is in the 'searched list'");
        }

        [Fact]
        public void GivenNoManifestFileItThrows()
        {
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();
            a.Should().Throw<ToolManifestCannotBeFoundException>().And.Message.Should()
                 .Contain(LocalizableStrings.CannotFindAManifestFile);

            a.Should().Throw<ToolManifestCannotBeFoundException>().And.VerboseMessage.Should()
                .Contain(string.Format(LocalizableStrings.ListOfSearched, ""));
        }

        [Fact]
        public void GivenMissingFieldManifestFileItThrows()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithMissingField);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestException>().And.Message.Should().Contain(
                string.Format(LocalizableStrings.InvalidManifestFilePrefix,
                    Path.Combine(_testDirectoryRoot, _manifestFilename),
                    "\t" + string.Format(LocalizableStrings.InPackage, "t-rex",
                        ("\t\t" + LocalizableStrings.ToolMissingVersion + Environment.NewLine +
                        "\t\t" + LocalizableStrings.FieldCommandsIsMissing))));
        }

        [Fact]
        public void GivenInvalidFieldsManifestFileItThrows()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithInvalidField);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestException>().And.Message.Should()
                .Contain(string.Format(LocalizableStrings.VersionIsInvalid, "1.*"));
        }

        [Fact]
        public void GivenInvalidTypeManifestFileItThrows()
        {
            _fileSystem.File.WriteAllText(
                Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithInvalidType);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestException>()
                .And.Message.Should().Contain(string.Format(LocalizableStrings.UnexpectedTypeInJson, "True|False", "isRoot"));
        }

        [Fact]
        public void GivenInvalidJsonIntergerManifestFileItThrows()
        {
            _fileSystem.File.WriteAllText(
                Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonInvalidJsonInterger);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestException>();
        }

        [Fact]
        public void GivenConflictedManifestFileInDifferentDirectoriesItReturnMergedContent()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subdirectoryOfTestRoot);
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonContentInParentDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(subdirectoryOfTestRoot, _manifestFilename),
                _jsonContentInCurrentDirectory);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult = toolManifest.Find();

            manifestResult.Should().Contain(
                p => p == new ToolManifestPackage(
                         new PackageId("t-rex"),
                         NuGetVersion.Parse("1.0.49"),
                         new[] { new ToolCommandName("t-rex") },
                         new DirectoryPath(subdirectoryOfTestRoot)),
                because: "when different manifest file has the same package id, " +
                         "only keep entry that is in the manifest close to current directory");
            manifestResult.Should().Contain(
                p => p == new ToolManifestPackage(
                         new PackageId("dotnetsay2"),
                         NuGetVersion.Parse("4.0.0"),
                         new[] { new ToolCommandName("dotnetsay2") },
                         new DirectoryPath(_testDirectoryRoot)));

            manifestResult.Should().Contain(
                p => p == new ToolManifestPackage(
                         new PackageId("dotnetsay"),
                         NuGetVersion.Parse("2.1.4"),
                         new[] { new ToolCommandName("dotnetsay") },
                         new DirectoryPath(subdirectoryOfTestRoot)),
                because: "combine both content in different manifests");
        }

        [Fact]
        public void GivenManifestFileInDifferentDirectoriesWhenFindContainPackageIdItCanGetResultInOrder()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subdirectoryOfTestRoot);
            string manifestFileInParentDirectory = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFileInParentDirectory,
                _jsonContentInParentDirectory);
            string manifestFileInSubDirectory = Path.Combine(subdirectoryOfTestRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFileInSubDirectory,
                _jsonContentInCurrentDirectory);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifests = toolManifest.FindByPackageId(new PackageId("t-rex"));

            manifests.Should().ContainInOrder(new List<FilePath>
            {
                new FilePath(manifestFileInSubDirectory),
                new FilePath(manifestFileInParentDirectory)
            }, "Order matters, the closest to probe start first.");

            var manifests2 = toolManifest.FindByPackageId(new PackageId("dotnetsay"));

            manifests2.Should().ContainInOrder(new List<FilePath>
            {
                new FilePath(manifestFileInSubDirectory)
            });

            var manifests3 = toolManifest.FindByPackageId(new PackageId("non-exist"));

            manifests3.Should().BeEmpty();
        }

        [Fact]
        public void GivenNoManifestFileWhenFindContainPackageIdItThrows()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subdirectoryOfTestRoot);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.FindByPackageId(new PackageId("t-rex"));

            a.Should().Throw<ToolManifestCannotBeFoundException>().And.Message.Should()
                .Contain(LocalizableStrings.CannotFindAManifestFile);

            a.Should().Throw<ToolManifestCannotBeFoundException>().And.VerboseMessage.Should()
                .Contain(string.Format(LocalizableStrings.ListOfSearched, ""));
        }

        [Fact]
        public void GivenConflictedManifestFileInDifferentDirectoriesItOnlyConsiderTheFirstIsRoot()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subdirectoryOfTestRoot);
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonContentInParentDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(subdirectoryOfTestRoot, _manifestFilename),
                _jsonContentInCurrentDirectoryIsRootTrue);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult = toolManifest.Find();

            manifestResult.Count.Should().Be(2, "only content in the current directory manifest file is considered");
        }

        [Fact]
        public void DifferentVersionOfManifestFileItShouldThrow()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContentHigherVersion);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestException>().And.Message.Should().Contain(string.Format(
                            LocalizableStrings.ManifestVersionHigherThanSupported,
                            99, 1));
        }

        [Fact]
        public void MissingIsRootInManifestFileItShouldThrow()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContentIsRootMissing);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.Find();

            a.Should().Throw<ToolManifestException>().And.Message.Should().Contain(LocalizableStrings.ManifestMissingIsRoot);
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryWhenFindByCommandNameItGetContent()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            toolManifest.TryFind(new ToolCommandName("dotnetsay"), out var result).Should().BeTrue();

            result.Should().Be(new ToolManifestPackage(
                new PackageId("dotnetsay"),
                NuGetVersion.Parse("2.1.4"),
                new[] { new ToolCommandName("dotnetsay") },
                new DirectoryPath(_testDirectoryRoot)));
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryWhenFindByCommandNameWithDifferentCasingItGetContent()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            toolManifest.TryFind(new ToolCommandName("dotnetSay"), out var result).Should().BeTrue();

            result.Should().Be(new ToolManifestPackage(
                new PackageId("dotnetsay"),
                NuGetVersion.Parse("2.1.4"),
                new[] { new ToolCommandName("dotnetsay") },
                new DirectoryPath(_testDirectoryRoot)));
        }

        [Fact]
        public void GivenManifestFileOnParentDirectoryWhenFindByCommandNameItGetContent()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            toolManifest.TryFind(new ToolCommandName("dotnetsay"), out var result).Should().BeTrue();

            result.Should().Be(new ToolManifestPackage(
                new PackageId("dotnetsay"),
                NuGetVersion.Parse("2.1.4"),
                new[] { new ToolCommandName("dotnetsay") },
                new DirectoryPath(_testDirectoryRoot)));
        }

        [Fact]
        public void GivenNoManifestFileWhenFindByCommandNameItReturnFalse()
        {
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            toolManifest.TryFind(new ToolCommandName("dotnetSay"), out var result).Should().BeFalse();
        }

        [Fact]
        public void GivenMissingFieldManifestFileWhenFindByCommandNameItThrows()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithMissingField);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.TryFind(new ToolCommandName("dotnetSay"), out var result);
            a.Should().Throw<ToolManifestException>();
        }

        [Fact]
        public void GivenInvalidFieldsManifestFileWhenFindByCommandNameItThrows()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithInvalidField);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.TryFind(new ToolCommandName("dotnetSay"), out var result);
            a.Should().Throw<ToolManifestException>();
        }

        [Fact]
        public void GivenInvalidJsonManifestFileWhenFindByCommandNameItThrows()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContentInvalidJson);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.TryFind(new ToolCommandName("dotnetSay"), out var result);
            a.Should().Throw<ToolManifestException>();
        }

        [Fact]
        public void GivenConflictedManifestFileInDifferentFieldsWhenFindByCommandNameItReturnMergedContent()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subdirectoryOfTestRoot);
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonContentInParentDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(subdirectoryOfTestRoot, _manifestFilename),
                _jsonContentInCurrentDirectory);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            toolManifest.TryFind(new ToolCommandName("t-rex"), out var result).Should().BeTrue();

            result.Should().Be(new ToolManifestPackage(
                new PackageId("t-rex"),
                NuGetVersion.Parse("1.0.49"),
                new[] { new ToolCommandName("t-rex") },
                new DirectoryPath(subdirectoryOfTestRoot)));
        }

        [Fact]
        public void GivenConflictedManifestFileInDifferentFieldsWhenFindByCommandNameItOnlyConsiderTheFirstIsRoot()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subdirectoryOfTestRoot);
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonContentInParentDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(subdirectoryOfTestRoot, _manifestFilename),
                _jsonContentInCurrentDirectoryIsRootTrue);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(subdirectoryOfTestRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            var manifestResult = toolManifest.Find();

            toolManifest.TryFind(new ToolCommandName("dotnetsay2"), out var result).Should().BeFalse();
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryWithMarkOfTheWebDetectorItThrows()
        {
            string manifestFilePath = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFilePath, _jsonContent);

            var fakeMarkOfTheWebDetector = new FakeDangerousFileDetector(manifestFilePath);
            var toolManifest
                = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem, fakeMarkOfTheWebDetector);
            Action a = () => toolManifest.Find();
            a.Should().Throw<ToolManifestException>()
                .And.Message

                .Should().Contain(
                string.Format(LocalizableStrings.ManifestHasMarkOfTheWeb, manifestFilePath),
                "The message is similar to Windows file property page");
        }

        [Fact]
        public void DifferentVersionOfManifestFileItThrows()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonContentHigherVersion);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());


            Action a = () => toolManifest.TryFind(new ToolCommandName("dotnetsay"), out var result);
            a.Should().Throw<ToolManifestException>();
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryItCanFindTheFirstManifestFile()
        {
            string manifestPath = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestPath, _jsonContent);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            FilePath toolmanifestFilePath = toolManifest.FindFirst();

            toolmanifestFilePath.Value.Should().Be(manifestPath);
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryItDoesNotThrowsWhenTheManifestFileIsNotValid()
        {
            string manifestPath = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestPath, _jsonWithMissingField);
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            FilePath toolmanifestFilePath = toolManifest.FindFirst();

            toolmanifestFilePath.Value.Should().Be(manifestPath);
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryItThrowsWhenTheManifestFileCannotBeFound()
        {
            var toolManifest =
                new ToolManifestFinder(
                    new DirectoryPath(_testDirectoryRoot),
                    _fileSystem,
                    new FakeDangerousFileDetector());

            Action a = () => toolManifest.FindFirst();

            a.Should().Throw<ToolManifestCannotBeFoundException>().And.Message.Should()
                .Contain(LocalizableStrings.CannotFindAManifestFile);

            a.Should().Throw<ToolManifestCannotBeFoundException>().And.VerboseMessage.Should()
                .Contain(string.Format(LocalizableStrings.ListOfSearched, ""));
        }

        [Fact]
        public void GivenConflictedManifestFileInDifferentDirectoriesItReturnMergedContentWithSourceManifestFile()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subdirectoryOfTestRoot);
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonContentInParentDirectory);
            _fileSystem.File.WriteAllText(Path.Combine(subdirectoryOfTestRoot, _manifestFilename),
                _jsonContentInCurrentDirectory);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(subdirectoryOfTestRoot), _fileSystem);
            var manifestResult = toolManifest.Inspect();

            manifestResult.Single(pair => pair.toolManifestPackage.PackageId.Equals(new PackageId("t-rex")))
                .SourceManifest.Value.Should().Be(Path.Combine(_testDirectoryRoot, "sub", _manifestFilename));

            manifestResult.Single(pair => pair.toolManifestPackage.PackageId.Equals(new PackageId("dotnetsay2")))
                .SourceManifest.Value.Should().Be(Path.Combine(_testDirectoryRoot, _manifestFilename));

            manifestResult.Single(pair => pair.toolManifestPackage.PackageId.Equals(new PackageId("dotnetsay")))
                .SourceManifest.Value.Should().Be(Path.Combine(_testDirectoryRoot, "sub", _manifestFilename));
        }

        [Fact]
        public void GivenNoManifestInspectShouldNotThrow()
        {
            var testRoot = Path.Combine(_testDirectoryRoot);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(testRoot), _fileSystem);
            Action a = () => toolManifest.Inspect();
            a.Should().NotThrow();
        }

        private string _jsonContent =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      },
      ""dotnetsay"":{
         ""version"":""2.1.4"",
         ""commands"":[
            ""dotnetsay""
         ]
      }
   }
}";

        private string _jsonWithDuplicatedPackagedId =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      },
      ""t-rex"":{
         ""version"":""2.1.4"",
         ""commands"":[
            ""t-rex""
         ]
      }
   }
}";

        private string _jsonWithMissingField =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""extra"":1
      }
   }
}";

        private string _jsonWithInvalidField =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.*"",
         ""commands"":[
            ""t-rex""
         ]
      }
   }
}";

        private string _jsonWithInvalidType =
            @"{
   ""version"":1,
   ""isRoot"":""true"",
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      }
   }
}";

        private string _jsonContentInCurrentDirectory =
            @"{
   ""version"":1,
   ""isRoot"":false,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.49"",
         ""commands"":[
            ""t-rex""
         ]
      },
      ""dotnetsay"":{
         ""version"":""2.1.4"",
         ""commands"":[
            ""dotnetsay""
         ]
      }
   }
}";

        private string _jsonContentInCurrentDirectoryIsRootTrue =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.49"",
         ""commands"":[
            ""t-rex""
         ]
      },
      ""dotnetsay"":{
         ""version"":""2.1.4"",
         ""commands"":[
            ""dotnetsay""
         ]
      }
   }
}";

        private string _jsonContentInParentDirectory =
            @"{
   ""version"":1,
   ""isRoot"":false,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      },
      ""dotnetsay2"":{
         ""version"":""4.0.0"",
         ""commands"":[
            ""dotnetsay2""
         ]
      }
   }
}";

        private string _jsonContentHigherVersion =
            @"{
   ""isRoot"":true,
   ""version"":99,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      },
      ""dotnetsay"":{
         ""version"":""2.1.4"",
         ""commands"":[
            ""dotnetsay""
         ]
      }
   }
}";

        private string _jsonContentIsRootMissing =
    @"{
   ""version"":1,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      }
   }
}";

        private string _jsonContentInvalidJson =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",,
         ""commands"":[
            ""t-rex""
         ]
      },
      ""dotnetsay"":{
         ""version"":""2.1.4"",
         ""commands"":[
            ""dotnetsay""
         ]
      }
   }
}";

        private string _jsonInvalidJsonInterger =
    @"{
   ""version"":1.23,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      }
   }
}";

    }
}
