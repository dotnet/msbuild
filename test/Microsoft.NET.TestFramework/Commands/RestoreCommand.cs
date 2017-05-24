// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class RestoreCommand : MSBuildCommand
    {
        private List<string> _sources = new List<string>();

        public RestoreCommand(ITestOutputHelper log, string projectPath, string relativePathToProject = null, MSBuildTest msbuild = null)
            : base(log, "Restore", projectPath, relativePathToProject, msbuild)
        {
        }

        public RestoreCommand AddSource(string source)
        {
            _sources.Add(source);
            return this;
        }

        public RestoreCommand AddSourcesFromCurrentConfig()
        {
            var settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(), null, null);
            var packageSourceProvider = new PackageSourceProvider(settings);

            foreach (var packageSource in packageSourceProvider.LoadPackageSources())
            {
                _sources.Add(packageSource.Source);
            }

            return this;
        }

        protected override ICommand CreateCommand(params string[] args)
        {
            var newArgs = new List<string>();

            newArgs.Add(FullPathProjectFile);

            if (_sources.Any())
            {
                newArgs.Add($"/p:RestoreSources={string.Join("%3B", _sources)}");
            }

            newArgs.AddRange(args);

            return MSBuild.CreateCommandForTarget("restore", newArgs.ToArray());
        }
    }
}
