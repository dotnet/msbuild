// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Runtime.Hosting;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.AxTlbImp_Tests
{
    sealed public class AxTlbBaseTask_Tests
    {
        /// <summary>
        /// Tests the /delaysign switch
        /// </summary>
        [Fact]
        public void DelaySign()
        {
            AxTlbBaseTask t = new ResolveComReference.AxImp();

            Assert.False(t.DelaySign); // "DelaySign should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/delaysign",
                false /* no response file */);

            t.DelaySign = true;
            Assert.True(t.DelaySign); // "DelaySign should be true"
            CommandLine.ValidateHasParameter(
                t,
                @"/delaysign",
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /keycontainer: switch
        /// </summary>
        [Fact]
        public void KeyContainer()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Key container is not supported, except under Windows"
            }

            var t = new ResolveComReference.TlbImp();
            t.TypeLibName = "FakeTlb.tlb";
            string badParameterValue = "badKeyContainer";
            string goodParameterValue = "myKeyContainer";

            try
            {
                t.ToolPath = Path.GetTempPath();

                Assert.Null(t.KeyContainer); // "KeyContainer should be null by default");
                CommandLine.ValidateNoParameterStartsWith(
                    t,
                    @"/keycontainer:",
                    false /* no response file */);

                t.KeyContainer = badParameterValue;
                Assert.Equal(badParameterValue, t.KeyContainer); // "New KeyContainer value should be set"
                CommandLine.ValidateHasParameter(t, @"/keycontainer:" + badParameterValue, false /* no response file */);
                Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.StrongNameUtils.NoKeyPairInContainer", t.KeyContainer);
                //ensure the key does not exist in the CSP
                StrongNameHelpers.StrongNameKeyDelete(goodParameterValue);

                IntPtr publicKeyBlob = IntPtr.Zero;
                int publicKeyBlobSize = 0;

                //add key to CSP
                if (StrongNameHelpers.StrongNameKeyGen(goodParameterValue, 1 /* leave key registered */, out publicKeyBlob, out publicKeyBlobSize) && publicKeyBlob != IntPtr.Zero)
                {
                    StrongNameHelpers.StrongNameFreeBuffer(publicKeyBlob);

                    t.KeyContainer = goodParameterValue;
                    Assert.Equal(goodParameterValue, t.KeyContainer); // "New KeyContainer value should be set"
                    CommandLine.ValidateHasParameter(t, @"/keycontainer:" + goodParameterValue, false /* no response file */);
                    Utilities.ExecuteTaskAndVerifyLogDoesNotContainErrorFromResource(t, "AxTlbBaseTask.StrongNameUtils.NoKeyPairInContainer", t.KeyContainer);
                }
                else
                {
                    Assert.True(false, "Key container could not be created (perhaps you are not running as admin).");
                }
            }
            finally
            {
                //remove key from CSP
                StrongNameHelpers.StrongNameKeyDelete(goodParameterValue);

                // get rid of the generated temp file
                if (goodParameterValue != null)
                {
                    File.Delete(goodParameterValue);
                }
            }
        }

        /// <summary>
        /// Tests the /keycontainer: switch with a space in the name
        /// </summary>
        [Fact]
        public void KeyContainerWithSpaces()
        {
            AxTlbBaseTask t = new ResolveComReference.AxImp();
            string testParameterValue = @"my Key Container";

            Assert.Null(t.KeyContainer); // "KeyContainer should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/keycontainer:",
                false /* no response file */);

            t.KeyContainer = testParameterValue;
            Assert.Equal(testParameterValue, t.KeyContainer); // "New KeyContainer value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/keycontainer:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /keyfile: switch
        /// </summary>
        [Fact]
        public void KeyFile()
        {
            var t = new ResolveComReference.AxImp();
            t.ActiveXControlName = "FakeControl.ocx";
            string badParameterValue = "myKeyFile.key";
            string goodParameterValue = null;

            try
            {
                goodParameterValue = FileUtilities.GetTemporaryFile();
                t.ToolPath = Path.GetTempPath();

                Assert.Null(t.KeyFile); // "KeyFile should be null by default"
                CommandLine.ValidateNoParameterStartsWith(
                    t,
                    @"/keyfile:",
                    false /* no response file */);

                t.KeyFile = badParameterValue;
                Assert.Equal(badParameterValue, t.KeyFile); // "New KeyFile value should be set"
                CommandLine.ValidateHasParameter(
                    t,
                    @"/keyfile:" + badParameterValue,
                    false /* no response file */);
                Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.InvalidKeyFileSpecified", t.KeyFile);

                t.KeyFile = goodParameterValue;
                Assert.Equal(goodParameterValue, t.KeyFile); // "New KeyFile value should be set"
                CommandLine.ValidateHasParameter(
                    t,
                    @"/keyfile:" + goodParameterValue,
                    false /* no response file */);
                Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.StrongNameUtils.NoKeyPairInFile", t.KeyFile);
            }
            finally
            {
                if (goodParameterValue != null)
                {
                    // get rid of the generated temp file
                    File.Delete(goodParameterValue);
                }
            }
        }

        /// <summary>
        /// Tests the /keyfile: switch with a space in the filename
        /// </summary>
        [Fact]
        public void KeyFileWithSpaces()
        {
            AxTlbBaseTask t = new ResolveComReference.TlbImp();
            string testParameterValue = @"C:\Program Files\myKeyFile.key";

            Assert.Null(t.KeyFile); // "KeyFile should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/keyfile:",
                false /* no response file */);

            t.KeyFile = testParameterValue;
            Assert.Equal(testParameterValue, t.KeyFile); // "New KeyFile value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/keyfile:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the SdkToolsPath property:  Should log an error if it's null or a bad path.  
        /// </summary>
        [Fact]
        public void SdkToolsPath()
        {
            var t = new ResolveComReference.TlbImp();
            t.TypeLibName = "FakeLibrary.tlb";
            string badParameterValue = @"C:\Program Files\Microsoft Visual Studio 10.0\My Fake SDK Path";
            string goodParameterValue = Path.GetTempPath();
            bool taskPassed;

            Assert.Null(t.SdkToolsPath); // "SdkToolsPath should be null by default"
            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            t.SdkToolsPath = badParameterValue;
            Assert.Equal(badParameterValue, t.SdkToolsPath); // "New SdkToolsPath value should be set"
            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            MockEngine e = new MockEngine();
            t.BuildEngine = e;
            t.SdkToolsPath = goodParameterValue;

            Assert.Equal(goodParameterValue, t.SdkToolsPath); // "New SdkToolsPath value should be set"
            taskPassed = t.Execute();
            Assert.False(taskPassed); // "Task should still fail -- there are other things wrong with it."

            // but that particular error shouldn't be there anymore.
            string sdkToolsPathMessage = t.Log.FormatResourceString("AxTlbBaseTask.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);
            string messageWithNoCode;
            string sdkToolsPathCode = t.Log.ExtractMessageCode(sdkToolsPathMessage, out messageWithNoCode);
            e.AssertLogDoesntContain(sdkToolsPathCode);
        }

        /// <summary>
        /// Tests the ToolPath property:  Should log an error if it's null or a bad path.  
        /// </summary>
        [Fact]
        public void ToolPath()
        {
            var t = new ResolveComReference.AxImp();
            t.ActiveXControlName = "FakeControl.ocx";
            string badParameterValue = @"C:\Program Files\Microsoft Visual Studio 10.0\My Fake SDK Path";
            string goodParameterValue = Path.GetTempPath();
            bool taskPassed;

            Assert.Null(t.ToolPath); // "ToolPath should be null by default"
            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            t.ToolPath = badParameterValue;
            Assert.Equal(badParameterValue, t.ToolPath); // "New ToolPath value should be set"
            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            MockEngine e = new MockEngine();
            t.BuildEngine = e;
            t.ToolPath = goodParameterValue;

            Assert.Equal(goodParameterValue, t.ToolPath); // "New ToolPath value should be set"
            taskPassed = t.Execute();
            Assert.False(taskPassed); // "Task should still fail -- there are other things wrong with it."

            // but that particular error shouldn't be there anymore.
            string toolPathMessage = t.Log.FormatResourceString("AxTlbBaseTask.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);
            string messageWithNoCode;
            string toolPathCode = t.Log.ExtractMessageCode(toolPathMessage, out messageWithNoCode);
            e.AssertLogDoesntContain(toolPathCode);
        }

        /// <summary>
        /// Tests that strong name sign-related parameters are validated properly, causing the task
        /// to fail if they are incorrectly set up.
        /// </summary>
        [Fact]
        public void TaskFailsWhenImproperlySigned()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Key container is not supported, except under Windows"
            }

            var t = new ResolveComReference.TlbImp();
            t.TypeLibName = "Blah.tlb";
            string tempKeyContainer = null;
            string tempKeyFile = null;

            try
            {
                tempKeyContainer = FileUtilities.GetTemporaryFile();
                tempKeyFile = FileUtilities.GetTemporaryFile();
                t.ToolPath = Path.GetTempPath();

                // DelaySign is passed without a KeyFile or a KeyContainer
                t.DelaySign = true;
                Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.CannotSpecifyDelaySignWithoutEitherKeyFileOrKeyContainer");

                // KeyContainer and KeyFile are both passed in
                t.KeyContainer = tempKeyContainer;
                t.KeyFile = tempKeyFile;
                Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.CannotSpecifyBothKeyFileAndKeyContainer");

                // All the inputs are correct, but the KeyContainer passed in is bad            
                t.DelaySign = false;
                t.KeyContainer = tempKeyContainer;
                t.KeyFile = null;
                Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.StrongNameUtils.NoKeyPairInContainer", t.KeyContainer);

                // All the inputs are correct, but the KeyFile passed in is bad            
                t.KeyContainer = null;
                t.KeyFile = tempKeyFile;
                Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxTlbBaseTask.StrongNameUtils.NoKeyPairInFile", t.KeyFile);
            }
            finally
            {
                if (tempKeyContainer != null)
                {
                    File.Delete(tempKeyContainer);
                }
                if (tempKeyFile != null)
                {
                    File.Delete(tempKeyContainer);
                }
            }
        }
    }

    sealed internal class Utilities
    {
        /// <summary>
        /// Given an instance of an AxImp task, executes that task (assuming all necessary parameters
        /// have been set ahead of time) and verifies that the execution log contains the error
        /// corresponding to the resource name passed in. 
        /// </summary>
        /// <param name="t">The task to execute and check</param>
        /// <param name="errorResource">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        internal static void ExecuteTaskAndVerifyLogContainsErrorFromResource(AxTlbBaseTask t, string errorResource, params object[] args)
        {
            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            bool taskPassed = t.Execute();
            Assert.False(taskPassed); // "Task should have failed"

            VerifyLogContainsErrorFromResource(e, t.Log, errorResource, args);
        }

        /// <summary>
        /// Given a log and a resource string, acquires the text of that resource string and
        /// compares it to the log.  Asserts if the log does not contain the desired string.
        /// </summary>
        /// <param name="e">The MockEngine that contains the log we're checking</param>
        /// <param name="log">The TaskLoggingHelper that we use to load the string resource</param>
        /// <param name="errorResource">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        internal static void VerifyLogContainsErrorFromResource(MockEngine e, TaskLoggingHelper log, string errorResource, params object[] args)
        {
            string errorMessage = log.FormatResourceString(errorResource, args);
            e.AssertLogContains(errorMessage);
        }

        /// <summary>
        /// Given an instance of an AxImp task, executes that task (assuming all necessary parameters
        /// have been set ahead of time) and verifies that the execution log does not contain the error
        /// corresponding to the resource name passed in. 
        /// </summary>
        /// <param name="t">The task to execute and check</param>
        /// <param name="errorResource">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        internal static void ExecuteTaskAndVerifyLogDoesNotContainErrorFromResource(AxTlbBaseTask t, string errorResource, params object[] args)
        {
            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            bool taskPassed = t.Execute();

            VerifyLogDoesNotContainErrorFromResource(e, t.Log, errorResource, args);
        }

        /// <summary>
        /// Given a log and a resource string, acquires the text of that resource string and
        /// compares it to the log.  Assert fails if the log contains the desired string.
        /// </summary>
        /// <param name="e">The MockEngine that contains the log we're checking</param>
        /// <param name="log">The TaskLoggingHelper that we use to load the string resource</param>
        /// <param name="errorResource">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        internal static void VerifyLogDoesNotContainErrorFromResource(MockEngine e, TaskLoggingHelper log, string errorResource, params object[] args)
        {
            string errorMessage = log.FormatResourceString(errorResource, args);
            e.AssertLogDoesntContain(errorMessage);
        }
    }
}
