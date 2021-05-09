using System;
using System.Text;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests : IDisposable
    {
        private const string s_testProject = @"
         <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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


        public void Dispose()
        {
            _env.Dispose();
        }
    }
}
