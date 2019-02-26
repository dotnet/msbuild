// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using Xunit.Abstractions;
using System;

namespace Microsoft.NET.TestFramework.Commands
{
    public class NuGetRestoreCommand : TestCommand
    {
        private List<string> _sources = new List<string>();
        
        private readonly string _projectRootPath;
        public string ProjectRootPath => _projectRootPath;

        public string ProjectFile { get; }

        public string FullPathProjectFile => Path.Combine(ProjectRootPath, ProjectFile);

        public NuGetRestoreCommand(ITestOutputHelper log, string projectRootPath, string relativePathToProject = null) : base(log)
        {
            _projectRootPath = projectRootPath;
            ProjectFile = MSBuildCommand.FindProjectFile(ref _projectRootPath, relativePathToProject);
        }

        public NuGetRestoreCommand AddSource(string source)
        {
            _sources.Add(source);
            return this;
        }

        public NuGetRestoreCommand AddSourcesFromCurrentConfig()
        {
            var settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(), null, null);
            var packageSourceProvider = new PackageSourceProvider(settings);

            foreach (var packageSource in packageSourceProvider.LoadPackageSources())
            {
                _sources.Add(packageSource.Source);
            }

            return this;
        }

        protected override SdkCommandSpec CreateCommand(params string[] args)
        {
            var newArgs = new List<string>();

            newArgs.Add("restore");

            if (_sources.Any())
            {
                newArgs.Add("-Source");
                newArgs.Add(string.Join(";", _sources));
            }

            newArgs.Add(FullPathProjectFile);

            newArgs.Add("-PackagesDirectory");
            newArgs.Add(TestContext.Current.NuGetCachePath);

            newArgs.AddRange(args);

            if (string.IsNullOrEmpty(TestContext.Current.NuGetExePath))
            {
                throw new InvalidOperationException("Path to nuget.exe not set");
            }
            else if (!File.Exists(TestContext.Current.NuGetExePath))
            {
                //  https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
                var client = new System.Net.WebClient();
                client.DownloadFile("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", TestContext.Current.NuGetExePath);
            }

            var ret = new SdkCommandSpec()
            {
                FileName = TestContext.Current.NuGetExePath,
                Arguments = newArgs
            };

            return ret;
        }
    }
}
