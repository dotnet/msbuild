// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// A class which encapsulates logic needed to forward arguments from the current process to another process
    /// invoked with the dotnet.exe host.
    /// </summary>
    internal class ForwardingAppImplementation
    {
        private const string HostExe = "dotnet";

        private readonly string _forwardApplicationPath;
        private readonly IEnumerable<string> _argsToForward;
        private readonly string _depsFile;
        private readonly string _runtimeConfig;
        private readonly string _additionalProbingPath;
        private Dictionary<string, string> _environmentVariables;

        private readonly string[] _allArgs;

        public ForwardingAppImplementation(
            string forwardApplicationPath,
            IEnumerable<string> argsToForward,
            string depsFile = null,
            string runtimeConfig = null,
            string additionalProbingPath = null,
            Dictionary<string, string> environmentVariables = null)
        {
            _forwardApplicationPath = forwardApplicationPath;
            _argsToForward = argsToForward;
            _depsFile = depsFile;
            _runtimeConfig = runtimeConfig;
            _additionalProbingPath = additionalProbingPath;
            _environmentVariables = environmentVariables;

            var allArgs = new List<string>();
            allArgs.Add("exec");

            if (_depsFile != null)
            {
                allArgs.Add("--depsfile");
                allArgs.Add(_depsFile);
            }

            if (_runtimeConfig != null)
            {
                allArgs.Add("--runtimeconfig");
                allArgs.Add(_runtimeConfig);
            }

            if (_additionalProbingPath != null)
            {
                allArgs.Add("--additionalprobingpath");
                allArgs.Add(_additionalProbingPath);
            }

            allArgs.Add(_forwardApplicationPath);
            allArgs.AddRange(_argsToForward);

            _allArgs = allArgs.ToArray();
        }

        public int Execute()
        {
            return GetProcessStartInfo().Execute();
        }

        public ProcessStartInfo GetProcessStartInfo()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = GetHostExeName(),
                Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(_allArgs),
                UseShellExecute = false
            };

            if (_environmentVariables != null)
            {
                foreach (var entry in _environmentVariables)
                {
                    processInfo.Environment[entry.Key] = entry.Value;
                }
            }

            return processInfo;
        }

        public ForwardingAppImplementation WithEnvironmentVariable(string name, string value)
        {
            _environmentVariables = _environmentVariables ?? new Dictionary<string, string>();

            _environmentVariables.Add(name, value);

            return this;
        }

        private string GetHostExeName()
        {
            return $"{HostExe}{FileNameSuffixes.CurrentPlatform.Exe}";
        }
    }
}
