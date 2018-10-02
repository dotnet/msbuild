// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.Build.Shared;
using Xunit;



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
        [Fact]
        public void TestGetProcAddress()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "No Kernel32.dll except on Windows"
            }

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
                    Assert.True(false);
                }

                // Make sure the pointer passed back for the method is not null
                Assert.NotEqual(processHandle, NativeMethodsShared.NullIntPtr);

                //Actually call the method
                GetProcessIdDelegate processIdDelegate = Marshal.GetDelegateForFunctionPointer<GetProcessIdDelegate>(processHandle);
                uint processId = processIdDelegate();

                //Make sure the return value is the same as retrieved from the .net methods to make sure everything works
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
            string nonexistentFile = FileUtilities.GetTemporaryFile();
            // Make sure that the file does not, in fact, exist.
            File.Delete(nonexistentFile);

            DateTime nonexistentFileTime = NativeMethodsShared.GetLastWriteFileUtcTime(nonexistentFile);
            Assert.Equal(DateTime.MinValue, nonexistentFileTime);
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
                    nonexistentDirectory = Path.Combine(currentDirectory, "foo", "bar", "baz") + Guid.NewGuid();

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
