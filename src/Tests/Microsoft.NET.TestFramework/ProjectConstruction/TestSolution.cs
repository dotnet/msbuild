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
            SolutionPath = Path.Combine(newSolutionPath, name, ".sln");
            ProjectPaths = new List<string>();

            var slnCreator = new DotnetNewCommand(log, "sln", "-o", $"{newSolutionPath}", "-n", $"{name}");
            slnCreator.Execute();

            foreach (var project in solutionProjects)
            {
                var slnProjectAdder = new DotnetCommand(log, "sln", $"{SolutionPath}", "add", project.Path);
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

        internal List<TestAsset> Projects { get; set; }

        public List<string> ProjectProperties()
        {
            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);

        }

    }
}
