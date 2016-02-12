// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Build
{
    internal class IncrementalPreconditions
    {
        private readonly ISet<string> _preconditions;
        private readonly bool _isProfile;

        public IncrementalPreconditions(bool isProfile)
        {
            _isProfile = isProfile;
            _preconditions = new HashSet<string>();
        }

        public void AddPrePostScriptPrecondition(string projectName, string scriptType)
        {
            _preconditions.Add($"[Pre / Post Scripts] Project {projectName} is using {scriptType} scripts.");
        }

        public void AddUnknownCompilerPrecondition(string projectName, string compilerName)
        {
            _preconditions.Add($"[Unknown Compiler] Project {projectName} is using unknown compiler {compilerName}.");
        }

        public void AddPathProbingPrecondition(string projectName, string commandName)
        {
            _preconditions.Add($"[PATH Probing] Project {projectName} is loading tool \"{commandName}\" from PATH");
        }

        public void AddForceUnsafePrecondition()
        {
            _preconditions.Add($"[Forced Unsafe] The build was marked as unsafe. Remove the {BuilderCommandApp.NoIncrementalFlag} flag to enable incremental compilation");
        }

        public bool PreconditionsDetected()
        {
            return _preconditions.Any();
        }

        private string PreconditionsMessage()
        {
            var log = new StringBuilder();

            log.AppendLine();
            log.Append("Incremental compilation has been disabled due to the following project properties:");

            foreach (var precondition in _preconditions)
            {
                log.AppendLine();
                log.Append("\t" + precondition);
            }

            log.AppendLine();
            log.AppendLine();

            log.Append(
                "Incremental compilation will be automatically enabled if the above mentioned project properties are not used. " +
                "For more information on the properties and how to address them, please consult:\n" +
                @"https://github.com/dotnet/cli/blob/master/Documentation/addressing-incremental-compilation-warnings.md");

            log.AppendLine();
            log.AppendLine();

            return log.ToString();
        }

        public string LogMessage()
        {
            if (PreconditionsDetected())
            {
                return _isProfile ? PreconditionsMessage().Yellow() : $"(The compilation time can be improved. Run \"dotnet build {BuilderCommandApp.BuildProfileFlag}\" for more information)";
            }

            return "";
        }
    }
}