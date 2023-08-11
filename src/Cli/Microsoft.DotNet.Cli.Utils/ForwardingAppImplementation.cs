// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// A class which encapsulates logic needed to forward arguments from the current process to another process
    /// invoked with the dotnet.exe host.
    /// </summary>
    internal class ForwardingAppImplementation
    {
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
            _environmentVariables = environmentVariables ?? new Dictionary<string, string>();

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

            foreach (var entry in _environmentVariables)
            {
                processInfo.Environment[entry.Key] = entry.Value;
            }

            return processInfo;
        }

        public ForwardingAppImplementation WithEnvironmentVariable(string name, string value)
        {
            _environmentVariables.Add(name, value);

            return this;
        }

        private string GetHostExeName()
        {
            // Should instead make this a full path to dotnet
            return System.Environment.ProcessPath;
        }
    }
}

#endif