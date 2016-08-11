// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Globalization;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Threading;

namespace Microsoft.Build.UnitTests.TargetDependencyAnalyzer_Tests
{
    [TestFixture]
    public class TargetDependencyAnalyzer_Tests
    {
        /// <summary>
        /// Regression test for bug VSWhidbey 523719.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void EmptyItemSpecInTargetInputs()
        {
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"

	            <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
	                <ItemGroup>
	                    <MyFile Include=`a.cs; b.cs; c.cs`/>
	                </ItemGroup>
	                <Target Name=`Build`
	                        Inputs=`@(MyFile->'%(NonExistentMetadata)')`
	                        Outputs=`foo.exe`>
	                        <Message Text=`Running Build target` Importance=`High`/>
	                </Target>
	            </Project>

                ");

            // It should have actually skipped the "Build" target since there were no inputs.
            ml.AssertLogDoesntContain("Running Build target");
        }

        /// <summary>
        /// Verify missing output metadata gives an error, bug100245
        /// </summary>
        [Test]
        public void EmptyItemSpecInTargetOutputs()
        {
           MockLogger ml = ObjectModelHelpers.BuildProjectExpectFailure(@"

            <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
      	        <Target Name=`generatemsbtasks`
		            Inputs=`@(TASKXML)`
		            Outputs=`@(TASKXML->'%(OutputFile)');@(TASKXML->'%(PasFile)');`>
       	           <Message Text=`Running Build target` Importance=`High`/>
	        </Target>
	        <ItemGroup>
		       <TASKXML Include=`bcc32task.xml`>
		           <OutputFile>bcc32task.cs</OutputFile>
		           <PasFile>bcc32task.pas</PasFile>
		       </TASKXML>
      		       <TASKXML Include=`ccc32task.xml`>
                           <OutputFile>cpp32task.cs</OutputFile>
		       </TASKXML>
	         </ItemGroup>
          </Project>");

            // It should have actually skipped the "Build" target since some output metadata was missing
            ml.AssertLogDoesntContain("Running Build target");
            ml.AssertLogContains("MSB4168");

            // Clear the mock logger object out so it is not reused            
            ml = null;

            ml = ObjectModelHelpers.BuildProjectExpectFailure(@"

            <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
      	        <Target Name=`generatemsbtasks`
		            Inputs=`@(TASKXML)`
		            Outputs=`@(TASKXML->'%(OutputFile)');@(TASKXML->'%(PasFile)');`>
                    <Message Text=`Running Build target` Importance=`High`/>
	            </Target>
	            <ItemGroup>
		           <TASKXML Include=`bcc32task.xml`>
			           <OutputFile>bcc32task.cs</OutputFile>
			           <PasFile>bcc32task.pas</PasFile>
		           </TASKXML>
      		      <TASKXML Include=`ccc32task.xml`>
                      <OutputFile>cpp32task.cs</OutputFile>
			          <!-- Note PasFile not defined for this item! -->
			           <PasFile></PasFile>
		          </TASKXML>
	         </ItemGroup>
          </Project>
                ");

            // It should have actually skipped the "Build" target since some output metadata was missing
            ml.AssertLogDoesntContain("Running Build target");
            ml.AssertLogContains("MSB4168");
        }


        /// <summary>
        /// Tests this case:
        /// 
        /// <Target Name="x"
        ///         Inputs="@(Items);c.cs"
        ///         Outputs="@(Items->'%(Filename).dll')" />
        /// 
        /// If Items = [a.cs;b.cs], and only b.cs is out of date w/r/t its
        /// correlated output b.dll, then we should only build "b" incrementally.
        /// </summary>
        [Test]
        public void MetaInputAndInputItemThatCorrelatesWithOutputItem()
        {
            string inputs = "@(Items);c.cs";
            string outputs = "@(Items->'%(Filename).dll')";
            FileWriteInfo[] filesToAnalyze = new FileWriteInfo[] 
                                             { 
                                                 new FileWriteInfo("a.cs", yesterday),
                                                 new FileWriteInfo("a.dll", today),
                                                 new FileWriteInfo("b.cs", today),
                                                 new FileWriteInfo("b.dll", yesterday),
                                                 new FileWriteInfo("c.cs", twoDaysAgo)
                                             };

            BuildItemGroup items = new BuildItemGroup();
            items.AddNewItem("Items", "a.cs");
            items.AddNewItem("Items", "b.cs");

            Hashtable itemsByName = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemsByName.Add("Items", items);

            DependencyAnalysisResult result = PerformDependencyAnalysisTestHelper(filesToAnalyze, itemsByName, inputs, outputs);

            Assertion.AssertEquals("Should only build partially.", DependencyAnalysisResult.IncrementalBuild, result);
        }

        /// <summary>
        /// Tests this case:
        /// 
        /// <Target Name="x"
        ///         Inputs="@(Items)"
        ///         Outputs="@(Items->'%(Filename).dll');@(Items->'%(Filename).xml')" />
        /// 
        /// If Items = [a.cs;b.cs;c.cs], and only b.cs is out of date w/r/t its
        /// correlated outputs (dll or xml), then we should only build "b" incrementally.
        /// </summary>
        [Test]
        public void InputItemThatCorrelatesWithMultipleTransformOutputItems()
        {
            string inputs = "@(Items)";
            string outputs = "@(Items->'%(Filename).dll');@(Items->'%(Filename).xml')";

            FileWriteInfo[] filesToAnalyze = new FileWriteInfo[] 
                                             { 
                                                 new FileWriteInfo("a.cs", yesterday),
                                                 new FileWriteInfo("a.dll", today),
                                                 new FileWriteInfo("a.xml", today),
                                                 new FileWriteInfo("b.cs", yesterday),
                                                 new FileWriteInfo("b.dll", twoDaysAgo),
                                                 new FileWriteInfo("b.xml", today),
                                                 new FileWriteInfo("c.cs", yesterday),
                                                 new FileWriteInfo("c.dll", today),
                                                 new FileWriteInfo("c.xml", today)
                                             };

            BuildItemGroup items = new BuildItemGroup();
            items.AddNewItem("Items", "a.cs");
            items.AddNewItem("Items", "b.cs");
            items.AddNewItem("Items", "c.cs");
            
            Hashtable itemsByName = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemsByName.Add("Items", items);

            DependencyAnalysisResult result = PerformDependencyAnalysisTestHelper(filesToAnalyze, itemsByName, inputs, outputs);

            Assertion.AssertEquals("Should only build partially.", DependencyAnalysisResult.IncrementalBuild, result);
        }

        private readonly DateTime today = DateTime.Today;
        private readonly DateTime yesterday = DateTime.Today.AddTicks(-TimeSpan.TicksPerDay);
        private readonly DateTime twoDaysAgo = DateTime.Today.AddTicks(-2*TimeSpan.TicksPerDay);

        private class FileWriteInfo
        {
            public string Path;
            public DateTime LastWriteTime;

            private FileWriteInfo() { }

            public FileWriteInfo(string path, DateTime lastWriteTime)
            {
                this.Path = path;
                this.LastWriteTime = lastWriteTime;
            }
        }

        /// <summary>
        /// Helper method for tests of PerformDependencyAnalysis.
        /// The setup required here suggests that the TargetDependencyAnalyzer
        /// class should be refactored.
        /// </summary>
        private DependencyAnalysisResult PerformDependencyAnalysisTestHelper
        (
            FileWriteInfo[] filesToAnalyze,
            Hashtable itemsByName,
            string inputs,
            string outputs
        )
        {
            Hashtable h1 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Hashtable h2 = new Hashtable(StringComparer.OrdinalIgnoreCase);
            return PerformDependencyAnalysisTestHelper(filesToAnalyze, itemsByName, inputs, outputs, out h1, out h2);
        }

        private DependencyAnalysisResult PerformDependencyAnalysisTestHelper
        (
            FileWriteInfo [] filesToAnalyze,
            Hashtable itemsByName,
            string inputs,
            string outputs,
            out Hashtable changedTargetInputs,
            out Hashtable upToDateTargetInputs
        )
        {
            List<string> filesToDelete = new List<string>();

            try
            {
                // first set the disk up
                for (int i = 0; i < filesToAnalyze.Length; ++i)
                {
                    string path = ObjectModelHelpers.CreateFileInTempProjectDirectory(filesToAnalyze[i].Path, "");
                    File.SetLastWriteTime(path, filesToAnalyze[i].LastWriteTime);
                    filesToDelete.Add(path);
                }

                // now create the project
                string unformattedProjectXml =
                    @"
   	                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
	                        <Target Name=`Build`
	                                Inputs=`{0}`
	                                Outputs=`{1}`>
	                        </Target>
	                    </Project>
                    ";
                Project p = ObjectModelHelpers.CreateInMemoryProject(String.Format(unformattedProjectXml, inputs, outputs));

                // now do the dependency analysis
                ItemBucket itemBucket = new ItemBucket(null, null, LookupHelpers.CreateLookup(itemsByName), 0);
                TargetDependencyAnalyzer analyzer = new TargetDependencyAnalyzer(ObjectModelHelpers.TempProjectDir, p.Targets["Build"], p.ParentEngine.LoggingServices, (BuildEventContext)null);
                
                return analyzer.PerformDependencyAnalysis(itemBucket, out changedTargetInputs, out upToDateTargetInputs);
            }
            finally
            {
                // finally clean up
                foreach (string path in filesToDelete)
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Test comparison of inputs/outputs: up to date
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate1()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                new DateTime(2000, 1, 1), /* input2 */ 
                new DateTime(2001, 1, 1), /* output1 */
                new DateTime(2001, 1, 1), /* output2 */
                false /* none out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: first input out of date wrt second output
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate2()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2002, 1, 1), /* input1 */
                new DateTime(2000, 1, 1), /* input2 */
                new DateTime(2003, 1, 1), /* output1 */
                new DateTime(2001, 1, 1), /* output2 */
                true /* some out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: second input out of date wrt first output
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate3()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                new DateTime(2002, 1, 1), /* input2 */
                new DateTime(2001, 1, 1), /* output1 */
                new DateTime(2003, 1, 1), /* output2 */
                true /* some out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: inputs and outputs have same dates
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate4()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                new DateTime(2000, 1, 1), /* input2 */
                new DateTime(2000, 1, 1), /* output1 */
                new DateTime(2000, 1, 1), /* output2 */
                false /* none out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: first input missing
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate5()
        {
            IsAnyOutOfDateTestHelper
                (
                null, /* input1 */
                new DateTime(2000, 1, 1), /* input2 */
                new DateTime(2002, 1, 1), /* output1 */
                new DateTime(2002, 1, 1), /* output2 */
                true /* some out of date */
                );
        }


        /// <summary>
        /// Test comparison of inputs/outputs: second input missing
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate6()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                null, /* input2 */
                new DateTime(2002, 1, 1), /* output1 */
                new DateTime(2002, 1, 1), /* output2 */
                true /* some out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: second output missing
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate7()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                new DateTime(2000, 1, 1), /* input2 */
                new DateTime(2002, 1, 1), /* output1 */
                null, /* output2 */
                true /* some out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: first output missing
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate8()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                new DateTime(2000, 1, 1), /* input2 */
                null, /* output1 */
                new DateTime(2002, 1, 1), /* output2 */
                true /* some out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: first input and first output missing
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate9()
        {
            IsAnyOutOfDateTestHelper
                (
                null, /* input1 */
                new DateTime(2000, 1, 1), /* input2 */
                null, /* output1 */
                new DateTime(2002, 1, 1), /* output2 */
                true /* some out of date */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: one input, two outputs, input out of date
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate10()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2002, 1, 1), /* input1 */
                null, /* input2 */
                new DateTime(2000, 1, 1), /* output1 */
                new DateTime(2002, 1, 1), /* output2 */
                true, /* some out of date */
                true, /* include input1 */
                false, /* do not include input2 */
                true, /* include output1 */
                true /* include output2 */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: one input, two outputs, input up to date
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate11()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                null, /* input2 */
                new DateTime(2002, 1, 1), /* output1 */
                new DateTime(2002, 1, 1), /* output2 */
                false, /* none out of date */
                true, /* include input1 */
                false, /* do not include input2 */
                true, /* include output1 */
                true /* include output2 */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: two inputs, one output, inputs up to date
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate12()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                new DateTime(2000, 1, 1), /* input2 */
                new DateTime(2002, 1, 1), /* output1 */
                null, /* output2 */
                false, /* none out of date */
                true, /* include input1 */
                true, /* include input2 */
                true, /* include output1 */
                false /* do not include output2 */
                );
        }

        /// <summary>
        /// Test comparison of inputs/outputs: two inputs, one output, second input out of date
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestIsAnyOutOfDate13()
        {
            IsAnyOutOfDateTestHelper
                (
                new DateTime(2000, 1, 1), /* input1 */
                new DateTime(2003, 1, 1), /* input2 */
                new DateTime(2002, 1, 1), /* output1 */
                null, /* output2 */
                true, /* some out of date */
                true, /* include input1 */
                true, /* include input2 */
                true, /* include output1 */
                false /* do not include output2 */
                );
        }

        /// <summary>
        /// Helper method for tests of IsAnyOutOfDate.
        /// The setup required here suggests that the TargetDependencyAnalyzer
        /// class should be refactored.
        /// </summary>
        /// <param name="input1Time"></param>
        /// <param name="input2Time"></param>
        /// <param name="output1Time"></param>
        /// <param name="output2Time"></param>
        /// <param name="isUpToDate"></param>
        private void IsAnyOutOfDateTestHelper
            (
            DateTime? input1Time,
            DateTime? input2Time,
            DateTime? output1Time,
            DateTime? output2Time,
            bool isUpToDate
            )
        {
            IsAnyOutOfDateTestHelper(input1Time, input2Time, output1Time, output2Time, isUpToDate, true, true, true, true);
        }

        /// <summary>
        /// Helper method for tests of IsAnyOutOfDate.
        /// The setup required here suggests that the TargetDependencyAnalyzer
        /// class should be refactored.
        /// </summary>
        /// <param name="input1Time"></param>
        /// <param name="input2Time"></param>
        /// <param name="output1Time"></param>
        /// <param name="output2Time"></param>
        /// <param name="isUpToDate"></param>
        private void IsAnyOutOfDateTestHelper
            (
            DateTime? input1Time, 
            DateTime? input2Time,
            DateTime? output1Time,
            DateTime? output2Time,
            bool expectedAnyOutOfDate,
            bool includeInput1,
            bool includeInput2,
            bool includeOutput1,
            bool includeOutput2
            )
        {
            ArrayList inputs = new ArrayList();
            ArrayList outputs = new ArrayList();

            string input1 = "NONEXISTENT_FILE";
            string input2 = "NONEXISTENT_FILE";
            string output1 = "NONEXISTENT_FILE";
            string output2 = "NONEXISTENT_FILE";

            try
            {
                if (input1Time != null)
                {
                    input1 = Path.GetTempFileName();
                    File.WriteAllText(input1, String.Empty);
                    File.SetLastWriteTime(input1, (DateTime)input1Time);
                }

                if (input2Time != null)
                {
                    input2 = Path.GetTempFileName();
                    File.WriteAllText(input2, String.Empty);
                    File.SetLastWriteTime(input2, (DateTime)input2Time);
                }

                if (output1Time != null)
                {
                    output1 = Path.GetTempFileName();
                    File.WriteAllText(output1, String.Empty);
                    File.SetLastWriteTime(output1, (DateTime)output1Time);
                }

                if (output2Time != null)
                {
                    output2 = Path.GetTempFileName();
                    File.WriteAllText(output2, String.Empty);
                    File.SetLastWriteTime(output2, (DateTime)output2Time);
                }

                if (includeInput1) inputs.Add(input1);
                if (includeInput2) inputs.Add(input2);
                if (includeOutput1) outputs.Add(output1);
                if (includeOutput2) outputs.Add(output2);

                DependencyAnalysisLogDetail detail;
                Assertion.AssertEquals(expectedAnyOutOfDate, TargetDependencyAnalyzer.IsAnyOutOfDate(out detail, Directory.GetCurrentDirectory(), inputs, outputs));
            }
            finally
            {
                if (File.Exists(input1)) File.Delete(input1);
                if (File.Exists(input2)) File.Delete(input2);
                if (File.Exists(output1)) File.Delete(output1);
                if (File.Exists(output2)) File.Delete(output2);
            }
        }
    }
}

