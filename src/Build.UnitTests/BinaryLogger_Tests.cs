using System;
using System.Collections.Generic;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

using Shouldly;

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

            var mockLogFromBuild = new MockLogger();

            // build and log into binary logger and mockLogger1
            ObjectModelHelpers.BuildProjectExpectSuccess(s_testProject, binaryLogger, mockLogFromBuild);

            var mockLogFromPlayback = new MockLogger();

            var binaryLogReader = new BinaryLogReplayEventSource();
            mockLogFromPlayback.Initialize(binaryLogReader);

            // read the binary log and replay into mockLogger2testassembly
            binaryLogReader.Replay(_logFile);

            // the binlog will have more information than recorded by the text log
            Assert.Contains(mockLogFromBuild.FullLog, mockLogFromPlayback.FullLog);
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
