// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class CacheCommand : TestCommand
    {
        private List<string> _profileProject = new List<string>();
        private string _framework;
        private string _output;
        private string _runtime;
        private string _frameworkVersion;
        private string _intermediateWorkingDirectory;

        public CacheCommand()
            : base("dotnet")
        {
        }

        public CacheCommand WithEntries(string profileProject)
        {
            _profileProject.Add($"--entries {profileProject}");

            return this;
        }
        public CacheCommand WithFramework(string framework)
        {
            _framework = framework;
            return this;
        }

        public CacheCommand WithFramework(NuGetFramework framework)
        {
            return WithFramework(framework.GetShortFolderName());
        }

        public CacheCommand WithOutput(string output)
        {
            _output = output;
            return this;
        }

        public CacheCommand WithRuntime(string runtime)
        {
            _runtime = runtime;
            return this;
        }

        public CacheCommand WithRuntimeFrameworkVersion(string frameworkVersion)
        {
            _frameworkVersion = frameworkVersion;
            return this;
        }

        public CacheCommand WithIntermediateWorkingDirectory(string intermediateWorkingDirectory)
        {
            _intermediateWorkingDirectory = intermediateWorkingDirectory;
            return this;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"cache {BuildArgs()} {args}";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"cache {BuildArgs()} {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        private string BuildArgs()
        {
            return string.Join(" ",
                ProfileProjectOption,
                FrameworkOption,
                OutputOption,
                IntermediateWorkingDirectoryOption,
                RuntimeOption,
                FrameworkVersionOption);
        }

        private string ProfileProjectOption =>  string.Join(" ", _profileProject) ;

        private string FrameworkOption => string.IsNullOrEmpty(_framework) ? "" : $"-f {_framework}";

        private string OutputOption => string.IsNullOrEmpty(_output) ? "" : $"-o {_output}";

        private string RuntimeOption => string.IsNullOrEmpty(_runtime) ? "" : $"-r {_runtime}";

        private string FrameworkVersionOption => string.IsNullOrEmpty(_frameworkVersion) ? "" : $" --framework-version {_frameworkVersion}";

        private string IntermediateWorkingDirectoryOption => string.IsNullOrEmpty(_intermediateWorkingDirectory)? "" : $" -w {_intermediateWorkingDirectory}";
    }
}
