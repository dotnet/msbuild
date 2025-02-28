// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Checks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public sealed class ExecCliBuildCheck_Tests
    {
        private readonly ExecCliBuildCheck _check;

        private readonly MockBuildCheckRegistrationContext _registrationContext;

        public ExecCliBuildCheck_Tests()
        {
            _check = new ExecCliBuildCheck();
            _registrationContext = new MockBuildCheckRegistrationContext();
            _check.RegisterActions(_registrationContext);
        }

        [Theory]
        [InlineData("dotnet build")]
        [InlineData("dotnet build&dotnet build")]
        [InlineData("dotnet     build")]
        [InlineData("dotnet clean")]
        [InlineData("dotnet msbuild")]
        [InlineData("dotnet restore")]
        [InlineData("dotnet publish")]
        [InlineData("dotnet pack")]
        [InlineData("dotnet test")]
        [InlineData("dotnet vstest")]
        [InlineData("dotnet build -p:Configuration=Release")]
        [InlineData("dotnet build /t:Restore;Clean")]
        [InlineData("some command&dotnet build&some other command")]
        [InlineData("some command&amp;dotnet build&amp;some other command")]
        [InlineData("msbuild")]
        [InlineData("msbuild /t:Build")]
        [InlineData("msbuild --t:Restore;Clean")]
        [InlineData("nuget restore")]
        [InlineData("dotnet run --project project.SLN")]
        [InlineData("dotnet run project.csproj")]
        [InlineData("dotnet run project.proj")]
        [InlineData("dotnet run")]
        public void ExecTask_WithCommandExecutingBuild_ShouldShowWarning(string command)
        {
            _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Exec", new Dictionary<string, TaskInvocationCheckData.TaskParameter>
            {
                { "Command", new TaskInvocationCheckData.TaskParameter(command, IsOutput: false) },
            }));

            _registrationContext.Results.Count.ShouldBe(1);
            _registrationContext.Results[0].CheckRule.Id.ShouldBe("BC0109");
        }

        [Theory]
        [InlineData("dotnet help")]
        [InlineData("where dotnet")]
        [InlineData("where msbuild")]
        [InlineData("where nuget")]
        [InlineData("dotnet bin/net472/project.dll")]
        [InlineData("")]
        [InlineData(null)]
        public void ExecTask_WithCommandNotExecutingBuild_ShouldNotShowWarning(string? command)
        {
            _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Exec", new Dictionary<string, TaskInvocationCheckData.TaskParameter>
            {
                { "Command", new TaskInvocationCheckData.TaskParameter(command, IsOutput: false) },
            }));

            _registrationContext.Results.Count.ShouldBe(0);
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
    }
}
