using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

using Microsoft.Build.BackEnd.Logging;
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
            ObjectModelHelpers.BuildProjectExpectSuccess(projectText, binaryLogger, mockLogFromBuild, serialFromBuild, parallelFromBuild);

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
            zipArchive.Entries.ShouldContain(zE => zE.Name.EndsWith("testtaskoutputfile.txt"));
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

        public void Dispose()
        {
            _env.Dispose();
        }
    }
}
