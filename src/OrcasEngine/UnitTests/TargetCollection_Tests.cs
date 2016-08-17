// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class AddTargetToCollection_Tests
    {
        private Engine engine;
        private Project myProject;
        private MockLogger myLogger;

        /// <summary>
        /// Creates the engine and parent object. Also registers the mock logger.
        /// </summary>
        [SetUp()]
        public void Initialize()
        {
            engine = new Engine();
            myProject = new Project(engine);
            myLogger = new MockLogger();
            myProject.ParentEngine.RegisterLogger(myLogger);
        }

        /// <summary>
        /// Unloads projects and un-registers logger.
        /// </summary>
        [TearDown()]
        public void Cleanup()
        {
            engine.UnloadProject(myProject);
            engine.UnregisterAllLoggers();
            engine = null;
            myProject = null;
            myLogger = null;
        }

        /// <summary>
        /// Un-registers the existing logger and registers a new copy.
        /// We will use this when we do multiple builds so that we can safely 
        /// assert on log messages for that particular build.
        /// </summary>
        private void ResetLogger()
        {
            engine.UnregisterAllLoggers();
            myLogger = new MockLogger();
            myProject.ParentEngine.RegisterLogger(myLogger);
        }

        /// <summary>
        /// Verifies that build failed with MSB4116 error
        /// </summary>
        /// <param name="buildResults"></param>
        private void AssertBuildFailedWithConditionWithMetadataError(bool buildResults)
        {
            Assertion.Assert(buildResults != true);
            Assertion.Assert(myLogger.FullLog.Contains("MSB4116"));
        }

        /// <summary>
        /// Builds the specified target and return the outputs
        /// </summary>
        /// <param name="targetToBuild"></param>
        /// <returns></returns>
        private ITaskItem[] BuildAndGatherOutputs(string targetToBuild)
        {
            Hashtable outputs = new Hashtable();
            myProject.Build(new string[] { "BuildMe" }, outputs);
            ITaskItem[] outputItems = (ITaskItem[])outputs["BuildMe"];
            return outputItems;
        }

        /// <summary>
        /// Returns the first target from the project
        /// </summary>
        /// <returns></returns>
        private Target GetTargetFromProject(string targetName)
        {
            Target target = null;
            foreach (Target t in myProject.Targets)
            {
                if (t.Name == targetName)
                {
                    target = t;
                    break;
                }
            }
            return target;
        }

        /// <summary>
        /// Deletes the temp files created
        /// </summary>
        /// <param name="files"></param>
        private void DeleteTempFiles(string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                File.Delete(files[i]);
            }
        }

        /// <summary>
        /// Creates temp files with specified timestamp
        /// </summary>
        /// <param name="number"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns></returns>
        private static string[] GetTempFiles(int number, DateTime lastWriteTime)
        {
            string[] files = new string[number];

            for (int i = 0; i < number; i++)
            {
                files[i] = Path.GetTempFileName();
                File.SetLastWriteTime(files[i], lastWriteTime);
            }
            return files;
        }

        /// <summary>
        /// Set inputs and outputs and verify they are respected
        /// </summary>
        [Test]
        public void SetInputsOutputsIncremental()
        {
            string oldFile = null, newFile = null;
            try
            {
                oldFile = GetTempFiles(1, new DateTime(2005, 1, 1))[0];
                newFile = GetTempFiles(1, new DateTime(2006, 1, 1))[0];

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t` Inputs=`" + newFile + "` Outputs=`" + oldFile + @"`>
                    <Message Text=`building target !!`/>                  
                </Target>
                </Project>
            ", logger);
                p.Build(new string[] { "t" });

                logger.AssertLogContains(new string[] { "building target !!" });
                logger.ClearLog();

                Target t = p.Targets["t"];

                // reverse inputs and outputs
                t.Inputs = (string)oldFile;
                t.Outputs = (string)newFile;

                p.ResetBuildStatus();
                p.Build(new string[] { "t" });

                logger.AssertLogDoesntContain("building target !!");
                

            }
            finally
            {
                DeleteTempFiles(new string[] { oldFile });
                DeleteTempFiles(new string[] { newFile });
            }
        }

        /// <summary>
        /// Get the inputs and outputs when it has not been set
        /// </summary>
        [Test]
        public void GetUnsetTargetInputsAndOutputs()
        {
            string targetOutputsString = String.Empty;
            string targetInputsString = String.Empty;

            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");

            Assertion.AssertEquals(myTarget.Inputs, targetInputsString);
            Assertion.AssertEquals(myTarget.Outputs, targetOutputsString);
        }

        /// <summary>
        /// Set the inputs and outputs as string.Empty. Getting it should return string.empty
        /// </summary>
        [Test]
        public void SetEmptyTargetInputsAndOutputs()
        {
            string targetOutputsString = String.Empty;
            string targetInputsString = String.Empty;

            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");
            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;

            Assertion.AssertEquals(myTarget.Inputs, targetInputsString);
            Assertion.AssertEquals(myTarget.Outputs, targetOutputsString);
        }

        /// <summary>
        /// Set the inputs and outputs as null. Getting it should return string.empty
        /// </summary>
        [Test]
        public void SetNullTargetInputsAndOutputs()
        {
            string targetOutputsString = String.Empty;
            string targetInputsString = String.Empty;

            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");
            myTarget.Inputs = null;
            myTarget.Outputs = null;

            Assertion.AssertEquals(myTarget.Inputs, targetInputsString);
            Assertion.AssertEquals(myTarget.Outputs, targetOutputsString);
        }

        /// <summary>
        /// Gets the inputs and outputs after setting it
        /// </summary>
        [Test]
        public void GetValidTargetInputsAndOutputs()
        {
            string targetOutputsString = "target_output";
            string targetInputsString = "target_input";
            
            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");
            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;

            Assertion.AssertEquals(myTarget.Inputs, targetInputsString);
            Assertion.AssertEquals(myTarget.Outputs, targetOutputsString);
        }

        /// <summary>
        /// Set the inputs and outputs of a target on an existing project
        /// </summary>
        [Test]
        public void SetTargetInputsAndOutputsOnAnAlreadyBuiltTarget()
        {
            string targetInputsString = "%(SomeItem.metadata)";
            string targetOutputsString = @"@(SomeItem->'%(metadata)')";

            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name=""BuildMe"">
                            <Exec Command=""echo @(SomeItem)foo""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);
            myProject.Build("BuildMe");

            Assertion.Assert(myLogger.FullLog.Contains("a;bfoo"));

            ResetLogger();

            Target myTarget = GetTargetFromProject("BuildMe");

            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;

            ITaskItem[] outputItems = BuildAndGatherOutputs("BuildMe");

            
            Assertion.Assert(myLogger.FullLog.Contains("afoo"));
            Assertion.Assert(myLogger.FullLog.Contains("bfoo"));
            Assertion.AssertEquals(outputItems[0].ToString(), "1");
            Assertion.AssertEquals(outputItems[1].ToString(), "2"); 
        }

        /// <summary>
        /// Change the inputs and outputs of a target of an existing project
        /// </summary>
        [Test]
        public void ChangingAnExistingTargetInputsAndOutputs()
        {
            string targetInputsString = "%(SomeItem2.metadata)";
            string targetOutputsString = @"@(SomeItem2->'%(metadata)')";

            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                            <SomeItem2 Include=""a2"">
                                <metadata>1-1</metadata>
                            </SomeItem2>
                            <SomeItem2 Include=""b2"">
                                <metadata>2-1</metadata>
                            </SomeItem2>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Inputs=""%(SomeItem.metadata)"" Outputs=""@(SomeItem->'%(metadata)')"">
                            <Exec Command=""echo @(SomeItem)foo""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);
            ITaskItem[] outputItems = BuildAndGatherOutputs("BuildMe");

            Assertion.Assert(myLogger.FullLog.Contains("afoo"));
            Assertion.Assert(myLogger.FullLog.Contains("bfoo"));
            Assertion.AssertEquals(outputItems[0].ToString(), "1");
            Assertion.AssertEquals(outputItems[1].ToString(), "2");

            ResetLogger();

            Target myTarget = GetTargetFromProject("BuildMe");

            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;

            BuildTask myTask = myTarget.AddNewTask("Exec");
            myTask.SetParameterValue("Command", "echo @(SomeItem2)foo");

            ITaskItem[] outputItems1 = BuildAndGatherOutputs("BuildMe");

            
            Assertion.Assert(myLogger.FullLog.Contains("a2foo"));
            Assertion.Assert(myLogger.FullLog.Contains("b2foo"));
            Assertion.AssertEquals(outputItems1[0].ToString(), "1-1");
            Assertion.AssertEquals(outputItems1[1].ToString(), "2-1");
        }

        /// <summary>
        /// Change the inputs of a target of an existing project
        /// </summary>
        [Test]
        public void ChangingAnExistingTargetInputs()
        {
            string targetInputsString = "%(SomeItem2.metadata)";

            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                            <SomeItem2 Include=""a2"">
                                <metadata>1-1</metadata>
                            </SomeItem2>
                            <SomeItem2 Include=""b2"">
                                <metadata>2-1</metadata>
                            </SomeItem2>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Inputs=""%(SomeItem.metadata)"" Outputs=""@(SomeItem->'%(metadata)')"">
                            <Exec Command=""echo @(SomeItem)foo""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);
            ITaskItem[] outputItems = BuildAndGatherOutputs("BuildMe");

            Assertion.Assert(myLogger.FullLog.Contains("afoo"));
            Assertion.Assert(myLogger.FullLog.Contains("bfoo"));
            Assertion.AssertEquals(outputItems[0].ToString(), "1");
            Assertion.AssertEquals(outputItems[1].ToString(), "2");

            ResetLogger();

            Target myTarget = GetTargetFromProject("BuildMe");

            myTarget.Inputs = targetInputsString;

            BuildTask myTask = myTarget.AddNewTask("Exec");
            myTask.SetParameterValue("Command", "echo @(SomeItem2)foo");

            ITaskItem[] outputItems1 = BuildAndGatherOutputs("BuildMe");

            
            Assertion.Assert(myLogger.FullLog.Contains("a;bfoo"));
            Assertion.Assert(myLogger.FullLog.Contains("a2foo"));
            Assertion.Assert(myLogger.FullLog.Contains("b2foo"));
            Assertion.AssertEquals(outputItems1[0].ToString(), "1");
            Assertion.AssertEquals(outputItems1[1].ToString(), "2");
        }

        /// <summary>
        /// Change the outputs of a target of an existing project
        /// </summary>
        [Test]
        public void ChangingAnExistingTargetOutputs()
        {
            string targetOutputsString = @"@(SomeItem2->'%(metadata)')";

            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                            <SomeItem2 Include=""a2"">
                                <metadata>1-1</metadata>
                            </SomeItem2>
                            <SomeItem2 Include=""b2"">
                                <metadata>2-1</metadata>
                            </SomeItem2>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Inputs=""%(SomeItem.metadata)"" Outputs=""@(SomeItem->'%(metadata)')"">
                            <Exec Command=""echo @(SomeItem)foo""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);
            ITaskItem[] outputItems = BuildAndGatherOutputs("BuildMe");

            Assertion.Assert(myLogger.FullLog.Contains("afoo"));
            Assertion.Assert(myLogger.FullLog.Contains("bfoo"));
            Assertion.AssertEquals(outputItems[0].ToString(), "1");
            Assertion.AssertEquals(outputItems[1].ToString(), "2");

            ResetLogger();

            Target myTarget = GetTargetFromProject("BuildMe");

            myTarget.Outputs = targetOutputsString;

            ITaskItem[] outputItems1 = BuildAndGatherOutputs("BuildMe");

            Assertion.Assert(myLogger.FullLog.Contains("afoo"));
            Assertion.Assert(myLogger.FullLog.Contains("bfoo"));
            Assertion.AssertEquals(outputItems1[0].ToString(), "1-1");
            Assertion.AssertEquals(outputItems1[1].ToString(), "2-1");
        }

        /// <summary>
        /// Get inputs and outputs of a target from an existing project
        /// </summary>
        [Test]
        public void GetInputsAndOutputsFromAnExistingTarget()
        {
            string targetOutputsString = "target_output";
            string targetInputsString = "target_input";

            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <Target Name=""BuildMe"" Inputs=""target_input"" Outputs=""target_output""/>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            Target myTarget = GetTargetFromProject("BuildMe");

            Assertion.AssertEquals("BuildMe", myTarget.Name);
            Assertion.AssertEquals(myTarget.Inputs, targetInputsString);
            Assertion.AssertEquals(myTarget.Outputs, targetOutputsString);
        }

        /// <summary>
        /// Add new target with inputs and outputs to an existing project
        /// </summary>
        [Test]
        public void AddNewTargetWithInputsAndOutputsOnAnExistingProject()
        {
            string targetOutputsString = "target_output";
            string targetInputsString = "target_input";
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <Target Name=""BuildMe""/>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            myProject.DefaultTargets = "BuildMe2";
            Target myTarget = myProject.Targets.AddNewTarget("BuildMe2");
            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;

            myProject.Build("BuildMe2");

            Assertion.AssertEquals(myTarget.Inputs, targetInputsString);
            Assertion.AssertEquals(myTarget.Outputs, targetOutputsString);
            Assertion.Assert(myLogger.FullLog.Contains("BuildMe2"));
        }

        /// <summary>
        /// Add additional attributes to target after setting the inputs and outputs
        /// </summary>
        [Test]
        public void AddAttributeAfterInputsAndOutputs()
        {
            string targetOutputsString = "target_output";
            string targetInputsString = "target_input";

            myProject.DefaultTargets = "BuildMe";
            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");
            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;
            myTarget.Condition = @"'1' == '1'";

            myProject.Build("BuildMe");

            Assertion.AssertEquals(myTarget.Inputs, targetInputsString);
            Assertion.AssertEquals(myTarget.Outputs, targetOutputsString);
            Assertion.Assert(myLogger.FullLog.Contains("BuildMe"));
        }

        /// <summary>
        /// Create a target and add an inputs attribute to the target
        /// </summary>
        [Test]
        public void AddNewTargetWithInputsString()
        {
            string targetInputsString = "%(SomeItem.metadata)";
            string targetOutputsString = "target_output";

            BuildItem item1 = myProject.AddNewItem("SomeItem", "a");
            BuildItem item2 = myProject.AddNewItem("SomeItem", "b");
            item1.SetMetadata("metadata", "1");
            item2.SetMetadata("metadata", "2");

            myProject.DefaultTargets = "BuildMe";

            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");
            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;

            BuildTask myTask = myTarget.AddNewTask("Exec");
            myTask.SetParameterValue("Command", "echo @(SomeItem)foo");

            myProject.Build("BuildMe");

            Assertion.Assert(myLogger.FullLog.Contains("BuildMe"));
            Assertion.Assert(myLogger.FullLog.Contains("Exec"));
            Assertion.Assert(myLogger.FullLog.Contains("afoo"));
            Assertion.Assert(myLogger.FullLog.Contains("bfoo"));
        }

        /// <summary>
        /// Create a target and add an outputs attribute to the target
        /// </summary>
        [Test]
        public void AddNewTargetWithOutputsString()
        {
            string targetOutputsString = @"@(SomeItem->'%(metadata)')";
            string targetInputsString = null;

            BuildItem item1 = myProject.AddNewItem("SomeItem", "a");
            BuildItem item2 = myProject.AddNewItem("SomeItem", "b");
            item1.SetMetadata("metadata", "1");
            item2.SetMetadata("metadata", "2");

            myProject.DefaultTargets = "BuildMe";

            // Add a new target specifying the inputs and outputs attribute
            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");
            myTarget.Inputs = targetInputsString;
            myTarget.Outputs = targetOutputsString;

            BuildTask myTask = myTarget.AddNewTask("Exec");
            myTask.SetParameterValue("Command", "echo @(SomeItem)foo");

            ITaskItem[] outputItems = BuildAndGatherOutputs("BuildMe");

            // Confirm the logger received what it was supposed to.
            Assertion.Assert(myLogger.FullLog.Contains("BuildMe"));
            Assertion.Assert(myLogger.FullLog.Contains("Exec"));
            Assertion.Assert(myLogger.FullLog.Contains("a;bfoo"));
            Assertion.AssertEquals(outputItems[0].ToString(), "1");
            Assertion.AssertEquals(outputItems[1].ToString(), "2"); 
        }

        /// <summary>
        /// Condition is a transform
        /// </summary>
        [Test]
        public void TargetCondtionIsATransform()
        {
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Condition=""'@(SomeItem->'%(metadata)')'=='1'"">
                            <Message Text=""[@(SomeItem)]""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);
            myProject.Build("BuildMe");

            Assertion.Assert(myLogger.FullLog.Contains("[a]"));
        }

        /// <summary>
        /// Condition has a metadata expression
        /// </summary>
        [Test]
        public void TargetCondtionHasAMetadata()
        {
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Condition=""'%(SomeItem.metadata)'=='1'"">
                            <Message Text=""[@(SomeItem)]""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            AssertBuildFailedWithConditionWithMetadataError(myProject.Build("BuildMe"));
        }

        /// <summary>
        /// Condition has a metadata expression
        /// </summary>
        [Test]
        public void TargetCondtionHasAMetadataButInvalidItemType()
        {
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                            <SomeItem1 Include=""test"" />
                        </ItemGroup>
                        <Target Name=""BuildMe"" Condition=""'@(SomeItem1, %(SomeItem.metadata))'=='1'"">
                            <Message Text=""[@(SomeItem)]""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            AssertBuildFailedWithConditionWithMetadataError(myProject.Build("BuildMe"));
        }

        /// <summary>
        /// Condition has a metadata expression
        /// </summary>
        [Test]
        public void TargetCondtionHasAMetadataButInvalidItemType2()
        {
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Condition=""'@(SomeItem, %(SomeItem.metadata))'=='1'"">
                            <Message Text=""[@(SomeItem)]""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            AssertBuildFailedWithConditionWithMetadataError(myProject.Build("BuildMe"));
        }

        /// <summary>
        /// Condition has a metadata expression
        /// </summary>
        [Test]
        public void TargetCondtionDependsOn2ValuesWhereOneIsAMetadata()
        {
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Condition=""'1' == '1' and %(SomeItem.metadata))'=='1'"">
                            <Message Text=""[@(SomeItem)]""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            AssertBuildFailedWithConditionWithMetadataError(myProject.Build("BuildMe"));
        }

        /// <summary>
        /// Condition has a metadata expression
        /// </summary>
        [Test]
        public void TargetCondtionHasRandomCharactersFollowedByMetadata()
        {
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Condition=""aaa%(SomeItem.metadata))'=='1'"">
                            <Message Text=""[@(SomeItem)]""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            AssertBuildFailedWithConditionWithMetadataError(myProject.Build("BuildMe"));
        }

        /// <summary>
        /// Condition has a metadata expression
        /// </summary>
        [Test]
        public void TargetCondtionHasRandomCharactersFollowedByMetadata2()
        {
            string projectContents = @"
                    <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <ItemGroup>
                            <SomeItem Include=""a"">
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include=""b"">
                                <metadata>2</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name=""BuildMe"" Condition=""@(aaa%(SomeItem.metadata)))'=='1'"">
                            <Message Text=""[@(SomeItem)]""/>
                        </Target>
                    </Project>
                ";

            myProject.LoadXml(projectContents);

            AssertBuildFailedWithConditionWithMetadataError(myProject.Build("BuildMe"));
        }

        /// <summary>
        /// Add a new target to a project
        /// </summary>
        [Test]
        public void AddNewTarget()
        {
            // The following code should create a project that effectively looks like this:
            //
            //      <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            //          <Target Name="BuildMe">
            //              <Exec Command="echo foo"/>
            //          </Target>
            //      </Project>
            //
            myProject.DefaultTargets = "BuildMe";
            myProject.SetProperty("MyProp", "goober", null);
            Target myTarget = myProject.Targets.AddNewTarget("BuildMe");
            BuildTask myTask = myTarget.AddNewTask("Exec");
            myTask.SetParameterValue("Command", "echo $(MyProp)foo");

            // Build the project.
            myProject.Build("BuildMe");

            // Confirm the logger received what it was supposed to.
            Assertion.Assert(myLogger.FullLog.Contains("BuildMe"));
            Assertion.Assert(myLogger.FullLog.Contains("Exec"));
            Assertion.Assert(myLogger.FullLog.Contains("gooberfoo"));
        }
    }

    [TestFixture]
    public class RemoveTargetFromCollection_Tests
    {
        [Test]
        public void RemoveTarget()
        {
            string projectOriginalContents = @"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`BuildMe` />
                    </Project>
                ";

            Project myProject = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);

            // Loop through all the targets in the project.
            int i = 0;
            Target myTarget = null;
            foreach (Target target in myProject.Targets)
            {
                myTarget = target;
                i++;
            }

            Assertion.AssertEquals("Expected exactly one target.", 1, i);
            Assertion.AssertEquals("BuildMe", myTarget.Name);

            // Remove the target.
            myProject.Targets.RemoveTarget(myTarget);

            // Build the project.  This should throw an exception, because the target doesn't exist.
            bool success = myProject.Build("BuildMe");

            Assertion.Assert("Project should have failed to build.", !success);
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RemoveTargetNull()
        {
            string projectOriginalContents = @"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`BuildMe` />
                    </Project>
                ";

            Project myProject = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);

            myProject.Targets.RemoveTarget(null);
        }

        // Dirty project.
    }

    [TestFixture]
    public class Target_Tests
    {
        [Test]
        public void TransformInTargetConditionLegal()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`a.ext;b.ext`/>
                  </ItemGroup>
                  <Target Name=`t` Condition=`@(x -> '%(filename)')=='a;b'`>
                    <Message Text=`#@(x)#`/>                  
                </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "#a.ext;b.ext#" });
        }
    }
}
