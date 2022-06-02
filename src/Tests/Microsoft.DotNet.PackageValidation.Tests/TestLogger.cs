// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class TestLogger : CompatibilityLoggerBase
    {
        public List<string> errors = new();
        public List<string> warnings = new();

        public TestLogger()
            : base(suppressionsFile: null, baselineAllErrors: false, noWarn: null)
        {
        }

        public override bool LogError(Suppression suppression, string code, string format, params string[] args)
        {
            errors.Add(code + " " + string.Format(format, args));
            return true;
        }

        public override bool LogWarning(Suppression suppression, string code, string format, params string[] args)
        {
            errors.Add(code + " " + string.Format(format, args));
            return true;
        }

        public override void LogMessage(MessageImportance importance, string format, params string[] args)
        {
        }
    }
}
