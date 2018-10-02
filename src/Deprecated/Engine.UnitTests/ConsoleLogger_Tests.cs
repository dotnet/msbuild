// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Globalization;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ConsoleLoggerTest
    {
        private class SimulatedConsole
        {
            private StringBuilder simulatedConsole;

            internal SimulatedConsole()
            {
                simulatedConsole = new StringBuilder();
            }

            internal void Clear()
            {
                simulatedConsole = new StringBuilder();
            }

            public override string ToString()
            {
                return simulatedConsole.ToString();
            }

            internal void Write(string s)
            {
                simulatedConsole.Append(s);
            }

            internal void WriteLine(string s)
            {
                Write(s);
                Write(Environment.NewLine);
            }

            internal void SetColor(ConsoleColor c)
            {
                switch(c)
                {
                    case ConsoleColor.Red:
                        simulatedConsole.Append("<red>");
                        break;

                    case ConsoleColor.Yellow:
                        simulatedConsole.Append("<yellow>");
                        break;

                    case ConsoleColor.Cyan:
                        simulatedConsole.Append("<cyan>");
                        break;

                    case ConsoleColor.DarkGray:
                        simulatedConsole.Append("<darkgray>");
                        break;

                    case ConsoleColor.Green:
                        simulatedConsole.Append("<green>");
                        break;

                    default:
                        simulatedConsole.Append("<ERROR: invalid color>");
                        break;
                }
            }

            internal void ResetColor()
            {
                simulatedConsole.Append("<reset color>");
            }

            public static implicit operator string(SimulatedConsole sc)
            {
                return sc.ToString();
            }
        }

        private static void SingleMessageTest(LoggerVerbosity v, MessageImportance j, bool shouldPrint)
        {
            for (int i = 1; i <= 2; i++)
            {
                SimulatedConsole sc = new SimulatedConsole();
                EventSource es = new EventSource();
                ConsoleLogger L = new ConsoleLogger(v,
                                  sc.Write, null, null);
                L.Initialize(es, i);
                string msg = "my 1337 message";
                
                BuildMessageEventArgs be = new BuildMessageEventArgs(msg, "help", "sender", j);
                be.BuildEventContext = new BuildEventContext(1, 2, 3, 4);
                es.RaiseMessageEvent(null, be);

                if (i == 2 && v == LoggerVerbosity.Diagnostic)
                {
                    string context =ResourceUtilities.FormatResourceString("BuildEventContext", LogFormatter.FormatLogTimeStamp(be.Timestamp), 0) + ">";
                    msg = context + ResourceUtilities.FormatResourceString("TaskMessageWithId", "my 1337 message", be.BuildEventContext.TaskId);
                }
                else if (i == 2 && v == LoggerVerbosity.Detailed)
                {
                    string context = ResourceUtilities.FormatResourceString("BuildEventContext", string.Empty, 0) + ">";
                    msg = context + "my 1337 message";
                }
                else if (i == 2)
                {
                    msg = "  " + msg;
                }

                Assertion.AssertEquals(shouldPrint ? msg + Environment.NewLine : String.Empty, sc.ToString());
            }
        }

        private sealed class MyCustomBuildEventArgs : CustomBuildEventArgs
        {
            internal MyCustomBuildEventArgs()
                : base()
            {
                // do nothing
            }

            internal MyCustomBuildEventArgs(string message)
                : base(message, null, null)
            {
                // do nothing
            }
        }

        class MyCustomBuildEventArgs2 : CustomBuildEventArgs { }

        /// <summary>
        /// We should not crash when given a null message, etc.
        /// </summary>
        [Test]
        public void NullEventFields()
        {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Diagnostic,
                                                    sc.Write, sc.SetColor,
                                                    sc.ResetColor);
                L.Initialize(es);

                // Not all parameters are null here, but that's fine, we assume the engine will never
                // fire a ProjectStarted without a project name, etc.
                es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs(null, null));
                es.RaiseProjectStartedEvent(null, new ProjectStartedEventArgs(null, null, "p", null, null, null));
                es.RaiseTargetStartedEvent(null, new TargetStartedEventArgs(null, null, "t", null, null));
                es.RaiseTaskStartedEvent(null, new TaskStartedEventArgs(null, null, null, null, "task"));
                es.RaiseMessageEvent(null, new BuildMessageEventArgs(null, null, null, MessageImportance.High));
                es.RaiseWarningEvent(null, new BuildWarningEventArgs(null, null, null, 0, 0, 0, 0, null, null, null));
                es.RaiseErrorEvent(null, new BuildErrorEventArgs(null, null, null, 0, 0, 0, 0, null, null, null));
                es.RaiseTaskFinishedEvent(null, new TaskFinishedEventArgs(null, null, null, null, "task", true));
                es.RaiseTargetFinishedEvent(null, new TargetFinishedEventArgs(null, null, "t", null, null, true));
                es.RaiseProjectFinishedEvent(null, new ProjectFinishedEventArgs(null, null, "p", true));
                es.RaiseBuildFinishedEvent(null, new BuildFinishedEventArgs(null, null, true));
                es.RaiseAnyEvent(null, new BuildFinishedEventArgs(null, null, true));
                es.RaiseStatusEvent(null, new BuildFinishedEventArgs(null, null, true));
                es.RaiseCustomEvent(null, new MyCustomBuildEventArgs2());
            // No exception raised
        }

        [Test]
        public void NullEventFieldsParallel()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Diagnostic,
                                                sc.Write, sc.SetColor,
                                                sc.ResetColor);
            L.Initialize(es, 2);
            BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

            BuildStartedEventArgs bse = new BuildStartedEventArgs(null, null);
            bse.BuildEventContext = buildEventContext;
            ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, null, null, "p", null, null, null, buildEventContext);
            pse.BuildEventContext = buildEventContext;
            TargetStartedEventArgs trse = new TargetStartedEventArgs(null, null, "t", null, null);
            trse.BuildEventContext = buildEventContext;
            TaskStartedEventArgs tase = new TaskStartedEventArgs(null, null, null, null, "task");
            tase.BuildEventContext = buildEventContext;
            BuildMessageEventArgs bmea = new BuildMessageEventArgs(null, null, null, MessageImportance.High);
            bmea.BuildEventContext = buildEventContext;
            BuildWarningEventArgs bwea = new BuildWarningEventArgs(null, null, null, 0, 0, 0, 0, null, null, null);
            bwea.BuildEventContext = buildEventContext;
            BuildErrorEventArgs beea = new BuildErrorEventArgs(null, null, null, 0, 0, 0, 0, null, null, null);
            beea.BuildEventContext = buildEventContext;
            TaskFinishedEventArgs trfea = new TaskFinishedEventArgs(null, null, null, null, "task", true);
            trfea.BuildEventContext = buildEventContext;
            TargetFinishedEventArgs tafea = new TargetFinishedEventArgs(null, null, "t", null, null, true);
            tafea.BuildEventContext = buildEventContext;
            ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs(null, null, "p", true);
            pfea.BuildEventContext = buildEventContext;
            BuildFinishedEventArgs bfea = new BuildFinishedEventArgs(null, null, true);
            bfea.BuildEventContext = buildEventContext;
            MyCustomBuildEventArgs2 mcea = new MyCustomBuildEventArgs2();
            mcea.BuildEventContext = buildEventContext;


            // Not all parameters are null here, but that's fine, we assume the engine will never
            // fire a ProjectStarted without a project name, etc.
            es.RaiseBuildStartedEvent(null, bse);
            es.RaiseProjectStartedEvent(null, pse);
            es.RaiseTargetStartedEvent(null, trse);
            es.RaiseTaskStartedEvent(null, tase);
            es.RaiseMessageEvent(null, bmea);
            es.RaiseWarningEvent(null, bwea);
            es.RaiseErrorEvent(null, beea);
            es.RaiseTaskFinishedEvent(null, trfea);
            es.RaiseTargetFinishedEvent(null, tafea);
            es.RaiseProjectFinishedEvent(null, pfea);
            es.RaiseBuildFinishedEvent(null, bfea);
            es.RaiseAnyEvent(null, bfea);
            es.RaiseStatusEvent(null, bfea);
            es.RaiseCustomEvent(null, mcea);
            // No exception raised
        }

        [Test]
        public void TestVerbosityLessThan()
        {
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals( false,
                (new SerialConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));
            
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals( true,
                (new SerialConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals(false,
                (new ParallelConsoleLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assertion.AssertEquals(true,
                (new ParallelConsoleLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));
        }

        /// <summary>
        /// Test of single message printing
        /// </summary>
        /// <owner>t-jeffv</owner>
        [Test]
        public void SingleMessageTests_quiet_low()
        {
                SingleMessageTest(LoggerVerbosity.Quiet,
                                 MessageImportance.Low, false);
        }

        [Test]
        public void SingleMessageTests_quiet_medium()
        {
                SingleMessageTest(LoggerVerbosity.Quiet,
                                 MessageImportance.Normal, false); 
        }

        [Test]
        public void SingleMessageTests_quiet_high()
        {
                SingleMessageTest(LoggerVerbosity.Quiet,
                                 MessageImportance.High, false);
        }

        [Test]
        public void SingleMessageTests_medium_low() 
        {
                SingleMessageTest(LoggerVerbosity.Minimal,
                                 MessageImportance.Low, false);
        }

        [Test]
        public void SingleMessageTests_medium_medium()
        {
                SingleMessageTest(LoggerVerbosity.Minimal,
                                 MessageImportance.Normal, false); 
        }

        [Test]
        public void SingleMessageTests_medium_high()
        {
                SingleMessageTest(LoggerVerbosity.Minimal,
                                 MessageImportance.High, true);
        }

        [Test]
        public void SingleMessageTests_normal_low()
        {
                SingleMessageTest(LoggerVerbosity.Normal,
                                 MessageImportance.Low, false);
        }

        [Test]
        public void SingleMessageTests_normal_medium()
        { 
                SingleMessageTest(LoggerVerbosity.Normal,
                                 MessageImportance.Normal, true); 
        }

        [Test]
        public void SingleMessageTests_normal_high()
        {
                SingleMessageTest(LoggerVerbosity.Normal,
                                 MessageImportance.High, true);
        }

        [Test]
        public void SingleMessageTests_detailed_low()
        {
                SingleMessageTest(LoggerVerbosity.Detailed,
                                 MessageImportance.Low, true);
        }

        [Test]
        public void SingleMessageTests_detailed_medium()
        {
                SingleMessageTest(LoggerVerbosity.Detailed,
                                 MessageImportance.Normal, true); 
        }

        [Test]
        public void SingleMessageTests_detailed_high()
        {
                SingleMessageTest(LoggerVerbosity.Detailed,
                                 MessageImportance.High, true);
        }

        [Test]
        public void SingleMessageTests_diagnostic_low()
        {
                SingleMessageTest(LoggerVerbosity.Diagnostic,
                                 MessageImportance.Low, true);
        }

        [Test]
        public void SingleMessageTests_diagnostic_medium() 
        {
                SingleMessageTest(LoggerVerbosity.Diagnostic,
                                 MessageImportance.Normal, true); 
        }

        [Test]
        public void SingleMessageTests_diagnostic_high()
        {
                SingleMessageTest(LoggerVerbosity.Diagnostic,
                                 MessageImportance.High, true);
        }

        [Test]
        public void ErrorColorTest()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();

            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet, sc.Write, sc.SetColor, sc.ResetColor);
            L.Initialize(es);

            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC", "31415", "file.vb", 42, 0, 0, 0, "Some long message", "help", "sender");
            es.RaiseErrorEvent(null, beea);
            Assertion.AssertEquals("<red>file.vb(42): VBC error 31415: Some long message" + Environment.NewLine + "<reset color>", sc.ToString());
        }

        [Test]
        public void ErrorColorTestParallel()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();

            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                sc.Write, sc.SetColor,
                                                sc.ResetColor);
            L.Initialize(es, 4);

            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");

            beea.BuildEventContext = new BuildEventContext(1, 2, 3, 4);

            es.RaiseErrorEvent(null, beea);

            Assertion.AssertEquals(
               "<red>file.vb(42): VBC error 31415: Some long message" +
               Environment.NewLine +"<reset color>",
               sc.ToString());
        }

        [Test]
        public void WarningColorTest()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                sc.Write, sc.SetColor,
                                                sc.ResetColor);
            L.Initialize(es);

            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");

            es.RaiseWarningEvent(null, bwea);

            Assertion.AssertEquals(
               "<yellow>file.vb(42): VBC warning 31415: Some long message" +
               Environment.NewLine + "<reset color>",
               sc.ToString());
        }

        [Test]
        public void WarningColorTestParallel()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                sc.Write, sc.SetColor,
                                                sc.ResetColor);
            L.Initialize(es, 2);

            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");

            bwea.BuildEventContext = new BuildEventContext(1, 2, 3, 4);
            es.RaiseWarningEvent(null, bwea);

            Assertion.AssertEquals(
               "<yellow>file.vb(42): VBC warning 31415: Some long message" +
               Environment.NewLine + "<reset color>",
               sc.ToString());
        }

        [Test]
        public void LowMessageColorTest()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Diagnostic,
                                                sc.Write, sc.SetColor,
                                                sc.ResetColor);
            L.Initialize(es);

            BuildMessageEventArgs msg =
                new BuildMessageEventArgs("text", "help", "sender",
                                          MessageImportance.Low);

            es.RaiseMessageEvent(null, msg);

            Assertion.AssertEquals(
               "<darkgray>text" +
               Environment.NewLine + "<reset color>",
               sc.ToString());
        }

        [Test]
        public void TestQuietWithHighMessage()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                    sc.Write, sc.SetColor,
                                                    sc.ResetColor);
                L.Initialize(es, i);

                BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

                BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
                bse.BuildEventContext = buildEventContext;
                es.RaiseBuildStartedEvent(null, bse);

                ProjectStartedEventArgs pse = new ProjectStartedEventArgs(1,"ps", null, "fname", "", null, null,new BuildEventContext(1, 1, 1, 1));
                pse.BuildEventContext = buildEventContext;
                es.RaiseProjectStartedEvent(null, pse);

                TargetStartedEventArgs trse = new TargetStartedEventArgs("ts", null, "trname", "pfile", "tfile");
                trse.BuildEventContext = buildEventContext;
                es.RaiseTargetStartedEvent(null, trse);

                TaskStartedEventArgs tase = new TaskStartedEventArgs("tks", null, "tname", "tfname", "tsname");
                tase.BuildEventContext = buildEventContext;
                es.RaiseTaskStartedEvent(null, tase);

                BuildMessageEventArgs bmea = new BuildMessageEventArgs("foo!", null, "sender", MessageImportance.High);
                bmea.BuildEventContext = buildEventContext;
                es.RaiseMessageEvent(null, bmea);

                TaskFinishedEventArgs tafea = new TaskFinishedEventArgs("tkf", null, "fname", "tsname", "tfname", true);
                tafea.BuildEventContext = buildEventContext;
                es.RaiseTaskFinishedEvent(null, tafea);

                TargetFinishedEventArgs trfea = new TargetFinishedEventArgs("tf", null, "trname", "fname", "tfile", true);
                trfea.BuildEventContext = buildEventContext;
                es.RaiseTargetFinishedEvent(null, trfea);

                ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs("pf", null, "fname", true);
                pfea.BuildEventContext = buildEventContext;
                es.RaiseProjectFinishedEvent(null, pfea);

                BuildFinishedEventArgs bfea = new BuildFinishedEventArgs("bf", null, true);
                bfea.BuildEventContext = buildEventContext;
                es.RaiseBuildFinishedEvent(null, bfea);

                Assertion.AssertEquals(String.Empty, sc.ToString());
            }
        }

        [Test]
        public void TestQuietWithError()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                    sc.Write, sc.SetColor, sc.ResetColor);
                L.Initialize(es, i);

                BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

                BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
                bse.BuildEventContext = buildEventContext;
                es.RaiseBuildStartedEvent(null, bse);

                ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1,"ps", null, "fname", "", null, null, new BuildEventContext(1, 2, 3, 4));
                pse.BuildEventContext = buildEventContext;
                es.RaiseProjectStartedEvent(null, pse);

                TargetStartedEventArgs trse = new TargetStartedEventArgs("ts", null, "trname", "pfile", "tfile");
                trse.BuildEventContext = buildEventContext;
                es.RaiseTargetStartedEvent(null, trse);

                TaskStartedEventArgs tase = new TaskStartedEventArgs("tks", null, "tname", "tfname", "tsname");
                tase.BuildEventContext = buildEventContext;
                es.RaiseTaskStartedEvent(null, tase);

                BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                                "31415", "file.vb", 42, 0, 0, 0,
                                "Some long message", "help", "sender");
                
                beea.BuildEventContext = buildEventContext;
                es.RaiseErrorEvent(null, beea);

                TaskFinishedEventArgs tafea = new TaskFinishedEventArgs("tkf", null, "fname", "tsname", "tfname", true);
                tafea.BuildEventContext = buildEventContext;
                es.RaiseTaskFinishedEvent(null, tafea);

                TargetFinishedEventArgs trfea = new TargetFinishedEventArgs("tf", null, "trname", "fname", "tfile", true);
                trfea.BuildEventContext = buildEventContext;
                es.RaiseTargetFinishedEvent(null, trfea);

                ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs("pf", null, "fname", true);
                pfea.BuildEventContext = buildEventContext;
                es.RaiseProjectFinishedEvent(null, pfea);

                BuildFinishedEventArgs bfea = new BuildFinishedEventArgs("bf", null, true);
                bfea.BuildEventContext = buildEventContext;
                es.RaiseBuildFinishedEvent(null, bfea);

                Console.WriteLine("==");
                Console.WriteLine(sc.ToString());
                Console.WriteLine("==");

                if (i == 1)
                {
                    Assertion.AssertEquals(
                            "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                            ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", "fname") + Environment.NewLine + Environment.NewLine +
                            "<reset color><red>file.vb(42): VBC error 31415: Some long message" + Environment.NewLine +
                            "<reset color><cyan>pf" + Environment.NewLine +
                            "<reset color>",
                            sc.ToString());
                }
                else
                {
                    Assertion.AssertEquals(
                            "<red>file.vb(42): VBC error 31415: Some long message" + Environment.NewLine + "<reset color>",
                            sc.ToString());
                }
            }
        }

        /// <summary>
        /// Quiet build with a warning; project finished should appear
        /// but not target finished
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestQuietWithWarning()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                    sc.Write, sc.SetColor, sc.ResetColor);
                L.Initialize(es, i);

                BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

                BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
                bse.BuildEventContext = buildEventContext;
                es.RaiseBuildStartedEvent(null, bse);

                ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, "ps", null, "fname", "", null, null, new BuildEventContext(1, 2, 3, 4));
                pse.BuildEventContext = buildEventContext;
                es.RaiseProjectStartedEvent(null, pse);

                TargetStartedEventArgs trse = new TargetStartedEventArgs("ts", null, "trname", "pfile", "tfile");
                trse.BuildEventContext = buildEventContext;
                es.RaiseTargetStartedEvent(null, trse);

                TaskStartedEventArgs tase = new TaskStartedEventArgs("tks", null, "tname", "tfname", "tsname");
                tase.BuildEventContext = buildEventContext;
                es.RaiseTaskStartedEvent(null, tase);

                BuildWarningEventArgs beea = new BuildWarningEventArgs("VBC",
                                "31415", "file.vb", 42, 0, 0, 0,
                                "Some long message", "help", "sender");


                beea.BuildEventContext = buildEventContext;
                es.RaiseWarningEvent(null, beea);

                TaskFinishedEventArgs tafea = new TaskFinishedEventArgs("tkf", null, "fname", "tsname", "tfname", true);
                tafea.BuildEventContext = buildEventContext;
                es.RaiseTaskFinishedEvent(null, tafea);

                TargetFinishedEventArgs trfea = new TargetFinishedEventArgs("tf", null, "trname", "fname", "tfile", true);
                trfea.BuildEventContext = buildEventContext;
                es.RaiseTargetFinishedEvent(null, trfea);

                ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs("pf", null, "fname", true);
                pfea.BuildEventContext = buildEventContext;
                es.RaiseProjectFinishedEvent(null, pfea);

                BuildFinishedEventArgs bfea = new BuildFinishedEventArgs("bf", null, true);
                bfea.BuildEventContext = buildEventContext;
                es.RaiseBuildFinishedEvent(null, bfea);

                Console.WriteLine("==");
                Console.WriteLine(sc.ToString());
                Console.WriteLine("==");

                if (i == 1)
                {
                    Assertion.AssertEquals(
                            "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                            ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", "fname") + Environment.NewLine + Environment.NewLine +
                            "<reset color><yellow>file.vb(42): VBC warning 31415: Some long message" + Environment.NewLine +
                            "<reset color><cyan>pf" + Environment.NewLine +
                            "<reset color>",
                            sc.ToString());
                }
                else
                {
                    Assertion.AssertEquals(
                            "<yellow>file.vb(42): VBC warning 31415: Some long message" + Environment.NewLine + "<reset color>",
                            sc.ToString());
                }
            }
        }

        /// <summary>
        /// Minimal with no errors or warnings should emit nothing.
        /// </summary>
        [Test]
        public void TestMinimalWithNormalMessage()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Minimal,
                                                    sc.Write, sc.SetColor,
                                                    sc.ResetColor);
                L.Initialize(es, i);

                BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

                BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
                bse.BuildEventContext = buildEventContext;
                es.RaiseBuildStartedEvent(null, bse);

                ProjectStartedEventArgs pse = new ProjectStartedEventArgs(1,"ps", null, "fname", "", null, null, new BuildEventContext(1, 1, 1, 1));
                pse.BuildEventContext = buildEventContext;
                es.RaiseProjectStartedEvent(null, pse);

                TargetStartedEventArgs trse = new TargetStartedEventArgs("ts", null, "trname", "pfile", "tfile");
                trse.BuildEventContext = buildEventContext;
                es.RaiseTargetStartedEvent(null, trse);

                TaskStartedEventArgs tase = new TaskStartedEventArgs("tks", null, "tname", "tfname", "tsname");
                tase.BuildEventContext = buildEventContext;
                es.RaiseTaskStartedEvent(null, tase);

                BuildMessageEventArgs bmea = new BuildMessageEventArgs("foo!", null, "sender", MessageImportance.Normal);
                bmea.BuildEventContext = buildEventContext;
                es.RaiseMessageEvent(null, bmea);

                TaskFinishedEventArgs tafea = new TaskFinishedEventArgs("tkf", null, "fname", "tsname", "tfname", true);
                tafea.BuildEventContext = buildEventContext;
                es.RaiseTaskFinishedEvent(null, tafea);

                TargetFinishedEventArgs trfea = new TargetFinishedEventArgs("tf", null, "trname", "fname", "tfile", true);
                trfea.BuildEventContext = buildEventContext;
                es.RaiseTargetFinishedEvent(null, trfea);

                ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs("pf", null, "fname", true);
                pfea.BuildEventContext = buildEventContext;
                es.RaiseProjectFinishedEvent(null, pfea);

                BuildFinishedEventArgs bfea = new BuildFinishedEventArgs("bf", null, true);
                bfea.BuildEventContext = buildEventContext;
                es.RaiseBuildFinishedEvent(null, bfea);

                Assertion.AssertEquals(String.Empty, sc.ToString());
            }
        }

        /// <summary>
        /// Minimal with error should emit project started, the error, and project finished
        /// </summary>
        [Test]
        public void TestMinimalWithError()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Minimal,
                                                    sc.Write, sc.SetColor, sc.ResetColor);
                L.Initialize(es, i);

                BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

                BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
                bse.BuildEventContext = buildEventContext;
                es.RaiseBuildStartedEvent(null, bse);

                ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, "ps", null, "fname", "", null, null, new BuildEventContext(1, 2, 3, 4));
                pse.BuildEventContext = buildEventContext;
                es.RaiseProjectStartedEvent(null, pse);

                TargetStartedEventArgs trse = new TargetStartedEventArgs("ts", null, "trname", "pfile", "tfile");
                trse.BuildEventContext = buildEventContext;
                es.RaiseTargetStartedEvent(null, trse);

                TaskStartedEventArgs tase = new TaskStartedEventArgs("tks", null, "tname", "tfname", "tsname");
                tase.BuildEventContext = buildEventContext;
                es.RaiseTaskStartedEvent(null, tase);

                BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                                "31415", "file.vb", 42, 0, 0, 0,
                                "Some long message", "help", "sender");

                beea.BuildEventContext = buildEventContext;
                es.RaiseErrorEvent(null, beea);

                TaskFinishedEventArgs tafea = new TaskFinishedEventArgs("tkf", null, "fname", "tsname", "tfname", true);
                tafea.BuildEventContext = buildEventContext;
                es.RaiseTaskFinishedEvent(null, tafea);

                TargetFinishedEventArgs trfea = new TargetFinishedEventArgs("tf", null, "trname", "fname", "tfile", true);
                trfea.BuildEventContext = buildEventContext;
                es.RaiseTargetFinishedEvent(null, trfea);

                ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs("pf", null, "fname", true);
                pfea.BuildEventContext = buildEventContext;
                es.RaiseProjectFinishedEvent(null, pfea);

                BuildFinishedEventArgs bfea = new BuildFinishedEventArgs("bf", null, true);
                bfea.BuildEventContext = buildEventContext;
                es.RaiseBuildFinishedEvent(null, bfea);

                Console.WriteLine("==");
                Console.WriteLine(sc.ToString());
                Console.WriteLine("==");

                if (i == 1)
                {
                    Assertion.AssertEquals(
                            "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                            ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", "fname") + Environment.NewLine + Environment.NewLine +
                            "<reset color><red>file.vb(42): VBC error 31415: Some long message" + Environment.NewLine +
                            "<reset color><cyan>pf" + Environment.NewLine +
                            "<reset color>",
                            sc.ToString());
                }
                else
                {
                    Assertion.AssertEquals(
                            "<red>file.vb(42): VBC error 31415: Some long message" + Environment.NewLine + "<reset color>",
                            sc.ToString());
                }
            }
        }

        /// <summary>
        /// Minimal with warning should emit project started, the warning, and project finished
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestMinimalWithWarning()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                    sc.Write, sc.SetColor, sc.ResetColor);
                L.Initialize(es, i);

                BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

                BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
                bse.BuildEventContext = buildEventContext;
                es.RaiseBuildStartedEvent(null, bse);

                ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, "ps", null, "fname", "", null, null, new BuildEventContext(1, 2, 3, 4));
                pse.BuildEventContext = buildEventContext;
                es.RaiseProjectStartedEvent(null, pse);

                TargetStartedEventArgs trse = new TargetStartedEventArgs("ts", null, "trname", "pfile", "tfile");
                trse.BuildEventContext = buildEventContext;
                es.RaiseTargetStartedEvent(null, trse);

                TaskStartedEventArgs tase = new TaskStartedEventArgs("tks", null, "tname", "tfname", "tsname");
                tase.BuildEventContext = buildEventContext;
                es.RaiseTaskStartedEvent(null, tase);

                BuildWarningEventArgs beea = new BuildWarningEventArgs("VBC",
                                "31415", "file.vb", 42, 0, 0, 0,
                                "Some long message", "help", "sender");


                beea.BuildEventContext = buildEventContext;
                es.RaiseWarningEvent(null, beea);

                TaskFinishedEventArgs tafea = new TaskFinishedEventArgs("tkf", null, "fname", "tsname", "tfname", true);
                tafea.BuildEventContext = buildEventContext;
                es.RaiseTaskFinishedEvent(null, tafea);

                TargetFinishedEventArgs trfea = new TargetFinishedEventArgs("tf", null, "trname", "fname", "tfile", true);
                trfea.BuildEventContext = buildEventContext;
                es.RaiseTargetFinishedEvent(null, trfea);

                ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs("pf", null, "fname", true);
                pfea.BuildEventContext = buildEventContext;
                es.RaiseProjectFinishedEvent(null, pfea);

                BuildFinishedEventArgs bfea = new BuildFinishedEventArgs("bf", null, true);
                bfea.BuildEventContext = buildEventContext;
                es.RaiseBuildFinishedEvent(null, bfea);

                Console.WriteLine("==");
                Console.WriteLine(sc.ToString());
                Console.WriteLine("==");

                if (i == 1)
                {
                    Assertion.AssertEquals(
                            "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                            ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", "fname") + Environment.NewLine + Environment.NewLine +
                            "<reset color><yellow>file.vb(42): VBC warning 31415: Some long message" + Environment.NewLine +
                            "<reset color><cyan>pf" + Environment.NewLine +
                            "<reset color>",
                            sc.ToString());
                }
                else
                {
                    Assertion.AssertEquals(
                            "<yellow>file.vb(42): VBC warning 31415: Some long message" + Environment.NewLine + "<reset color>",
                            sc.ToString());
                }
            }
        }

        /// <summary>
        /// Minimal with warning should emit project started, the warning, and project finished
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TestDirectEventHandlers()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Quiet,
                                                    sc.Write, sc.SetColor, sc.ResetColor);
                L.Initialize(es, i);

                BuildEventContext buildEventContext = new BuildEventContext(1, 2, 3, 4);

                BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
                bse.BuildEventContext = buildEventContext;
                L.BuildStartedHandler(null, bse);

                ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, "ps", null, "fname", "", null, null, new BuildEventContext(1, 2, 3, 4));
                pse.BuildEventContext = buildEventContext;
                L.ProjectStartedHandler(null, pse);

                TargetStartedEventArgs trse = new TargetStartedEventArgs("ts", null, "trname", "pfile", "tfile");
                trse.BuildEventContext = buildEventContext;
                L.TargetStartedHandler(null, trse);

                TaskStartedEventArgs tase = new TaskStartedEventArgs("tks", null, "tname", "tfname", "tsname");
                tase.BuildEventContext = buildEventContext;
                L.TaskStartedHandler(null, tase);

                BuildWarningEventArgs beea = new BuildWarningEventArgs("VBC",
                                "31415", "file.vb", 42, 0, 0, 0,
                                "Some long message", "help", "sender");


                beea.BuildEventContext = buildEventContext;
                L.WarningHandler(null, beea);

                TaskFinishedEventArgs tafea = new TaskFinishedEventArgs("tkf", null, "fname", "tsname", "tfname", true);
                tafea.BuildEventContext = buildEventContext;
                L.TaskFinishedHandler(null, tafea);

                TargetFinishedEventArgs trfea = new TargetFinishedEventArgs("tf", null, "trname", "fname", "tfile", true);
                trfea.BuildEventContext = buildEventContext;
                L.TargetFinishedHandler(null, trfea);

                ProjectFinishedEventArgs pfea = new ProjectFinishedEventArgs("pf", null, "fname", true);
                pfea.BuildEventContext = buildEventContext;
                L.ProjectFinishedHandler(null, pfea);

                BuildFinishedEventArgs bfea = new BuildFinishedEventArgs("bf", null, true);
                bfea.BuildEventContext = buildEventContext;
                L.BuildFinishedHandler(null, bfea);

                Console.WriteLine("==");
                Console.WriteLine(sc.ToString());
                Console.WriteLine("==");

                if (i == 1)
                {
                    Assertion.AssertEquals(
                            "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                            ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", "fname") + Environment.NewLine + Environment.NewLine +
                            "<reset color><yellow>file.vb(42): VBC warning 31415: Some long message" + Environment.NewLine +
                            "<reset color><cyan>pf" + Environment.NewLine +
                            "<reset color>",
                            sc.ToString());
                }
                else
                {
                    Assertion.AssertEquals(
                            "<yellow>file.vb(42): VBC warning 31415: Some long message" + Environment.NewLine + "<reset color>",
                            sc.ToString());
                }
            }
        }

        [Test]
        public void SingleLineFormatNoop()
        {
            string s = "foo";
            SerialConsoleLogger cl = new SerialConsoleLogger();

            string ss = cl.IndentString(s, 0);

            //should be a no-op
            Assertion.AssertEquals("foo" + Environment.NewLine, ss); 
        }

        [Test]
        public void MultilineFormatWindowsLineEndings()
        {
            string newline = "\r\n";
            string s = "foo" + newline + "bar" +
                       newline + "baz" + newline;
            SerialConsoleLogger cl = new SerialConsoleLogger();

            string ss = cl.IndentString(s, 4);

            //should convert lines to system format
            Assertion.AssertEquals("    foo" + Environment.NewLine +
                                   "    bar" + Environment.NewLine +
                                   "    baz" + Environment.NewLine + 
                                   "    " + Environment.NewLine, ss); 
        }

        [Test]
        public void MultilineFormatUnixLineEndings()
        {
            string s = "foo\nbar\nbaz\n";
            SerialConsoleLogger cl = new SerialConsoleLogger();

            string ss = cl.IndentString(s, 0);

            //should convert lines to system format
            Assertion.AssertEquals("foo" + Environment.NewLine +
                                   "bar" + Environment.NewLine +
                                   "baz" + Environment.NewLine + Environment.NewLine, ss); 
        }

        [Test]
        public void MultilineFormatMixedLineEndings()
        {
            string s = "foo" + "\r\n\r\n" + "bar" + "\n" + "baz" + "\n\r\n\n" +
                "jazz" + "\r\n" + "razz" + "\n\n" + "matazz" + "\n" + "end";

            SerialConsoleLogger cl = new SerialConsoleLogger();

            string ss = cl.IndentString(s, 0);

            //should convert lines to system format
            Assertion.AssertEquals("foo" + Environment.NewLine + Environment.NewLine +
                                   "bar" + Environment.NewLine +
                                   "baz" + Environment.NewLine + Environment.NewLine + Environment.NewLine +
                                   "jazz" + Environment.NewLine +
                                   "razz" + Environment.NewLine + Environment.NewLine +
                                   "matazz" + Environment.NewLine +
                                   "end" + Environment.NewLine, ss);
        }

        [Test]
        public void NestedProjectMinimal()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Minimal,
                                                sc.Write, sc.SetColor, sc.ResetColor);
            L.Initialize(es, 1);

            es.RaiseBuildStartedEvent(null,
                          new BuildStartedEventArgs("bs", null));

            //Clear time dependant build started message
            sc.Clear();

            es.RaiseProjectStartedEvent(null,
                          new ProjectStartedEventArgs("ps1", null, "fname1", "", null, null));

            es.RaiseTargetStartedEvent(null,
                          new TargetStartedEventArgs("ts", null,
                                                     "trname", "fname", "tfile"));

            es.RaiseProjectStartedEvent(null,
                          new ProjectStartedEventArgs("ps2", null, "fname2", "", null, null));

            Assertion.AssertEquals(string.Empty, sc.ToString());

            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");

            es.RaiseErrorEvent(null, beea);

            Assertion.AssertEquals(
                "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", "fname1") + Environment.NewLine +
                                        Environment.NewLine + "<reset color>" +
                "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                ResourceUtilities.FormatResourceString("ProjectStartedPrefixForNestedProjectWithDefaultTargets", "fname1", "fname2") + Environment.NewLine +
                                                      Environment.NewLine + "<reset color>" +
                "<red>" + "file.vb(42): VBC error 31415: Some long message" +
                                                      Environment.NewLine + "<reset color>",
                sc.ToString());
        }

        [Test]
        public void NestedProjectNormal()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal,
                                                sc.Write, sc.SetColor, sc.ResetColor);
            L.Initialize(es);

            es.RaiseBuildStartedEvent(null,
                          new BuildStartedEventArgs("bs", null));


            //Clear time dependant build started message
            string expectedOutput = null;
            string actualOutput = null;
            sc.Clear();

            es.RaiseProjectStartedEvent(null,
                          new ProjectStartedEventArgs("ps1", null, "fname1", "", null, null));

            #region Check
            expectedOutput =
                        "<cyan>" + BaseConsoleLogger.projectSeparatorLine + Environment.NewLine +
                        ResourceUtilities.FormatResourceString("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", "fname1") + Environment.NewLine +
                        Environment.NewLine + "<reset color>";
            actualOutput = sc.ToString();

            Assertion.AssertEquals(expectedOutput, actualOutput);
            Console.WriteLine("1 [" + expectedOutput + "] [" + actualOutput + "]");
            sc.Clear();
            #endregion

            es.RaiseTargetStartedEvent(null,
                          new TargetStartedEventArgs("ts", null,
                                                     "tarname", "fname", "tfile"));
            #region Check
            expectedOutput = String.Empty;
            actualOutput = sc.ToString();

            Console.WriteLine("2 [" + expectedOutput + "] [" + actualOutput + "]");
            Assertion.AssertEquals(expectedOutput, actualOutput);
            sc.Clear();
            #endregion

            es.RaiseTaskStartedEvent(null, new TaskStartedEventArgs("", "", "", "", "Exec"));
            es.RaiseProjectStartedEvent(null,
                          new ProjectStartedEventArgs("ps2", null, "fname2", "", null, null));

            #region Check
            expectedOutput =
                "<cyan>" + ResourceUtilities.FormatResourceString("TargetStartedPrefix", "tarname") + Environment.NewLine + "<reset color>"
                + "<cyan>" + "    " + BaseConsoleLogger.projectSeparatorLine
                                          + Environment.NewLine +
                "    " + ResourceUtilities.FormatResourceString("ProjectStartedPrefixForNestedProjectWithDefaultTargets", "fname1", "fname2") + Environment.NewLine +
                Environment.NewLine + "<reset color>";
            actualOutput = sc.ToString();

            Console.WriteLine("3 [" + expectedOutput + "] [" + actualOutput + "]");
            Assertion.AssertEquals(expectedOutput, actualOutput);
            sc.Clear();
            #endregion

            es.RaiseProjectFinishedEvent(null,
                          new ProjectFinishedEventArgs("pf2", null, "fname2", true));
            es.RaiseTaskFinishedEvent(null, new TaskFinishedEventArgs("", "", "", "", "Exec", true));

            #region Check
            expectedOutput = String.Empty;
            actualOutput = sc.ToString();

            Console.WriteLine("4 [" + expectedOutput + "] [" + actualOutput + "]");
            Assertion.AssertEquals(expectedOutput, actualOutput);
            sc.Clear();
            #endregion

            es.RaiseTargetFinishedEvent(null,
                          new TargetFinishedEventArgs("tf", null, "tarname", "fname", "tfile", true));

            #region Check
            expectedOutput = String.Empty;
            actualOutput = sc.ToString();

            Console.WriteLine("5 [" + expectedOutput + "] [" + actualOutput + "]");
            Assertion.AssertEquals(expectedOutput, actualOutput);
            sc.Clear();
            #endregion

            es.RaiseProjectFinishedEvent(null,
                          new ProjectFinishedEventArgs("pf1", null, "fname1", true));

            #region Check
            expectedOutput = String.Empty;
            actualOutput = sc.ToString();

            Console.WriteLine("6 [" + expectedOutput + "] [" + actualOutput + "]");
            Assertion.AssertEquals(expectedOutput, actualOutput);
            sc.Clear();
            #endregion

            es.RaiseBuildFinishedEvent(null,
                          new BuildFinishedEventArgs("bf", null, true));

            #region Check
            expectedOutput = "<green>" + Environment.NewLine + "bf" +
                        Environment.NewLine + "<reset color>" +
                "    " + ResourceUtilities.FormatResourceString("WarningCount", 0) +
                        Environment.NewLine + "<reset color>" +
                "    " + ResourceUtilities.FormatResourceString("ErrorCount", 0) +
                        Environment.NewLine + "<reset color>" +
                        Environment.NewLine;

            // Would like to add...
            //    + ResourceUtilities.FormatResourceString("TimeElapsed", String.Empty);
            // ...but this assumes that the time goes on the far right in every locale.

            actualOutput = sc.ToString().Substring(0, expectedOutput.Length);

            Console.WriteLine("7 [" + expectedOutput + "] [" + actualOutput + "]");
            Assertion.AssertEquals(expectedOutput, actualOutput);
            sc.Clear();
            #endregion

        }

        [Test]
        public void CustomDisplayedAtDetailed()
        {
            EventSource es = new EventSource(); 
            SimulatedConsole sc = new SimulatedConsole(); 
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Detailed,
                                                sc.Write, null, null);
            L.Initialize(es); 

            MyCustomBuildEventArgs c = 
                    new MyCustomBuildEventArgs("msg");

            es.RaiseCustomEvent(null, c);

            Assertion.AssertEquals("msg" + Environment.NewLine, 
                                   sc.ToString()); 
        }

        [Test]
        public void CustomDisplayedAtDiagnosticMP()
        {
            EventSource es = new EventSource();
            SimulatedConsole sc = new SimulatedConsole();
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Diagnostic,
                                                sc.Write, null, null);
            L.Initialize(es,2);

            MyCustomBuildEventArgs c =
                    new MyCustomBuildEventArgs("msg");
            c.BuildEventContext = new BuildEventContext(1, 1, 1, 1);
            es.RaiseCustomEvent(null, c);

            Assertion.Assert(sc.ToString().Contains("msg"));
        }

        [Test]
        public void CustomNotDisplayedAtNormal()
        {
            EventSource es = new EventSource(); 
            SimulatedConsole sc = new SimulatedConsole(); 
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal,
                                                sc.Write, null, null);
            L.Initialize(es); 

            MyCustomBuildEventArgs c = 
                    new MyCustomBuildEventArgs("msg");

            es.RaiseCustomEvent(null, c);

            Assertion.AssertEquals(String.Empty, sc.ToString()); 
        }

        /// <summary>
        /// Create some properties and log them
        /// </summary>
        /// <param name="cl"></param>
        /// <returns></returns>
        private void WriteAndValidateProperties(BaseConsoleLogger cl, SimulatedConsole sc, bool expectToSeeLogging)
        {

            Hashtable properties = new Hashtable();
            properties.Add("prop1", "val1");
            properties.Add("prop2", "val2");
            string prop1 = string.Empty;
            string prop2 = string.Empty;

            if (cl is SerialConsoleLogger)
            {
                ArrayList propertyList = ((SerialConsoleLogger) cl).ExtractPropertyList(properties);
                ((SerialConsoleLogger)cl).WriteProperties(propertyList);
                prop1 = String.Format(CultureInfo.CurrentCulture, "{0,-30} = {1}", "prop1", "val1");
                prop2 = String.Format(CultureInfo.CurrentCulture, "{0,-30} = {1}", "prop2", "val2");
            }
            else
            {
                BuildEventArgs buildEvent = new BuildErrorEventArgs("", "", "", 0, 0, 0, 0, "", "", "");
                buildEvent.BuildEventContext = new BuildEventContext(1, 2, 3, 4);
                ((ParallelConsoleLogger)cl).WriteProperties(buildEvent, properties);
                 prop1 = String.Format(CultureInfo.CurrentCulture, "{0} = {1}", "prop1", "val1");
                 prop2 = String.Format(CultureInfo.CurrentCulture, "{0} = {1}", "prop2", "val2");
            }
            string log = sc.ToString();

            Console.WriteLine("[" + log + "]");


            // Being careful not to make locale assumptions here, eg about sorting
            if (expectToSeeLogging)
            {
                Assertion.Assert(log.Contains(prop1));
                Assertion.Assert(log.Contains(prop2));
            }
            else
            {
                Assertion.Assert(!log.Contains(prop1));
                Assertion.Assert(!log.Contains(prop2));
            }
        }

        /// <summary>
        /// Basic test of properties list display
        /// </summary>
        [Test]
        public void DisplayPropertiesList()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger cl = new SerialConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);

            WriteAndValidateProperties(cl, sc, true);

            sc = new SimulatedConsole();
            ParallelConsoleLogger cl2 = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);

            WriteAndValidateProperties(cl2, sc, true);
        }

        /// <summary>
        /// Basic test of properties list not being displayed except in Diagnostic
        /// </summary>
        [Test]
        public void DoNotDisplayPropertiesListInDetailed()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger cl = new SerialConsoleLogger(LoggerVerbosity.Detailed, sc.Write, null, null);

            WriteAndValidateProperties(cl, sc, false);

            sc = new SimulatedConsole();
            ParallelConsoleLogger cl2 = new ParallelConsoleLogger(LoggerVerbosity.Detailed, sc.Write, null, null);

            WriteAndValidateProperties(cl2, sc, false);

        }

        /// <summary>
        /// Basic test of properties list not being displayed when disabled
        /// </summary>
        [Test]
        public void DoNotDisplayPropertiesListIfDisabled()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger cl = new SerialConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
            cl.Parameters = "noitemandpropertylist";
            cl.ParseParameters();

            WriteAndValidateProperties(cl, sc, false);

            sc = new SimulatedConsole();
            ParallelConsoleLogger cl2 = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
            cl2.Parameters = "noitemandpropertylist";
            cl2.ParseParameters();

            WriteAndValidateProperties(cl, sc, false);
        }       

        /// <summary>
        /// Create some items and log them
        /// </summary>
        /// <returns></returns>
        private void WriteAndValidateItems(BaseConsoleLogger cl, SimulatedConsole sc, bool expectToSeeLogging)
        {
            Hashtable items = new Hashtable();
            items.Add("type", (ITaskItem)new TaskItem("spec"));
            items.Add("type2", (ITaskItem)new TaskItem("spec2"));

            string item1type = string.Empty;
            string item2type = string.Empty;
            string item1spec = string.Empty;
            string item2spec = string.Empty;

            if (cl is SerialConsoleLogger)
            {
                SortedList itemList = ((SerialConsoleLogger)cl).ExtractItemList(items);
                ((SerialConsoleLogger)cl).WriteItems(itemList);
                item1spec = "spec" + Environment.NewLine;
                item2spec = "spec2" + Environment.NewLine;
            }
            else
            {
                BuildEventArgs buildEvent = new BuildErrorEventArgs("", "", "", 0, 0, 0, 0, "", "", "");
                buildEvent.BuildEventContext = new BuildEventContext(1, 2, 3, 4);
                ((ParallelConsoleLogger)cl).WriteItems(buildEvent, items);
                item1spec = Environment.NewLine + "    spec" + Environment.NewLine;
                item2spec = Environment.NewLine + "    spec2" + Environment.NewLine;
            }

            item1type = "type" + Environment.NewLine;
            item2type = "type2" + Environment.NewLine;
            
            string log = sc.ToString();

            Console.WriteLine("[" + log + "]");



            // Being careful not to make locale assumptions here, eg about sorting
            if (expectToSeeLogging)
            {
                Assertion.Assert(log.Contains(item1type));
                Assertion.Assert(log.Contains(item2type));
                Assertion.Assert(log.Contains(item1spec));
                Assertion.Assert(log.Contains(item2spec));
            }
            else
            {
                Assertion.Assert(!log.Contains(item1type));
                Assertion.Assert(!log.Contains(item2type));
                Assertion.Assert(!log.Contains(item1spec));
                Assertion.Assert(!log.Contains(item2spec));
            }
        }

        /// <summary>
        /// Verify passing in an empty item list does not print anything out
        /// </summary>
        /// <returns></returns>
        [Test]
        public void WriteItemsEmptyList()
        {
            Hashtable items = new Hashtable();

            for (int i = 0; i < 2; i++)
            {
                BaseConsoleLogger cl = null;
                SimulatedConsole sc = new SimulatedConsole();
                if (i == 0)
                {
                    cl = new SerialConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
                }
                else
                {
                    cl = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
                }

                if (cl is SerialConsoleLogger)
                {
                    SortedList itemList = ((SerialConsoleLogger)cl).ExtractItemList(items);
                    ((SerialConsoleLogger)cl).WriteItems(itemList);
                }
                else
                {
                    BuildEventArgs buildEvent = new BuildErrorEventArgs("", "", "", 0, 0, 0, 0, "", "", "");
                    buildEvent.BuildEventContext = new BuildEventContext(1, 2, 3, 4);
                    ((ParallelConsoleLogger)cl).WriteItems(buildEvent, items);
                }

                string log = sc.ToString();

                // There should be nothing in the log
                Assert.IsTrue(log.Length == 0, "Iteration of I: " + i);
                Console.WriteLine("Iteration of i: " + i + "[" + log + "]");
            }
        }

        /// <summary>
        /// Verify passing in an empty item list does not print anything out
        /// </summary>
        /// <returns></returns>
        [Test]
        public void WritePropertiesEmptyList()
        {
            Hashtable properties = new Hashtable();


            for (int i = 0; i < 2; i++)
            {
                BaseConsoleLogger cl = null;
                SimulatedConsole sc = new SimulatedConsole();
                if (i == 0)
                {
                    cl = new SerialConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
                }
                else
                {
                    cl = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
                }

                if (cl is SerialConsoleLogger)
                {
                    ArrayList propertyList = ((SerialConsoleLogger)cl).ExtractPropertyList(properties);
                    ((SerialConsoleLogger)cl).WriteProperties(propertyList);
                }
                else
                {
                    BuildEventArgs buildEvent = new BuildErrorEventArgs("", "", "", 0, 0, 0, 0, "", "", "");
                    buildEvent.BuildEventContext = new BuildEventContext(1, 2, 3, 4);
                    ((ParallelConsoleLogger)cl).WriteProperties(buildEvent, properties);
                }

                string log = sc.ToString();

                // There should be nothing in the log
                Assert.IsTrue(log.Length == 0, "Iteration of I: " + i);
                Console.WriteLine("Iteration of i: " + i + "[" + log + "]");
            }
        }

        /// <summary>
        /// Basic test of item list display
        /// </summary>
        [Test]
        public void DisplayItemsList()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger cl = new SerialConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);

            WriteAndValidateItems(cl, sc, true);

            sc = new SimulatedConsole();
            ParallelConsoleLogger cl2 = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);

            WriteAndValidateItems(cl2, sc, true);
        }

        /// <summary>
        /// Basic test of item list not being displayed except in Diagnostic
        /// </summary>
        [Test]
        public void DoNotDisplayItemListInDetailed()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger cl = new SerialConsoleLogger(LoggerVerbosity.Detailed, sc.Write, null, null);

            WriteAndValidateItems(cl, sc, false);

            sc = new SimulatedConsole();
            ParallelConsoleLogger cl2 = new ParallelConsoleLogger(LoggerVerbosity.Detailed, sc.Write, null, null);

            WriteAndValidateItems(cl2, sc, false);
        }

        /// <summary>
        /// Basic test of item list not being displayed when disabled
        /// </summary>
        [Test]
        public void DoNotDisplayItemListIfDisabled()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger cl = new SerialConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
            cl.Parameters = "noitemandpropertylist";
            cl.ParseParameters();

            WriteAndValidateItems(cl, sc, false);

            sc = new SimulatedConsole();
            ParallelConsoleLogger cl2 = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
            cl2.Parameters = "noitemandpropertylist";
            cl2.ParseParameters();

            WriteAndValidateItems(cl2, sc, false);
        }       

        [Test]
        public void ParametersEmptyTests()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger L = new SerialConsoleLogger(LoggerVerbosity.Normal, sc.Write, null, null);

            L.Parameters = "";
            L.ParseParameters();
            Assertion.Assert(L.ShowSummary == false);

            L.Parameters = null;
            L.ParseParameters();
            Assertion.Assert(L.ShowSummary == false);

            sc = new SimulatedConsole();
            ParallelConsoleLogger cl2 = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, sc.Write, null, null);
            cl2.Parameters = "noitemandpropertylist";
            cl2.ParseParameters();

            WriteAndValidateItems(cl2, sc, false);
        }

        [Test]
        public void ParametersParsingTests()
        {
            SimulatedConsole sc = new SimulatedConsole();
            SerialConsoleLogger L = new SerialConsoleLogger(LoggerVerbosity.Normal, sc.Write, null, null);

            L.Parameters = "NoSuMmaRy";
            L.ParseParameters();
            Assertion.Assert(L.ShowSummary == false);

            L.Parameters = ";;NoSuMmaRy;";
            L.ParseParameters();
            Assertion.Assert(L.ShowSummary == false);

            sc = new SimulatedConsole();
            ParallelConsoleLogger L2 = new ParallelConsoleLogger(LoggerVerbosity.Normal, sc.Write, null, null);

            L2.Parameters = "NoSuMmaRy";
            L2.ParseParameters();
            Assertion.Assert(L2.ShowSummary == false);

            L2.Parameters = ";;NoSuMmaRy;";
            L2.ParseParameters();
            Assertion.Assert(L2.ShowSummary == false);
        }

        /// <summary>
        /// ResetConsoleLoggerState should reset the state of the console logger
        /// </summary>
        [Test]
        public void ResetConsoleLoggerStateTestBasic()
        {
            // Create an event source
            EventSource es = new EventSource();
            //Create a simulated console
            SimulatedConsole sc = new SimulatedConsole();

            // error and warning string for 1 error and 1 warning
            // errorString = 1 Error(s)
            // warningString = 1 Warning(s)
            string errorString = ResourceUtilities.FormatResourceString("ErrorCount", 1);
            string warningString = ResourceUtilities.FormatResourceString("WarningCount", 1);

            // Create a ConsoleLogger with Normal verbosity
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal,
                                                sc.Write, sc.SetColor, sc.ResetColor);
            // Initialize ConsoleLogger
            L.Initialize(es);

            // BuildStarted Event
            es.RaiseBuildStartedEvent(null,
                          new BuildStartedEventArgs("bs", null));

            // Introduce a warning
            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                            "31415", "file.vb", 42, 0, 0, 0,
                            "Some long message", "help", "sender");

            es.RaiseWarningEvent(null, bwea);

            // Introduce an error
            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");

            es.RaiseErrorEvent(null, beea);

            // BuildFinished Event
            es.RaiseBuildFinishedEvent(null,
                          new BuildFinishedEventArgs("bf",
                                                     null, true));

            // Log so far
            string actualLog = sc.ToString();

            Console.WriteLine("==");
            Console.WriteLine(sc.ToString());
            Console.WriteLine("==");

            // Verify that the log has correct error and warning string
            Assertion.Assert(actualLog.Contains(errorString));
            Assertion.Assert(actualLog.Contains(warningString));
            Assertion.Assert(actualLog.Contains("<red>"));
            Assertion.Assert(actualLog.Contains("<yellow>"));

            // Clear the log obtained so far
            sc.Clear();

            // BuildStarted event
            es.RaiseBuildStartedEvent(null,
                         new BuildStartedEventArgs("bs", null));

            // BuildFinished 
            es.RaiseBuildFinishedEvent(null,
                          new BuildFinishedEventArgs("bf",
                                                     null, true));
            // Log so far
            actualLog = sc.ToString();

            Console.WriteLine("==");
            Console.WriteLine(sc.ToString());
            Console.WriteLine("==");

            // Verify that the error and warning from the previous build is not
            // reported in the subsequent build
            Assertion.Assert(!actualLog.Contains(errorString));
            Assertion.Assert(!actualLog.Contains(warningString));
            Assertion.Assert(!actualLog.Contains("<red>"));
            Assertion.Assert(!actualLog.Contains("<yellow>"));
            
            // errorString = 0 Error(s)
            // warningString = 0 Warning(s)
            errorString = ResourceUtilities.FormatResourceString("ErrorCount", 0);
            warningString = ResourceUtilities.FormatResourceString("WarningCount", 0);
            
            // Verify that the log has correct error and warning string
            Assertion.Assert(actualLog.Contains(errorString));
            Assertion.Assert(actualLog.Contains(warningString));
        }

        /// <summary>
        /// ConsoleLogger::Initialize() should reset the state of the console logger
        /// </summary>
        [Test]
        public void ResetConsoleLoggerState_Initialize()
        {
            // Create an event source
            EventSource es = new EventSource();
            //Create a simulated console
            SimulatedConsole sc = new SimulatedConsole();

            // error and warning string for 1 error and 1 warning
            // errorString = 1 Error(s)
            // warningString = 1 Warning(s)
            string errorString = ResourceUtilities.FormatResourceString("ErrorCount", 1);
            string warningString = ResourceUtilities.FormatResourceString("WarningCount", 1);

            // Create a ConsoleLogger with Normal verbosity
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal,
                                                sc.Write, sc.SetColor, sc.ResetColor);
            // Initialize ConsoleLogger
            L.Initialize(es);

            // BuildStarted Event
            es.RaiseBuildStartedEvent(null,
                          new BuildStartedEventArgs("bs", null));

            // Introduce a warning
            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                            "31415", "file.vb", 42, 0, 0, 0,
                            "Some long message", "help", "sender");

            es.RaiseWarningEvent(null, bwea);

            // Introduce an error
            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");

            es.RaiseErrorEvent(null, beea);

            // NOTE: We don't call the es.RaiseBuildFinishedEvent(...) here as this 
            // would call ResetConsoleLoggerState and we will fail to detect if Initialize() 
            // is not calling it.

            // Log so far
            string actualLog = sc.ToString();

            Console.WriteLine("==");
            Console.WriteLine(sc.ToString());
            Console.WriteLine("==");

            // Verify that the log has correct error and warning string
            Assertion.Assert(actualLog.Contains("<red>"));
            Assertion.Assert(actualLog.Contains("<yellow>"));

            // Clear the log obtained so far
            sc.Clear();

            //Initilialize (This should call ResetConsoleLoggerState(...))
            L.Initialize(es);

            // BuildStarted event
            es.RaiseBuildStartedEvent(null,
                         new BuildStartedEventArgs("bs", null));

            // BuildFinished 
            es.RaiseBuildFinishedEvent(null,
                          new BuildFinishedEventArgs("bf",
                                                     null, true));
            // Log so far
            actualLog = sc.ToString();

            Console.WriteLine("==");
            Console.WriteLine(sc.ToString());
            Console.WriteLine("==");

            // Verify that the error and warning from the previous build is not
            // reported in the subsequent build
            Assertion.Assert(!actualLog.Contains("<red>"));
            Assertion.Assert(!actualLog.Contains("<yellow>"));

            // errorString = 0 Error(s)
            errorString = ResourceUtilities.FormatResourceString("ErrorCount", 0);
            // warningString = 0 Warning(s)
            warningString = ResourceUtilities.FormatResourceString("WarningCount", 0);

            // Verify that the log has correct error and warning string
            Assertion.Assert(actualLog.Contains(errorString));
            Assertion.Assert(actualLog.Contains(warningString));
        }

        /// <summary>
        /// ResetConsoleLoggerState should reset PerformanceCounters
        /// </summary>
        [Test]
        public void ResetConsoleLoggerState_PerformanceCounters()
        {
            for (int i = 1; i <= 2; i++)
            {
                EventSource es = new EventSource();
                //Create a simulated console
                SimulatedConsole sc = new SimulatedConsole();
                // Create a ConsoleLogger with Normal verbosity
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal, sc.Write, sc.SetColor, sc.ResetColor);
                // Initialize ConsoleLogger
                L.Parameters = "Performancesummary";
                L.Initialize(es, i);
                // prjPerfString = Project Performance Summary:
                string prjPerfString = ResourceUtilities.FormatResourceString("ProjectPerformanceSummary", null);
                // targetPerfString = Target Performance Summary:
                string targetPerfString = ResourceUtilities.FormatResourceString("TargetPerformanceSummary", null);
                // taskPerfString = Task Performance Summary:
                string taskPerfString = ResourceUtilities.FormatResourceString("TaskPerformanceSummary", null);

                // BuildStarted Event
                es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs("bs", null));
                //Project Started Event
                ProjectStartedEventArgs project1Started = new ProjectStartedEventArgs(1, null, null, "p", "t", null, null, new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));
                project1Started.BuildEventContext = new BuildEventContext(1, 1, 1, 1);
                es.RaiseProjectStartedEvent(null, project1Started);
                TargetStartedEventArgs targetStarted1 = new TargetStartedEventArgs(null, null, "t", null, null);
                targetStarted1.BuildEventContext = project1Started.BuildEventContext;
                // TargetStarted Event
                es.RaiseTargetStartedEvent(null, targetStarted1);

                TaskStartedEventArgs taskStarted1 = new TaskStartedEventArgs(null, null, null, null, "task");
                taskStarted1.BuildEventContext = project1Started.BuildEventContext;
                // TaskStarted Event 
                es.RaiseTaskStartedEvent(null, taskStarted1);

                BuildMessageEventArgs messsage1 = new BuildMessageEventArgs(null, null, null, MessageImportance.High);
                messsage1.BuildEventContext = project1Started.BuildEventContext;
                // Message Event
                es.RaiseMessageEvent(null, messsage1);
                TaskFinishedEventArgs taskFinished1 = new TaskFinishedEventArgs(null, null, null, null, "task", true);
                taskFinished1.BuildEventContext = project1Started.BuildEventContext;
                // TaskFinished Event
                es.RaiseTaskFinishedEvent(null, taskFinished1);

                TargetFinishedEventArgs targetFinished1 = new TargetFinishedEventArgs(null, null, "t", null, null, true);
                targetFinished1.BuildEventContext = project1Started.BuildEventContext;
                // TargetFinished Event
                es.RaiseTargetFinishedEvent(null, targetFinished1);

                ProjectStartedEventArgs project2Started = new ProjectStartedEventArgs(2, null, null, "p2", "t2", null, null, project1Started.BuildEventContext);
                //Project Started Event
                project2Started.BuildEventContext = new BuildEventContext(2, 2, 2, 2);
                es.RaiseProjectStartedEvent(null, project2Started);
                TargetStartedEventArgs targetStarted2 = new TargetStartedEventArgs(null, null, "t2", null, null);
                targetStarted2.BuildEventContext = project2Started.BuildEventContext;
                // TargetStarted Event
                es.RaiseTargetStartedEvent(null, targetStarted2);

                TaskStartedEventArgs taskStarted2 = new TaskStartedEventArgs(null, null, null, null, "task2");
                taskStarted2.BuildEventContext = project2Started.BuildEventContext;
                // TaskStarted Event 
                es.RaiseTaskStartedEvent(null, taskStarted2);

                BuildMessageEventArgs messsage2 = new BuildMessageEventArgs(null, null, null, MessageImportance.High);
                messsage2.BuildEventContext = project2Started.BuildEventContext;
                // Message Event
                es.RaiseMessageEvent(null, messsage2);
                TaskFinishedEventArgs taskFinished2 = new TaskFinishedEventArgs(null, null, null, null, "task2", true);
                taskFinished2.BuildEventContext = project2Started.BuildEventContext;
                // TaskFinished Event
                es.RaiseTaskFinishedEvent(null, taskFinished2);

                TargetFinishedEventArgs targetFinished2 = new TargetFinishedEventArgs(null, null, "t2", null, null, true);
                targetFinished2.BuildEventContext = project2Started.BuildEventContext;
                // TargetFinished Event
                es.RaiseTargetFinishedEvent(null, targetFinished2);

                ProjectFinishedEventArgs finished2 = new ProjectFinishedEventArgs(null, null, "p2", true);
                finished2.BuildEventContext = project2Started.BuildEventContext;
                // ProjectFinished Event
                es.RaiseProjectFinishedEvent(null, finished2);            // BuildFinished Event

                ProjectFinishedEventArgs finished1 = new ProjectFinishedEventArgs(null, null, "p", true);
                finished1.BuildEventContext = project1Started.BuildEventContext;
                // ProjectFinished Event
                es.RaiseProjectFinishedEvent(null, finished1);            // BuildFinished Event
                es.RaiseBuildFinishedEvent(null,
                              new BuildFinishedEventArgs("bf",
                                                         null, true));
                // Log so far
                string actualLog = sc.ToString();

                Console.WriteLine("==");
                Console.WriteLine(sc.ToString());
                Console.WriteLine("==");

                // Verify that the log has perf summary
                // Project perf summary
                Assertion.Assert(actualLog.Contains(prjPerfString));
                // Target perf summary
                Assertion.Assert(actualLog.Contains(targetPerfString));
                // Task Perf summary
                Assertion.Assert(actualLog.Contains(taskPerfString));

                // Clear the log obtained so far
                sc.Clear();

                // BuildStarted event
                es.RaiseBuildStartedEvent(null,
                             new BuildStartedEventArgs("bs", null));
                // BuildFinished 
                es.RaiseBuildFinishedEvent(null,
                              new BuildFinishedEventArgs("bf",
                                                         null, true));
                // Log so far
                actualLog = sc.ToString();

                Console.WriteLine("==");
                Console.WriteLine(sc.ToString());
                Console.WriteLine("==");

                // Verify that the log doesn't have perf summary
                Assertion.Assert(!actualLog.Contains(prjPerfString));
                Assertion.Assert(!actualLog.Contains(targetPerfString));
                Assertion.Assert(!actualLog.Contains(taskPerfString));
            }
        }


        [Test]
        public void DeferredMessages()
        {
            EventSource es = new EventSource();
            //Create a simulated console
            SimulatedConsole sc = new SimulatedConsole();
            // Create a ConsoleLogger with Detailed verbosity
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Detailed, sc.Write, sc.SetColor, sc.ResetColor);
            L.Initialize(es, 2);
            es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs("bs", null));
            TaskCommandLineEventArgs messsage1 = new TaskCommandLineEventArgs("Message", null, MessageImportance.High);
            messsage1.BuildEventContext = new BuildEventContext(1, 1, 1, 1);
            // Message Event
            es.RaiseMessageEvent(null, messsage1);
            es.RaiseBuildFinishedEvent(null,
                          new BuildFinishedEventArgs("bf",
                                                     null, true));
            string actualLog = sc.ToString();
            Assertion.Assert(actualLog.Contains(ResourceUtilities.FormatResourceString("DeferredMessages")));

            es = new EventSource();
             sc = new SimulatedConsole();
            // Create a ConsoleLogger with Normal verbosity
            L = new ConsoleLogger(LoggerVerbosity.Normal, sc.Write, sc.SetColor, sc.ResetColor);
            L.Initialize(es, 2);
            es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs("bs", null));
            BuildMessageEventArgs messsage2 = new BuildMessageEventArgs("Message", null, null, MessageImportance.High);
            messsage2.BuildEventContext = new BuildEventContext(1, 1, 1, 1);
            // Message Event
            es.RaiseMessageEvent(null, messsage2);
            es.RaiseBuildFinishedEvent(null,
                          new BuildFinishedEventArgs("bf",
                                                     null, true));
            actualLog = sc.ToString();
            Assertion.Assert(actualLog.Contains(ResourceUtilities.FormatResourceString("DeferredMessagesAvailiable")));
            
            es = new EventSource();
            sc = new SimulatedConsole();
            // Create a ConsoleLogger with Normal verbosity
            L = new ConsoleLogger(LoggerVerbosity.Normal, sc.Write, sc.SetColor, sc.ResetColor);
            L.Initialize(es, 2);
            es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs("bs", null));
            messsage2 = new BuildMessageEventArgs("Message", null, null, MessageImportance.High);
            messsage2.BuildEventContext = new BuildEventContext(1, 1, 1, 1);
            // Message Event
            es.RaiseMessageEvent(null, messsage2);
            ProjectStartedEventArgs project = new ProjectStartedEventArgs(1,"Hello,","HI","None","Build",null,null, messsage1.BuildEventContext);
            project.BuildEventContext = messsage1.BuildEventContext;
            es.RaiseProjectStartedEvent(null, project);
            es.RaiseBuildFinishedEvent(null,
                          new BuildFinishedEventArgs("bf",
                                                     null, true));
            actualLog = sc.ToString();
            Assertion.Assert(actualLog.Contains("Message"));
        }

        [Test]
        public void VerifyMPLoggerSwitch()
        {
            for (int i = 0; i < 2; i++)
            {
                EventSource es = new EventSource();
                //Create a simulated console
                SimulatedConsole sc = new SimulatedConsole();
                // Create a ConsoleLogger with Normal verbosity
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal, sc.Write, sc.SetColor, sc.ResetColor);
                //Make sure the MPLogger switch will property work on both Initialize methods
                L.Parameters = "EnableMPLogging";
                if (i == 0)
                {
                    L.Initialize(es, 1);
                }
                else
                {
                    L.Initialize(es);
                }
                es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs("bs", null));
                BuildEventContext context = new BuildEventContext(1, 1, 1, 1);
                BuildEventContext context2 = new BuildEventContext(2, 2, 2, 2);

                ProjectStartedEventArgs project = new ProjectStartedEventArgs(1, "Hello,", "HI", "None", "Build", null, null, context);
                project.BuildEventContext = context;
                es.RaiseProjectStartedEvent(null, project);

                TargetStartedEventArgs targetStarted1 = new TargetStartedEventArgs(null, null, "t", null, null);
                targetStarted1.BuildEventContext = context;
                es.RaiseTargetStartedEvent(null, targetStarted1);

                BuildMessageEventArgs messsage1 = new BuildMessageEventArgs("Message", null, null, MessageImportance.High);
                messsage1.BuildEventContext = context;
                es.RaiseMessageEvent(null, messsage1);
                string actualLog = sc.ToString();
                string resourceString = ResourceUtilities.FormatResourceString("ProjectStartedTopLevelProjectWithTargetNames", "None", 1, "Build");
                Assertion.Assert(actualLog.Contains(resourceString));
            }
        }

        [Test]
        public void TestPrintTargetNamePerMessage()
        {
            EventSource es = new EventSource();
            //Create a simulated console
            SimulatedConsole sc = new SimulatedConsole();
            // Create a ConsoleLogger with Normal verbosity
            ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal, sc.Write, sc.SetColor, sc.ResetColor);
            L.Initialize(es, 2);
            es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs("bs", null));
            BuildEventContext context =new BuildEventContext(1, 1, 1, 1);
            BuildEventContext context2 =new BuildEventContext(2, 2, 2, 2);

            ProjectStartedEventArgs project = new ProjectStartedEventArgs(1, "Hello,", "HI", "None", "Build", null, null, context);
            project.BuildEventContext = context;
            es.RaiseProjectStartedEvent(null, project);

            ProjectStartedEventArgs project2 = new ProjectStartedEventArgs(2, "Hello,", "HI", "None", "Build", null, null, context2);
            project2.BuildEventContext = context2;
            es.RaiseProjectStartedEvent(null, project2);

            TargetStartedEventArgs targetStarted1 = new TargetStartedEventArgs(null, null, "t", null, null);
            targetStarted1.BuildEventContext = context;
            es.RaiseTargetStartedEvent(null, targetStarted1);

            TargetStartedEventArgs targetStarted2 = new TargetStartedEventArgs(null, null, "t2", null, null);
            targetStarted2.BuildEventContext = context2;
            es.RaiseTargetStartedEvent(null, targetStarted2);

            BuildMessageEventArgs messsage1 = new BuildMessageEventArgs("Message", null, null,MessageImportance.High);
            messsage1.BuildEventContext = context;
            BuildMessageEventArgs messsage2 = new BuildMessageEventArgs("Message2", null, null, MessageImportance.High);
            messsage2.BuildEventContext = context2;
            BuildMessageEventArgs messsage3 = new BuildMessageEventArgs("Message3", null, null, MessageImportance.High);
            messsage3.BuildEventContext = context;
            es.RaiseMessageEvent(null, messsage1);
            es.RaiseMessageEvent(null, messsage2);
            es.RaiseMessageEvent(null, messsage3);
            string actualLog = sc.ToString();
            Assertion.Assert(actualLog.Contains("t:"));
        }

        /// <summary>
        /// Verify that in the MP case and the older serial logger that there is no extra newline after the project done event. 
        /// We cannot verify there is a newline after the project done event for the MP single proc log because
        /// nunit is showing up as an unknown output type, this causes us to not print the newline because we think it may be to a 
        /// text file.
        /// </summary>
        [Test]
        public void TestNewLineAfterProjectFinished()
        {

            for (int i = 0; i < 3; i++)
            {
                Console.Out.WriteLine("Iteration of I is {" + i + "}");

                
                EventSource es = new EventSource();
                //Create a simulated console
                SimulatedConsole sc = new SimulatedConsole();
                ConsoleLogger L = new ConsoleLogger(LoggerVerbosity.Normal, sc.Write, sc.SetColor, sc.ResetColor);

                if (i < 2)
                {
                    // On the second pass through use the MP single proc logger
                    if (i == 1)
                    {

                        L.Parameters = "EnableMPLogging";
                    }
                    // Use the old single proc logger
                    L.Initialize(es, 1);
                }
                else
                {
                    // Use the parallel logger
                    L.Initialize(es, 2);
                }

                es.RaiseBuildStartedEvent(null, new BuildStartedEventArgs("bs", null));
                BuildEventContext context = new BuildEventContext(1, 1, 1, 1);

                ProjectStartedEventArgs project = new ProjectStartedEventArgs(1, "Hello,", "HI", "None", "Build", null, null, context);
                project.BuildEventContext = context;
                es.RaiseProjectStartedEvent(null, project);

                TargetStartedEventArgs targetStarted1 = new TargetStartedEventArgs(null, null, "t", null, null);
                targetStarted1.BuildEventContext = context;
                es.RaiseTargetStartedEvent(null, targetStarted1);

                BuildMessageEventArgs messsage1 = new BuildMessageEventArgs("Message", null, null, MessageImportance.High);
                messsage1.BuildEventContext = context;
                es.RaiseMessageEvent(null, messsage1);

                ProjectFinishedEventArgs projectFinished = new ProjectFinishedEventArgs("Finished,", "HI", "projectFile", true);
                projectFinished.BuildEventContext = context;
                es.RaiseProjectFinishedEvent(null, projectFinished);

                string actualLog = sc.ToString();

                switch(i)
                {
                    case 0:
                        // There is no project finished event printed in normal verbosity
                        Assertion.Assert(!actualLog.Contains(projectFinished.Message));
                        break;
                     // In both case 1 and case 2 verify that there is no extra newline after the done event (this is because the nunit console is seen as an unknown device
                     // and we do not want the extra newline in something other than a console device.
                    case 1:
                    case 2:
                        Assertion.Assert(actualLog.Contains(ResourceUtilities.FormatResourceString("ProjectFinishedPrefixWithTargetNamesMultiProc", "None", "Build")  + Environment.NewLine));
                        Assertion.Assert(!actualLog.Contains(ResourceUtilities.FormatResourceString("ProjectFinishedPrefixWithTargetNamesMultiProc", "None", "Build")  + Environment.NewLine + Environment.NewLine));
                        break;
                }
            }
        }

    }
}

