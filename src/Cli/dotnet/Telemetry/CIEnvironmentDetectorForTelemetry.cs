// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class CIEnvironmentDetectorForTelemetry : ICIEnvironmentDetector
    {
        // variables that will hold boolean values
        private static readonly string[] _booleanVariables = new string[] {
            "TF_BUILD", // Azure Pipelines
            "GITHUB_ACTIONS", // GitHub Actions
            "APPVEYOR", // AppVeyor
            "CI", // a general-use flag
            "TRAVIS", // Travis CI
            "CIRCLECI", // CircleCI
        };

        private static readonly string[][] _allNotNullVariables = new string[][] {
            new string[]{ "CODEBUILD_BUILD_ID", "AWS_REGION" }, // AWS CodeBuild
            new string[]{ "BUILD_ID", "BUILD_URL" }, // Jenkins
            new string[]{ "BUILD_ID", "PROJECT_ID" } // Google Cloud Build
        };

        private static readonly string[] _ifNonNullVariables = new string[] {
            "TEAMCITY_VERSION", // TeamCity
            "JB_SPACE_API_URL" // JetBrains Space
        };

        public IsCIEnvironment IsCIEnvironment()
        {
            foreach (var booleanVariable in _booleanVariables)
            {
                if (bool.TryParse(Environment.GetEnvironmentVariable(booleanVariable), out bool envVar) && envVar)
                {
                    return Cli.Telemetry.IsCIEnvironment.True;
                }
            }

            foreach (var variables in _allNotNullVariables) {
                if (variables.All((variable) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable))))
                {
                    return Cli.Telemetry.IsCIEnvironment.True;
                }
            }

            foreach (var variable in _ifNonNullVariables) {
                if(Environment.GetEnvironmentVariable(variable) is {} v) {
                    return Cli.Telemetry.IsCIEnvironment.True;
                }
            }

            return Cli.Telemetry.IsCIEnvironment.False;
        }
    }

    internal enum IsCIEnvironment
    {
        True,
        False
    }
}
