// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageObtainerMock : IToolPackageObtainer
    {
        private readonly string _toolsPath;
        public const string FakeEntrypointName = "SimulatorEntryPoint.dll";
        public const string FakeCommandName = "SimulatorCommand";
        private readonly Action _beforeRunObtain;
        private readonly Action _duringObtain;
        private static IFileSystem _fileSystem;
        private string _fakeExecutableDirectory;
        private List<MockFeed> _mockFeeds;
        private string _packageIdVersionDirectory;

        public ToolPackageObtainerMock(
            IFileSystem fileSystemWrapper = null,
            bool useDefaultFeed = true,
            IEnumerable<MockFeed> additionalFeeds = null,
            Action beforeRunObtain = null,
            Action duringObtain = null,
            string toolsPath = null)
        {
            _toolsPath = toolsPath ?? "toolsPath";
            _beforeRunObtain = beforeRunObtain ?? (() => {});
            _duringObtain = duringObtain ?? (() => {});
            _fileSystem = fileSystemWrapper ?? new FileSystemWrapper();
            _mockFeeds = new List<MockFeed>();

            if (useDefaultFeed)
            {
                _mockFeeds.Add(new MockFeed
                {
                    Type = MockFeedType.FeedFromLookUpNugetConfig,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = "global.tool.console.demo",
                                    Version = "1.0.4"
                            }
                        }
                });
            }

            if (additionalFeeds != null)
            {
                _mockFeeds.AddRange(additionalFeeds);
            }
        }

        public ToolConfigurationAndExecutablePath ObtainAndReturnExecutablePath(
            string packageId,
            string packageVersion = null,
            FilePath? nugetconfig = null,
            string targetframework = null,
            string source = null,
            string verbosity = null)
        {
            var stagedFile = Path.Combine(_toolsPath, ".stage", Path.GetRandomFileName());
            bool afterStage = false;

            var toolPackageObtainTransaction = new ToolPackageObtainTransaction(
                obtainAndReturnExecutablePath: (_) =>
                {
                    if (Directory.Exists(Path.Combine(_toolsPath, packageId)))
                    {
                        throw new PackageObtainException(
                            string.Format(CommonLocalizableStrings.ToolPackageConflictPackageId, packageId));
                    }

                    _beforeRunObtain();

                    PickFeedByNugetConfig(nugetconfig);
                    PickFeedBySource(source);

                    MockFeedPackage package = _mockFeeds
                        .SelectMany(f => f.Packages)
                        .Where(p => MatchPackageVersion(p, packageId, packageVersion)).OrderByDescending(p => p.Version)
                        .FirstOrDefault();

                    if (package == null)
                    {
                        throw new PackageObtainException("simulated cannot find package");
                    }

                    packageVersion = package.Version;
                    targetframework = targetframework ?? "targetframework";

                    _packageIdVersionDirectory = Path.Combine(_toolsPath, packageId, packageVersion);

                    _fakeExecutableDirectory = Path.Combine(_packageIdVersionDirectory,
                        packageId, packageVersion, "morefolders", "tools",
                        targetframework);

                    SimulateStageFile();
                    _duringObtain();

                    _fileSystem.File.Delete(stagedFile);
                    afterStage = true;

                    _fileSystem.Directory.CreateDirectory(_packageIdVersionDirectory);
                    _fileSystem.File.CreateEmptyFile(Path.Combine(_packageIdVersionDirectory, "project.assets.json"));
                    _fileSystem.Directory.CreateDirectory(_fakeExecutableDirectory);
                    var fakeExecutable = Path.Combine(_fakeExecutableDirectory, FakeEntrypointName);
                    _fileSystem.File.CreateEmptyFile(fakeExecutable);

                    return new ToolConfigurationAndExecutablePath(
                        toolConfiguration: new ToolConfiguration(FakeCommandName, FakeEntrypointName),
                        executable : new FilePath(fakeExecutable));;
                },

                rollback: (_) =>
                {
                    if (afterStage == false)
                    {
                        if (_fileSystem.File.Exists(stagedFile))
                        {
                            _fileSystem.File.Delete(stagedFile);
                        }
                    }
                    else
                    {
                        if (_fileSystem.Directory.Exists(Path.Combine(_toolsPath, packageId)))
                        {
                            _fileSystem.Directory.Delete(Path.Combine(_toolsPath, packageId), true);
                        }
                    }
                }
            );

            using(var transactionScope = new TransactionScope())
            {
                Transaction.Current.EnlistVolatile(toolPackageObtainTransaction, EnlistmentOptions.None);
                var toolConfigurationAndExecutablePath = toolPackageObtainTransaction.ObtainAndReturnExecutablePath();

                transactionScope.Complete();
                return toolConfigurationAndExecutablePath;
            }
        }

        private void SimulateStageFile()
        {
            var stageDirectory = Path.Combine(_toolsPath, ".stage");
            if (!_fileSystem.Directory.Exists(stageDirectory))
            {
                _fileSystem.Directory.CreateDirectory(stageDirectory);
            }

            _fileSystem.File.CreateEmptyFile(Path.Combine(stageDirectory, "stagedfile"));
        }

        private void PickFeedBySource(string source)
        {
            if (source != null)
            {
                var feed = _mockFeeds.SingleOrDefault(
                    f => f.Type == MockFeedType.Source
                         && f.Uri == source);

                if (feed != null)
                {
                    _mockFeeds = new List<MockFeed>
                    {
                        feed
                    };
                }
                else
                {
                    _mockFeeds = new List<MockFeed>();
                }
            }
        }

        private void PickFeedByNugetConfig(FilePath? nugetconfig)
        {
            if (nugetconfig != null)
            {
                if (!_fileSystem.File.Exists(nugetconfig.Value.Value))
                {
                    throw new PackageObtainException(
                        string.Format(CommonLocalizableStrings.NuGetConfigurationFileDoesNotExist,
                            Path.GetFullPath(nugetconfig.Value.Value)));
                }

                var feed = _mockFeeds.SingleOrDefault(
                    f => f.Type == MockFeedType.ExplicitNugetConfig
                         && f.Uri == nugetconfig.Value.Value);

                if (feed != null)
                {
                    _mockFeeds = new List<MockFeed>
                    {
                        feed
                    };
                }
                else
                {
                    _mockFeeds = new List<MockFeed>();
                }
            }
        }

        private static bool MatchPackageVersion(MockFeedPackage p, string packageId, string packageVersion)
        {
            if (packageVersion == null)
            {
                return p.PackageId == packageId;
            }
            return p.PackageId == packageId && p.Version == packageVersion;
        }
    }
}
