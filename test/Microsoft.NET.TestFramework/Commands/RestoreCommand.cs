// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class RestoreCommand : TestCommand
    {
        private List<string> _sources = new List<string>();
        private bool _captureStdOut;

        public RestoreCommand(MSBuildTest msbuild, string projectPath, string relativePathToProject = null)
            : base(msbuild, projectPath, relativePathToProject)
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

        public RestoreCommand CaptureStdOut()
        {
            _captureStdOut = true;
            return this;
        }

        public override CommandResult Execute(params string[] args)
        {
            var newArgs = new List<string>();

            newArgs.Add(FullPathProjectFile);

            if (_sources.Any())
            {
                newArgs.Add($"/p:RestoreSources={string.Join("%3B", _sources)}");
            }

            newArgs.AddRange(args);

            var command = MSBuild.CreateCommandForTarget("restore", newArgs.ToArray());

            if (_captureStdOut)
            {
                command = command.CaptureStdOut();
            }

            return command.Execute();
        }
    }
}
