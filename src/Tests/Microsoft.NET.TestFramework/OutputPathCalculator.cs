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
    public class OutputPathCalculator
    {
        public string ProjectPath { get; set; }
        public string TargetFramework { get; set; }
        public string RuntimeIdentifier { get; set; }
        public bool? UseStandardOutputPaths { get; set; }
        

        public bool IsSdkProject { get; set; } = true;


        public static OutputPathCalculator FromTestAsset(string projectPath, TestAsset testAsset = null)
        {
            if (testAsset?.TestProject != null)
            {
                return FromTestProject(projectPath, testAsset.TestProject);
            }

            if (!File.Exists(projectPath) && Directory.Exists(projectPath))
            {
                projectPath = Directory.GetFiles(projectPath, "*.*proj").FirstOrDefault();
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
                UseStandardOutputPaths = testProject.UseStandardOutputPaths,
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
                    string targetFrameworks = targetFrameworksElement.Value;
                    if (!targetFrameworks.Contains(';'))
                    {
                        targetFramework = targetFrameworks;
                    }
                }
                else
                {
                    var targetFrameworkElement = propertyGroup.Element(ns + "TargetFramework");
                    targetFramework = targetFrameworkElement?.Value;
                }
            }
            return targetFramework ?? "";
        }

        public bool IsMultiTargeted()
        {
            if (!string.IsNullOrEmpty(TargetFramework) && TargetFramework.Contains(';'))
            { 
                return true;
            }
            var project = XDocument.Load(ProjectPath);

            var ns = project.Root.Name.Namespace;

            var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
            var targetFrameworksElement = propertyGroup.Element(ns + "TargetFrameworks");
            if (targetFrameworksElement != null)
            {
                string targetFrameworks = targetFrameworksElement.Value;
                return !string.IsNullOrEmpty(targetFrameworks);
            }

            return false;

        }

        private bool UsesStandardOutputPaths()
        {
            if (UseStandardOutputPaths.HasValue)
            {
                return UseStandardOutputPaths.Value;
            }

            

            //  If we end up enabling standard output paths when targeting a certain version of .NET or higher, the logic for the rest of this method would look something like this:s
            //if (!IsSdkProject)
            //{
            //    return false;
            //}

            //string targetFramework = TryGetTargetFramework();

            //if (targetFramework == null)
            //{
            //    return false;
            //}

            //if (IsMultiTargeted())
            //{
            //    return false;
            //}

            //var framework = NuGetFramework.Parse(targetFramework);
            //if (framework.Framework != ".NETCoreApp")
            //{
            //    return false;
            //}
            //if (framework.Version.Major >= 8)
            //{
            //    return true;
            //}
            //else
            //{
            //    return false;
            //}

            return false;
        }

        public string GetOutputDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = "")
        {
            if (UsesStandardOutputPaths())
            {
                string pivot = configuration.ToLowerInvariant();
                if (IsMultiTargeted() && !string.IsNullOrEmpty(targetFramework))
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
                return System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", "build", pivot);
            }
            else
            {
                if (string.IsNullOrEmpty(targetFramework))
                {
                    targetFramework = TryGetTargetFramework();
                }

                configuration = configuration ?? string.Empty;
                runtimeIdentifier = runtimeIdentifier ?? string.Empty;

                if (IsSdkProject)
                {
                    string output = System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", configuration, targetFramework, runtimeIdentifier);
                    return output;
                }
                else
                {
                    string output = System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", configuration);
                    return output;
                }
            }
        }

        public string GetPublishDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = "")
        {
            if (UsesStandardOutputPaths())
            {
                string pivot = configuration.ToLowerInvariant();
                if (IsMultiTargeted() && !string.IsNullOrEmpty(targetFramework))
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
                return System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", "publish", pivot);
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

        public string GetPackageDirectory(string configuration = "Debug")
        {
            if (UsesStandardOutputPaths())
            {
                return System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", "package", configuration.ToLowerInvariant());
            }
            else
            {
                return System.IO.Path.Combine(Path.GetDirectoryName(ProjectPath), "bin", configuration);
            }
        }
    }
}
