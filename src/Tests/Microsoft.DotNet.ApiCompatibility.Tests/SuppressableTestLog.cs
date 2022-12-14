// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal class SuppressableTestLog : ISuppressableLog
    {
        public List<string> errors = new();
        public List<string> warnings = new();
        public bool SuppressionWasLogged => errors.Count != 0;

        public bool LogError(Suppression suppression, string code, string format, params string[] args)
        {
            errors.Add($"{code} {string.Format(format, args)}");
            return true;
        }
        public void LogError(string code, string format, params string[] args) { }
        public void LogMessage(MessageImportance importance, string format, params string[] args) { }
        public bool LogWarning(Suppression suppression, string code, string format, params string[] args)
        {
            errors.Add($"{code} {string.Format(format, args)}");
            return true;
        }
        public void LogWarning(string code, string format, params string[] args) { }
    }
}
