// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class CIEnvironmentDetectorForTelemetry : ICIEnvironmentDetector
    {
        // Systems that provide boolean values only, so we can simply parse and check for true
        private static readonly string[] _booleanVariables = new string[] {
            // Azure Pipelines - https://docs.microsoft.com/en-us/azure/devops/pipelines/build/variables#system-variables-devops-services
            "TF_BUILD",
            // GitHub Actions - https://docs.github.com/en/actions/learn-github-actions/environment-variables#default-environment-variables
            "GITHUB_ACTIONS",
            // AppVeyor - https://www.appveyor.com/docs/environment-variables/
            "APPVEYOR",
            // A general-use flag - Many of the major players support this: AzDo, GitHub, GitLab, AppVeyor, Travis CI, CircleCI.
            // Given this, we could potentially remove all of these other options?
            "CI",
            // Travis CI - https://docs.travis-ci.com/user/environment-variables/#default-environment-variables
            "TRAVIS",
            // CircleCI - https://circleci.com/docs/2.0/env-vars/#built-in-environment-variables
            "CIRCLECI",
        };

        // Systems where every variable must be present and not-null before returning true
        private static readonly string[][] _allNotNullVariables = new string[][] {
            // AWS CodeBuild - https://docs.aws.amazon.com/codebuild/latest/userguide/build-env-ref-env-vars.html
            new string[]{ "CODEBUILD_BUILD_ID", "AWS_REGION" },
            // Jenkins - https://github.com/jenkinsci/jenkins/blob/master/core/src/main/resources/jenkins/model/CoreEnvironmentContributor/buildEnv.groovy
            new string[]{ "BUILD_ID", "BUILD_URL" },
            // Google Cloud Build - https://cloud.google.com/build/docs/configuring-builds/substitute-variable-values#using_default_substitutions
            new string[]{ "BUILD_ID", "PROJECT_ID" }
        };

        // Systems where the variable must be present and not-null
        private static readonly string[] _ifNonNullVariables = new string[] {
            // TeamCity - https://www.jetbrains.com/help/teamcity/predefined-build-parameters.html#Predefined+Server+Build+Parameters
            "TEAMCITY_VERSION",
            // JetBrains Space - https://www.jetbrains.com/help/space/automation-environment-variables.html#general
            "JB_SPACE_API_URL"
        };

        public bool IsCIEnvironment()
        {
            foreach (var booleanVariable in _booleanVariables)
            {
                if (bool.TryParse(Environment.GetEnvironmentVariable(booleanVariable), out bool envVar) && envVar)
                {
                    return true;
                }
            }

            foreach (var variables in _allNotNullVariables)
            {
                if (variables.All((variable) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable))))
                {
                    return true;
                }
            }

            foreach (var variable in _ifNonNullVariables)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
