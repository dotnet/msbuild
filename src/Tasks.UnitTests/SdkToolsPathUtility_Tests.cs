// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Microsoft.Build.UnitTests;
using System.IO;
using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class SdkToolsPathUtility_Tests
    {
        private string _defaultSdkToolsPath = NativeMethodsShared.IsWindows ? "C:\\ProgramFiles\\WIndowsSDK\\bin" : "/ProgramFiles/WindowsSDK/bin";
        private TaskLoggingHelper _log = null;
        private string _toolName = "MyTool.exe";
        private MockEngine _mockEngine = null;
        private MockFileExists _mockExists = null;

        public SdkToolsPathUtility_Tests()
        {
            // Create a delegate helper to make the testing of a method which uses a lot of fileExists a bit easier
            _mockExists = new MockFileExists(_defaultSdkToolsPath);

            // We need an engine to see any logging messages the method may log
            _mockEngine = new MockEngine();

            // Dummy task to get a TaskLoggingHelper
            TaskToLogFrom loggingTask = new TaskToLogFrom();
            loggingTask.BuildEngine = _mockEngine;
            _log = loggingTask.Log;
            _log.TaskResources = AssemblyResources.PrimaryResources;
        }


        #region Misc
        /// <summary>
        /// Test the case where the sdkToolsPath is null or empty
        /// </summary>
        [Fact]
        public void GeneratePathToToolNullOrEmptySdkToolPath()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileExistsOnlyInX86, ProcessorArchitecture.X86, null, _toolName, _log, true);
            Assert.Null(toolPath);

            string comment = ResourceUtilities.FormatResourceString("General.SdkToolsPathNotSpecifiedOrToolDoesNotExist", _toolName, null);
            _mockEngine.AssertLogContains(comment);
            Assert.Equal(0, _mockEngine.Warnings);

            comment = ResourceUtilities.FormatResourceString("General.SdkToolsPathToolDoesNotExist", _toolName, null, ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Latest));
            _mockEngine.AssertLogContains(comment);
            Assert.Equal(1, _mockEngine.Errors);
        }

        /// <summary>
        /// Test the case where the sdkToolsPath is null or empty and we do not want to log errors or warnings
        /// </summary>
        [Fact]
        public void GeneratePathToToolNullOrEmptySdkToolPathNoLogging()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileExistsOnlyInX86, ProcessorArchitecture.X86, null, _toolName, _log, false);
            Assert.Null(toolPath);

            string comment = ResourceUtilities.FormatResourceString("General.SdkToolsPathNotSpecifiedOrToolDoesNotExist", _toolName, null);
            _mockEngine.AssertLogDoesntContain(comment);
            Assert.Equal(0, _mockEngine.Warnings);

            comment = ResourceUtilities.FormatResourceString("General.SdkToolsPathToolDoesNotExist", _toolName, null, ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45));
            _mockEngine.AssertLogDoesntContain(comment);
            Assert.Equal(0, _mockEngine.Errors);
        }

        #endregion

        #region Test x86
        /// <summary>
        /// Test the case where the processor architecture is x86 and the tool exists in the x86 sdk path
        /// </summary>
        [Fact]
        public void GeneratePathToToolX86ExistsOnx86()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileExistsOnlyInX86, ProcessorArchitecture.X86, _defaultSdkToolsPath, _toolName, _log, true);

            // Path we expect to get out of the method
            string expectedPath = Path.Combine(_defaultSdkToolsPath, _toolName);

            // Message to show when the test fails.
            string message = "Expected to find the tool in the defaultSdkToolsPath but the method returned:" + toolPath;
            Assert.True(string.Equals(expectedPath, toolPath, StringComparison.OrdinalIgnoreCase), message);
            Assert.True(String.IsNullOrEmpty(_mockEngine.Log));
        }


        #endregion

        #region Test x64
        /// <summary>
        /// Test the case where the processor architecture is x64 and the tool exists in the x64 sdk path
        /// </summary>
        [Fact]
        public void GeneratePathToToolX64ExistsOnx64()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileExistsOnlyInX64, ProcessorArchitecture.AMD64, _defaultSdkToolsPath, _toolName, _log, true);

            // Path we expect to get out of the method
            string expectedPath = Path.Combine(_defaultSdkToolsPath, "x64");
            expectedPath = Path.Combine(expectedPath, _toolName);

            // Message to show when the test fails.
            string message = "Expected to find the tool in " + expectedPath + " but the method returned:" + toolPath;
            Assert.True(string.Equals(expectedPath, toolPath, StringComparison.OrdinalIgnoreCase), message);
            Assert.True(String.IsNullOrEmpty(_mockEngine.Log));
        }

        /// <summary>
        /// Test the case where the processor architecture is x64 and the tool does not exists in the x64 sdk path but does exist in the x86 path
        /// </summary>
        [Fact]
        public void GeneratePathToToolX64ExistsOnx86()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileExistsOnlyInX86, ProcessorArchitecture.AMD64, _defaultSdkToolsPath, _toolName, _log, true);

            // Path we expect to get out of the method
            string expectedPath = Path.Combine(_defaultSdkToolsPath, _toolName);

            // Message to show when the test fails.
            string message = "Expected to find the tool in " + expectedPath + " but the method returned:" + toolPath;
            Assert.True(string.Equals(expectedPath, toolPath, StringComparison.OrdinalIgnoreCase), message);
            Assert.True(String.IsNullOrEmpty(_mockEngine.Log));
        }
        #endregion

        #region Test Ia64
        /// <summary>
        /// Test the case where the processor architecture is ia64 and the tool exists in the ia64 sdk path
        /// </summary>
        [Fact]
        public void GeneratePathToToolIa64ExistsOnIa64()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileExistsOnlyInIa64, ProcessorArchitecture.IA64, _defaultSdkToolsPath, _toolName, _log, true);

            // Path we expect to get out of the method
            string expectedPath = Path.Combine(_defaultSdkToolsPath, "ia64");
            expectedPath = Path.Combine(expectedPath, _toolName);

            // Message to show when the test fails.
            string message = "Expected to find the tool in " + expectedPath + " but the method returned:" + toolPath;
            Assert.True(string.Equals(expectedPath, toolPath, StringComparison.OrdinalIgnoreCase), message);
            Assert.True(String.IsNullOrEmpty(_mockEngine.Log));
        }

        /// <summary>
        /// Test the case where the processor architecture is ia64 and the tool does not exists in the ia64 sdk path but does exist in the x86 path
        /// </summary>
        [Fact]
        public void GeneratePathToToolIa64ExistsOnx86()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileExistsOnlyInX86, ProcessorArchitecture.IA64, _defaultSdkToolsPath, _toolName, _log, true);

            // Path we expect to get out of the method
            string expectedPath = Path.Combine(_defaultSdkToolsPath, _toolName);

            // Message to show when the test fails.
            string message = "Expected to find the tool in " + expectedPath + " but the method returned:" + toolPath;
            Assert.True(string.Equals(expectedPath, toolPath, StringComparison.OrdinalIgnoreCase), message);
            Assert.True(String.IsNullOrEmpty(_mockEngine.Log));
        }
        #endregion


        /// <summary>
        /// Test the case where the processor architecture is x86 and the tool does not exist in the x86 sdk path (or anywhere for that matter)
        /// </summary>
        [Fact]
        public void GeneratePathToToolX86DoesNotExistAnywhere()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileDoesNotExist, ProcessorArchitecture.X86, _defaultSdkToolsPath, _toolName, _log, true);
            Assert.Null(toolPath);

            string comment = ResourceUtilities.FormatResourceString("General.PlatformSDKFileNotFoundSdkToolsPath", _toolName, _defaultSdkToolsPath, _defaultSdkToolsPath);
            _mockEngine.AssertLogContains(comment);

            comment = ResourceUtilities.FormatResourceString("General.SdkToolsPathToolDoesNotExist", _toolName, _defaultSdkToolsPath, ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Latest));
            _mockEngine.AssertLogContains(comment);
            Assert.Equal(1, _mockEngine.Errors);
        }

        /// <summary>
        /// Test the case where there are illegal chars in the sdktoolspath and Path.combine has a problem.
        /// </summary>
        [Fact]
        public void VerifyErrorWithIllegalChars()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "No invalid path characters under Unix"
            }

            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileDoesNotExist, ProcessorArchitecture.X86, "./?><;)(*&^%$#@!", _toolName, _log, true);
            Assert.Null(toolPath);
            _mockEngine.AssertLogContains("MSB3666");
            Assert.Equal(1, _mockEngine.Errors);
        }

        /// <summary>
        /// Test the case where the processor architecture is x86 and the tool does not exist in the x86 sdk path (or anywhere for that matter)and we do not want to log
        /// </summary>
        [Fact]
        public void GeneratePathToToolX86DoesNotExistAnywhereNoLogging()
        {
            string toolPath = SdkToolsPathUtility.GeneratePathToTool(_mockExists.MockFileDoesNotExist, ProcessorArchitecture.X86, _defaultSdkToolsPath, _toolName, _log, false);
            Assert.Null(toolPath);

            string comment = ResourceUtilities.FormatResourceString("General.PlatformSDKFileNotFoundSdkToolsPath", _toolName, _defaultSdkToolsPath, _defaultSdkToolsPath);
            _mockEngine.AssertLogDoesntContain(comment);

            comment = ResourceUtilities.FormatResourceString("General.SdkToolsPathToolDoesNotExist", _toolName, _defaultSdkToolsPath, ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45));
            _mockEngine.AssertLogDoesntContain(comment);
            Assert.Equal(0, _mockEngine.Errors);
        }

        #region Helper Classes
        // Task just so we can access to a real taskLogging helper and inspect the log.
        internal class TaskToLogFrom : Task
        {
            /// <summary>
            /// Empty execute, this task will never be executed
            /// </summary>
            /// <returns></returns>
            public override bool Execute()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// This class is used for testing the ability of the SdkToolsPathUtility class to handle situations when
        /// the toolname exists or does not exist.
        /// </summary>
        internal class MockFileExists
        {
            #region Data
            /// <summary>
            /// Path to the x86 sdk tools location
            /// </summary>
            private string _sdkToolsPath = null;
            #endregion

            #region Constructor

            /// <summary>
            /// This class gives the ability to create a fileexists delegate which helps in testing the sdktoolspath utility class
            /// which makes extensive use of fileexists.
            /// The sdkToolsPath is the expected location of the x86 sdk directory.
            /// </summary>
            public MockFileExists(string sdkToolsPath)
            {
                _sdkToolsPath = sdkToolsPath;
            }
            #endregion

            #region Properties
            /// <summary>
            /// A file exists object that will only return true if path passed in is the sdkToolsPath
            /// </summary>
            public FileExists MockFileExistsOnlyInX86
            {
                get
                {
                    return new FileExists(ExistsOnlyInX86);
                }
            }

            /// <summary>
            /// A file exists object that will only return true if path passed in is the sdkToolsPath\X64
            /// </summary>
            public FileExists MockFileExistsOnlyInX64
            {
                get
                {
                    return new FileExists(ExistsOnlyInX64);
                }
            }

            /// <summary>
            /// A file exists object that will only return true if path passed in is the sdkToolsPath\Ia64
            /// </summary>
            public FileExists MockFileExistsOnlyInIa64
            {
                get
                {
                    return new FileExists(ExistsOnlyInIa64);
                }
            }

            /// <summary>
            /// File exists delegate which will always return true
            /// </summary>
            public FileExists MockFileExistsInAll
            {
                get
                {
                    return new FileExists(ExistsInAll);
                }
            }

            /// <summary>
            /// File Exists delegate which will always return false
            /// </summary>
            public FileExists MockFileDoesNotExist
            {
                get
                {
                    return new FileExists(DoesNotExist);
                }
            }
            #endregion

            #region FileExists Methods
            /// <summary>
            /// A file exists object that will only return true if path passed in is the sdkToolsPath
            /// </summary>
            private bool ExistsOnlyInX86(string filePath)
            {
                return string.Equals(Path.GetDirectoryName(filePath), _sdkToolsPath, StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// A file exists object that will only return true if path passed in is the sdkToolsPath\x64
            /// </summary>
            private bool ExistsOnlyInX64(string filePath)
            {
                return string.Equals(Path.GetDirectoryName(filePath), Path.Combine(_sdkToolsPath, "x64"), StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// A file exists object that will only return true if path passed in is the sdkToolsPath
            /// </summary>
            private bool ExistsOnlyInIa64(string filePath)
            {
                return string.Equals(Path.GetDirectoryName(filePath), Path.Combine(_sdkToolsPath, "ia64"), StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// File Exists delegate which will always return true
            /// </summary>
            private bool ExistsInAll(string filePath)
            {
                return true;
            }

            /// <summary>
            /// File Exists delegate which will always return false
            /// </summary>
            private bool DoesNotExist(string filePath)
            {
                return false;
            }
            #endregion
        }
        #endregion
    }
}
