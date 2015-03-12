// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.Globalization;

using ResGen = Microsoft.Build.Tasks.GenerateResource.ResGen;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ResGen_Tests
    {
        /// <summary>
        /// Verify InputFiles:
        ///  - Defaults to null, in which case the task just returns true and continues
        ///  - If there are InputFiles, verify that they all show up on the command line
        ///  - Verify that OutputFiles defaults appropriately
        /// </summary>
        [TestMethod]
        public void InputFiles()
        {
            ResGen t = new ResGen();
            ITaskItem[] singleTestFile = { new TaskItem("foo.resx") };
            ITaskItem[] singleOutput = { new TaskItem("foo.resources") };
            ITaskItem[] multipleTestFiles = { new TaskItem("hello.resx"), new TaskItem("world.resx"), new TaskItem("!.resx") };
            ITaskItem[] multipleOutputs = { new TaskItem("hello.resources"), new TaskItem("world.resources"), new TaskItem("!.resources") };

            // Default:  InputFiles is null
            Assert.IsNull(t.InputFiles, "InputFiles is null by default");
            Assert.IsNull(t.OutputFiles, "OutputFiles is null by default");
            ExecuteTaskAndVerifyLogContainsResource(t, true /* task passes */, "ResGen.NoInputFiles");

            // One input file -- compile
            t.InputFiles = singleTestFile;
            t.StronglyTypedLanguage = null;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));

            Assert.AreEqual(singleTestFile, t.InputFiles, "New InputFiles value should be set");
            Assert.IsNull(t.OutputFiles, "OutputFiles is null until default name generation is triggered");

            string commandLineParameter = String.Join(",", new string[] { singleTestFile[0].ItemSpec, singleOutput[0].ItemSpec });
            CommandLine.ValidateHasParameter(t, commandLineParameter, true /* resgen 4.0 supports response files */);
            CommandLine.ValidateHasParameter(t, @"/compile", true /* resgen 4.0 supports response files */);

            // One input file -- STR
            t.InputFiles = singleTestFile;
            t.StronglyTypedLanguage = "c#";

            CommandLine.ValidateHasParameter(t, singleTestFile[0].ItemSpec, false /* resgen 4.0 does not appear to support response files for STR */);
            CommandLine.ValidateHasParameter(t, singleOutput[0].ItemSpec, false /* resgen 4.0 does not appear to support response files for STR */);
            CommandLine.ValidateHasParameter(t, "/str:c#,,,", false /* resgen 4.0 does not appear to support response files for STR */);

            // Multiple input files -- compile
            t.InputFiles = multipleTestFiles;
            t.OutputFiles = null; // want it to reset to default
            t.StronglyTypedLanguage = null;

            Assert.AreEqual(multipleTestFiles, t.InputFiles, "New InputFiles value should be set");

            CommandLine.ValidateHasParameter(t, @"/compile", true /* resgen 4.0 supports response files */);
            for (int i = 0; i < multipleTestFiles.Length; i++)
            {
                commandLineParameter = String.Join(",", new string[] { multipleTestFiles[i].ItemSpec, multipleOutputs[i].ItemSpec });
                CommandLine.ValidateHasParameter(t, commandLineParameter, true /* resgen 4.0 supports response files */);
            }

            // Multiple input files -- STR (should error)
            t.InputFiles = multipleTestFiles;
            t.StronglyTypedLanguage = "vb";

            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.STRLanguageButNotExactlyOneSourceFile");
        }

        /// <summary>
        /// Verify OutputFiles:
        ///  - Default values were tested by InputFiles()
        ///  - Verify that if InputFiles and OutputFiles are different lengths (and both exist), an error is logged
        ///  - Verify that if OutputFiles are set explicitly, they map and show up on the command line as expected
        /// </summary>
        [TestMethod]
        public void OutputFiles()
        {
            ResGen t = new ResGen();

            ITaskItem[] differentLengthInput = { new TaskItem("hello.resx") };
            ITaskItem[] differentLengthOutput = { new TaskItem("world.resources"), new TaskItem("!.resources") };
            ITaskItem[] differentLengthDefaultOutput = { new TaskItem("hello.resources") };

            // Different length inputs -- should error
            t.InputFiles = differentLengthInput;
            t.OutputFiles = differentLengthOutput;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));

            Assert.AreEqual(differentLengthInput, t.InputFiles, "New InputFiles value should be set");
            Assert.AreEqual(differentLengthOutput, t.OutputFiles, "New OutputFiles value should be set");

            ExecuteTaskAndVerifyLogContainsErrorFromResource
                (
                t,
                "General.TwoVectorsMustHaveSameLength",
                differentLengthInput.Length,
                differentLengthOutput.Length,
                "InputFiles",
                "OutputFiles"
                );

            // If only OutputFiles is set, then the task should return -- as far as 
            // it's concerned, no work needs to be done.
            t = new ResGen(); // zero out the log
            t.InputFiles = null;
            t.OutputFiles = differentLengthOutput;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));

            Assert.IsNull(t.InputFiles, "New InputFiles value should be set");
            Assert.AreEqual(differentLengthOutput, t.OutputFiles, "New OutputFiles value should be set");

            ExecuteTaskAndVerifyLogContainsResource(t, true /* task passes */, "ResGen.NoInputFiles");

            // However, iff OutputFiles is set to null, it should revert back to default
            t.InputFiles = differentLengthInput;
            t.OutputFiles = null;

            Assert.AreEqual(differentLengthInput, t.InputFiles, "New InputFiles value should be set");
            Assert.IsNull(t.OutputFiles, "OutputFiles is null until default name generation is triggered");

            string commandLineParameter = String.Join(",", new string[] { differentLengthInput[0].ItemSpec, differentLengthDefaultOutput[0].ItemSpec });
            CommandLine.ValidateHasParameter(t, commandLineParameter, true /* resgen 4.0 supports response files */);
            CommandLine.ValidateHasParameter(t, @"/compile", true /* resgen 4.0 supports response files */);

            // Explicitly setting output
            ITaskItem[] inputFiles = { new TaskItem("foo.resx") };
            ITaskItem[] defaultOutput = { new TaskItem("foo.resources") };
            ITaskItem[] explicitOutput = { new TaskItem("bar.txt") };

            t.InputFiles = inputFiles;
            t.OutputFiles = null;

            Assert.AreEqual(inputFiles, t.InputFiles, "New InputFiles value should be set");
            Assert.IsNull(t.OutputFiles, "OutputFiles is null until default name generation is triggered");

            commandLineParameter = String.Join(",", new string[] { inputFiles[0].ItemSpec, defaultOutput[0].ItemSpec });
            CommandLine.ValidateHasParameter(t, commandLineParameter, true /* resgen 4.0 supports response files */);
            CommandLine.ValidateHasParameter(t, @"/compile", true /* resgen 4.0 supports response files */);

            t.OutputFiles = explicitOutput;

            Assert.AreEqual(inputFiles, t.InputFiles, "New InputFiles value should be set");
            Assert.AreEqual(explicitOutput, t.OutputFiles, "New OutputFiles value should be set");

            commandLineParameter = String.Join(",", new string[] { inputFiles[0].ItemSpec, explicitOutput[0].ItemSpec });
            CommandLine.ValidateHasParameter(t, commandLineParameter, true /* resgen 4.0 supports response files */);
            CommandLine.ValidateHasParameter(t, @"/compile", true /* resgen 4.0 supports response files */);
        }

        /// <summary>
        /// Tests ResGen's /publicClass switch
        /// </summary>
        [TestMethod]
        public void PublicClass()
        {
            ResGen t = new ResGen();
            ITaskItem[] throwawayInput = { new TaskItem("hello.resx") };

            t.InputFiles = throwawayInput;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));

            Assert.IsFalse(t.PublicClass, "PublicClass should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/publicClass", true /* resgen 4.0 supports response files */);

            t.PublicClass = true;
            Assert.IsTrue(t.PublicClass, "PublicClass should be true");
            CommandLine.ValidateHasParameter(t, @"/publicClass", true /* resgen 4.0 supports response files */);
        }

        /// <summary>
        /// Tests the /r: parameter (passing in reference assemblies)
        /// </summary>
        [TestMethod]
        public void References()
        {
            ResGen t = new ResGen();
            ITaskItem[] throwawayInput = { new TaskItem("hello.resx") };
            ITaskItem a = new TaskItem();
            ITaskItem b = new TaskItem();

            t.InputFiles = throwawayInput;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));

            a.ItemSpec = "foo.dll";
            b.ItemSpec = "bar.dll";

            ITaskItem[] singleReference = { a };
            ITaskItem[] multipleReferences = { a, b };

            Assert.IsNull(t.References, "References should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, "/r:", true /* resgen 4.0 supports response files */);

            // Single reference
            t.References = singleReference;
            Assert.AreEqual(singleReference, t.References, "New References value should be set");
            CommandLine.ValidateHasParameter(t, "/r:" + singleReference[0].ItemSpec, true /* resgen 4.0 supports response files */);

            // MultipleReferences
            t.References = multipleReferences;
            Assert.AreEqual(multipleReferences, t.References, "New References value should be set");

            foreach (ITaskItem reference in multipleReferences)
            {
                CommandLine.ValidateHasParameter(t, "/r:" + reference.ItemSpec, true /* resgen 4.0 supports response files */);
            }
            // test cases where command line length is equal to the maximum allowed length and just above the maximum allowed length
            // we do some calculation here to do ensure that the resulting command lines match these cases (see Case 1 and 2 below)

            // reference switch adds space + "/r:", (4 characters)
            int referenceSwitchDelta = 4;

            //subtract one because leading space is added to command line arguments
            int maxCommandLineLength = 28000 - 1;
            int referencePathLength = 200;

            //min reference argument is " /r:a.dll"
            int minReferenceArgumentLength = referenceSwitchDelta + "a.dll".Length;

            // reference name is of the form aaa...aaa###.dll (repeated a characters followed by 3 
            // digit identifier for uniqueness followed by the .dll file extension
            StringBuilder referencePathBuilder = new StringBuilder();
            referencePathBuilder.Append('a', referencePathLength - (3 /* 3 digit identifier */ + 4 /* file extension */));
            string longReferenceNameBase = referencePathBuilder.ToString();


            // reference switch length plus the length of the reference path
            int referenceArgumentLength = referencePathLength + referenceSwitchDelta;

            t = CreateCommandLineResGen();

            // compute command line with only one reference switch so remaining added reference
            // arguments will have the same length, since the first reference argument added may not have a 
            // leading space
            List<ITaskItem> references = new List<ITaskItem>();
            references.Add(new TaskItem() { ItemSpec = "a.dll" });

            t.References = references.ToArray();
            int baseCommandLineLength = CommandLine.GetCommandLine(t, false).Length;

            Assert.IsTrue(baseCommandLineLength < maxCommandLineLength, "Cannot create command line less than the maximum allowed command line");

            // calculate how many reference arguments will need to be added and what the length of the last argument
            // should be so that the command line length is equal to the maximum allowed length
            int remainder;
            int quotient = Math.DivRem(maxCommandLineLength - baseCommandLineLength, referenceArgumentLength, out remainder);
            if (remainder < minReferenceArgumentLength)
            {
                remainder += referenceArgumentLength;
                quotient--;
            }

            // compute the length of the last reference argument
            int lastReferencePathLength = remainder - (4 /* switch length */ + 4 /* file extension */);

            for (int i = 0; i < quotient; i++)
            {
                string refIndex = i.ToString().PadLeft(3, '0');
                references.Add(new TaskItem() { ItemSpec = (longReferenceNameBase + refIndex + ".dll") });
            }


            //
            // Case 1: Command line length is equal to the maximum allowed value
            //

            // create last reference argument
            referencePathBuilder.Clear();
            referencePathBuilder.Append('b', lastReferencePathLength).Append(".dll");
            ITaskItem lastReference = new TaskItem() { ItemSpec = referencePathBuilder.ToString() };
            references.Add(lastReference);

            // set references
            t.References = references.ToArray();

            int commandLineLength = CommandLine.GetCommandLine(t, false).Length;
            Assert.IsTrue(commandLineLength == maxCommandLineLength, "Command line length {0} is not equal to the maximum possible command line length {1}", commandLineLength, maxCommandLineLength);

            ExecuteTaskAndVerifyLogDoesNotContainResource
            (
                t,
                false,
                "ResGen.CommandTooLong",
                CommandLine.GetCommandLine(t, false).Length
            );

            VerifyLogDoesNotContainResource((MockEngine)t.BuildEngine, GetPrivateLog(t), "ToolTask.CommandTooLong", typeof(ResGen).Name);

            //
            // Case 2: Command line length is one more than the maximum allowed value
            //


            // make last reference name longer by one character so that command line should become too long
            referencePathBuilder.Insert(0, 'b');
            lastReference.ItemSpec = referencePathBuilder.ToString();

            // reset ResGen task, since execution can change the command line
            t = CreateCommandLineResGen();
            t.References = references.ToArray();

            commandLineLength = CommandLine.GetCommandLine(t, false).Length;
            Assert.IsTrue(commandLineLength == (maxCommandLineLength + 1), "Command line length {0} is not one more than the maximum possible command line length {1}", commandLineLength, maxCommandLineLength + 1);

            ExecuteTaskAndVerifyLogContainsErrorFromResource
            (
                t,
                "ResGen.CommandTooLong",
                CommandLine.GetCommandLine(t, false).Length
            );

            VerifyLogDoesNotContainResource((MockEngine)t.BuildEngine, GetPrivateLog(t), "ToolTask.CommandTooLong", typeof(ResGen).Name);
        }

        private ResGen CreateCommandLineResGen()
        {
            ResGen t = new ResGen();
            ITaskItem[] throwawayInput = { new TaskItem("hello.resx") };

            // setting these values should ensure that no response file is used
            t.UseCommandProcessor = false;
            t.StronglyTypedLanguage = "CSharp";

            t.InputFiles = throwawayInput;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));
            return t;
        }

        /// <summary>
        /// Tests the SdkToolsPath property:  Should log an error if it's null or a bad path.  
        /// </summary>
        [TestMethod]
        public void SdkToolsPath()
        {
            ResGen t = new ResGen();
            string badParameterValue = @"C:\Program Files\Microsoft Visual Studio 10.0\My Fake SDK Path";
            string goodParameterValue = Path.GetTempPath();
            ITaskItem[] throwawayInput = { new TaskItem("hello.resx") };

            // Without any inputs, the task just passes
            t.InputFiles = throwawayInput;

            Assert.IsNull(t.SdkToolsPath, "SdkToolsPath should be null by default");
            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            t.SdkToolsPath = badParameterValue;
            Assert.AreEqual(badParameterValue, t.SdkToolsPath, "New SdkToolsPath value should be set");
            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            MockEngine e = new MockEngine();
            t.BuildEngine = e;
            t.SdkToolsPath = goodParameterValue;

            Assert.AreEqual(goodParameterValue, t.SdkToolsPath, "New SdkToolsPath value should be set");

            bool taskPassed = t.Execute();
            Assert.IsFalse(taskPassed, "Task should still fail -- there are other things wrong with it.");

            // but that particular error shouldn't be there anymore.
            string sdkToolsPathMessage = t.Log.FormatResourceString("ResGen.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);
            string messageWithNoCode;
            string sdkToolsPathCode = t.Log.ExtractMessageCode(sdkToolsPathMessage, out messageWithNoCode);
            e.AssertLogDoesntContain(sdkToolsPathCode);
        }

        /// <summary>
        /// Verifies the parameters that for resgen.exe's /str: switch
        /// </summary>
        [TestMethod]
        public void StronglyTypedParameters()
        {
            ResGen t = new ResGen();
            ITaskItem[] throwawayInput = { new TaskItem("hello.resx") };

            string strLanguage = "c#";
            string strNamespace = "Microsoft.Build.Foo";
            string strClass = "MyFoo";
            string strFile = "MyFoo.cs";

            // Without any inputs, the task just passes
            t.InputFiles = throwawayInput;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));

            // Language is null by default
            Assert.IsNull(t.StronglyTypedLanguage, "StronglyTypedLanguage should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, "/str:", false /* resgen 4.0 does not appear to support response files for STR */);

            // If other STR parameters are passed, we error, suggesting they might want a language as well.
            t.StronglyTypedNamespace = strNamespace;
            CommandLine.ValidateNoParameterStartsWith(t, "/str:", false /* resgen 4.0 does not appear to support response files for STR */);
            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.STRClassNamespaceOrFilenameWithoutLanguage");

            t.StronglyTypedNamespace = "";
            t.StronglyTypedClassName = strClass;
            CommandLine.ValidateNoParameterStartsWith(t, "/str:", false /* resgen 4.0 does not appear to support response files for STR */);
            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.STRClassNamespaceOrFilenameWithoutLanguage");

            t.StronglyTypedClassName = "";
            t.StronglyTypedFileName = strFile;
            CommandLine.ValidateNoParameterStartsWith(t, "/str:", false /* resgen 4.0 does not appear to support response files for STR */);
            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.STRClassNamespaceOrFilenameWithoutLanguage");

            // However, if it is passed, the /str: switch gets added
            t.StronglyTypedLanguage = strLanguage;
            t.StronglyTypedNamespace = "";
            t.StronglyTypedClassName = "";
            t.StronglyTypedFileName = "";

            CommandLine.ValidateHasParameter(t, "/str:" + strLanguage + ",,,", false /* resgen 4.0 does not appear to support response files for STR */);

            t.StronglyTypedNamespace = strNamespace;
            CommandLine.ValidateHasParameter(t, "/str:" + strLanguage + "," + strNamespace + ",,", false /* resgen 4.0 does not appear to support response files for STR */);

            t.StronglyTypedClassName = strClass;
            CommandLine.ValidateHasParameter(t, "/str:" + strLanguage + "," + strNamespace + "," + strClass + ",", false /* resgen 4.0 does not appear to support response files for STR */);

            t.StronglyTypedFileName = strFile;
            CommandLine.ValidateHasParameter(t, "/str:" + strLanguage + "," + strNamespace + "," + strClass + "," + strFile, false /* resgen 4.0 does not appear to support response files for STR */);
        }

        /// <summary>
        /// Tests the ToolPath property:  Should log an error if it's null or a bad path.  
        /// </summary>
        [TestMethod]
        public void ToolPath()
        {
            ResGen t = new ResGen();
            string badParameterValue = @"C:\Program Files\Microsoft Visual Studio 10.0\My Fake SDK Path";
            string goodParameterValue = Path.GetTempPath();
            ITaskItem[] throwawayInput = { new TaskItem("hello.resx") };

            // Without any inputs, the task just passes
            t.InputFiles = throwawayInput;

            Assert.IsNull(t.ToolPath, "ToolPath should be null by default");
            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            t.ToolPath = badParameterValue;
            Assert.AreEqual(badParameterValue, t.ToolPath, "New ToolPath value should be set");
            ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "ResGen.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);

            MockEngine e = new MockEngine();
            t.BuildEngine = e;
            t.ToolPath = goodParameterValue;

            Assert.AreEqual(goodParameterValue, t.ToolPath, "New ToolPath value should be set");

            bool taskPassed = t.Execute();
            Assert.IsFalse(taskPassed, "Task should still fail -- there are other things wrong with it.");

            // but that particular error shouldn't be there anymore.
            string toolPathMessage = t.Log.FormatResourceString("ResGen.SdkOrToolPathNotSpecifiedOrInvalid", t.SdkToolsPath, t.ToolPath);
            string messageWithNoCode;
            string toolPathCode = t.Log.ExtractMessageCode(toolPathMessage, out messageWithNoCode);
            e.AssertLogDoesntContain(toolPathCode);
        }

        /// <summary>
        /// Tests ResGen's /useSourcePath switch
        /// </summary>
        [TestMethod]
        public void UseSourcePath()
        {
            ResGen t = new ResGen();
            ITaskItem[] throwawayInput = { new TaskItem("hello.resx") };

            t.InputFiles = throwawayInput;
            t.ToolPath = Path.GetDirectoryName(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version45));

            Assert.IsFalse(t.UseSourcePath, "UseSourcePath should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/useSourcePath", true /* resgen 4.0 supports response files */);

            t.UseSourcePath = true;
            Assert.IsTrue(t.UseSourcePath, "UseSourcePath should be true");
            CommandLine.ValidateHasParameter(t, @"/useSourcePath", true /* resgen 4.0 supports response files */);
        }

        #region Helper Functions

        /// <summary>
        /// Given an instance of a ResGen task, executes that task (assuming all necessary parameters
        /// have been set ahead of time) and verifies that the execution log contains the error
        /// corresponding to the resource name passed in. 
        /// </summary>
        /// <param name="t">The task to execute and check</param>
        /// <param name="errorResource">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        private void ExecuteTaskAndVerifyLogContainsErrorFromResource(ResGen t, string errorResource, params object[] args)
        {
            ExecuteTaskAndVerifyLogContainsResource(t, false, errorResource, args);
        }

        /// <summary>
        /// Given an instance of a ResGen task, executes that task (assuming all necessary parameters
        /// have been set ahead of time), verifies that the task had the expected result, and checks 
        /// the log for the string corresponding to the resource name passed in
        /// </summary>
        /// <param name="t">The task to execute and check</param>
        /// <param name="expectedResult">true if the task is expected to pass, false otherwise</param>
        /// <param name="resourceString">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        private void ExecuteTaskAndVerifyLogContainsResource(ResGen t, bool expectedResult, string resourceString, params object[] args)
        {
            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            bool taskPassed = t.Execute();
            Assert.IsTrue(taskPassed == expectedResult, "Unexpected task result");

            VerifyLogContainsResource(e, t.Log, resourceString, args);
        }

        /// <summary>
        /// Given a log and a resource string, acquires the text of that resource string and
        /// compares it to the log.  Asserts if the log does not contain the desired string.
        /// </summary>
        /// <param name="e">The MockEngine that contains the log we're checking</param>
        /// <param name="log">The TaskLoggingHelper that we use to load the string resource</param>
        /// <param name="errorResource">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        private void VerifyLogContainsResource(MockEngine e, TaskLoggingHelper log, string messageResource, params object[] args)
        {
            string message = log.FormatResourceString(messageResource, args);
            e.AssertLogContains(message);
        }

        /// <summary>
        /// Given an instance of a ResGen task, executes that task (assuming all necessary parameters
        /// have been set ahead of time), verifies that the task had the expected result, and ensures 
        /// the log does not contain the string corresponding to the resource name passed in
        /// </summary>
        /// <param name="t">The task to execute and check</param>
        /// <param name="expectedResult">true if the task is expected to pass, false otherwise</param>
        /// <param name="resourceString">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        private void ExecuteTaskAndVerifyLogDoesNotContainResource(ResGen t, bool expectedResult, string resourceString, params object[] args)
        {
            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            bool taskPassed = t.Execute();
            Assert.IsTrue(taskPassed == expectedResult, "Unexpected task result");

            VerifyLogDoesNotContainResource(e, t.Log, resourceString, args);
        }

        /// <summary>
        /// Given a log and a resource string, acquires the text of that resource string and
        /// compares it to the log.  Assert fails if the log contain the desired string.
        /// </summary>
        /// <param name="e">The MockEngine that contains the log we're checking</param>
        /// <param name="log">The TaskLoggingHelper that we use to load the string resource</param>
        /// <param name="errorResource">The name of the resource string to check the log for</param>
        /// <param name="args">Arguments needed to format the resource string properly</param>
        private void VerifyLogDoesNotContainResource(MockEngine e, TaskLoggingHelper log, string messageResource, params object[] args)
        {
            string message = log.FormatResourceString(messageResource, args);
            e.AssertLogDoesntContain(message);
        }

        /// <summary>
        /// Gets the LogPrivate on the given ToolTask instance. We need to use reflection since
        /// LogPrivate is a private property.
        /// </summary>
        /// <returns></returns>
        static private TaskLoggingHelper GetPrivateLog(ToolTask task)
        {
            PropertyInfo logPrivateProperty = typeof(ToolTask).GetProperty("LogPrivate", BindingFlags.Instance | BindingFlags.NonPublic);
            return (TaskLoggingHelper)logPrivateProperty.GetValue(task, null);
        }

        #endregion // Helper Functions
    }
}
