// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Checks;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public sealed class DoubleWritesCheck_Tests
    {
        private readonly DoubleWritesCheck _check;

        private readonly MockBuildCheckRegistrationContext _registrationContext;

        public DoubleWritesCheck_Tests()
        {
            _check = new DoubleWritesCheck();
            _registrationContext = new MockBuildCheckRegistrationContext();
            _check.RegisterActions(_registrationContext);
        }

        private TaskInvocationCheckData MakeTaskInvocationData(string taskName, Dictionary<string, TaskInvocationCheckData.TaskParameter> parameters)
        {
            string projectFile = Framework.NativeMethods.IsWindows ? @"C:\fake\project.proj" : "/fake/project.proj";
            return new TaskInvocationCheckData(
                projectFile,
                null,
                Construction.ElementLocation.EmptyLocation,
                taskName,
                projectFile,
                parameters);
        }

        [Fact]
        public void TestCopyTask()
        {
            _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Copy", new Dictionary<string, TaskInvocationCheckData.TaskParameter>
                {
                    { "SourceFiles", new TaskInvocationCheckData.TaskParameter("source1", IsOutput: false) },
                    { "DestinationFolder", new TaskInvocationCheckData.TaskParameter("outdir", IsOutput: false) },
                }));
            _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Copy", new Dictionary<string, TaskInvocationCheckData.TaskParameter>
                {
                    { "SourceFiles", new TaskInvocationCheckData.TaskParameter("source1", IsOutput: false) },
                    { "DestinationFiles", new TaskInvocationCheckData.TaskParameter(Path.Combine("outdir", "source1"), IsOutput: false) },
                }));

            _registrationContext.Results.Count.ShouldBe(1);
            _registrationContext.Results[0].CheckRule.Id.ShouldBe("BC0102");
        }

        [Theory]
        [InlineData("Csc")]
        [InlineData("Vbc")]
        [InlineData("Fsc")]
        public void TestCompilerTask(string taskName)
        {
            for (int i = 0; i < 2; i++)
            {
                _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData(taskName, new Dictionary<string, TaskInvocationCheckData.TaskParameter>
                    {
                        { "OutputAssembly", new TaskInvocationCheckData.TaskParameter("out.dll", IsOutput: false) },
                        { "OutputRefAssembly", new TaskInvocationCheckData.TaskParameter("out_ref.dll", IsOutput: false) },
                        { "DocumentationFile", new TaskInvocationCheckData.TaskParameter("out.xml", IsOutput: false) },
                        { "PdbFile", new TaskInvocationCheckData.TaskParameter("out.pdb", IsOutput: false) },
                    }));
            }

            _registrationContext.Results.Count.ShouldBe(4);
            _registrationContext.Results.ForEach(result => result.CheckRule.Id.ShouldBe("BC0102"));
        }
    }
}
