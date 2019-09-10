// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class PublishCommand : DotnetCommand
    {
        private string _framework;
        private string _output;
        private string _runtime;
        private List<string> _targetManifests = new List<string>();
        private bool? _selfContained;

        public PublishCommand WithFramework(string framework)
        {
            _framework = framework;
            return this;
        }

        public PublishCommand WithFramework(NuGetFramework framework)
        {
            return WithFramework(framework.GetShortFolderName());
        }

        public PublishCommand WithOutput(string output)
        {
            _output = output;
            return this;
        }

        public PublishCommand WithRuntime(string runtime)
        {
            _runtime = runtime;
            return this;
        }

        public PublishCommand WithTargetManifest(string manifest)
        {
            _targetManifests.Add( $"--manifest {manifest}");
            return this;
        }

        public PublishCommand WithSelfContained(bool value)
        {
            _selfContained = value;
            return this;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"publish {BuildArgs()} {args}";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"publish {BuildArgs()} {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        private string BuildArgs()
        {
            return string.Join(" ", 
                FrameworkOption,
                OutputOption,
                TargetOption,
                RuntimeOption,
                SelfContainedOption);
        }

        private string FrameworkOption => string.IsNullOrEmpty(_framework) ? "" : $"-f {_framework}";

        private string OutputOption => string.IsNullOrEmpty(_output) ? "" : $"-o {_output}";

        private string RuntimeOption => string.IsNullOrEmpty(_runtime) ? "" : $"-r {_runtime}";

        private string TargetOption => string.Join(" ", _targetManifests);

        private string SelfContainedOption => _selfContained.HasValue ? $"--self-contained:{_selfContained.Value}" : "";
    }
}
