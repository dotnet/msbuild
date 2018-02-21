// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ShellShim.Tests
{
    internal class FakeEnvironmentProvider : IEnvironmentProvider
    {
        private readonly Dictionary<string, string> _environmentVariables;

        public FakeEnvironmentProvider(Dictionary<string, string> environmentVariables)
        {
            _environmentVariables =
                environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables));
        }

        public IEnumerable<string> ExecutableExtensions { get; }

        public string GetCommandPath(string commandName, params string[] extensions)
        {
            throw new NotImplementedException();
        }

        public string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
        {
            throw new NotImplementedException();
        }

        public string GetCommandPathFromRootPath(string rootPath, string commandName,
            IEnumerable<string> extensions)
        {
            throw new NotImplementedException();
        }

        public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
        {
            throw new NotImplementedException();
        }

        public string GetEnvironmentVariable(string name)
        {
            return _environmentVariables.ContainsKey(name) ? _environmentVariables[name] : "";
        }
    }
}
