// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
#if FEATURE_WIN32_REGISTRY
using Microsoft.Win32;
#endif
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
#if FEATURE_WIN32_REGISTRY
    /// <summary>
    /// Verify we can an argument exception if we try and pass a empty registry root
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestEmptyRegistryRoot()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo("", "v3.0", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL));

    /// <summary>
    /// Verify we can an argumentNull exception if we try and pass a null registry root
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoListTestNullRegistryRoot()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(null, "v3.0", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL));

    /// <summary>
    /// Verify we can an argument exception if we try and pass a empty registry suffix
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestEmptyRegistrySuffix()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "v3.0", "", null, null, System.Reflection.ProcessorArchitecture.MSIL));

    /// <summary>
    /// Verify we can an argumentNull exception if we try and pass a null registry suffix
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestNullRegistrySuffix()
        => Should.Throw<ArgumentNullException>(() =>
        ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "v3.0", null, null, null, System.Reflection.ProcessorArchitecture.MSIL));

    /// <summary>
    /// Verify we can an argument exception if we try and pass a empty registry suffix
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestEmptyTargetRuntime()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL));

    /// <summary>
    /// Verify we can an argumentNull exception if we try and pass a null target runtime version
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestNullTargetRuntimeVersion()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", null, "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL));

    /// <summary>
    /// Verify we can get a list of directories out of the public API.
    /// </summary>
    [WindowsOnlyFact]
    public void GetAssemblyFoldersExInfoTest()
    {
        SetupAssemblyFoldersExTestConditionRegistryKey();
        IList<AssemblyFoldersExInfo> directories;
        try
        {
            directories = ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "v3.0", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL);
        }
        finally
        {
            RemoveAssemblyFoldersExTestConditionRegistryKey();
        }

        directories.ShouldNotBeNull();
        directories.Count.ShouldBe(2);
        directories[0].DirectoryPath.ShouldBe(@"C:\V1Control2", StringCompareShould.IgnoreCase);
        directories[1].DirectoryPath.ShouldBe(@"C:\V1Control", StringCompareShould.IgnoreCase);
    }

    private static void SetupAssemblyFoldersExTestConditionRegistryKey()
    {
        RegistryKey baseKey = Registry.CurrentUser;
        baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.UnitTest", false);
        RegistryKey folderKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component1");
        folderKey.SetValue("", @"C:\V1Control");

        RegistryKey servicePackKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component2");
        servicePackKey.SetValue("", @"C:\V1Control2");
    }

    private static void RemoveAssemblyFoldersExTestConditionRegistryKey()
    {
        RegistryKey baseKey = Registry.CurrentUser;
        try
        {
            baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component1");
            baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component2");
        }
        catch (Exception)
        {
        }
    }
#endif
}
