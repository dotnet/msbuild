// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Tools.Compiler.Tests
{
    public class GivenThatIWantToCompileFSharpPrograms
    {
        private readonly static string s_testProjectsRoot = Path.Combine(
            AppContext.BaseDirectory, 
            "TestAssets", 
            "TestProjects", 
            "FSharpTestProjects");

        [Fact]
        public void Compilation_of_app_with_invalid_source_should_fail()
        {
            var testProject = Path.Combine(s_testProjectsRoot, "CompileFailApp", "project.json"); 
            var buildCommand = new BuildCommand(testProject);

            var oldDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(testProject));

            buildCommand.Execute().Should().Fail();

            Directory.SetCurrentDirectory(oldDirectory);
        }

        [Fact]
        public void Compilation_of_valid_app_should_succeed()
        {
            var testProject = Path.Combine(s_testProjectsRoot, "TestAppWithArgs", "project.json"); 
            var buildCommand = new BuildCommand(testProject);

            var oldDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(testProject));

            buildCommand.Execute().Should().Pass();

            Directory.SetCurrentDirectory(oldDirectory);
        }

        [Fact]
        public void Compilation_of_app_with_P2P_reference_to_fsharp_library_should_be_runnable()
        {
            var testProject = Path.Combine(s_testProjectsRoot, "TestApp", "project.json"); 
            var runCommand = new RunCommand(testProject);

            var oldDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(testProject));

            var result = runCommand.Execute();

            result.Should().Pass();

            Directory.SetCurrentDirectory(oldDirectory);
        }
    }
}
