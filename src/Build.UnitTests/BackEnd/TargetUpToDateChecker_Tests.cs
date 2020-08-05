// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Win32.SafeHandles;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class TargetUpToDateChecker_Tests : IDisposable
    {
        private MockHost _mockHost;
        private readonly ITestOutputHelper _testOutputHelper;

        public TargetUpToDateChecker_Tests(ITestOutputHelper testOutputHelper)
        {
            _mockHost = new MockHost();
            _testOutputHelper = testOutputHelper;
        }

        public void Dispose()
        {
            // Remove any temp files that have been created by each test
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        [Fact]
        public void EmptyItemSpecInTargetInputs()
        {
            MockLogger ml = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
	                <ItemGroup>
	                    <MyFile Include='a.cs; b.cs; c.cs'/>
	                </ItemGroup>
	                <Target Name='Build'
	                        Inputs=""@(MyFile->'%(NonExistentMetadata)')""
	                        Outputs='foo.exe'>
	                        <Message Text='Running Build target' Importance='High'/>
	                </Target>
	            </Project>"))));

            bool success = p.Build(new string[] { "Build" }, new ILogger[] { ml });

            Assert.True(success);

            // It should have actually skipped the "Build" target since there were no inputs.
            ml.AssertLogDoesntContain("Running Build target");
        }

        /// <summary>
        /// Verify missing output metadata does not cause errors.
        /// </summary>
        [Fact]
        public void EmptyItemSpecInTargetOutputs()
        {
            MockLogger ml = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
      	        <Target Name='Build'
		            Inputs='@(TASKXML)'
		            Outputs=""@(TASKXML->'%(OutputFile)');@(TASKXML->'%(PasFile)');"">
       	           <Message Text='Running Build target' Importance='High'/>
	            </Target>
	            <ItemGroup>
		            <TASKXML Include='bcc32task.xml'>
                        <OutputFile>bcc32task.cs</OutputFile>
		            </TASKXML>
      		        <TASKXML Include='ccc32task.xml'>
                        <PasFile>ccc32task.pas</PasFile>
		            </TASKXML>
	            </ItemGroup>
              </Project>"))));

            bool success = p.Build("Build", new ILogger[] { ml });

            Assert.True(success);

            // It should not have skipped the "Build" target since some output metadata was missing
            ml.AssertLogContains("Running Build target");

            ml = new MockLogger();
            p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
      	        <Target Name='Build'
		            Inputs='@(TASKXML)'
		            Outputs=""@(TASKXML->'%(OutputFile)');@(TASKXML->'%(PasFile)');"">
       	           <Message Text='Running Build target' Importance='High'/>
	            </Target>
	            <ItemGroup>
		            <TASKXML Include='bcc32task.xml'>
		            </TASKXML>
      		        <TASKXML Include='ccc32task.xml'>
		            </TASKXML>
	            </ItemGroup>
              </Project>"))));

            success = p.Build("Build", new ILogger[] { ml });

            Assert.True(success);

            // It should have actually skipped the "Build" target since some output metadata was missing
            ml.AssertLogDoesntContain("Running Build target");
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
        [Fact]
        public void MetaInputAndInputItemThatCorrelatesWithOutputItem()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            string inputs = "@(Items);c.cs";
            string outputs = "@(Items->'%(Filename).dll')";
            FileWriteInfo[] filesToAnalyze = new FileWriteInfo[]
                                             {
                                                 new FileWriteInfo("a.cs", _yesterday),
                                                 new FileWriteInfo("a.dll", _today),
                                                 new FileWriteInfo("b.cs", _today),
                                                 new FileWriteInfo("b.dll", _yesterday),
                                                 new FileWriteInfo("c.cs", _twoDaysAgo)
                                             };

            List<ProjectItemInstance> items = new List<ProjectItemInstance>();
            items.Add(new ProjectItemInstance(project, "Items", "a.cs", project.FullPath));
            items.Add(new ProjectItemInstance(project, "Items", "b.cs", project.FullPath));

            ItemDictionary<ProjectItemInstance> itemsByName = new ItemDictionary<ProjectItemInstance>();
            itemsByName.ImportItems(items);

            DependencyAnalysisResult result = PerformDependencyAnalysisTestHelper(filesToAnalyze, itemsByName, inputs, outputs);

            Assert.Equal(DependencyAnalysisResult.IncrementalBuild, result); // "Should only build partially."
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
        [Fact]
        public void InputItemThatCorrelatesWithMultipleTransformOutputItems()
        {
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            string inputs = "@(Items)";
            string outputs = "@(Items->'%(Filename).dll');@(Items->'%(Filename).xml')";

            FileWriteInfo[] filesToAnalyze = new FileWriteInfo[]
                                             {
                                                 new FileWriteInfo("a.cs", _yesterday),
                                                 new FileWriteInfo("a.dll", _today),
                                                 new FileWriteInfo("a.xml", _today),
                                                 new FileWriteInfo("b.cs", _yesterday),
                                                 new FileWriteInfo("b.dll", _twoDaysAgo),
                                                 new FileWriteInfo("b.xml", _today),
                                                 new FileWriteInfo("c.cs", _yesterday),
                                                 new FileWriteInfo("c.dll", _today),
                                                 new FileWriteInfo("c.xml", _today)
                                             };

            List<ProjectItemInstance> items = new List<ProjectItemInstance>();
            items.Add(new ProjectItemInstance(project, "Items", "a.cs", project.FullPath));
            items.Add(new ProjectItemInstance(project, "Items", "b.cs", project.FullPath));
            items.Add(new ProjectItemInstance(project, "Items", "c.cs", project.FullPath));

            ItemDictionary<ProjectItemInstance> itemsByName = new ItemDictionary<ProjectItemInstance>();
            itemsByName.ImportItems(items);

            DependencyAnalysisResult result = PerformDependencyAnalysisTestHelper(filesToAnalyze, itemsByName, inputs, outputs);

            Assert.Equal(DependencyAnalysisResult.IncrementalBuild, result); // "Should only build partially."
        }

        /// <summary>
        /// Tests this case:
        /// 
        /// <Target Name="x"
        ///         Inputs="@(Items);@(MoreItems)"
        ///         Outputs="@(Items->'%(Filename).dll');@(MoreItems->'%(Filename).xml')" />
        /// 
        /// If Items = [a.cs;b.cs;c.cs], and only b.cs is out of date w/r/t its
        /// correlated outputs (dll or xml), then we should only build "b" incrementally.
        /// </summary>
        [Fact]
        public void MultiInputItemsThatCorrelatesWithMultipleTransformOutputItems()
        {
            Console.WriteLine("MultiInputItemsThatCorrelatesWithMultipleTransformOutputItems");
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            string inputs = "@(Items);@(MoreItems)";
            string outputs = "@(Items->'%(Filename).dll');@(MoreItems->'%(Filename).xml')";

            FileWriteInfo[] filesToAnalyze = new FileWriteInfo[]
                                             {
                                                 new FileWriteInfo("a.cs", _yesterday),
                                                 new FileWriteInfo("a.txt", _yesterday),
                                                 new FileWriteInfo("a.dll", _today),
                                                 new FileWriteInfo("a.xml", _today),
                                                 new FileWriteInfo("b.cs", _yesterday),
                                                 new FileWriteInfo("b.txt", _yesterday),
                                                 new FileWriteInfo("b.dll", _twoDaysAgo),
                                                 new FileWriteInfo("b.xml", _today),
                                                 new FileWriteInfo("c.cs", _yesterday),
                                                 new FileWriteInfo("c.txt", _yesterday),
                                                 new FileWriteInfo("c.dll", _today),
                                                 new FileWriteInfo("c.xml", _today)
                                             };

            List<ProjectItemInstance> items = new List<ProjectItemInstance>();
            items.Add(new ProjectItemInstance(project, "Items", "a.cs", project.FullPath));
            items.Add(new ProjectItemInstance(project, "Items", "b.cs", project.FullPath));
            items.Add(new ProjectItemInstance(project, "Items", "c.cs", project.FullPath));
            items.Add(new ProjectItemInstance(project, "MoreItems", "a.txt", project.FullPath));
            items.Add(new ProjectItemInstance(project, "MoreItems", "b.txt", project.FullPath));
            items.Add(new ProjectItemInstance(project, "MoreItems", "c.txt", project.FullPath));

            ItemDictionary<ProjectItemInstance> itemsByName = new ItemDictionary<ProjectItemInstance>();
            itemsByName.ImportItems(items);

            ItemDictionary<ProjectItemInstance> changedTargetInputs = new ItemDictionary<ProjectItemInstance>();
            ItemDictionary<ProjectItemInstance> upToDateTargetInputs = new ItemDictionary<ProjectItemInstance>();
            DependencyAnalysisResult result = PerformDependencyAnalysisTestHelper(filesToAnalyze, itemsByName, inputs, outputs, out changedTargetInputs, out upToDateTargetInputs);

            foreach (ProjectItemInstance itemInstance in changedTargetInputs)
            {
                Console.WriteLine("Changed: {0}:{1}", itemInstance.ItemType, itemInstance.EvaluatedInclude);
            }

            Assert.Equal(DependencyAnalysisResult.IncrementalBuild, result); // "Should only build partially."

            // Even though they were all up to date, we still expect to see an empty marker
            // so that lookups can correctly *not* find items of that type
            Assert.True(changedTargetInputs.HasEmptyMarker("MoreItems"));
        }

        [Fact]
        public void InputItemsTransformedToDifferentNumberOfOutputsFewer()
        {
            Console.WriteLine("InputItemsTransformedToDifferentNumberOfOutputsFewer");
            MockLogger logger = new MockLogger();
            string projectText = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

    <ItemGroup>
        <Foo Include=`foo.txt`><Bar>SomeMetaThing</Bar></Foo>
        <Foo Include=`foo1.txt`><Bar>SomeMetaThing</Bar></Foo>
    </ItemGroup>

    <Target Name=`Build`
            Inputs=`@(Foo)`
            Outputs=`@(Foo->Metadata('Bar')->Distinct())`>

        <Message Text=`%(Foo.Bar)` />
    </Target>
</Project>
            ");
            Project p = new Project(XmlReader.Create(new StringReader(projectText.Replace("`", "\""))));

            Assert.True(p.Build(new string[] { "Build" }, new ILogger[] { logger }));

            logger.AssertLogContains("SomeMetaThing");
        }

        [Fact]
        public void InputItemsTransformedToDifferentNumberOfOutputsFewer1()
        {
            Console.WriteLine("InputItemsTransformedToDifferentNumberOfOutputsFewer1");
            MockLogger logger = new MockLogger();
            string projectText = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

    <ItemGroup>
        <Foo Include=`foo.txt`><Bar>SomeMetaThing</Bar></Foo>
        <Foo Include=`foo1.txt`><Bar>SomeMetaThing</Bar></Foo>
    </ItemGroup>

    <Target Name=`Build`
            Inputs=`@(Foo->Metadata('Bar')->Distinct())`
            Outputs=`@(Foo)`>

        <Message Text=`%(Foo.Bar)` />
    </Target>
</Project>
            ");
            Project p = new Project(XmlReader.Create(new StringReader(projectText.Replace("`", "\""))));

            Assert.True(p.Build(new string[] { "Build" }, new ILogger[] { logger }));

            logger.AssertLogContains("SomeMetaThing");
        }

        [Fact]
        public void InputItemsTransformedToDifferentNumberOfOutputsMore()
        {
            Console.WriteLine("InputItemsTransformedToDifferentNumberOfOutputsMore");
            MockLogger logger = new MockLogger();
            string projectText = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

    <ItemGroup>
        <Foo Include=`foo.txt`><Bar>1;2;3;4;5;6;7;8;9</Bar></Foo>
        <Foo Include=`foo1.txt`><Bar>a;b;c;d;e;f;g</Bar></Foo>
    </ItemGroup>

    <Target Name=`Build`
            Inputs=`@(Foo)`
            Outputs=`@(Foo->Metadata('Bar')->Distinct())`>

        <Message Text=`%(Foo.Bar)` />
    </Target>
</Project>
            ");
            Project p = new Project(XmlReader.Create(new StringReader(projectText.Replace("`", "\""))));

            Assert.True(p.Build(new string[] { "Build" }, new ILogger[] { logger }));

            logger.AssertLogContains("1;2;3;4;5;6;7;8;9");
            logger.AssertLogContains("a;b;c;d;e;f;g");
        }

        [Fact]
        public void InputItemsTransformedToDifferentNumberOfOutputsMore1()
        {
            Console.WriteLine("InputItemsTransformedToDifferentNumberOfOutputsMore1");
            MockLogger logger = new MockLogger();
            string projectText = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

    <ItemGroup>
        <Foo Include=`foo.txt`><Bar>1;2;3;4;5;6;7;8;9</Bar></Foo>
        <Foo Include=`foo1.txt`><Bar>a;b;c;d;e;f;g</Bar></Foo>
    </ItemGroup>

    <Target Name=`Build`
            Inputs=`@(Foo->Metadata('Bar')->Distinct())`
            Outputs=`@(Foo)`>

        <Message Text=`%(Foo.Bar)` />
    </Target>
</Project>
            ");
            Project p = new Project(XmlReader.Create(new StringReader(projectText.Replace("`", "\""))));

            Assert.True(p.Build(new string[] { "Build" }, new ILogger[] { logger }));

            logger.AssertLogContains("1;2;3;4;5;6;7;8;9");
            logger.AssertLogContains("a;b;c;d;e;f;g");
        }

        [Fact]
        public void InputItemsTransformedToDifferentNumberOfOutputsTwoWays()
        {
            Console.WriteLine("InputItemsTransformedToDifferentNumberOfOutputsTwoWays");
            MockLogger logger = new MockLogger();
            File.WriteAllText("foo1.txt", "");
            File.WriteAllText("foo.txt", "");
            Thread.Sleep(100);
            File.WriteAllText("1111", "");
            File.WriteAllText("a", "");
            string projectText = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

    <ItemGroup>
        <Foo Include=`foo.txt`><Bar>1111</Bar></Foo>
        <Foo Include=`foo1.txt`><Bar>a</Bar></Foo>
    </ItemGroup>

    <Target Name=`Build`
            Inputs=`@(Foo)`
            Outputs=`@(Foo->Metadata('Bar'));@(Foo->'%(Filename).goo')`>

        <Message Text=`%(Foo.Bar)` />
    </Target>
</Project>
            ");
            Project p = new Project(XmlReader.Create(new StringReader(projectText.Replace("`", "\""))));

            Assert.True(p.Build(new string[] { "Build" }, new ILogger[] { logger }));

            logger.AssertLogContains("foo.goo");
            logger.AssertLogContains("foo1.goo");

            File.Delete("foo1.txt");
            File.Delete("foo.txt");
            File.Delete("a");
            File.Delete("1111");
        }

        /// <summary>
        /// Ensure that items not involved in the incremental build are explicitly empty
        /// </summary>
        [Fact]
        public void MultiInputItemsThatCorrelatesWithMultipleTransformOutputItems2()
        {
            Console.WriteLine("MultiInputItemsThatCorrelatesWithMultipleTransformOutputItems2");
            string currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(ObjectModelHelpers.TempProjectDir);
                MockLogger logger = new MockLogger();
                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
<Project InitialTargets='Setup' xmlns='msbuildnamespace'>

  <ItemGroup>
    <A Include='A' />
    <B Include='B' />
  </ItemGroup>

  <Target Name='Build' DependsOnTargets='GAFT'>
        <Message Text='Build: @(Outs)' />
        <Message Text='Build: GAFTOutsA @(GAFTOutsA)' />
        <Message Text='Build: GAFTOutsB @(GAFTOutsB)' />
  </Target>

  <Target Name='Setup'>
        <WriteLinesToFile 
            File='A'
            Lines='A'
            Overwrite='true'/>
            
        <WriteLinesToFile 
            File='B.out'
            Lines='B.out'
            Overwrite='true'/>

        <Exec Command='sleep.exe 1' />

        <WriteLinesToFile 
            File='B'
            Lines='B'
            Overwrite='true'/>
            
        <WriteLinesToFile 
            File='A.out'
            Lines='A.out'
            Overwrite='true'/>

  </Target>

  <Target Name='GAFT'  Inputs='@(A);@(B)' Outputs=""@(A->'%(Filename).out');@(B->'%(Filename).out')"">
        <CreateItem Include=""@(A->'%(Filename).out')"">
            <Output TaskParameter='Include' ItemName='GAFTOutsA' />
        </CreateItem>
        <Message Text='GAFT A:@(A)' />
        <CreateItem Include=""@(B->'%(Filename).out')"">
            <Output TaskParameter='Include' ItemName='GAFTOutsB' />
        </CreateItem>
        <Message Text='GAFT B:@(B)' />
  </Target>
</Project>
                "))));

                p.Build(new string[] { "Build" }, new ILogger[] { logger });

                // If the log contains B.out twice, then there is leakage from the parent lookup
                logger.AssertLogDoesntContain("B.out;B.out");
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        private readonly DateTime _today = DateTime.Today;
        private readonly DateTime _yesterday = DateTime.Today.AddTicks(-TimeSpan.TicksPerDay);
        private readonly DateTime _twoDaysAgo = DateTime.Today.AddTicks(-2 * TimeSpan.TicksPerDay);

        private class FileWriteInfo
        {
            public string Path;
            public DateTime LastWriteTime;

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
            ItemDictionary<ProjectItemInstance> itemsByName,
            string inputs,
            string outputs
        )
        {
            ItemDictionary<ProjectItemInstance> h1 = new ItemDictionary<ProjectItemInstance>();
            ItemDictionary<ProjectItemInstance> h2 = new ItemDictionary<ProjectItemInstance>();
            return PerformDependencyAnalysisTestHelper(filesToAnalyze, itemsByName, inputs, outputs, out h1, out h2);
        }

        private DependencyAnalysisResult PerformDependencyAnalysisTestHelper
        (
            FileWriteInfo[] filesToAnalyze,
            ItemDictionary<ProjectItemInstance> itemsByName,
            string inputs,
            string outputs,
            out ItemDictionary<ProjectItemInstance> changedTargetInputs,
            out ItemDictionary<ProjectItemInstance> upToDateTargetInputs
        )
        {
            List<string> filesToDelete = new List<string>();

            try
            {
                // first set the disk up
                for (int i = 0; i < filesToAnalyze.Length; ++i)
                {
                    string path = ObjectModelHelpers.CreateFileInTempProjectDirectory(filesToAnalyze[i].Path, "");
                    File.SetCreationTime(path, filesToAnalyze[i].LastWriteTime);
                    File.SetLastWriteTime(path, filesToAnalyze[i].LastWriteTime);
                    filesToDelete.Add(path);
                }

                // Wait
                Thread.Sleep(50);

                // now create the project
                string unformattedProjectXml = ObjectModelHelpers.CleanupFileContents(
                    @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
	                      <Target Name='Build'
	                              Inputs=""{0}""
	                              Outputs=""{1}"">
	                      </Target>
	                  </Project>");

                string projectFile = Path.Combine(ObjectModelHelpers.TempProjectDir, "temp.proj");
                string formattedProjectXml = String.Format(unformattedProjectXml, inputs, outputs);
                File.WriteAllText(projectFile, formattedProjectXml);

                // Wait
                Thread.Sleep(50);

                filesToDelete.Add(projectFile);

                Project project = new Project(projectFile);
                ProjectInstance p = project.CreateProjectInstance();

                // now do the dependency analysis
                ItemBucket itemBucket = new ItemBucket(null, null, new Lookup(itemsByName, new PropertyDictionary<ProjectPropertyInstance>()), 0);
                TargetUpToDateChecker analyzer = new TargetUpToDateChecker(p, p.Targets["Build"], _mockHost, BuildEventContext.Invalid);

                return analyzer.PerformDependencyAnalysis(itemBucket, out changedTargetInputs, out upToDateTargetInputs);
            }
            finally
            {
                // finally clean up
                foreach (string path in filesToDelete)
                {
                    if (File.Exists(path)) File.Delete(path);
                }

                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            }
        }

        /// <summary>
        /// Test comparison of inputs/outputs: up to date
        /// </summary>
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
        [Fact]
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
            List<string> inputs = new List<string>();
            List<string> outputs = new List<string>();

            string input1 = "NONEXISTENT_FILE";
            string input2 = "NONEXISTENT_FILE";
            string output1 = "NONEXISTENT_FILE";
            string output2 = "NONEXISTENT_FILE";

            try
            {
                if (input1Time != null)
                {
                    input1 = FileUtilities.GetTemporaryFile();
                    File.WriteAllText(input1, String.Empty);
                    File.SetLastWriteTime(input1, (DateTime)input1Time);
                }

                if (input2Time != null)
                {
                    input2 = FileUtilities.GetTemporaryFile();
                    File.WriteAllText(input2, String.Empty);
                    File.SetLastWriteTime(input2, (DateTime)input2Time);
                }

                if (output1Time != null)
                {
                    output1 = FileUtilities.GetTemporaryFile();
                    File.WriteAllText(output1, String.Empty);
                    File.SetLastWriteTime(output1, (DateTime)output1Time);
                }

                if (output2Time != null)
                {
                    output2 = FileUtilities.GetTemporaryFile();
                    File.WriteAllText(output2, String.Empty);
                    File.SetLastWriteTime(output2, (DateTime)output2Time);
                }

                if (includeInput1) inputs.Add(input1);
                if (includeInput2) inputs.Add(input2);
                if (includeOutput1) outputs.Add(output1);
                if (includeOutput2) outputs.Add(output2);

                DependencyAnalysisLogDetail detail;
                Assert.Equal(expectedAnyOutOfDate, TargetUpToDateChecker.IsAnyOutOfDate(out detail, Directory.GetCurrentDirectory(), inputs, outputs));
            }
            finally
            {
                if (File.Exists(input1)) File.Delete(input1);
                if (File.Exists(input2)) File.Delete(input2);
                if (File.Exists(output1)) File.Delete(output1);
                if (File.Exists(output2)) File.Delete(output2);
            }
        }

        private static readonly DateTime Old = new DateTime(2000, 1, 1);
        private static readonly DateTime Middle = new DateTime(2001, 1, 1);
        private static readonly DateTime New = new DateTime(2002, 1, 1);

        [Fact(Skip = "Creating a symlink on Windows requires elevation.")]
        public void NewSymlinkOldDestinationIsUpToDate()
        {
            SimpleSymlinkInputCheck(symlinkWriteTime: New,
                targetWriteTime: Old, 
                outputWriteTime: Middle,
                expectedOutOfDate: false);
        }

        [Fact(Skip = "Creating a symlink on Windows requires elevation.")]
        public void OldSymlinkOldDestinationIsUpToDate()
        {
            SimpleSymlinkInputCheck(symlinkWriteTime: Old,
                targetWriteTime: Middle,
                outputWriteTime: New,
                expectedOutOfDate: false);
        }

        [Fact(Skip = "Creating a symlink on Windows requires elevation.")]
        public void OldSymlinkNewDestinationIsNotUpToDate()
        {
            SimpleSymlinkInputCheck(symlinkWriteTime: Old,
                targetWriteTime: New,
                outputWriteTime: Middle,
                expectedOutOfDate: true);
        }

        [Fact(Skip = "Creating a symlink on Windows requires elevation.")]
        public void NewSymlinkNewDestinationIsNotUpToDate()
        {
            SimpleSymlinkInputCheck(symlinkWriteTime: Middle,
                targetWriteTime: Middle,
                outputWriteTime: Old,
                expectedOutOfDate: true);
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, UInt32 dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileTime(SafeFileHandle hFile, ref long creationTime,
            ref long lastAccessTime, ref long lastWriteTime);

        private void SimpleSymlinkInputCheck(DateTime symlinkWriteTime, DateTime targetWriteTime,
            DateTime outputWriteTime, bool expectedOutOfDate)
        {
            var inputs = new List<string>();
            var outputs = new List<string>();

            string inputTarget = "NONEXISTENT_FILE";
            string inputSymlink = "NONEXISTENT_FILE";
            string outputTarget = "NONEXISTENT_FILE";

            try
            {
                inputTarget = FileUtilities.GetTemporaryFile();
                _testOutputHelper.WriteLine($"Created input file {inputTarget}");
                File.SetLastWriteTime(inputTarget, targetWriteTime);

                inputSymlink = FileUtilities.GetTemporaryFile(null, ".linkin", createFile: false);

                if (!CreateSymbolicLink(inputSymlink, inputTarget, 0))
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                // File.SetLastWriteTime on the symlink sets the target write time,
                // so set the symlink's write time the hard way
                using (SafeFileHandle handle =
                    NativeMethodsShared.CreateFile(
                        inputSymlink, NativeMethodsShared.GENERIC_READ | 0x100 /* FILE_WRITE_ATTRIBUTES */,
                        NativeMethodsShared.FILE_SHARE_READ, IntPtr.Zero, NativeMethodsShared.OPEN_EXISTING,
                        NativeMethodsShared.FILE_ATTRIBUTE_NORMAL | NativeMethodsShared.FILE_FLAG_OPEN_REPARSE_POINT,
                        IntPtr.Zero))
                {
                    if (handle.IsInvalid)
                    {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }

                    long symlinkWriteTimeTicks = symlinkWriteTime.ToFileTimeUtc();

                    if (!SetFileTime(handle, ref symlinkWriteTimeTicks, ref symlinkWriteTimeTicks,
                            ref symlinkWriteTimeTicks))
                    {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }
                }

                _testOutputHelper.WriteLine($"Created input link {inputSymlink}");

                outputTarget = FileUtilities.GetTemporaryFile();
                _testOutputHelper.WriteLine($"Created output file {outputTarget}");
                File.SetLastWriteTime(outputTarget, outputWriteTime);

                inputs.Add(inputSymlink);
                outputs.Add(outputTarget);


                DependencyAnalysisLogDetail detail;
                Assert.Equal(expectedOutOfDate,
                    TargetUpToDateChecker.IsAnyOutOfDate(out detail, Directory.GetCurrentDirectory(), inputs, outputs));
            }
            finally
            {
                if (File.Exists(inputTarget)) File.Delete(inputTarget);
                if (File.Exists(inputSymlink)) File.Delete(inputSymlink);
                if (File.Exists(outputTarget)) File.Delete(outputTarget);
            }
        }
    }
}

