// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests : IDisposable
    {
        private const string s_testProject = @"
         <Project>
            <PropertyGroup>
               <TestProperty>Test</TestProperty>
            </PropertyGroup>
            <ItemGroup>
               <TestItem Include=""Test"" />
            </ItemGroup>
            <Target Name='Target1'>
               <Message Text='MessageOutputText'/>
            </Target>
            <Target Name='Target2' AfterTargets='Target1'>
               <Exec Command='echo a'/>
            </Target>
         </Project>";

        private const string s_testProject2 = @"
        <Project>
            <ItemGroup>
            <Compile Include=""0.cs"" />
            </ItemGroup>
            <ItemDefinitionGroup>
            <Compile>
                <MetadataFromItemDefinition>fromItemDefinition%61%62%63&lt;&gt;</MetadataFromItemDefinition>
            </Compile>
            </ItemDefinitionGroup>
            <Target Name=""Build"" Outputs=""@(CombinedOutput)"">
            <ItemGroup>
                <Compile Include=""1.cs"">
                <MetadataName>MetadataValue1%61%62%63&lt;&gt;</MetadataName>
                </Compile>
                <Compile Remove=""1.cs"" />
                <Compile Include=""2.cs"" />
                <Compile Include=""3.cs"">
                <CustomMetadata>custom%61%62%63&lt;&gt;</CustomMetadata>
                </Compile>
            </ItemGroup>
            <Message Importance=""High"" Condition=""$(Test) != true"" Text=""Hello"" />
            <CombinePath BasePath=""base"" Paths=""@(Compile)"">
                <Output TaskParameter=""CombinedPaths"" ItemName=""CombinedOutput""/>
            </CombinePath>
            <ItemGroup>
                <Compile Remove=""2.cs"" />
            </ItemGroup>
            </Target>
        </Project>";

        private readonly TestEnvironment _env;
        private string _logFile;

        public BinaryLoggerTests(ITestOutputHelper output)
        {
            _env = TestEnvironment.Create(output);

            // this is needed to ensure the binary logger does not pollute the environment
            _env.WithEnvironmentInvariant();

            _logFile = _env.ExpectFile(".binlog").Path;
        }

        [Theory]
        [InlineData(s_testProject)]
        [InlineData(s_testProject2)]
        public void TestBinaryLoggerRoundtrip(string projectText)
        {
            var binaryLogger = new BinaryLogger();

            binaryLogger.Parameters = _logFile;

            var mockLogFromBuild = new MockLogger();

            var serialFromBuildText = new StringBuilder();
            var serialFromBuild = new SerialConsoleLogger(Framework.LoggerVerbosity.Diagnostic, t => serialFromBuildText.Append(t), colorSet: null, colorReset: null);
            serialFromBuild.Parameters = "NOPERFORMANCESUMMARY";

            var parallelFromBuildText = new StringBuilder();
            var parallelFromBuild = new ParallelConsoleLogger(Framework.LoggerVerbosity.Diagnostic, t => parallelFromBuildText.Append(t), colorSet: null, colorReset: null);
            parallelFromBuild.Parameters = "NOPERFORMANCESUMMARY";

            // build and log into binary logger, mock logger, serial and parallel console loggers
            // no logging on evaluation
            using (ProjectCollection collection = new())
            {
                Project project = ObjectModelHelpers.CreateInMemoryProject(collection, projectText);
                project.Build(new ILogger[] { binaryLogger, mockLogFromBuild, serialFromBuild, parallelFromBuild }).ShouldBeTrue();
            }

            var mockLogFromPlayback = new MockLogger();

            var serialFromPlaybackText = new StringBuilder();
            var serialFromPlayback = new SerialConsoleLogger(Framework.LoggerVerbosity.Diagnostic, t => serialFromPlaybackText.Append(t), colorSet: null, colorReset: null);
            serialFromPlayback.Parameters = "NOPERFORMANCESUMMARY";

            var parallelFromPlaybackText = new StringBuilder();
            var parallelFromPlayback = new ParallelConsoleLogger(Framework.LoggerVerbosity.Diagnostic, t => parallelFromPlaybackText.Append(t), colorSet: null, colorReset: null);
            parallelFromPlayback.Parameters = "NOPERFORMANCESUMMARY";

            var binaryLogReader = new BinaryLogReplayEventSource();
            mockLogFromPlayback.Initialize(binaryLogReader);
            serialFromPlayback.Initialize(binaryLogReader);
            parallelFromPlayback.Initialize(binaryLogReader);

            // read the binary log and replay into mockLogger2
            binaryLogReader.Replay(_logFile);

            // the binlog will have more information than recorded by the text log
            mockLogFromPlayback.FullLog.ShouldContainWithoutWhitespace(mockLogFromBuild.FullLog);

            var serialExpected = serialFromBuildText.ToString();
            var serialActual = serialFromPlaybackText.ToString();
            var parallelExpected = parallelFromBuildText.ToString();
            var parallelActual = parallelFromPlaybackText.ToString();

            serialActual.ShouldContainWithoutWhitespace(serialExpected);
            parallelActual.ShouldContainWithoutWhitespace(parallelExpected);
        }

        [Fact]
        public void BinaryLoggerShouldSupportFilePathExplicitParameter()
        {
            var binaryLogger = new BinaryLogger();
            binaryLogger.Parameters = $"LogFile={_logFile}";

            ObjectModelHelpers.BuildProjectExpectSuccess(s_testProject, binaryLogger);
        }

        [Fact]
        public void UnusedEnvironmentVariablesDoNotAppearInBinaryLog()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("EnvVar1", "itsValue");
                env.SetEnvironmentVariable("EnvVar2", "value2");
                env.SetEnvironmentVariable("EnvVar3", "value3");
                string contents = @"
<Project DefaultTargets=""PrintEnvVar"">

<PropertyGroup>
<MyProp1>value</MyProp1>
<MyProp2>$(EnvVar2)</MyProp2>
</PropertyGroup>

<Target Name=""PrintEnvVar"">
<Message Text=""Environment variable EnvVar3 has value $(EnvVar3)"" Importance=""High"" />
</Target>

</Project>";
                TransientTestFolder logFolder = env.CreateFolder(createFolder: true);
                TransientTestFile projectFile = env.CreateFile(logFolder, "myProj.proj", contents);
                BinaryLogger logger = new();
                logger.Parameters = _logFile;
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} -bl:{logger.Parameters}", out bool success);
                success.ShouldBeTrue();
                RunnerUtilities.ExecMSBuild($"{logger.Parameters} -flp:logfile={Path.Combine(logFolder.Path, "logFile.log")};verbosity=diagnostic", out success);
                success.ShouldBeTrue();
                string text = File.ReadAllText(Path.Combine(logFolder.Path, "logFile.log"));
                text.ShouldContain("EnvVar2");
                text.ShouldContain("value2");
                text.ShouldContain("EnvVar3");
                text.ShouldContain("value3");
                text.ShouldNotContain("EnvVar1");
                text.ShouldNotContain("itsValue");
            }
        }

        [WindowsFullFrameworkOnlyFact(additionalMessage: "Tests if the AppDomain used to load the task is included in the log text for the event, which is true only on Framework.")]
        public void AssemblyLoadsDuringTaskRunLoggedWithAppDomain() => AssemblyLoadsDuringTaskRun("AppDomain: [Default]");

        [DotNetOnlyFact(additionalMessage: "Tests if the AssemblyLoadContext used to load the task is included in the log text for the event, which is true only on Core.")]
        public void AssemblyLoadsDuringTaskRunLoggedWithAssemblyLoadContext() => AssemblyLoadsDuringTaskRun("AssemblyLoadContext: Default");

        private void AssemblyLoadsDuringTaskRun(string additionalEventText)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                string contents = $"""
                    <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" DefaultTargets="Hello">
                      <!-- This simple inline task displays "Hello, world!" -->
                      <UsingTask
                        TaskName="HelloWorld"
                        TaskFactory="RoslynCodeTaskFactory"
                        AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll" >
                        <ParameterGroup />
                        <Task>
                          <Using Namespace="System"/>
                          <Using Namespace="System.IO"/>
                          <Using Namespace="System.Reflection"/>
                          <Code Type="Fragment" Language="cs">
                    <![CDATA[
                        // Display "Hello, world!"
                        Log.LogMessage("Hello, world!");
                    	//load assembly
                    	var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    	var diagAssembly = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(assemblies[0].Location), "System.Diagnostics.Debug.dll"));
                    	Log.LogMessage("Loaded: " + diagAssembly);
                    ]]>
                          </Code>
                        </Task>
                      </UsingTask>

                    <Target Name="Hello">
                      <HelloWorld />
                    </Target>
                    </Project>
                    """;
                TransientTestFolder logFolder = env.CreateFolder(createFolder: true);
                TransientTestFile projectFile = env.CreateFile(logFolder, "myProj.proj", contents);
                BinaryLogger logger = new();
                logger.Parameters = _logFile;
                env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
                RunnerUtilities.ExecMSBuild($"{projectFile.Path} -nr:False -bl:{logger.Parameters} -flp1:logfile={Path.Combine(logFolder.Path, "logFile.log")};verbosity=diagnostic -flp2:logfile={Path.Combine(logFolder.Path, "logFile2.log")};verbosity=normal", out bool success);
                success.ShouldBeTrue();

                string assemblyLoadedEventText =
                    "Assembly loaded during TaskRun (InlineCode.HelloWorld): System.Diagnostics.Debug";
                string text = File.ReadAllText(Path.Combine(logFolder.Path, "logFile.log"));
                text.ShouldContain(assemblyLoadedEventText);
                text.ShouldContain(additionalEventText);
                // events should not be in logger with verbosity normal
                string text2 = File.ReadAllText(Path.Combine(logFolder.Path, "logFile2.log"));
                text2.ShouldNotContain(assemblyLoadedEventText);
                text2.ShouldNotContain(additionalEventText);
                RunnerUtilities.ExecMSBuild($"{logger.Parameters} -flp1:logfile={Path.Combine(logFolder.Path, "logFile3.log")};verbosity=diagnostic -flp2:logfile={Path.Combine(logFolder.Path, "logFile4.log")};verbosity=normal", out success);
                success.ShouldBeTrue();
                text = File.ReadAllText(Path.Combine(logFolder.Path, "logFile3.log"));
                text.ShouldContain(assemblyLoadedEventText);
                text.ShouldContain(additionalEventText);
                // events should not be in logger with verbosity normal
                text2 = File.ReadAllText(Path.Combine(logFolder.Path, "logFile4.log"));
                text2.ShouldNotContain(assemblyLoadedEventText);
                text2.ShouldNotContain(additionalEventText);
            }
        }

        [Fact]
        public void BinaryLoggerShouldEmbedFilesViaTaskOutput()
        {
            using var buildManager = new BuildManager();
            var binaryLogger = new BinaryLogger()
            {
                Parameters = $"LogFile={_logFile}",
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.ZipFile,
            };
            var testProject = @"
<Project>
    <Target Name=""Build"">
        <WriteLinesToFile File=""testtaskoutputfile.txt"" Lines=""abc;def;ghi""/>
        <CreateItem Include=""testtaskoutputfile.txt"">
            <Output TaskParameter=""Include"" ItemName=""EmbedInBinlog"" />
        </CreateItem>
    </Target>
</Project>";
            ObjectModelHelpers.BuildProjectExpectSuccess(testProject, binaryLogger);
            var projectImportsZipPath = Path.ChangeExtension(_logFile, ".ProjectImports.zip");
            using var fileStream = new FileStream(projectImportsZipPath, FileMode.Open);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // Can't just compare `Name` because `ZipArchive` does not handle unix directory separators well
            // thus producing garbled fully qualified paths in the actual .ProjectImports.zip entries
            zipArchive.Entries.ShouldContain(zE => zE.Name.EndsWith("testtaskoutputfile.txt"),
                $"Embedded files: {string.Join(",", zipArchive.Entries)}");
        }

        [RequiresSymbolicLinksFact]
        public void BinaryLoggerShouldEmbedSymlinkFilesViaTaskOutput()
        {
            string testFileName = "foobar.txt";
            string symlinkName = "symlink1.txt";
            string symlinkLvl2Name = "symlink2.txt";
            string emptyFileName = "empty.txt";
            TransientTestFolder testFolder = _env.DefaultTestDirectory.CreateDirectory("TestDir");
            TransientTestFolder testFolder2 = _env.DefaultTestDirectory.CreateDirectory("TestDir2");
            TransientTestFile testFile = testFolder.CreateFile(testFileName, string.Join(Environment.NewLine, new[] { "123", "456" }));
            string symlinkPath = Path.Combine(testFolder2.Path, symlinkName);
            string symlinkLvl2Path = Path.Combine(testFolder2.Path, symlinkLvl2Name);
            string emptyFile = testFolder.CreateFile(emptyFileName).Path;

            string errorMessage = string.Empty;
            Assert.True(NativeMethodsShared.MakeSymbolicLink(symlinkPath, testFile.Path, ref errorMessage), errorMessage);
            Assert.True(NativeMethodsShared.MakeSymbolicLink(symlinkLvl2Path, symlinkPath, ref errorMessage), errorMessage);

            using var buildManager = new BuildManager();
            var binaryLogger = new BinaryLogger()
            {
                Parameters = $"LogFile={_logFile}",
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.ZipFile,
            };
            var testProjectFmt = @"
<Project>
    <Target Name=""Build"" Inputs=""{0}"" Outputs=""testtaskoutputfile.txt"">
        <ReadLinesFromFile
            File=""{0}"" >
            <Output
                TaskParameter=""Lines""
                ItemName=""ItemsFromFile""/>
        </ReadLinesFromFile>
        <WriteLinesToFile File=""testtaskoutputfile.txt"" Lines=""@(ItemsFromFile);abc;def;ghi""/>
        <CreateItem Include=""testtaskoutputfile.txt"">
            <Output TaskParameter=""Include"" ItemName=""EmbedInBinlog"" />
        </CreateItem>
        <CreateItem Include=""{0}"">
            <Output TaskParameter=""Include"" ItemName=""EmbedInBinlog"" />
        </CreateItem>
        <CreateItem Include=""{1}"">
            <Output TaskParameter=""Include"" ItemName=""EmbedInBinlog"" />
        </CreateItem>
        <CreateItem Include=""{2}"">
            <Output TaskParameter=""Include"" ItemName=""EmbedInBinlog"" />
        </CreateItem>
    </Target>
</Project>";
            var testProject = string.Format(testProjectFmt, symlinkPath, symlinkLvl2Path, emptyFile);
            ObjectModelHelpers.BuildProjectExpectSuccess(testProject, binaryLogger);
            var projectImportsZipPath = Path.ChangeExtension(_logFile, ".ProjectImports.zip");
            using var fileStream = new FileStream(projectImportsZipPath, FileMode.Open);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // Can't just compare `Name` because `ZipArchive` does not handle unix directory separators well
            // thus producing garbled fully qualified paths in the actual .ProjectImports.zip entries
            zipArchive.Entries.ShouldContain(zE => zE.Name.EndsWith("testtaskoutputfile.txt"),
                customMessage: $"Embedded files: {string.Join(",", zipArchive.Entries)}");
            zipArchive.Entries.ShouldContain(zE => zE.Name.EndsWith(symlinkName),
                customMessage: $"Embedded files: {string.Join(",", zipArchive.Entries)}");
            zipArchive.Entries.ShouldContain(zE => zE.Name.EndsWith(symlinkLvl2Name),
                customMessage: $"Embedded files: {string.Join(",", zipArchive.Entries)}");
            zipArchive.Entries.ShouldContain(zE => zE.Name.EndsWith(emptyFileName),
                customMessage: $"Embedded files: {string.Join(",", zipArchive.Entries)}");
        }

        [Fact]
        public void BinaryLoggerShouldNotThrowWhenMetadataCannotBeExpanded()
        {
            var binaryLogger = new BinaryLogger
            {
                Parameters = $"LogFile={_logFile}"
            };

            const string project = @"
<Project>
<ItemDefinitionGroup>
  <F>
   <MetadataFileName>a\b\%(Filename).c</MetadataFileName>
  </F>
 </ItemDefinitionGroup>
 <ItemGroup>
  <F Include=""-in &quot;x\y\z&quot;"" />
 </ItemGroup>
 <Target Name=""X"" />
</Project>";

            ObjectModelHelpers.BuildProjectExpectSuccess(project, binaryLogger);
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/6323.
        /// </summary>
        /// <remarks>
        /// This isn't strictly a binlog test, but it fits here because
        /// all log event types will be used when the binlog is attached.
        /// </remarks>
        [Fact]
        public void MessagesCanBeLoggedWhenProjectsAreCached()
        {
            using var env = TestEnvironment.Create();

            env.SetEnvironmentVariable("MSBUILDDEBUGFORCECACHING", "1");

            using var buildManager = new BuildManager();

            var binaryLogger = new BinaryLogger
            {
                Parameters = $"LogFile={_logFile}"
            };

            // To trigger #6323, there must be at least two project instances.
            var referenceProject = _env.CreateTestProjectWithFiles("reference.proj", @"
         <Project>
            <Target Name='Target2'>
               <Exec Command='echo a'/>
            </Target>
         </Project>");

            var entryProject = _env.CreateTestProjectWithFiles("entry.proj", $@"
         <Project>
            <Target Name='BuildSelf'>
               <Message Text='MessageOutputText'/>
               <MSBuild Projects='{referenceProject.ProjectFile}' Targets='Target2' />
               <MSBuild Projects='{referenceProject.ProjectFile}' Targets='Target2' /><!-- yes, again. That way it's a cached result -->
            </Target>
         </Project>");

            buildManager.Build(new BuildParameters() { Loggers = new ILogger[] { binaryLogger } },
                new BuildRequestData(entryProject.ProjectFile, new Dictionary<string, string>(), null, new string[] { "BuildSelf" }, null))
                .OverallResult.ShouldBe(BuildResultCode.Success);
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/7828
        /// </summary>
        /// <remarks>
        /// This test verifies,
        /// 1. When binary log and verbosity=diagnostic are both set, the equivalent command line is printed.
        /// 2. When binary log and non-diag verbosity are set, the equivalent command line is NOT printed.
        /// </remarks>
        [Fact]
        public void SuppressCommandOutputForNonDiagVerbosity()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var contents = @"
                    <Project>
                        <Target Name='Target2'>
                            <Exec Command='echo a'/>
                        </Target>
                    </Project>";
                BinaryLogger logger = new();
                logger.Parameters = _logFile;
                TransientTestFolder testFolder = env.CreateFolder(createFolder: true);

                TransientTestFile projectFile1 = env.CreateFile(testFolder, "testProject01.proj", contents);
                string consoleOutput1 = RunnerUtilities.ExecMSBuild($"{projectFile1.Path} -bl:{logger.Parameters} -verbosity:diag -nologo", out bool success1);
                success1.ShouldBeTrue();
                var expected1 = $"-nologo -bl:{logger.Parameters} -verbosity:diag {projectFile1.Path}";
                consoleOutput1.ShouldContain(expected1);

                foreach (var verbosity in new string[] { "q", "m", "n", "d" })
                {
                    TransientTestFile projectFile2 = env.CreateFile(testFolder, $"testProject_{verbosity}.proj", contents);
                    string consoleOutput2 = RunnerUtilities.ExecMSBuild($"{projectFile2.Path} -bl:{logger.Parameters} -verbosity:{verbosity} -nologo", out bool success2);
                    success2.ShouldBeTrue();
                    var expected2 = $"-nologo -bl:{logger.Parameters} -verbosity:{verbosity} {projectFile2.Path}";
                    consoleOutput2.ShouldNotContain(expected2);
                }
            }
        }

        public void Dispose()
        {
            _env.Dispose();
        }
    }
}
