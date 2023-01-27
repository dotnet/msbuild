// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.NET.TestFramework.ProjectConstruction
{
    /// <summary>
    /// Create an on-disk solution for testing that has given projects.
    /// This has dependencies on the functionality of .NET new and .NET sln add; if those are broken, tests revolving around solutions will also break.
    /// </summary>
    public class TestSolution
    {
        public TestSolution(ITestOutputHelper log, string newSolutionPath, List<TestAsset> solutionProjects, [CallerMemberName] string name = null)
        {
            Name = name;
            SolutionPath = Path.Combine(newSolutionPath, $"{name + ".sln"}");
            Projects = new List<TestAsset>();
            ProjectPaths = new List<string>();

            var slnCreator = new DotnetNewCommand(log, "sln", "-o", $"{newSolutionPath}", "-n", $"{name}");
            var slnCreationResult = slnCreator
                .WithVirtualHive()
                .Execute();

            if (slnCreationResult.ExitCode != 0)
            {
                throw new Exception($"This test failed during a call to dotnet new. If {newSolutionPath} is valid, it's likely this test is failing because of dotnet new. If there are failing .NET new tests, please fix those and then see if this test still fails.");
            }

            foreach (var project in solutionProjects)
            {
                var slnProjectAdder = new DotnetCommand(log, "sln", $"{SolutionPath}", "add", Path.Combine(project.Path, project.TestProject.Name));
                slnProjectAdder.Execute();
                ProjectPaths.Add(project.Path);
                Projects.Add(project);
            }
        }

        /// <summary>
        /// The FULL path to the newly created, on-disk solution, including the .sln file.
        /// </summary>
        public string SolutionPath { get; set; }

        /// <summary>
        /// The delegated or generated name of the solution.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A List of the paths of projects that were initially added to the solution.
        /// </summary>
        public List<string> ProjectPaths { get; set; }


        /// <summary>
        ///  The internal list of projects that contain actual testAssets.
        ///  Not exposed publically to avoid incorrectly adding to this list or editing it without editing the solution file.
        /// </summary>
        internal List<TestAsset> Projects { get; set; }

        /// <summary>
        ///  Gives the property values for each project in the test solution.
        ///  Can throw exceptions if the path to the project provided by tfm and configuration is not correct.
        /// </summary>
        /// <param name="targetFrameworksAndConfigurationsInOrderPerProject">A pair, first starting with the targetframework of the designated project.
        /// The second item should be the Configuration expected of the second project.
        /// This should match the order in which projects were added to the solution.
        /// </param>
        /// <returns>A dictionary of property -> value mappings for every subproject in the solution.</returns>
        public List<Dictionary<string, string>> ProjectProperties(List<(string targetFramework, string configuration)> targetFrameworksAndConfigurationsInOrderPerProject = null)
        {
            var properties = new List<Dictionary<string, string>>();
            int i = 0;

            foreach (var testAsset in Projects)
            {
                TestProject testProject = testAsset.TestProject;

                var tfm = ToolsetInfo.CurrentTargetFramework;
                var config = "Debug";

                if (targetFrameworksAndConfigurationsInOrderPerProject != null)
                {
                    var tfmAndConfiguration = targetFrameworksAndConfigurationsInOrderPerProject.ElementAtOrDefault(i);
                    if (!tfmAndConfiguration.Equals(default))
                    {
                        tfm = tfmAndConfiguration.Item1;
                        config = tfmAndConfiguration.Item2;
                    }
                }

                properties.Add(testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: tfm, configuration: config));
                ++i;
            }
            return properties;

        }

    }
}
