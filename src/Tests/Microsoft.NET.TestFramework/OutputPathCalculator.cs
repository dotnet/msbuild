// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Xml.Linq;
using NuGet.Frameworks;
using Xunit.Sdk;

namespace Microsoft.NET.TestFramework
{
    class OutputPathCalculator
    {
        public string ProjectPath { get; set; }
        public string TargetFramework { get; set; }
        public string RuntimeIdentifier { get; set; }
        public bool? UseArtifactsOutput { get; set; }
        

        public bool IsSdkProject { get; set; } = true;


        public static OutputPathCalculator FromTestAsset(string projectPath, TestAsset testAsset)
        {
            if (testAsset.TestProject != null)
            {
                return FromTestProject(projectPath, testAsset.TestProject);
            }

            var calculator = new OutputPathCalculator()
            {
                ProjectPath = projectPath,
            };

            return calculator;
        }

        public static OutputPathCalculator FromTestProject(string projectPath, TestProject testProject)
        {
            var calculator = new OutputPathCalculator()
            {
                ProjectPath = projectPath,
                TargetFramework = testProject.TargetFrameworks,
                RuntimeIdentifier = testProject.RuntimeIdentifier,
                UseArtifactsOutput = testProject.UseArtifactsOutput,
                IsSdkProject = testProject.IsSdkProject
            };

            return calculator;
        }

        public string TryGetTargetFramework()
        {
            string targetFramework = TargetFramework;

            if (string.IsNullOrEmpty(targetFramework))
            {
                var project = XDocument.Load(ProjectPath);

                var ns = project.Root.Name.Namespace;

                var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                var targetFrameworksElement = propertyGroup.Element(ns + "TargetFrameworks");
                if (targetFrameworksElement != null)
                {
                    targetFramework = targetFrameworksElement.Value;
                }
                else
                {
                    var targetFrameworkElement = propertyGroup.Element(ns + "TargetFramework");
                    targetFramework = targetFrameworkElement?.Value;
                }
            }
            return targetFramework ?? "";
        }

        private bool UsesArtifactsFolder()
        {
            if (UseArtifactsOutput.HasValue)
            {
                return UseArtifactsOutput.Value;
            }

            if (!IsSdkProject)
            {
                return false;
            }

            string targetFramework = TryGetTargetFramework();

            if (targetFramework == null || targetFramework.Contains(';'))
            {
                return false;
            }

            var framework = NuGetFramework.Parse(targetFramework);
            if (framework.Framework != ".NETCoreApp")
            {
                return false;
            }
            if (framework.Version.Major >= 8)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public string GetOutputDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = "")
        {
            if (UsesArtifactsFolder())
            {
                string pivot = configuration.ToLowerInvariant();
                if (!string.IsNullOrEmpty(targetFramework))
                {
                    pivot += "_" + targetFramework;
                }
                if (string.IsNullOrEmpty(runtimeIdentifier))
                {
                    runtimeIdentifier = RuntimeIdentifier;
                }
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    pivot += "_" + runtimeIdentifier;
                }
                return System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "artifacts", "bin", pivot);
            }
            else
            {
                if (string.IsNullOrEmpty(targetFramework))
                {
                    targetFramework = TryGetTargetFramework();
                }

                configuration = configuration ?? string.Empty;
                runtimeIdentifier = runtimeIdentifier ?? string.Empty;

                string output = System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", configuration, targetFramework, runtimeIdentifier);
                return output;
            }
        }

        public string GetPublishDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = "")
        {
            if (UsesArtifactsFolder())
            {
                string pivot = configuration.ToLowerInvariant();
                if (!string.IsNullOrEmpty(targetFramework))
                {
                    pivot += "_" + targetFramework;
                }
                if (string.IsNullOrEmpty(runtimeIdentifier))
                {
                    runtimeIdentifier = RuntimeIdentifier;
                }
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    pivot += "_" + runtimeIdentifier;
                }
                return System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "artifacts", "publish", pivot);
            }
            else
            {
                if (string.IsNullOrEmpty(targetFramework))
                {
                    targetFramework = TryGetTargetFramework();
                }

                configuration = configuration ?? string.Empty;
                runtimeIdentifier = runtimeIdentifier ?? string.Empty;

                string output = System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", configuration, targetFramework, runtimeIdentifier, "publish");
                return output;
            }
        }

        public string GetIntermediateDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = "")
        {
            if (string.IsNullOrEmpty(targetFramework))
            {
                targetFramework = TryGetTargetFramework();
            }

            configuration = configuration ?? string.Empty;
            runtimeIdentifier = runtimeIdentifier ?? string.Empty;

            string output = System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "obj", configuration, targetFramework, runtimeIdentifier);
            return output;
        }
    }
}
