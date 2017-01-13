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
        /// Confirms we can find a file on the system path.
        /// </summary>
        [Fact]
        public void FindFileOnPath()
        {
            string expectedCmdPath;
            string shellName;
            if (NativeMethodsShared.IsWindows)
            {
#if FEATURE_SPECIAL_FOLDERS
                expectedCmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
#else
                expectedCmdPath = Path.Combine(FileUtilities.GetFolderPath(FileUtilities.SpecialFolder.System), "cmd.exe");
#endif
                shellName = "cmd.exe";
            }
            else
            {
                expectedCmdPath = "/bin/sh";
                shellName = "sh";
            }

            string cmdPath = NativeMethodsShared.FindOnPath(shellName);
            Assert.NotNull(cmdPath);

            // for the NUnit "Standard Out" tab
            Console.WriteLine("Expected location of \"" + shellName + "\": " + expectedCmdPath);
            Console.WriteLine("Found \"" + shellName + "\" here: " + cmdPath);

            Assert.Equal(0, String.Compare(cmdPath, expectedCmdPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Confirms we can find a file on the system path even if the path
        /// to the file is very long.
        /// </summary>
        // This test causes crashes on CoreCLR because it alters MAX_PATH, causing a buffer overflow in SHGetFolderPathW.
        [Fact]
        [Trait("CrashesOnNetCore", "true")]
        public void FindFileOnPathAfterResizingBuffer()
        {
            int savedMaxPath = NativeMethodsShared.MAX_PATH;

            try
            {
                // make the default buffer size very small -- intentionally don't use
                // zero, otherwise StringBuilder will use some default larger capacity
                NativeMethodsShared.MAX_PATH = 1;

                FindFileOnPath();
            }
            finally
            {
                NativeMethodsShared.MAX_PATH = savedMaxPath;
            }
        }
        /// <summary>
        /// Confirms we cannot find a bogus file on the system path.
        /// </summary>
        [Fact]
        public void DoNotFindFileOnPath()
        {
            string bogusFile = Path.ChangeExtension(Guid.NewGuid().ToString(), ".txt");
            // for the NUnit "Standard Out" tab
            Console.WriteLine("The bogus file name is: " + bogusFile);

            string bogusFilePath = NativeMethodsShared.FindOnPath(bogusFile);

            Assert.Null(bogusFilePath);
        }

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
