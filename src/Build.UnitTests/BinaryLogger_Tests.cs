using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests : IDisposable
    {
        private static string s_testProject = @"
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
        private readonly TestEnvironment _env;
        private string _logFile;

        public BinaryLoggerTests(ITestOutputHelper output)
        {
            _env = TestEnvironment.Create(output);

            // this is needed to ensure the binary logger does not pollute the environment
            _env.WithEnvironmentInvariant();

            _logFile = _env.ExpectFile(".binlog").Path;
        }

        [Fact]
        public void TestBinaryLoggerRoundtrip()
        {
            var binaryLogger = new BinaryLogger();

            binaryLogger.Parameters = _logFile;

            var mockLogger1 = new MockLogger();

            // build and log into binary logger and mockLogger1
            ObjectModelHelpers.BuildProjectExpectSuccess(s_testProject, binaryLogger, mockLogger1);

            var mockLogger2 = new MockLogger();

            var binaryLogReader = new BinaryLogReplayEventSource();
            mockLogger2.Initialize(binaryLogReader);

            // read the binary log and replay into mockLogger2testassembly
            binaryLogReader.Replay(_logFile);

            Assert.Equal(mockLogger1.FullLog, mockLogger2.FullLog);
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
