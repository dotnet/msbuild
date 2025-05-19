// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Checks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public sealed class ExecCliBuildCheck_Tests
    {
        private const int MaxStackSizeWindows = 1024 * 1024; // 1 MB
        private const int MaxStackSizeLinux = 1024 * 1024 * 8; // 8 MB

        private readonly ExecCliBuildCheck _check;

        private readonly MockBuildCheckRegistrationContext _registrationContext;

        public static TheoryData<string?> BuildCommandTestData => new TheoryData<string?>(
            "dotnet build",
            "dotnet build&dotnet build",
            "dotnet     build",
            "dotnet clean",
            "dotnet msbuild",
            "dotnet restore",
            "dotnet publish",
            "dotnet pack",
            "dotnet test",
            "dotnet vstest",
            "dotnet build -p:Configuration=Release",
            "dotnet build /t:Restore;Clean",
            "dotnet build&some command",
            "some command&dotnet build&some other command",
            "some command&dotnet build",
            "some command&amp;dotnet build&amp;some other command",
            "msbuild",
            "msbuild /t:Build",
            "msbuild --t:Restore;Clean",
            "nuget restore",
            "dotnet run --project project.SLN",
            "dotnet run project.csproj",
            "dotnet run project.proj",
            "dotnet run",
            string.Join(";", new string('a', 1025), "dotnet build", new string('a', 1025)),
            string.Join(";", new string('a', RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? MaxStackSizeWindows * 2 : MaxStackSizeLinux * 2), "dotnet build"));

        public static TheoryData<string?> NonBuildCommandTestData => new TheoryData<string?>(
            "dotnet help",
            "where dotnet",
            "where msbuild",
            "where nuget",
            "dotnet bin/net472/project.dll",
            string.Empty,
            null,
            new string('a', RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? MaxStackSizeWindows * 2 : MaxStackSizeLinux * 2));

        public ExecCliBuildCheck_Tests()
        {
            _check = new ExecCliBuildCheck();
            _registrationContext = new MockBuildCheckRegistrationContext();
            _check.RegisterActions(_registrationContext);
        }

        [Theory]
        [MemberData(nameof(BuildCommandTestData))]
        public void ExecTask_WithCommandExecutingBuild_ShouldShowWarning(string? command)
        {
            _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Exec", new Dictionary<string, TaskInvocationCheckData.TaskParameter>
            {
                { "Command", new TaskInvocationCheckData.TaskParameter(command, IsOutput: false) },
            }));

            _registrationContext.Results.Count.ShouldBe(1);
            _registrationContext.Results[0].CheckRule.Id.ShouldBe("BC0302");
        }

        [Theory]
        [MemberData(nameof(NonBuildCommandTestData))]
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
