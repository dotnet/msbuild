using System;
using System.IO;
using Microsoft.Build.Logging;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests
    {
        private const string MSBUILDTARGETOUTPUTLOGGING = nameof(MSBUILDTARGETOUTPUTLOGGING);

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

        [Fact]
        public void TestBinaryLoggerRoundtrip()
        {
            var originalTargetOutputLogging = Environment.GetEnvironmentVariable(MSBUILDTARGETOUTPUTLOGGING);

            try
            {
                var binaryLogger = new BinaryLogger();

                // with this file name, the file will be archived as as build artifact so we can inspect it later
                // this is needed to investigate an intermittent failure of this test on Ubuntu 14
                var logFilePath = "Microsoft.Build.Engine.UnitTests.dll_TestBinaryLoggerRoundtrip.binlog";
                binaryLogger.Parameters = logFilePath;

                var mockLogger1 = new MockLogger();

                // build and log into binary logger and mockLogger1
                ObjectModelHelpers.BuildProjectExpectSuccess(s_testProject, binaryLogger, mockLogger1);

                var mockLogger2 = new MockLogger();

                var binaryLogReader = new BinaryLogReplayEventSource();
                mockLogger2.Initialize(binaryLogReader);

                // read the binary log and replay into mockLogger2
                binaryLogReader.Replay(logFilePath);

                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }

                Assert.Equal(mockLogger1.FullLog, mockLogger2.FullLog);
            }
            finally
            {
                Environment.SetEnvironmentVariable(MSBUILDTARGETOUTPUTLOGGING, originalTargetOutputLogging);
            }
        }
    }
}
