// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Compatibility.ErrorSuppression;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class TestLogger : ICompatibilityLogger
    {
        public List<string> errors = new();

        public void LogError(Suppression suppression, string code, string format, params string[] args)
        {
            errors.Add(code + " " + string.Format(format, args));
        }

        public void LogErrorHeader(string message) { }

        public void LogMessage(MessageImportance importance, string format, params string[] args) { }
    }
}
