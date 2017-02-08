// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using System.Threading;
using System.Collections;

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   OnErrorHandling
     * Owner:   jomof
     *
     * Tests that exercise the <OnError> tag.
     */
    [TestFixture]
    sealed public class OnErrorHandling
    {
        /*
         * Method:  Basic
         * Owner:   jomof
         *
         * Construct a simple OnError tag.
         */
        [Test]
        public void Basic()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError ExecuteTargets='CleanUp'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount==1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
        }

        /// <summary>
        /// Target items and properties should be published to the project level even when a task that
        /// outputs them fails. (Of course the task must have populated its property before it errors.)
        /// Then these items and properties should be visible to the onerror targets.
        /// </summary>
        [Test]
        public void FailingTaskStillPublishesOutputs()
        {
            MockLogger l = new MockLogger();

            string resx = Path.Combine(Path.GetTempPath(), "FailingTaskStillPublishesOutputs.resx");

            try
            {
                File.WriteAllText(resx, @"
                    <root>
                      <resheader name=""resmimetype"">
                        <value>text/microsoft-resx</value>
                      </resheader>
                      <resheader name=""version"">
                        <value>2.0</value>
                      </resheader>
                      <resheader name=""reader"">
                        <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                      </resheader>
                      <resheader name=""writer"">
                        <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                      </resheader>
                      <data name=""a"">
                        <value>aa</value>
                      </data>
                      <data name=""b"">
                        <value>bb</value>
                      </data>
                    </root>");

                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                    <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                        <Target Name='Build'>
                            
                            <GenerateResource
                                Sources='" + resx + @"'
                                ExecuteAsTool='false'
                                StronglyTypedLanguage='!@:|'>
                                    <Output TaskParameter='FilesWritten' ItemName='FilesWrittenItem'/>
                                    <Output TaskParameter='FilesWritten' PropertyName='FilesWrittenProperty'/>
                            </GenerateResource>
                                               
                            <OnError ExecuteTargets='ErrorTarget'/>
                        </Target>

                        <Target Name='ErrorTarget'>    
                            <Message Text='[@(fileswrittenitem)]'/>
                            <Message Text='[$(fileswrittenproperty)]'/>
                        </Target>
                    </Project>",
                    l
                );

                p.Build(new string[] { "Build" }, null);

                string resource = Path.ChangeExtension(resx, ".resources");

                Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount >= 1);
                l.AssertLogContains("[" + resource + "]", "[" + resource + "]");

                // And outputs are visible at the project level
                Assertion.AssertEquals(resource, p.GetEvaluatedItemsByName("FilesWrittenItem")[0].FinalItemSpec);
                Assertion.AssertEquals(resource, p.GetEvaluatedProperty("FilesWrittenProperty"));

                p.ResetBuildStatus();

                // But are gone after resetting of course
                Assertion.AssertEquals(0, p.GetEvaluatedItemsByName("FilesWrittenItem").Count);
                Assertion.AssertEquals(null, p.GetEvaluatedProperty("FilesWrittenProperty"));

            }
            finally
            {
                File.Delete(resx);
            }
        }

        /// <summary>
        /// Target items and properties should be published to the project level when an OnError
        /// target runs, and those items and properties should be visible to the OnError targets.
        /// </summary>
        [Test]
        public void OnErrorSeesPropertiesAndItemsFromFirstTarget()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 

                   <Target Name='Build'>
                      <!-- Create a bunch of items and properties -->
                      <CreateItem Include='a1'>
                        <Output ItemName='i1' TaskParameter='Include'/>
                      </CreateItem> 
                      <ItemGroup>
                        <i1 Include='a2'/>
                      </ItemGroup> 
                      <CreateProperty Value='v1'>
                        <Output PropertyName='p1' TaskParameter='Value'/>
                      </CreateProperty>
                      <PropertyGroup>
                        <p2>v2</p2>
                      </PropertyGroup>

                      <Error Text='This is an error.'/>
                      <OnError ExecuteTargets='ErrorTarget'/>
                   </Target>

                   <Target Name='ErrorTarget'>
                      <Message Text='[@(i1)][$(p1)][$(p2)]'/>
                   </Target>

                </Project>",
                l
            );

            p.Build(new string[] { "Build" }, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            l.AssertLogContains("[a1;a2][v1][v2]");
        }

        /*
         * Method:  TwoExecuteTargets
         * Owner:   jomof
         *
         * Make sure two execute targets can be called.
         */
        [Test]
        public void TwoExecuteTargets()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='CleanUp2'>
                      <Message Text='CleanUp2-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError ExecuteTargets='CleanUp;CleanUp2'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount==1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
            Assertion.Assert("The CleanUp2 target should have been called.", (l.FullLog.IndexOf("CleanUp2-was-called") != -1));
        }

        /*
         * Method:  TwoOnErrorClauses
         * Owner:   jomof
         *
         * Make sure two OnError clauses can be used.
         */
        [Test]
        public void TwoOnErrorClauses()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='CleanUp2'>
                      <Message Text='CleanUp2-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError ExecuteTargets='CleanUp'/>
                      <OnError ExecuteTargets='CleanUp2'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount==1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
            Assertion.Assert("The CleanUp2 target should have been called.", (l.FullLog.IndexOf("CleanUp2-was-called") != -1));
        }

        /*
         * Method:  DependentTarget
         * Owner:   jomof
         *
         * Make sure that a target that is a dependent of a target called because of an
         * OnError clause is called
         */
        [Test]
        public void DependentTarget()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp' DependsOnTargets='CleanUp2'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='CleanUp2'>
                      <Message Text='CleanUp2-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError ExecuteTargets='CleanUp'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount==1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
            Assertion.Assert("The CleanUp2 target should have been called.", (l.FullLog.IndexOf("CleanUp2-was-called") != -1));
        }

        /*
         * Method:  ErrorInChildIsHandledInParent
         * Owner:   jomof
         *
         * If a target is dependent on a child target and that child target errors,
         * then the parent's OnError clauses should fire.
         */
        [Test]
        public void ErrorInChildIsHandledInParent()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='BuildStep1'>
                      <Error Text='Error-in-build-step-1.'/>
                   </Target>
                   <Target Name='Build' DependsOnTargets='BuildStep1'>
                      <OnError ExecuteTargets='CleanUp'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'BuildStep1' failed.", l.ErrorCount==1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
            Assertion.Assert("The BuildStep1 target should have been called.", (l.FullLog.IndexOf("Error-in-build-step-1") != -1));
        }


        /*
         * Method:  NonExistentExecuteTarget
         * Owner:   jomof
         *
         * Construct a simple OnError tag.
         */
        [Test]
        public void NonExistentExecuteTarget()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError ExecuteTargets='CleanUp'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected at least one error because 'Build' failed and one error because 'CleanUp' didn't exist.", l.ErrorCount==2);
            Assertion.Assert("The CleanUp target should not have been called.", (l.FullLog.IndexOf("CleanUp-was-called") == -1));

        }

        /*
         * Method:  TrueCondition
         * Owner:   jomof
         *
         * Test the case when the result of the condition is 'true'
         */
        [Test]
        public void TrueCondition()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError Condition=`'A'!='B'` ExecuteTargets='CleanUp'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
        }

        /*
         * Method:  FalseCondition
         * Owner:   jomof
         *
         * Test the case when the result of the condition is 'false'
         */
        [Test]
        public void FalseCondition()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError Condition=`'A'=='B'` ExecuteTargets='CleanUp'/>
                   </Target>
                </Project>",
                l
            );


            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The CleanUp target should not have been called.", (l.FullLog.IndexOf("CleanUp-was-called") == -1));
        }

        /*
         * Method:  PropertiesInExecuteTargets
         * Owner:   jomof
         *
         * Make sure that properties in ExecuteTargets are properly expanded.
         */
        [Test]
        public void PropertiesInExecuteTargets()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <PropertyGroup>
                     <Part1>Clean</Part1>
                     <Part2>Up</Part2>
                   </PropertyGroup>
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError Condition=`'A'!='B'` ExecuteTargets='$(Part1)$(Part2)'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
        }

        /*
         * Method:  ErrorTargetsContinueAfterErrorsInErrorHandler
         * Owner:   jomof
         *
         * If an error occurs in an error handling target, then continue processing
         * remaining error targets
         */
        [Test]
        public void ErrorTargetsContinueAfterErrorsInErrorHandler()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                   <Target Name='CleanUp1'>
                      <Message Text='CleanUp1-was-called.'/>
                      <Error Text='Error in CleanUp1.'/>
                   </Target>
                   <Target Name='CleanUp2'>
                      <Message Text='CleanUp2-was-called.'/>
                      <Error Text='Error in CleanUp2.'/>
                   </Target>
                   <Target Name='CleanUp3'>
                      <Message Text='CleanUp3-was-called.'/>
                      <Error Text='Error in CleanUp3.'/>
                   </Target>
                   <Target Name='CoreBuild'>
                      <Error Text='This is an error.'/>
                      <OnError ExecuteTargets='CleanUp1;CleanUp2'/>
                   </Target>
                   <Target Name='Build' DependsOnTargets='CoreBuild'>
                      <OnError ExecuteTargets='CleanUp3'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Four build errors expect: One from CoreBuild and on each from the error handlers.", l.ErrorCount == 4);
            Assertion.Assert("The CleanUp1 target should have been called.", (l.FullLog.IndexOf("CleanUp1-was-called") != -1));
            Assertion.Assert("The CleanUp2 target should have been called.", (l.FullLog.IndexOf("CleanUp2-was-called") != -1));
            Assertion.Assert("The CleanUp3 target should have been called.", (l.FullLog.IndexOf("CleanUp3-was-called") != -1));
        }

        /*
         * Method:  ExecuteTargetIsMissing
         * Owner:   jomof
         *
         * If an OnError specifies an ExecuteTarget that is missing, that's an error
         */
        [Test]
        public void ExecuteTargetIsMissing()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <OnError Condition=`'A'!='B'` ExecuteTargets='CleanUp'/>
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed and one because 'CleanUp' doesn't exist.", l.ErrorCount == 2);
        }

        /*
         * Method:  CommentsAroundOnError
         * Owner:   jomof
         *
         * Since there is special-case code to ignore comments around OnError blocks,
         * let's test this case.
         */
        [Test]
        public void CommentsAroundOnError()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <Error Text='This is an error.'/>
                      <!-- Comment before OnError -->
                      <OnError Condition=`'A'!='B'` ExecuteTargets='CleanUp'/>
                      <!-- Comment after OnError -->
                   </Target>
                </Project>",
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The CleanUp target should have been called.", (l.FullLog.IndexOf("CleanUp-was-called") != -1));
        }

        /*
         * Method:  CircularDependency
         * Owner:   jomof
         *
         * Detect circular dependencies and break out.
         */
        [Test]
        public void CircularDependency()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <Target Name='Build'>
                      <Error Text='Error in Build-Target'/>
                      <OnError ExecuteTargets='Build'/>
                   </Target>
                </Project>",
                l
            );


            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed and one error because of the circular dependency.", l.ErrorCount == 2);
        }

        /*
         * Method:  OutOfOrderOnError
         * Owner:   jomof
         *
         * OnError clauses must come at the end of a Target, it can't be sprinkled in-between tasks. Catch this case.
         */
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void OutOfOrderOnError()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <Target Name='CleanUp'>
                      <Message Text='CleanUp-was-called.'/>
                   </Target>
                   <Target Name='Build'>
                      <OnError ExecuteTargets='CleanUp'/>
                      <Delete Files='__non__existent_file__'/>
                   </Target>
                </Project>",
                l
            );

            /* No build required */
        }


#region Postbuild
        /*
         * Method:  PostBuildBasic
         * Owner:   jomof
         *
         * Handle the basic post-build case where the user has asked for 'On_Success' and
         * none of the build steps fail.
         */
        [Test]
        public void PostBuildBasic()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject
            (
                PostBuildBuilder("On_Success", FailAt.Nowhere),
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected no error because 'Build' succeeded.", l.ErrorCount == 0);
            Assertion.Assert("The ResGen target should have been called.", (l.FullLog.IndexOf("ResGen-was-called") != -1));
            Assertion.Assert("The Compile target should have been called.", (l.FullLog.IndexOf("Compile-was-called") != -1));
            Assertion.Assert("The GenerateSatellites target should have been called.", (l.FullLog.IndexOf("GenerateSatellites-was-called") != -1));
            Assertion.Assert("The PostBuild target should have been called.", (l.FullLog.IndexOf("PostBuild-was-called") != -1));
        }

        /*
         * Method:  PostBuildOnSuccessWhereCompileFailed
         * Owner:   jomof
         *
         * User asked for 'On_Success' but the compile step failed. We don't expect post-build
         * to be called.
         */
        [Test]
        public void PostBuildOnSuccessWhereCompileFailed()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject
            (
                PostBuildBuilder("On_Success", FailAt.Compile),
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The ResGen target should have been called.", (l.FullLog.IndexOf("ResGen-was-called") != -1));
            Assertion.Assert("The Compile target should have been called.", (l.FullLog.IndexOf("Compile-was-called") != -1));
            Assertion.Assert("The Compile target should have failed.", (l.FullLog.IndexOf("Compile-step-failed") != -1));
            Assertion.Assert("The GenerateSatellites target should not have been called.", (l.FullLog.IndexOf("GenerateSatellites-was-called") == -1));
            Assertion.Assert("The PostBuild target should not have been called.", (l.FullLog.IndexOf("PostBuild-was-called") == -1));
        }

        /*
         * Method:  PostBuildOnSuccessWhereGenerateSatellitesFailed
         * Owner:   jomof
         *
         * User asked for 'On_Success' but the PostBuildOnSuccessWhereGenerateSatellitesFailed step
         * failed. We don't expect post-build to be called.
         */
        [Test]
        public void PostBuildOnSuccessWhereGenerateSatellitesFailed()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject
            (
                PostBuildBuilder("On_Success", FailAt.GenerateSatellites),
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The ResGen target should have been called.", (l.FullLog.IndexOf("ResGen-was-called") != -1));
            Assertion.Assert("The Compile target should have been called.", (l.FullLog.IndexOf("Compile-was-called") != -1));
            Assertion.Assert("The GenerateSatellites target should have been called.", (l.FullLog.IndexOf("GenerateSatellites-was-called") != -1));
            Assertion.Assert("The GenerateSatellites target should have failed.", (l.FullLog.IndexOf("GenerateSatellites-step-failed") != -1));
            Assertion.Assert("The PostBuild target should not have been called.", (l.FullLog.IndexOf("PostBuild-was-called") == -1));
        }

        /*
         * Method:  PostBuildAlwaysWhereCompileFailed
         * Owner:   jomof
         *
         * User asked for 'Always' but the compile step failed. We expect the post-build
         * to be called.
         */
        [Test]
        public void PostBuildAlwaysWhereCompileFailed()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject
            (
                PostBuildBuilder("Always", FailAt.Compile),
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The ResGen target should have been called.", (l.FullLog.IndexOf("ResGen-was-called") != -1));
            Assertion.Assert("The Compile target should have been called.", (l.FullLog.IndexOf("Compile-was-called") != -1));
            Assertion.Assert("The Compile target should have failed.", (l.FullLog.IndexOf("Compile-step-failed") != -1));
            Assertion.Assert("The GenerateSatellites target should not have been called.", (l.FullLog.IndexOf("GenerateSatellites-was-called") == -1));
            Assertion.Assert("The PostBuild target should have been called.", (l.FullLog.IndexOf("PostBuild-was-called") != -1));
        }

        /*
         * Method:  PostBuildFinalOutputChangedWhereCompileFailed
         * Owner:   jomof
         *
         * User asked for 'Final_Output_Changed' but the Compile step failed.
         * We expect post-build to be called.
         */
        [Test]
        public void PostBuildFinalOutputChangedWhereCompileFailed()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject
            (
                PostBuildBuilder("Final_Output_Changed", FailAt.Compile),
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The ResGen target should have been called.", (l.FullLog.IndexOf("ResGen-was-called") != -1));
            Assertion.Assert("The Compile target should have been called.", (l.FullLog.IndexOf("Compile-was-called") != -1));
            Assertion.Assert("The Compile target should have failed.", (l.FullLog.IndexOf("Compile-step-failed") != -1));
            Assertion.Assert("The GenerateSatellites target should not have been called.", (l.FullLog.IndexOf("GenerateSatellites-was-called") == -1));
            Assertion.Assert("The PostBuild target should not have been called.", (l.FullLog.IndexOf("PostBuild-was-called") == -1));
        }

        /*
         * Method:  PostBuildFinalOutputChangedWhereGenerateSatellitesFailed
         * Owner:   jomof
         *
         * User asked for 'Final_Output_Changed' but the GenerateSatellites step failed.
         * We expect post-build to be called because Compile succeeded (and wrote to the output).
         */
        [Test]
        public void PostBuildFinalOutputChangedWhereGenerateSatellitesFailed()
        {
            MockLogger l = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject
            (
                PostBuildBuilder("Final_Output_Changed", FailAt.GenerateSatellites),
                l
            );

            p.Build(new string [] {"Build"}, null);

            Assertion.Assert("Expected one error because 'Build' failed.", l.ErrorCount == 1);
            Assertion.Assert("The ResGen target should have been called.", (l.FullLog.IndexOf("ResGen-was-called") != -1));
            Assertion.Assert("The Compile target should have been called.", (l.FullLog.IndexOf("Compile-was-called") != -1));
            Assertion.Assert("The GenerateSatellites target should have been called.", (l.FullLog.IndexOf("GenerateSatellites-was-called") != -1));
            Assertion.Assert("The GenerateSatellites target should have failed.", (l.FullLog.IndexOf("GenerateSatellites-step-failed") != -1));
            Assertion.Assert("The PostBuild target should have been called.", (l.FullLog.IndexOf("PostBuild-was-called") != -1));
        }


        /*
         * The different places that PostBuildBuilder might be instructed to fail at
         */
        private enum FailAt
        {
            Compile,
            GenerateSatellites,
            Nowhere
        }

        /*
         * Method:  PostBuildBuilder
         * Owner:   jomof
         *
         * Build a project file that mimics the fairly complex way we plan to use OnError
         * to handle all the different combinations of project failures and post-build
         * conditions.
         *
         */
        private static string PostBuildBuilder
        (
            string controlFlag,  // On_Success, Always, Final_Output_Changed
            FailAt failAt
        )
        {
            string compileStep = "";
            if (FailAt.Compile == failAt)
            {
                compileStep = "<Error Text='Compile-step-failed'/>";
            }

            string generateSatellites = "";
            if (FailAt.GenerateSatellites == failAt)
            {
                generateSatellites = "<Error Text='GenerateSatellites-step-failed'/>";
            }

            return String.Format(@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <PropertyGroup>
                     <Flag>{0}</Flag>
                   </PropertyGroup>
                   <Target Name='ResGen'>
                      <Message Text='ResGen-was-called.'/>
                   </Target>
                   <Target Name='Compile'>
                      <Message Text='Compile-was-called.'/>
                      <RemoveDir Directories='c:\__Hopefully_Nonexistent_Directory__'/>
                      {1}
                      <CreateItem Include='Yes'>
                         <Output TaskParameter='Include' ItemName='FinalOutputChanged'/>
                      </CreateItem>
                   </Target>
                   <Target Name='GenerateSatellites'>
                      <Message Text='GenerateSatellites-was-called.'/>
                      {2}
                   </Target>
                   <Target Name='PostBuild'>
                      <Message Text='PostBuild-was-called.'/>
                   </Target>
                   <Target Name='PostBuildOnSuccess' Condition=`'$(Flag)'!='Final_Output_Changed'` DependsOnTargets='PostBuild'>
                      <Message Text='PostBuildOnSuccess-was-called.'/>
                   </Target>
                   <Target Name='PostBuildOnOutputChange' Condition=`'$(Flag)_@(FinalOutputChanged)'=='Final_Output_Changed_Yes'` DependsOnTargets='PostBuild'>
                      <Message Text='PostBuildOnOutputChange-was-called.'/>
                   </Target>
                   <Target Name='CoreBuildSucceeded' DependsOnTargets='PostBuildOnSuccess;PostBuildOnOutputChange'>
                      <Message Text='CoreBuildSucceeded-was-called.'/>
                   </Target>
                   <Target Name='CoreBuild' DependsOnTargets='ResGen;Compile;GenerateSatellites'>
                      <Message Text='CoreBuild-was-called.'/>
                      <OnError Condition=`'$(Flag)'=='Always'` ExecuteTargets=`PostBuild`/>
                      <OnError ExecuteTargets=`PostBuildOnOutputChange`/>
                   </Target>
                   <Target Name='Build' DependsOnTargets='CoreBuild;CoreBuildSucceeded'>
                      <Message Text='Build-target-was-called.'/>
                   </Target>
                </Project>
                ", controlFlag, compileStep, generateSatellites);
        }
#endregion
    }
}
