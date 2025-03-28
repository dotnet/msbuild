// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Xunit;
using Xunit.NetCore.Extensions;



#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class NativeMethodsShared_Tests
    {
        #region Data

        // Create a delegate to test the GetProcessId method when using GetProcAddress
        private delegate uint GetProcessIdDelegate();

        #endregion

        #region Tests

        /// <summary>
        /// Verify that getProcAddress works, bug previously was due to a bug in the attributes used to pinvoke the method
        /// when that bug was in play this test would fail.
        /// </summary>
        [WindowsOnlyFact("No Kernel32.dll except on Windows.")]
        [SupportedOSPlatform("windows")] // bypass CA1416: Validate platform compatibility
        public void TestGetProcAddress()
        {
            IntPtr kernel32Dll = NativeMethodsShared.LoadLibrary("kernel32.dll");
            try
            {
                IntPtr processHandle = NativeMethodsShared.NullIntPtr;
                if (kernel32Dll != NativeMethodsShared.NullIntPtr)
                {
                    processHandle = NativeMethodsShared.GetProcAddress(kernel32Dll, "GetCurrentProcessId");
                }
                else
                {
                    Assert.Fail();
                }

                // Make sure the pointer passed back for the method is not null
                Assert.NotEqual(processHandle, NativeMethodsShared.NullIntPtr);

                // Actually call the method
                GetProcessIdDelegate processIdDelegate = Marshal.GetDelegateForFunctionPointer<GetProcessIdDelegate>(processHandle);
                uint processId = processIdDelegate();

                // Make sure the return value is the same as retrieved from the .net methods to make sure everything works
                Assert.Equal((uint)Process.GetCurrentProcess().Id, processId); // "Expected the .net processId to match the one from GetCurrentProcessId"
            }
            finally
            {
                if (kernel32Dll != NativeMethodsShared.NullIntPtr)
                {
                    NativeMethodsShared.FreeLibrary(kernel32Dll);
                }
            }
        }

        /// <summary>
        /// Verifies that when NativeMethodsShared.GetLastWriteFileUtcTime() is called on a
        /// missing time, DateTime.MinValue is returned.
        /// </summary>
        [Fact]
        public void GetLastWriteFileUtcTimeReturnsMinValueForMissingFile()
        {
            string nonexistentFile = FileUtilities.GetTemporaryFileName();

            DateTime nonexistentFileTime = NativeMethodsShared.GetLastWriteFileUtcTime(nonexistentFile);
            Assert.Equal(DateTime.MinValue, nonexistentFileTime);
        }

        /// <summary>
        /// Verifies that when NativeMethodsShared.GetLastWriteFileUtcTime() is called on a
        /// *directory*, DateTime.MinValue is returned.
        /// </summary>
        [Fact]
        public void GetLastWriteFileUtcTimeReturnsMinValueForDirectory()
        {
            string directory = FileUtilities.GetTemporaryDirectory(createDirectory: true);

            DateTime directoryTime = NativeMethodsShared.GetLastWriteFileUtcTime(directory);
            Assert.Equal(DateTime.MinValue, directoryTime);
        }

        /// <summary>
        /// Verifies that when NativeMethodsShared.GetLastWriteDirectoryUtcTime() is called on a
        /// *file*, it returns DateTime.MinValue
        /// </summary>
        [Fact]
        public void GetLastWriteDirectoryUtcTimeReturnsMinValueForFile()
        {
            string file = FileUtilities.GetTemporaryFile();

            DateTime directoryTime;
            Assert.False(NativeMethodsShared.GetLastWriteDirectoryUtcTime(file, out directoryTime));
            Assert.Equal(DateTime.MinValue, directoryTime);
        }


        /// <summary>
        /// Verifies that NativeMethodsShared.SetCurrentDirectory(), when called on a nonexistent
        /// directory, will not set the current directory to that location.
        /// </summary>
        [Fact]
        public void SetCurrentDirectoryDoesNotSetNonexistentFolder()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string nonexistentDirectory = Path.Combine(currentDirectory, "foo", "bar", "baz");

            // Make really sure the nonexistent directory doesn't actually exist
            if (Directory.Exists(nonexistentDirectory))
            {
                for (int i = 0; i < 10; i++)
                {
                    nonexistentDirectory = $"{Path.Combine(currentDirectory, "foo", "bar", "baz")}{Guid.NewGuid()}";

                    if (!Directory.Exists(nonexistentDirectory))
                    {
                        break;
                    }
                }
            }

            Assert.False(Directory.Exists(nonexistentDirectory),
                "Tried 10 times to get a nonexistent directory name and failed -- please try again");

            bool exceptionCaught = false;
            try
            {
                NativeMethodsShared.SetCurrentDirectory(nonexistentDirectory);
            }
            catch (Exception e)
            {
                exceptionCaught = true;
                Console.WriteLine(e.Message);
            }
            finally
            {
                // verify that the current directory did not change
                Assert.False(exceptionCaught); // "SetCurrentDirectory should not throw!"
                Assert.Equal(currentDirectory, Directory.GetCurrentDirectory());
            }
        }

        #endregion
    }
}
