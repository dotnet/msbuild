// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Shouldly;
using Xunit;
#if FEATURE_WINDOWSINTEROP
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
#endif
using NativeMethods = Microsoft.Build.Framework.NativeMethods;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Tests for native file-system, process, and console helpers.
///  <para>
///   Console tests cover <see cref="NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes"/>, specifically the
///   <see cref="NativeMethods.ConsoleConfigurationOverride"/> seam used by the MSBuild Server node to
///   report the client's terminal capabilities instead of the node's own redirected stdout
///   (see dotnet/msbuild#13940).
///  </para>
/// </summary>
public class NativeMethods_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

#if FEATURE_WINDOWSINTEROP
    private delegate uint GetProcessIdDelegate();

    [WindowsOnlyFact("No Kernel32.dll except on Windows.")]
    [SupportedOSPlatform("windows6.1")]
    public void TestGetProcAddress()
    {
        HMODULE kernel32Dll = PInvoke.LoadLibrary("kernel32.dll");
        try
        {
            kernel32Dll.IsNull.ShouldBeFalse();

            IntPtr processAddress = (IntPtr)PInvoke.GetProcAddress(kernel32Dll, "GetCurrentProcessId").Value;
            processAddress.ShouldNotBe(IntPtr.Zero);

            GetProcessIdDelegate processIdDelegate = Marshal.GetDelegateForFunctionPointer<GetProcessIdDelegate>(processAddress);
            uint processId = processIdDelegate();

            using Process currentProcess = Process.GetCurrentProcess();
            processId.ShouldBe((uint)currentProcess.Id);
        }
        finally
        {
            if (!kernel32Dll.IsNull)
            {
                PInvoke.FreeLibrary(kernel32Dll);
            }
        }
    }
#endif

    [Fact]
    public void GetLastWriteFileUtcTimeReturnsMinValueForMissingFile()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string nonexistentFile = env.GetTempFile().Path;

        NativeMethods.GetLastWriteFileUtcTime(nonexistentFile).ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public void GetLastWriteFileUtcTimeReturnsMinValueForDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;

        NativeMethods.GetLastWriteFileUtcTime(directory).ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public void GetLastWriteDirectoryUtcTimeReturnsMinValueForFile()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string file = env.CreateFile().Path;

        NativeMethods.GetLastWriteDirectoryUtcTime(file, out DateTime directoryTime).ShouldBeFalse();
        directoryTime.ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public void SetCurrentDirectoryDoesNotSetNonexistentFolder()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string currentDirectory = Directory.GetCurrentDirectory();
        env.SetCurrentDirectory(currentDirectory);
        string nonexistentDirectory = Path.Combine(env.CreateFolder().Path, "missing");
        Directory.Exists(nonexistentDirectory).ShouldBeFalse();

        NativeMethods.SetCurrentDirectory(nonexistentDirectory);

        Directory.GetCurrentDirectory().ShouldBe(currentDirectory);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void QueryIsScreenAndTryEnableAnsiColorCodes_HonorsOverride(bool acceptAnsi, bool outputIsScreen)
    {
        try
        {
            NativeMethods.ConsoleConfigurationOverride = (acceptAnsi, outputIsScreen);

            (bool actualAnsi, bool actualScreen, uint? originalConsoleMode) =
                NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();

            actualAnsi.ShouldBe(acceptAnsi);
            actualScreen.ShouldBe(outputIsScreen);

            // The node must not touch its own console mode when an override is present, so there is
            // nothing to restore.
            originalConsoleMode.ShouldBeNull();
        }
        finally
        {
            NativeMethods.ConsoleConfigurationOverride = null;
        }
    }

    [Fact]
    public void QueryIsScreenAndTryEnableAnsiColorCodes_OverrideAppliesToStandardError()
    {
        try
        {
            NativeMethods.ConsoleConfigurationOverride = (acceptAnsiColorCodes: true, outputIsScreen: true);

            (bool actualAnsi, bool actualScreen, uint? originalConsoleMode) =
                NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes(useStandardError: true);

            actualAnsi.ShouldBeTrue();
            actualScreen.ShouldBeTrue();
            originalConsoleMode.ShouldBeNull();
        }
        finally
        {
            NativeMethods.ConsoleConfigurationOverride = null;
        }
    }
}
