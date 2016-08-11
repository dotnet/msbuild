// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class FileLogger_Tests
    {
        /// <summary>
        /// Basic test of the file logger.  Writes to a log file in the temp directory.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void Basic()
        {
            FileLogger fileLogger = new FileLogger();
            string logFile = Path.GetTempFileName();
            fileLogger.Parameters = "verbosity=Normal;logfile=" + logFile;
            
            Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`Build`>
                        <Message Text=`Hello world from the FileLogger`/>
                    </Target>
                </Project>
                ", fileLogger);

            project.Build();

            project.ParentEngine.UnregisterAllLoggers();

            string log = File.ReadAllText(logFile);
            Assertion.Assert("Log should have contained message", log.Contains("Hello world from the FileLogger"));

            File.Delete(logFile);
        }

        /// <summary>
        /// Basic case of logging a message to a file
        /// Verify it logs and encoding is ANSI
        /// </summary>
        [Test]
        public void BasicNoExistingFile()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                SetUpFileLoggerAndLogMessage("logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                VerifyFileContent(log, "message here");

                // Verify no BOM (ANSI encoding)
                byte[] content = ReadRawBytes(log);
                Assertion.AssertEquals((byte)109, content[0]); // 'm'
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }
        
        /// <summary>
        /// Invalid file should error nicely
        /// </summary>
        [Test]
        [ExpectedException(typeof(LoggerException))]
        public void InvalidFile()
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

        /// <summary>
        /// Specific verbosity overrides global verbosity
        /// </summary>
        [Test]
        public void SpecificVerbosity()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                FileLogger fl = new FileLogger();
                EventSource es = new EventSource();
                fl.Parameters = "verbosity=diagnostic;logfile=" + log;  // diagnostic specific setting
                fl.Verbosity = LoggerVerbosity.Quiet ; // quiet global setting
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
        [Test]
        public void ValidVerbosities()
        {
            string[] verbositySettings = new string[] {"Q", "quiet", "m", "minimal", "N", "normal", "d", "detailed", "diag", "DIAGNOSTIC"};
            LoggerVerbosity[] verbosityEnumerations = new LoggerVerbosity[] {LoggerVerbosity.Quiet, LoggerVerbosity.Quiet,
                                                                             LoggerVerbosity.Minimal, LoggerVerbosity.Minimal,
                                                                             LoggerVerbosity.Normal, LoggerVerbosity.Normal,
                                                                             LoggerVerbosity.Detailed, LoggerVerbosity.Detailed,
                                                                             LoggerVerbosity.Diagnostic, LoggerVerbosity.Diagnostic};
            for (int i = 0; i < verbositySettings.Length; i++)
            {
                FileLogger fl = new FileLogger();
                fl.Parameters = "verbosity=" + verbositySettings[i] + ";";
                EventSource es = new EventSource();
                fl.Initialize(es);
                fl.Shutdown();
                Assertion.AssertEquals(fl.Verbosity, verbosityEnumerations[i]);
            }

            // Do the same using the v shorthand
            for (int i = 0; i < verbositySettings.Length; i++)
            {
                FileLogger fl = new FileLogger();
                fl.Parameters = "v=" + verbositySettings[i] + ";";
                EventSource es = new EventSource();
                fl.Initialize(es);
                fl.Shutdown();
                Assertion.AssertEquals(fl.Verbosity, verbosityEnumerations[i]);
            }
        }

        /// <summary>
        /// Invalid verbosity setting
        /// </summary>
        [Test]
        [ExpectedException(typeof(LoggerException))]
        public void InvalidVerbosity()
        {
            FileLogger fl = new FileLogger();
            fl.Parameters = "verbosity=CookiesAndCream";
            EventSource es = new EventSource();
            fl.Initialize(es);
        }

        /// <summary>
        /// Invalid encoding setting
        /// </summary>
        [Test]
        [ExpectedException(typeof(LoggerException))]
        public void InvalidEncoding()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                FileLogger fl = new FileLogger();
                EventSource es = new EventSource();
                fl.Parameters = "encoding=foo;logfile=" + log;
                fl.Initialize(es);
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }


        /// <summary>
        /// Valid encoding setting
        /// </summary>
        [Test]
        public void ValidEncoding()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                SetUpFileLoggerAndLogMessage("encoding=utf-16;logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                byte[] content = ReadRawBytes(log);

                // FF FE is the BOM for UTF16
                Assertion.AssertEquals((byte)255, content[0]);
                Assertion.AssertEquals((byte)254, content[1]);
            }
            finally
            {
                if (null != log) File.Delete(log);
            }
        }

        /// <summary>
        /// Valid encoding setting
        /// </summary>
        [Test]
        public void ValidEncoding2()
        {
            string log = null;

            try
            {
                log = GetTempFilename();
                SetUpFileLoggerAndLogMessage("encoding=utf-8;logfile=" + log, new BuildMessageEventArgs("message here", null, null, MessageImportance.High));
                byte[] content = ReadRawBytes(log);

                // EF BB BF is the BOM for UTF8
                Assertion.AssertEquals((byte)239, content[0]);
                Assertion.AssertEquals((byte)187, content[1]);
                Assertion.AssertEquals((byte)191, content[2]);
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
        [Test]
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
        [Test]
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
        /// Gets a filename for a nonexistent temporary file.
        /// </summary>
        /// <returns></returns>
        private string GetTempFilename()
        {
            string path = Path.GetTempFileName();
            File.Delete(path);
            return path;
        }

        /// <summary>
        /// Writes a string to a file.
        /// </summary>
        /// <param name="log"></param>
        private void WriteContentToFile(string log)
        {
            using (StreamWriter sw = new StreamWriter(log))
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
            EventSource es = new EventSource();
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
            using (StreamReader sr = new StreamReader(file))
            {
                actualContent = sr.ReadToEnd();
            }

            string[] actualLines = actualContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] expectedLines = expectedContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Assertion.AssertEquals(expectedLines.Length, actualLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
            {
                Assertion.AssertEquals(expectedLines[i].Trim(), actualLines[i].Trim());
            }
        }

        #region DistributedLogger
        /// <summary>
        /// Check the ability of the distributed logger to correctly tell its internal file logger where to log the file
        /// </summary>
        [Test]
        public void DistributedFileLoggerParameters()
        {
                DistributedFileLogger fileLogger = new DistributedFileLogger();
                try
                {
                    fileLogger.NodeId = 0;
                    fileLogger.Initialize(new EventSource());
                    Assert.IsTrue(string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=msbuild0.log;", StringComparison.OrdinalIgnoreCase) == 0);
                    fileLogger.Shutdown();

                    fileLogger.NodeId = 3;
                    fileLogger.Parameters = "logfile="+Path.Combine(Environment.CurrentDirectory,"mylogfile.log");
                    fileLogger.Initialize(new EventSource());
                    Assert.IsTrue(string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=" + Path.Combine(Environment.CurrentDirectory, "mylogfile3.log") + ";", StringComparison.OrdinalIgnoreCase) == 0);
                    fileLogger.Shutdown();

                    fileLogger.NodeId = 4;
                    fileLogger.Parameters = "logfile=" + Path.Combine(Environment.CurrentDirectory, "mylogfile.log");
                    fileLogger.Initialize(new EventSource());
                    Assert.IsTrue(string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=" + Path.Combine(Environment.CurrentDirectory, "mylogfile4.log") + ";", StringComparison.OrdinalIgnoreCase) == 0);
                    fileLogger.Shutdown();

                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "tempura"));
                    fileLogger.NodeId = 1;
                    fileLogger.Parameters = "logfile=" + Path.Combine(Environment.CurrentDirectory, "tempura\\mylogfile.log");
                    fileLogger.Initialize(new EventSource());
                    Assert.IsTrue(string.Compare(fileLogger.InternalFilelogger.Parameters, "ForceNoAlign;ShowEventId;ShowCommandLine;logfile=" + Path.Combine(Environment.CurrentDirectory, "tempura\\mylogfile1.log") + ";", StringComparison.OrdinalIgnoreCase) == 0);
                    fileLogger.Shutdown();
                }
                finally
                {
                    if(Directory.Exists(Path.Combine(Environment.CurrentDirectory, "tempura")))
                    {
                        File.Delete(Path.Combine(Environment.CurrentDirectory, "tempura\\mylogfile1.log"));
                        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "tempura"));
                    }
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "mylogfile0.log"));
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "mylogfile3.log"));
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "mylogfile4.log"));
                }
        }

        [Test]
        [ExpectedException(typeof(LoggerException))]
        public void DistributedLoggerBadPath()
        {
            DistributedFileLogger fileLogger = new DistributedFileLogger();
            fileLogger.NodeId = 0;
            fileLogger.Initialize(new EventSource());

            fileLogger.NodeId = 1;
            fileLogger.Parameters = "logfile=" + Path.Combine(Environment.CurrentDirectory, "\\DONTEXIST\\mylogfile.log");
            fileLogger.Initialize(new EventSource());
            Assert.IsTrue(string.Compare(fileLogger.InternalFilelogger.Parameters, ";ShowCommandLine;logfile=" + Path.Combine(Environment.CurrentDirectory, "\\DONTEXIST\\mylogfile2.log"), StringComparison.OrdinalIgnoreCase) == 0);
        }

        [Test]
        [ExpectedException(typeof(LoggerException))]
        public void DistributedLoggerNullEmpty()
        {
            DistributedFileLogger fileLogger = new DistributedFileLogger();
            fileLogger.NodeId = 0;
            fileLogger.Initialize(new EventSource());

            fileLogger.NodeId = 1;
            fileLogger.Parameters = "logfile=";
            fileLogger.Initialize(new EventSource());
            Assert.Fail();
        }
        #endregion

    }
}
