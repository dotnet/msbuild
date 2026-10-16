// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.CommandLine;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class TaskHostEnvironment_Tests
{
    [Theory]
    [InlineData(true, null, "task")]
    [InlineData(true, "host", "task")]
    [InlineData(true, "host", null)]
    [InlineData(false, null, "task")]
    [InlineData(false, "host", "task")]
    [InlineData(false, "host", null)]
    public void OrdinaryVariablesAreNotReconciled(bool isWindows, string? hostValue, string? taskValue)
    {
        CreateTable("CUSTOM_VARIABLE", hostValue, taskValue, isWindows: isWindows).ShouldBeEmpty();
        CreateTable("MSBUILDCOORDINATORGRANTID", hostValue, taskValue, isWindows: isWindows).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, "task")]
    [InlineData("host", "task")]
    [InlineData("host", null)]
    [InlineData("", "task")]
    [InlineData("host", "")]
    public void LegacyPolicyPreservesStartupReconciliation(string? hostValue, string? taskValue)
    {
        var table = CreateTable("CUSTOM_VARIABLE", hostValue, taskValue, useLegacyBehavior: true);
        table.Count.ShouldBe(1);
        table["CUSTOM_VARIABLE"].Key.ShouldBe(taskValue);
        table["CUSTOM_VARIABLE"].Value.ShouldBe(hostValue);
    }

    [Theory]
    [InlineData("PROCESSOR_ARCHITECTURE", "x86", "AMD64")]
    [InlineData("PROCESSOR_ARCHITEW6432", "AMD64", null)]
    [InlineData("PROCESSOR_ARCHITEW6432", null, "AMD64")]
    [InlineData("ProgramFiles", "host", "parent")]
    [InlineData("ProgramW6432", "host", "parent")]
    [InlineData("CommonProgramFiles", "host", "parent")]
    [InlineData("CommonProgramW6432", "host", "parent")]
    [InlineData("programfiles", "host", "parent")]
    public void WindowsArchitectureVariablesAreReconciled(string name, string? hostValue, string? taskValue)
    {
        var table = CreateTable(name, hostValue, taskValue);
        table.Count.ShouldBe(1);
        table[name].Key.ShouldBe(taskValue);
        table[name].Value.ShouldBe(hostValue);
        CreateTable(name, hostValue, taskValue, isWindows: false).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualValuesDoNotNeedReconciliation(bool useLegacyBehavior)
    {
        CreateTable("PROCESSOR_ARCHITECTURE", "AMD64", "amd64", useLegacyBehavior).ShouldBeEmpty();
        CreateTable("ProgramFiles", null, null, useLegacyBehavior).ShouldBeEmpty();
        CreateTable("ProgramFiles", "", "", useLegacyBehavior).ShouldBeEmpty();
    }

    private static IDictionary<string, KeyValuePair<string?, string?>> CreateTable(
        string name, string? hostValue, string? taskValue, bool useLegacyBehavior = false, bool isWindows = true)
    {
        Dictionary<string, string> hostEnvironment = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> taskEnvironment = new(StringComparer.OrdinalIgnoreCase);
        if (hostValue is not null)
        {
            hostEnvironment[name] = hostValue;
        }

        if (taskValue is not null)
        {
            taskEnvironment[name] = taskValue;
        }

        return TaskHostEnvironment.CreateMismatchedEnvironmentTable(taskEnvironment, hostEnvironment, useLegacyBehavior, isWindows);
    }
}
