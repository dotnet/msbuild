// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.Build
{
    public class EnvVars
    {
        public static string EnsureVariable(string variableName)
        {
            string value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrEmpty(value))
            {
                throw new BuildFailureException($"'{variableName}' environment variable was not found.");
            }

            return value;
        }
    }
}
