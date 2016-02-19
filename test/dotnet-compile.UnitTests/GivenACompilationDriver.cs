// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;
using Moq;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Tools.Compiler.Tests
{
    public class GivenACompilationDriverController
    {
        private string _projectJson;
        private Mock<ICompiler> _managedCompilerMock;
        private Mock<ICompiler> _nativeCompilerMock;
        private List<ProjectContext> _contexts;
        private CompilerCommandApp _args;

        public GivenACompilationDriverController()
        {
            _projectJson =
                Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects", "TestAppWithLibrary", "TestApp", "project.json");
            _managedCompilerMock = new Mock<ICompiler>();
            _managedCompilerMock.Setup(c => c
                .Compile(It.IsAny<ProjectContext>(), It.IsAny<CompilerCommandApp>()))
                .Returns(true);
            _nativeCompilerMock = new Mock<ICompiler>();
            _nativeCompilerMock.Setup(c => c
                .Compile(It.IsAny<ProjectContext>(), It.IsAny<CompilerCommandApp>()))
                .Returns(true);

            _contexts = new List<ProjectContext>
            {
                ProjectContext.Create(_projectJson, NuGetFramework.Parse("dnxcore50"))
            };

            _args = new CompilerCommandApp("dotnet compile", ".NET Compiler", "Compiler for the .NET Platform");
        }

        [Fact]
        public void It_compiles_all_project_contexts()
        {
            var compiledProjectContexts = new List<ProjectContext>();
            _managedCompilerMock.Setup(c => c
                .Compile(It.IsAny<ProjectContext>(), It.IsAny<CompilerCommandApp>()))
                .Callback<ProjectContext, CompilerCommandApp>((p, c) => compiledProjectContexts.Add(p))
                .Returns(true);

            var compilerController = new CompilationDriver(_managedCompilerMock.Object, _nativeCompilerMock.Object);

            compilerController.Compile(_contexts, _args);

            compiledProjectContexts.Should().BeEquivalentTo(_contexts);
        }

        [Fact]
        public void It_does_not_compile_native_when_the_native_parameter_is_not_passed()
        {
            var compilerController = new CompilationDriver(_managedCompilerMock.Object, _nativeCompilerMock.Object);

            compilerController.Compile(_contexts, _args);

            _nativeCompilerMock.Verify(c => c.Compile(It.IsAny<ProjectContext>(), It.IsAny<CompilerCommandApp>()), Times.Never);
        }

        [Fact]
        public void It_does_compile_native_when_the_native_parameter_is_passed()
        {
            var compilerController = new CompilationDriver(_managedCompilerMock.Object, _nativeCompilerMock.Object);

            _args.IsNativeValue = true;

            compilerController.Compile(_contexts, _args);

            _nativeCompilerMock.Verify(c => c.Compile(It.IsAny<ProjectContext>(), It.IsAny<CompilerCommandApp>()), Times.Once);
        }
    }
}
