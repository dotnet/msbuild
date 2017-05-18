// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable 436

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;



using EventSourceSink = Microsoft.Build.BackEnd.Logging.EventSourceSink;
using Project = Microsoft.Build.Evaluation.Project;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class FileLogger_Tests
    {
        /// <summary>
        /// Basic test of the file logger.  Writes to a log file in the temp directory.
        /// </summary>
        [Fact]
        public void Basic()
        {
            FileLogger fileLogger = new FileLogger();
            string logFile = FileUtilities.GetTemporaryFile();
            fileLogger.Parameters = "verbosity=Normal;logfile=" + logFile;

            Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`Build`>
                        <Message Text=`Hello world from the FileLogger`/>
                    </Target>
                </Project>
                ");

            project.Build(fileLogger);

            project.ProjectCollection.UnregisterAllLoggers();

            string log = File.ReadAllText(logFile);
            Assert.True(log.Contains("Hello world from the FileLogger")); // "Log should have contained message"

            File.Delete(logFile);
        }

        /// <summary>
        /// Basic case of logging a message to a file
        /// Verify it logs and encoding is ANSI
        /// </summary>
        [Fact]
        public void BasicNoExistingFile()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                SetUpFileLoggerAndLogMessage("logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                VerifyFileContent(log, "message here");

                
                byte[] content = ReadRawBytes(log);
                Assert.Equal((byte)109, content[0]); // 'm'
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }

        /// <summary>
        /// Invalid file should error nicely
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        [Trait("Category", "mono-osx-failing")]
        public void InvalidFile()
        {
            Assert.Throws<LoggerException>(() =>
            {
                string log = null;

                try
                {
                    SetUpFileLoggerAndLogMessage("logfile=||invalid||", new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                }
                finally
                {
                    if (null != log) File.Delete(log);
                }
            }
           );
        }
        /// <summary>
        /// Specific verbosity overrides global verbosity
        /// </summary>
        [Fact]
        public void SpecificVerbosity()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                FileLogger fl = new FileLogger();
                EventSourceSink es = new EventSourceSink();
                fl.Parameters = "verbosity=diagnostic;logfile=" + log;  // diagnostic specific setting
                fl.Verbosity = LoggerVerbosity.Quiet; // quiet global setting
                fl.Initialize(es);
                fl.MessageHandler(null, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                fl.Shutdown();

                // expect message to appear because diagnostic not quiet verbosity was used
                VerifyFileContent(log, "message here");
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }

        /// <summary>
        /// Test the short hand verbosity settings for the file logger
        /// </summary>
        [Fact]
        public void ValidVerbosities()
        {
            string[] verbositySettings = new string[] { "Q", "quiet", "m", "minimal", "N", "normal", "d", "detailed", "diag", "DIAGNOSTIC" };
            LoggerVerbosity[] verbosityEnumerations = new LoggerVerbosity[] {LoggerVerbosity.Quiet, LoggerVerbosity.Quiet,
                                                                             LoggerVerbosity.Minimal, LoggerVerbosity.Minimal,
                                                                             LoggerVerbosity.Normal, LoggerVerbosity.Normal,
                                                                             LoggerVerbosity.Detailed, LoggerVerbosity.Detailed,
                                                                             LoggerVerbosity.Diagnostic, LoggerVerbosity.Diagnostic};
            for (int i = 0; i < verbositySettings.Length; i++)
            {
                FileLogger fl = new FileLogger();
                fl.Parameters = "verbosity=" + verbositySettings[i] + ";";
                EventSourceSink es = new EventSourceSink();
                fl.Initialize(es);
                fl.Shutdown();
                Assert.Equal(fl.Verbosity, verbosityEnumerations[i]);
            }

            // Do the same using the v shorthand
            for (int i = 0; i < verbositySettings.Length; i++)
            {
                FileLogger fl = new FileLogger();
                fl.Parameters = "v=" + verbositySettings[i] + ";";
                EventSourceSink es = new EventSourceSink();
                fl.Initialize(es);
                fl.Shutdown();
                Assert.Equal(fl.Verbosity, verbosityEnumerations[i]);
            }
        }

        /// <summary>
        /// Invalid verbosity setting
        /// </summary>
        [Fact]
        public void InvalidVerbosity()
        {
            Assert.Throws<LoggerException>(() =>
            {
                FileLogger fl = new FileLogger();
                fl.Parameters = "verbosity=CookiesAndCream";
                EventSourceSink es = new EventSourceSink();
                fl.Initialize(es);
            }
           );
        }
        /// <summary>
        /// Invalid encoding setting
        /// </summary>
        [Fact]
        public void InvalidEncoding()
        {
            Assert.Throws<LoggerException>(() =>
            {
                string log = null;

                try
                {
                    log = GetTempFilename();
                    FileLogger fl = new FileLogger();
                    EventSourceSink es = new EventSourceSink();
                    fl.Parameters = "encoding=foo;logfile=" + log;
                    fl.Initialize(es);
                }
                finally
                {
                    if (null != log) File.Delete(log);
                }
            }
           );
        }

        /// <summary>
        /// Valid encoding setting
        /// </summary>
        [Fact]
        public void ValidEncoding()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                SetUpFileLoggerAndLogMessage("encoding=utf-16;logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                byte[] content = ReadRawBytes(log);

                // FF FE is the BOM for UTF16
                Assert.Equal((byte)255, content[0]);
                Assert.Equal((byte)254, content[1]);
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }

        /// <summary>
        /// Valid encoding setting
        /// </summary>
        [Fact]
        public void ValidEncoding2()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                SetUpFileLoggerAndLogMessage("encoding=utf-8;logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                byte[] content = ReadRawBytes(log);

                // EF BB BF is the BOM for UTF8
                Assert.Equal((byte)239, content[0]);
                Assert.Equal((byte)187, content[1]);
                Assert.Equal((byte)191, content[2]);
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }

        /// <summary>
        /// Read the raw byte content of a file
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private byte[] ReadRawBytes(string log)
        {
            byte[] content;
            using (FileStream stream = new FileStream(log, FileMode.Open))
            {
                content = new byte[stream.Length];

                for (int i = 0; i < stream.Length; i++)
                {
                    content[i] = (byte)stream.ReadByte();
                }
            }

            return content;
        }

        /// <summary>
        /// Logging a message to a file that already exists should overwrite it
        /// </summary>
        [Fact]
        public void BasicExistingFileNoAppend()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                WriteContentToFile(log);
                SetUpFileLoggerAndLogMessage("logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                VerifyFileContent(log, "message here");
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }

        /// <summary>
        /// Logging to a file that already exists, with "append" set, should append
        /// </summary>
        [Fact]
        public void BasicExistingFileAppend()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                WriteContentToFile(log);
                SetUpFileLoggerAndLogMessage("append;logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                VerifyFileContent(log, "existing content\nmessage here");
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }

        /// <summary>
        /// Logging to a file in a directory that doesn't exists
        /// </summary>
        [Fact]
        public void BasicNoExistingDirectory()
        {
            string directory = Path.Combine(ObjectModelHelpers.TempProjectDir, Guid.NewGuid().ToString("N"));
            string log = Path.Combine(directory, "build.log");
            Assert.False(Directory.Exists(directory));
            Assert.False(File.Exists(log));

            try
            {
                SetUpFileLoggerAndLogMessage("logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                VerifyFileContent(log, "message here");
            }
            finally
            {
                ObjectModelHelpers.DeleteDirectory(directory);
            }
        }

        [Theory]
        [InlineData("warningsonly")]
        [InlineData("errorsonly")]
        [InlineData("errorsonly;warningsonly")]
        public void EmptyErrorLogUsingWarningsErrorsOnly(string loggerOption)
        {
            using (var env = TestEnvironment.Create())
            {
                var logFile = env.CreateFile(".log").Path;

                // Note: Only the ParallelConsoleLogger supports this scenario (log file empty on no error/warn). We
                // need to explicitly enable it here with the 'ENABLEMPLOGGING' flag.
                FileLogger fileLogger = new FileLogger {Parameters = $"{loggerOption};logfile={logFile};ENABLEMPLOGGING" };

                Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`Build`>
                        <Message Text=`Hello world from the FileLogger`/>
                    </Target>
                </Project>");

                project.Build(fileLogger);
                project.ProjectCollection.UnregisterAllLoggers();

                // File should exist and be 0 length (no summary information, etc.)
                var result = new FileInfo(logFile);
                Assert.True(result.Exists);
                Assert.Equal(0, new FileInfo(logFile).Length);
            }
        }

        /// <summary>
        /// Gets a filename for a nonexistent temporary file.
        /// </summary>
        /// <returns></returns>
        private string GetTempFilename()
        {
            string path = FileUtilities.GetTemporaryFile();
            File.Delete(path);
            return path;
        }

        /// <summary>
        /// Writes a string to a file.
        /// </summary>
        /// <param name="log"></param>
        private void WriteContentToFile(string log)
        {
            using (StreamWriter sw = FileUtilities.OpenWrite(log, false))
            {
                sw.WriteLine("existing content");
            }
        }

        /// <summary>
        /// Creates a FileLogger, sets its parameters and initializes it,
        /// logs a message to it, and calls shutdown
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private void SetUpFileLoggerAndLogMessage(string parameters, BuildMessageEventArgs message)
        {
            FileLogger fl = new FileLogger();
            EventSourceSink es = new EventSourceSink();
            fl.Parameters = parameters;
            fl.Initialize(es);
            fl.MessageHandler(null, message);
            fl.Shutdown();
            return;
        }

        /// <summary>
        /// Verifies that a file contains exactly the expected content.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="expectedContent"></param>
        private void VerifyFileContent(string file, string expectedContent)
        {
            string actualContent;
            using (StreamReader sr = FileUtilities.OpenRead(file))
            {
                actualContent = sr.ReadToEnd();
            }

            string[] actualLines = actualContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] expectedLines = expectedContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(expectedLines.Length, actualLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
            {
                Assert.Equal(expectedLines[i].Trim(), actualLines[i].Trim());
            }
        }

        #region DistributedLogger
        /// <summary>
        /// Check the ability of the distributed logger to correctly tell its internal file logger where to log the file
        /// </summary>
        [Fact]
        public void DistributedFileLoggerParameters()
        {
            DistributedFileLogger fileLogger = new DistributedFileLogger();
            try
            {
                fileLogger.NodeId = 0;
                fileLogger.Initialize(new EventSourceSink());
                Assert.Equal(0, string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=msbuild0.log;", StringComparison.OrdinalIgnoreCase));
                fileLogger.Shutdown();

                fileLogger.NodeId = 3;
                fileLogger.Parameters = "logfile=" + Path.Combine(Directory.GetCurrentDirectory(), "mylogfile.log");
                fileLogger.Initialize(new EventSourceSink());
                Assert.Equal(0, string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=" + Path.Combine(Directory.GetCurrentDirectory(), "mylogfile3.log") + ";", StringComparison.OrdinalIgnoreCase));
                fileLogger.Shutdown();

                fileLogger.NodeId = 4;
                fileLogger.Parameters = "logfile=" + Path.Combine(Directory.GetCurrentDirectory(), "mylogfile.log");
                fileLogger.Initialize(new EventSourceSink());
                Assert.Equal(0, string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=" + Path.Combine(Directory.GetCurrentDirectory(), "mylogfile4.log") + ";", StringComparison.OrdinalIgnoreCase));
                fileLogger.Shutdown();

                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "tempura"));
                fileLogger.NodeId = 1;
                fileLogger.Parameters = "logfile=" + Path.Combine(Directory.GetCurrentDirectory(), "tempura", "mylogfile.log");
                fileLogger.Initialize(new EventSourceSink());
                Assert.Equal(0, string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=" + Path.Combine(Directory.GetCurrentDirectory(), "tempura", "mylogfile1.log") + ";", StringComparison.OrdinalIgnoreCase));
                fileLogger.Shutdown();
            }
            finally
            {
                if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "tempura")))
                {
                    File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "tempura", "mylogfile1.log"));
                    FileUtilities.DeleteWithoutTrailingBackslash(Path.Combine(Directory.GetCurrentDirectory(), "tempura"));
                }
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "mylogfile0.log"));
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "mylogfile3.log"));
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "mylogfile4.log"));
            }
        }

        [Fact]
        public void DistributedLoggerNullEmpty()
        {
            Assert.Throws<LoggerException>(() =>
            {
                DistributedFileLogger fileLogger = new DistributedFileLogger();
                fileLogger.NodeId = 0;
                fileLogger.Initialize(new EventSourceSink());

                fileLogger.NodeId = 1;
                fileLogger.Parameters = "logfile=";
                fileLogger.Initialize(new EventSourceSink());
                Assert.True(false);
            }
           );
        }
        #endregion

    }
}
