// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class TestLogger : IPackageLogger
    {
        public List<string> errors = new();

        public void LogError(string code, string format, params string[] args)
        {
            errors.Add(code + " " + string.Format(format, args));
        }
    }
}
