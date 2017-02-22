// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Microsoft.Build.Evaluation;
using Microsoft.Build.SharedUtilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class XMakeAppTests
    {
#if USE_MSBUILD_DLL_EXTN
        private const string MSBuildExeName = "MSBuild.dll";
#else
        private const string MSBuildExeName = "MSBuild.exe";
#endif

        private const string AutoResponseFileName = "MSBuild.rsp";

        [Fact]
        public void GatherCommandLineSwitchesTwoProperties()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/p:a=b", "/p:c=d" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Property];
            Assert.Equal("a=b", parameters[0]);
            Assert.Equal("c=d", parameters[1]);
        }

        [Fact]
        public void GatherCommandLineSwitchesMaxCpuCountWithArgument()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/m:2" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.MaxCPUCount];
            Assert.Equal("2", parameters[0]);
            Assert.Equal(1, parameters.Length);

            Assert.Equal(false, switches.HaveErrors());
        }

        [Fact]
        public void GatherCommandLineSwitchesMaxCpuCountWithoutArgument()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/m:3", "/m" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.MaxCPUCount];
            Assert.Equal(Convert.ToString(Environment.ProcessorCount), parameters[1]);
            Assert.Equal(2, parameters.Length);

            Assert.Equal(false, switches.HaveErrors());
        }

        /// <summary>
        ///  /m: should be an error, unlike /m:1 and /m
        /// </summary>
        [Fact]
        public void GatherCommandLineSwitchesMaxCpuCountWithoutArgumentButWithColon()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            ArrayList arguments = new ArrayList();
            arguments.AddRange(new string[] { "/m:" });

            MSBuildApp.GatherCommandLineSwitches(arguments, switches);

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.MaxCPUCount];
            Assert.Equal(0, parameters.Length);

            Assert.True(switches.HaveErrors());
        }

        /*
         * Quoting Rules:
         * 
         * A string is considered quoted if it is enclosed in double-quotes. A double-quote can be escaped with a backslash, or it
         * is automatically escaped if it is the last character in an explicitly terminated quoted string. A backslash itself can
         * be escaped with another backslash IFF it precedes a double-quote, otherwise it is interpreted literally.
         * 
         * e.g.
         *      abc"cde"xyz         --> "cde" is quoted
         *      abc"xyz             --> "xyz" is quoted (the terminal double-quote is assumed)
         *      abc"xyz"            --> "xyz" is quoted (the terminal double-quote is explicit)
         * 
         *      abc\"cde"xyz        --> "xyz" is quoted (the terminal double-quote is assumed)
         *      abc\\"cde"xyz       --> "cde" is quoted
         *      abc\\\"cde"xyz      --> "xyz" is quoted (the terminal double-quote is assumed)
         * 
         *      abc"""xyz           --> """ is quoted
         *      abc""""xyz          --> """ and "xyz" are quoted (the terminal double-quote is assumed)
         *      abc"""""xyz         --> """ is quoted
         *      abc""""""xyz        --> """ and """ are quoted
         *      abc"cde""xyz        --> "cde"" is quoted
         *      abc"xyz""           --> "xyz"" is quoted (the terminal double-quote is explicit)
         * 
         *      abc""xyz            --> nothing is quoted
         *      abc""cde""xyz       --> nothing is quoted
         */

        [Fact]
        public void SplitUnquotedTest()
        {
            ArrayList sa;
            int emptySplits;

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abcdxyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abcdxyz", sa[0]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abcc dxyz");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abcc", sa[0]);
            Assert.Equal("dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abcc;dxyz", ';');
            Assert.Equal(2, sa.Count);
            Assert.Equal("abcc", sa[0]);
            Assert.Equal("dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,c;dxyz", ';', ',');
            Assert.Equal(3, sa.Count);
            Assert.Equal("abc", sa[0]);
            Assert.Equal("c", sa[1]);
            Assert.Equal("dxyz", sa[2]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,c;dxyz", 2, false, false, out emptySplits, ';', ',');
            Assert.Equal(0, emptySplits);
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc", sa[0]);
            Assert.Equal("c;dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,,;dxyz", int.MaxValue, false, false, out emptySplits, ';', ',');
            Assert.Equal(2, emptySplits);
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc", sa[0]);
            Assert.Equal("dxyz", sa[1]);

            // nothing quoted
            sa = QuotingUtilities.SplitUnquoted("abc,,;dxyz", int.MaxValue, true, false, out emptySplits, ';', ',');
            Assert.Equal(0, emptySplits);
            Assert.Equal(4, sa.Count);
            Assert.Equal("abc", sa[0]);
            Assert.Equal(String.Empty, sa[1]);
            Assert.Equal(String.Empty, sa[2]);
            Assert.Equal("dxyz", sa[3]);

            // "c d" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"c d\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\"c d\"xyz", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\"x z");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\"x z", sa[0]);

            // "x z" is quoted (the terminal double-quote is explicit)
            sa = QuotingUtilities.SplitUnquoted("abc\"x z\"");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\"x z\"", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\\\"cde\"x z");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\\\"cde\"x z", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            // "c e" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\"c e\"x z");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc\\\"c", sa[0]);
            Assert.Equal("e\"x z", sa[1]);

            // "c e" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\"c e\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\\\\\"c e\"xyz", sa[0]);

            // "c e" is quoted
            // "x z" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\"c e\"x z");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc\\\\\"c e\"x", sa[0]);
            Assert.Equal("z", sa[1]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\\\"cde\"x z");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\\\\\\\"cde\"x z", sa[0]);

            // "xyz" is quoted (the terminal double-quote is assumed)
            // "c e" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\\\\\\\"c e\"x z");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc\\\\\\\"c", sa[0]);
            Assert.Equal("e\"x z", sa[1]);

            // """ is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\"\"\"xyz", sa[0]);

            // " "" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\" \"\"xyz", sa[0]);

            // "x z" is quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\"\" \"x z");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc\"\"", sa[0]);
            Assert.Equal("\"x z", sa[1]);

            // " "" and "xyz" are quoted (the terminal double-quote is assumed)
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"\"x z");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\" \"\"\"x z", sa[0]);

            // """ is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"\"\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\"\"\"\"\"xyz", sa[0]);

            // """ is quoted
            // "x z" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"\"\"x z");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc\"\"\"\"\"x", sa[0]);
            Assert.Equal("z", sa[1]);

            // " "" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"\"\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\" \"\"\"\"xyz", sa[0]);

            // """ and """ are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\"\"\"\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\"\"\"\"\"\"xyz", sa[0]);

            // " "" and " "" are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\" \"\"\" \"\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\" \"\"\" \"\"xyz", sa[0]);

            // """ and """ are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\" \"\"\"xyz");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc\"\"\"", sa[0]);
            Assert.Equal("\"\"\"xyz", sa[1]);

            // """ and """ are quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"\"\" \"\"\"x z");
            Assert.Equal(3, sa.Count);
            Assert.Equal("abc\"\"\"", sa[0]);
            Assert.Equal("\"\"\"x", sa[1]);
            Assert.Equal("z", sa[2]);

            // "c e"" is quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"c e\"\"xyz");
            Assert.Equal(1, sa.Count);
            Assert.Equal("abc\"c e\"\"xyz", sa[0]);

            // "c e"" is quoted
            // "x z" is not quoted
            sa = QuotingUtilities.SplitUnquoted("abc\"c e\"\"x z");
            Assert.Equal(2, sa.Count);
            Assert.Equal("abc\"c e\"\"x", sa[0]);
            Assert.Equal("z", sa[1]);

            // nothing is quoted
            sa = QuotingUtilities.SplitUnquoted("a c\"\"x z");
            Assert.Equal(3, sa.Count);
            Assert.Equal("a", sa[0]);
            Assert.Equal("c\"\"x", sa[1]);
            Assert.Equal("z", sa[2]);

            // nothing is quoted
            sa = QuotingUtilities.SplitUnquoted("a c\"\"c e\"\"x z");
            Assert.Equal(4, sa.Count);
            Assert.Equal("a", sa[0]);
            Assert.Equal("c\"\"c", sa[1]);
            Assert.Equal("e\"\"x", sa[2]);
            Assert.Equal("z", sa[3]);
        }

        [Fact]
        public void UnquoteTest()
        {
            int doubleQuotesRemoved;

            // "cde" is quoted
            Assert.Equal("abccdexyz", QuotingUtilities.Unquote("abc\"cde\"xyz", out doubleQuotesRemoved));
            Assert.Equal(2, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is assumed)
            Assert.Equal("abcxyz", QuotingUtilities.Unquote("abc\"xyz", out doubleQuotesRemoved));
            Assert.Equal(1, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is explicit)
            Assert.Equal("abcxyz", QuotingUtilities.Unquote("abc\"xyz\"", out doubleQuotesRemoved));
            Assert.Equal(2, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is assumed)
            Assert.Equal("abc\"cdexyz", QuotingUtilities.Unquote("abc\\\"cde\"xyz", out doubleQuotesRemoved));
            Assert.Equal(1, doubleQuotesRemoved);

            // "cde" is quoted
            Assert.Equal("abc\\cdexyz", QuotingUtilities.Unquote("abc\\\\\"cde\"xyz", out doubleQuotesRemoved));
            Assert.Equal(2, doubleQuotesRemoved);

            // "xyz" is quoted (the terminal double-quote is assumed)
            Assert.Equal("abc\\\"cdexyz", QuotingUtilities.Unquote("abc\\\\\\\"cde\"xyz", out doubleQuotesRemoved));
            Assert.Equal(1, doubleQuotesRemoved);

            // """ is quoted
            Assert.Equal("abc\"xyz", QuotingUtilities.Unquote("abc\"\"\"xyz", out doubleQuotesRemoved));
            Assert.Equal(2, doubleQuotesRemoved);

            // """ and "xyz" are quoted (the terminal double-quote is assumed)
            Assert.Equal("abc\"xyz", QuotingUtilities.Unquote("abc\"\"\"\"xyz", out doubleQuotesRemoved));
            Assert.Equal(3, doubleQuotesRemoved);

            // """ is quoted
            Assert.Equal("abc\"xyz", QuotingUtilities.Unquote("abc\"\"\"\"\"xyz", out doubleQuotesRemoved));
            Assert.Equal(4, doubleQuotesRemoved);

            // """ and """ are quoted
            Assert.Equal("abc\"\"xyz", QuotingUtilities.Unquote("abc\"\"\"\"\"\"xyz", out doubleQuotesRemoved));
            Assert.Equal(4, doubleQuotesRemoved);

            // "cde"" is quoted
            Assert.Equal("abccde\"xyz", QuotingUtilities.Unquote("abc\"cde\"\"xyz", out doubleQuotesRemoved));
            Assert.Equal(2, doubleQuotesRemoved);

            // "xyz"" is quoted (the terminal double-quote is explicit)
            Assert.Equal("abcxyz\"", QuotingUtilities.Unquote("abc\"xyz\"\"", out doubleQuotesRemoved));
            Assert.Equal(2, doubleQuotesRemoved);

            // nothing is quoted
            Assert.Equal("abcxyz", QuotingUtilities.Unquote("abc\"\"xyz", out doubleQuotesRemoved));
            Assert.Equal(2, doubleQuotesRemoved);

            // nothing is quoted
            Assert.Equal("abccdexyz", QuotingUtilities.Unquote("abc\"\"cde\"\"xyz", out doubleQuotesRemoved));
            Assert.Equal(4, doubleQuotesRemoved);
        }

        [Fact]
        public void ExtractSwitchParametersTest()
        {
            string commandLineArg = "\"/p:foo=\"bar";
            int doubleQuotesRemovedFromArg;
            string unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":\"foo=\"bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(2, doubleQuotesRemovedFromArg);

            commandLineArg = "\"/p:foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(2, doubleQuotesRemovedFromArg);

            commandLineArg = "/p:foo=bar";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(0, doubleQuotesRemovedFromArg);

            commandLineArg = "\"\"/p:foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":foo=bar\"", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(3, doubleQuotesRemovedFromArg);

            // this test is totally unreal -- we'd never attempt to extract switch parameters if the leading character is not a
            // switch indicator (either '-' or '/') -- here the leading character is a double-quote
            commandLineArg = "\"\"\"/p:foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":foo=bar\"", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "/p", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(3, doubleQuotesRemovedFromArg);

            commandLineArg = "\"/pr\"operty\":foo=bar";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "property", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(3, doubleQuotesRemovedFromArg);

            commandLineArg = "\"/pr\"op\"\"erty\":foo=bar\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":foo=bar", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "property", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(6, doubleQuotesRemovedFromArg);

            commandLineArg = "/p:\"foo foo\"=\"bar bar\";\"baz=onga\"";
            unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);
            Assert.Equal(":\"foo foo\"=\"bar bar\";\"baz=onga\"", MSBuildApp.ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, "p", unquotedCommandLineArg.IndexOf(':')));
            Assert.Equal(6, doubleQuotesRemovedFromArg);
        }

        [Fact]
        public void Help()
        {
            Assert.Equal(MSBuildApp.ExitType.Success,
                MSBuildApp.Execute(
#if FEATURE_GET_COMMANDLINE
                    @"c:\bin\msbuild.exe -? "
#else
                    new [] {@"c:\bin\msbuild.exe", "-?"}
#endif
                ));
        }

        [Fact]
        public void ErrorCommandLine()
        {
#if FEATURE_GET_COMMANDLINE
            Assert.Equal(MSBuildApp.ExitType.SwitchError,
                MSBuildApp.Execute(@"c:\bin\msbuild.exe -junk"));

            Assert.Equal(MSBuildApp.ExitType.SwitchError,
                MSBuildApp.Execute(@"msbuild.exe -t"));

            Assert.Equal(MSBuildApp.ExitType.InitializationError,
                MSBuildApp.Execute(@"msbuild.exe @bogus.rsp"));
#else
            Assert.Equal(
                MSBuildApp.ExitType.SwitchError,
                MSBuildApp.Execute(new[] { @"c:\bin\msbuild.exe", "-junk" }));

            Assert.Equal(MSBuildApp.ExitType.SwitchError,
                MSBuildApp.Execute(new[] { @"msbuild.exe", "-t" }));

            Assert.Equal(
                MSBuildApp.ExitType.InitializationError,
                MSBuildApp.Execute(new[] { @"msbuild.exe", "@bogus.rsp" }));
#endif
        }

        [Fact]
        public void ValidVerbosities()
        {
            Assert.Equal(LoggerVerbosity.Quiet, MSBuildApp.ProcessVerbositySwitch("Q"));
            Assert.Equal(LoggerVerbosity.Quiet, MSBuildApp.ProcessVerbositySwitch("quiet"));
            Assert.Equal(LoggerVerbosity.Minimal, MSBuildApp.ProcessVerbositySwitch("m"));
            Assert.Equal(LoggerVerbosity.Minimal, MSBuildApp.ProcessVerbositySwitch("minimal"));
            Assert.Equal(LoggerVerbosity.Normal, MSBuildApp.ProcessVerbositySwitch("N"));
            Assert.Equal(LoggerVerbosity.Normal, MSBuildApp.ProcessVerbositySwitch("normal"));
            Assert.Equal(LoggerVerbosity.Detailed, MSBuildApp.ProcessVerbositySwitch("d"));
            Assert.Equal(LoggerVerbosity.Detailed, MSBuildApp.ProcessVerbositySwitch("detailed"));
            Assert.Equal(LoggerVerbosity.Diagnostic, MSBuildApp.ProcessVerbositySwitch("diag"));
            Assert.Equal(LoggerVerbosity.Diagnostic, MSBuildApp.ProcessVerbositySwitch("DIAGNOSTIC"));
        }

        [Fact]
        public void InvalidVerbosity()
        {
            Assert.Throws<CommandLineSwitchException>(() =>
            {
                MSBuildApp.ProcessVerbositySwitch("loquacious");
            }
           );
        }
        [Fact]
        public void ValidMaxCPUCountSwitch()
        {
            Assert.Equal(1, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "1" }));
            Assert.Equal(2, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "2" }));
            Assert.Equal(3, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "3" }));
            Assert.Equal(4, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "4" }));
            Assert.Equal(8, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "8" }));
            Assert.Equal(63, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "63" }));

            // Should pick last value
            Assert.Equal(4, MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "8", "4" }));
        }

        [Fact]
        public void InvalidMaxCPUCountSwitch1()
        {
            Assert.Throws<CommandLineSwitchException>(() =>
            {
                MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "-1" });
            }
           );
        }

        [Fact]
        public void InvalidMaxCPUCountSwitch2()
        {
            Assert.Throws<CommandLineSwitchException>(() =>
            {
                MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "0" });
            }
           );
        }

        [Fact]
        public void InvalidMaxCPUCountSwitch3()
        {
            Assert.Throws<CommandLineSwitchException>(() =>
            {
                // Too big
                MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "foo" });
            }
           );
        }

        [Fact]
        public void InvalidMaxCPUCountSwitch4()
        {
            Assert.Throws<CommandLineSwitchException>(() =>
            {
                MSBuildApp.ProcessMaxCPUCountSwitch(new string[] { "1025" });
            }
           );
        }

#if FEATURE_CULTUREINFO_CONSOLE_FALLBACK
        /// <summary>
        /// Regression test for bug where the MSBuild.exe command-line app
        /// would sometimes set the UI culture to just "en" which is considered a "neutral" UI 
        /// culture, which didn't allow for certain kinds of string formatting/parsing.
        /// </summary>
        /// <remarks>
        /// fr-FR, de-DE, and fr-CA are guaranteed to be available on all BVTs, so we must use one of these
        /// </remarks>
        [Fact]
        public void SetConsoleUICulture()
        {
            Thread thisThread = Thread.CurrentThread;

            // Save the current UI culture, so we can restore it at the end of this unit test.
            CultureInfo originalUICulture = thisThread.CurrentUICulture;

            thisThread.CurrentUICulture = new CultureInfo("fr-FR");
            MSBuildApp.SetConsoleUI();

            // Make sure this doesn't throw an exception.
            string bar = String.Format(CultureInfo.CurrentUICulture, "{0}", (int)1);

            // Restore the current UI culture back to the way it was at the beginning of this unit test.
            thisThread.CurrentUICulture = originalUICulture;
        }
#endif

#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Invalid configuration file should not dump stack.
        /// </summary>
        [Fact]
        public void ConfigurationInvalid()
        {
            string startDirectory = null;
            string output = null;
            string oldValueForMSBuildOldOM = null;

            try
            {
                oldValueForMSBuildOldOM = Environment.GetEnvironmentVariable("MSBuildOldOM");
                Environment.SetEnvironmentVariable("MSBuildOldOM", "");

                startDirectory = CopyMSBuild();
                var msbuildExeName = Path.GetFileName(RunnerUtilities.PathToCurrentlyRunningMsBuildExe);
                var newPathToMSBuildExe = Path.Combine(startDirectory, msbuildExeName);
                var pathToConfigFile = Path.Combine(startDirectory, msbuildExeName + ".config");

                string configContent = @"<?xml version =""1.0""?>
                                            <configuration>
                                                <configSections>
                                                    <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" />
                                                    <foo/>
                                                </configSections>
                                                <startup>
                                                    <supportedRuntime version=""v4.0""/>
                                                </startup>
                                                <foo/>
                                                <msbuildToolsets default=""X"">
                                                <foo/>
                                                    <toolset toolsVersion=""X""> 
                                                        <foo/>
                                                    <property name=""MSBuildBinPath"" value=""Y""/>
                                                    <foo/>
                                                    </toolset>
                                                <foo/>
                                                </msbuildToolsets>
                                                <foo/>
                                            </configuration>";
                File.WriteAllText(pathToConfigFile, configContent);

                var pathToProjectFile = Path.Combine(startDirectory, "foo.proj");
                string projectString =
                   "<?xml version='1.0' encoding='utf-8'?>" +
                    "<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='X'>" +
                    "<Target Name='t'></Target>" +
                    "</Project>";
                File.WriteAllText(pathToProjectFile, projectString);

                var msbuildParameters = "\"" + pathToProjectFile + "\"";

                bool successfulExit;
                output = RunnerUtilities.ExecMSBuild(newPathToMSBuildExe, msbuildParameters, out successfulExit);
                Assert.False(successfulExit);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
            finally
            {
                if (output != null)
                {
                    Console.WriteLine(output);
                }

                try
                {
                    // Process does not let go its lock on the exe file until about 1 millisecond after 
                    // p.WaitForExit() returns. Do I know why? No I don't.
                    RobustDelete(startDirectory);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MSBuildOldOM", oldValueForMSBuildOldOM);
                }
            }

            // If there's a space in the %TEMP% path, the config file is read in the static constructor by the URI class and we catch there;
            // if there's not, we will catch when we try to read the toolsets. Either is fine; we just want to not crash.
            Assert.True(output.Contains("MSB1043") || output.Contains("MSB4136"));
        }
#endif

        /// <summary>
        /// Try hard to delete a file or directory specified
        /// </summary>
        private void RobustDelete(string path)
        {
            if (path != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        if (Directory.Exists(path))
                        {
                            FileUtilities.DeleteWithoutTrailingBackslash(path, true /*and files*/);
                        }
                        else if (File.Exists(path))
                        {
                            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); // make writeable
                            File.Delete(path);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Thread.Sleep(10);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Tests that the environment gets passed on to the node during build.
        /// </summary>
        [Fact]
        public void TestEnvironment()
        {
            string projectString = ObjectModelHelpers.CleanupFileContents(
                   @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                    <Target Name=""t""><Error Text='Error' Condition=""'$(MyEnvVariable)' == ''""/></Target>
                    </Project>");
            string tempdir = Path.GetTempPath();
            string projectFileName = Path.Combine(tempdir, "msbEnvironmenttest.proj");
            string quotedProjectFileName = "\"" + projectFileName + "\"";

            try
            {
                Environment.SetEnvironmentVariable("MyEnvVariable", "1");
                using (StreamWriter sw = FileUtilities.OpenWrite(projectFileName, false))
                {
                    sw.WriteLine(projectString);
                }
                //Should pass
#if FEATURE_GET_COMMANDLINE
                Assert.Equal(MSBuildApp.ExitType.Success, MSBuildApp.Execute(@"c:\bin\msbuild.exe " + quotedProjectFileName));
#else
                Assert.Equal(
                    MSBuildApp.ExitType.Success,
                    MSBuildApp.Execute(new[] { @"c:\bin\msbuild.exe", quotedProjectFileName }));
#endif
            }
            finally
            {
                Environment.SetEnvironmentVariable("MyEnvVariable", null);
                File.Delete(projectFileName);
            }
        }

        [Fact]
        public void MSBuildEngineLogger()
        {
            string projectString =
                   "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                    "<Target Name=\"t\"><Message Text=\"Hello\"/></Target>" +
                    "</Project>";
            string tempdir = Path.GetTempPath();
            string projectFileName = Path.Combine(tempdir, "msbLoggertest.proj");
            string quotedProjectFileName = "\"" + projectFileName + "\"";

            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(projectFileName, false))
                {
                    sw.WriteLine(projectString);
                }
#if FEATURE_GET_COMMANDLINE
                //Should pass
                Assert.Equal(MSBuildApp.ExitType.Success,
                    MSBuildApp.Execute(@"c:\bin\msbuild.exe /logger:FileLogger,""Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" " + quotedProjectFileName));

                //Should fail as we are not changing existing lines
                Assert.Equal(MSBuildApp.ExitType.InitializationError,
                        MSBuildApp.Execute(@"c:\bin\msbuild.exe /logger:FileLogger,Microsoft.Build,Version=11111 " + quotedProjectFileName));
#else
                //Should pass
                Assert.Equal(
                    MSBuildApp.ExitType.Success,
                    MSBuildApp.Execute(
                        new[]
                            {
                                NativeMethodsShared.IsWindows ? @"c:\bin\msbuild.exe" : "/msbuild.exe",
                                @"/logger:FileLogger,""Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a""",
                                quotedProjectFileName
                            }));

                //Should fail as we are not changing existing lines
                Assert.Equal(
                    MSBuildApp.ExitType.InitializationError,
                    MSBuildApp.Execute(
                        new[]
                            {
                                NativeMethodsShared.IsWindows ? @"c:\bin\msbuild.exe" : "/msbuild.exe",
                                "/logger:FileLogger,Microsoft.Build,Version=11111", quotedProjectFileName
                            }));
#endif
            }
            finally
            {
                File.Delete(projectFileName);
            }
        }

#if FEATURE_SPECIAL_FOLDERS
        private string _pathToArbitraryBogusFile = NativeMethodsShared.IsWindows // OK on 64 bit as well
                                                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")
                                                        : "/bin/cat";
#else
        private string _pathToArbitraryBogusFile = NativeMethodsShared.IsWindows // OK on 64 bit as well
                                                        ? Path.Combine(FileUtilities.GetFolderPath(FileUtilities.SpecialFolder.System), "notepad.exe")
                                                        : "/bin/cat";
#endif

        /// <summary>
        /// Basic case
        /// </summary>
        [Fact]
        public void GetCommandLine()
        {
            var msbuildParameters = "\"" + _pathToArbitraryBogusFile + "\"" + (NativeMethodsShared.IsWindows ? " /v:diag" : " -v:diag");
            Assert.True(File.Exists(_pathToArbitraryBogusFile));

            bool successfulExit;
            string output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
            Assert.False(successfulExit);

            Assert.Contains(RunnerUtilities.PathToCurrentlyRunningMsBuildExe + (NativeMethodsShared.IsWindows ? " /v:diag " : " -v:diag ") + _pathToArbitraryBogusFile, output, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Quoted path
        /// </summary>
        [Fact]
        public void GetCommandLineQuotedExe()
        {
            var msbuildParameters = "\"" + _pathToArbitraryBogusFile + "\"" + (NativeMethodsShared.IsWindows ? " /v:diag" : " -v:diag");
            Assert.True(File.Exists(_pathToArbitraryBogusFile));

            bool successfulExit;
            string pathToMSBuildExe = RunnerUtilities.PathToCurrentlyRunningMsBuildExe;
            // This @pathToMSBuildExe is used directly with Process, so don't quote it on
            // Unix
            if (NativeMethodsShared.IsWindows)
            {
                pathToMSBuildExe = "\"" + pathToMSBuildExe + "\"";
            }

            string output = RunnerUtilities.ExecMSBuild(pathToMSBuildExe, msbuildParameters, out successfulExit);
            Assert.False(successfulExit);

            Assert.Contains(RunnerUtilities.PathToCurrentlyRunningMsBuildExe + (NativeMethodsShared.IsWindows ? " /v:diag " : " -v:diag ") + _pathToArbitraryBogusFile, output, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// On path
        /// </summary>
        [Fact]
        public void GetCommandLineQuotedExeOnPath()
        {
            string output = null;
            string current = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(RunnerUtilities.PathToCurrentlyRunningMsBuildExe));

                var msbuildParameters = "\"" + _pathToArbitraryBogusFile + "\"" + (NativeMethodsShared.IsWindows ? " /v:diag" : " -v:diag");

                bool successfulExit;
                output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
                Assert.False(successfulExit);
            }
            finally
            {
                Directory.SetCurrentDirectory(current);
            }

            Assert.Contains(RunnerUtilities.PathToCurrentlyRunningMsBuildExe + (NativeMethodsShared.IsWindows ? " /v:diag " : " -v:diag ") + _pathToArbitraryBogusFile, output, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and should
        /// take priority over any other response files.
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryFoundImplicitly()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, AutoResponseFileName);

            string currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                // Find the project in the current directory
                Directory.SetCurrentDirectory(directory);

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(String.Empty, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=1]"));
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
                File.Delete(projectPath);
                File.Delete(rspPath);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and should
        /// take priority over any other response files.
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryExplicit()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, AutoResponseFileName);

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                var msbuildParameters = "\"" + projectPath + "\"";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=1]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and not any random .rsp
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryRandomName()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, "foo.rsp");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                var msbuildParameters = "\"" + projectPath + "\"";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, 
        /// but lower precedence than the actual command line
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryCommandLineWins()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, AutoResponseFileName);

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1";
                File.WriteAllText(rspPath, rspContent);

                var msbuildParameters = "\"" + projectPath + "\"" + " /p:A=2";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=2]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, 
        /// but lower precedence than the actual command line and higher than the msbuild.rsp next to msbuild.exe
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryWinsOverMainMSBuildRsp()
        {
            string directory = null;
            string exeDirectory = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string projectPath = Path.Combine(directory, "my.proj");
                string rspPath = Path.Combine(directory, AutoResponseFileName);

                exeDirectory = CopyMSBuild();
                string exePath = Path.Combine(exeDirectory, MSBuildExeName);
                string mainRspPath = Path.Combine(exeDirectory, AutoResponseFileName);

                Directory.CreateDirectory(exeDirectory);

                File.WriteAllText(mainRspPath, "/p:A=0");

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                File.WriteAllText(rspPath, "/p:A=1");

                var msbuildParameters = "\"" + projectPath + "\"";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(exePath, msbuildParameters, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=1]"));
            }
            finally
            {
                RobustDelete(directory);
                RobustDelete(exeDirectory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, 
        /// but not if it's the same as the msbuild.exe directory
        /// </summary>
        [Fact]
        public void ProjectDirectoryIsMSBuildExeDirectory()
        {
            string directory = null;

            try
            {
                directory = CopyMSBuild();
                string projectPath = Path.Combine(directory, "my.proj");
                string rspPath = Path.Combine(directory, AutoResponseFileName);
                string exePath = Path.Combine(directory, MSBuildExeName);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                File.WriteAllText(rspPath, "/p:A=1");

                var msbuildParameters = "\"" + projectPath + "\"";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(exePath, msbuildParameters, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=1]"));
            }
            finally
            {
                RobustDelete(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution with /noautoresponse in, is an error
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryItselfWithNoAutoResponseSwitch()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, AutoResponseFileName);

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1 /noautoresponse";
                File.WriteAllText(rspPath, rspContent);

                var msbuildParameters = "\"" + projectPath + "\"";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
                Assert.False(successfulExit);

                Assert.True(output.Contains("MSB1027")); // msbuild.rsp cannot have /noautoresponse in it
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be ignored if cmd line has /noautoresponse
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryButCommandLineNoAutoResponseSwitch()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");
            string rspPath = Path.Combine(directory, AutoResponseFileName);

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string rspContent = "/p:A=1 /noautoresponse";
                File.WriteAllText(rspPath, rspContent);

                var msbuildParameters = "\"" + projectPath + "\" /noautoresponse";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=]"));
            }
            finally
            {
                File.Delete(projectPath);
                File.Delete(rspPath);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Any msbuild.rsp in the directory of the specified project/solution should be read, and should
        /// take priority over any other response files. Sanity test when there isn't one.
        /// </summary>
        [Fact]
        public void ResponseFileInProjectDirectoryNullCase()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(directory, "my.proj");

            try
            {
                Directory.CreateDirectory(directory);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                var msbuildParameters = "\"" + projectPath + "\"";

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(msbuildParameters, out successfulExit);
                Assert.True(successfulExit);

                Assert.True(output.Contains("[A=]"));
            }
            finally
            {
                File.Delete(projectPath);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

#region IgnoreProjectExtensionTests

        /// <summary>
        /// Test the case where the extension is a valid extension but is not a project
        /// file extension. In this case no files should be ignored
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchOneProjNotFoundExtension()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected my.proj to be only project found"
        }

        /// <summary>
        /// Test the case where two identical extensions are asked to be ignored
        /// </summary>
        [Fact]
        public void TestTwoIdenticalExtensionsToIgnore()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension", ".phantomextension" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected my.proj to be only project found"
        }
        /// <summary>
        /// Pass a null and an empty list of project extensions to ignore, this simulates the switch not being set on the commandline
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchNullandEmptyProjectsToIgnore()
        {
            string[] projects = new string[] { "my.proj" };
            string[] extensionsToIgnore = (string[])null;
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected my.proj to be only project found"

            extensionsToIgnore = new string[] { };
            Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected my.proj to be only project found"
        }

        /// <summary>
        /// Pass in one extension and a null value
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchNullInList()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "my.proj" };
                string[] extensionsToIgnore = new string[] { ".phantomextension", null };
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected my.proj to be only project found"
            }
           );
        }
        /// <summary>
        /// Pass in one extension and an empty string
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchEmptyInList()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "my.proj" };
                string[] extensionsToIgnore = new string[] { ".phantomextension", string.Empty };
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected my.proj to be only project found"
            }
           );
        }
        /// <summary>
        /// If only a dot is specified then the extension is invalid
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchExtensionWithoutDot()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "my.proj" };
                string[] extensionsToIgnore = new string[] { "phantomextension" };
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase));
            }
           );
        }
        /// <summary>
        /// Put some junk into the extension, in this case there should be an exception
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchMalformed()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "my.proj" };
                string[] extensionsToIgnore = new string[] { ".C:\\boocatmoo.a" };
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                Assert.Equal(0, String.Compare("my.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected my.proj to be only project found"
            }
           );
        }
        /// <summary>
        /// Test what happens if there are no project or solution files in the directory
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchWildcards()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "my.proj" };
                string[] extensionsToIgnore = new string[] { ".proj*", ".nativeproj?" };
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
                // Should not get here
                Assert.True(false);
            }
           );
        }
        [Fact]
        public void TestProcessProjectSwitch()
        {
            string[] projects = new string[] { "test.nativeproj", "test.vcproj" };
            string[] extensionsToIgnore = new string[] { ".phantomextension", ".vcproj" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.nativeproj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.nativeproj to be only project found"

            projects = new string[] { "test.nativeproj", "test.vcproj", "test.proj" };
            extensionsToIgnore = new string[] { ".phantomextension", ".vcproj" };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.proj to be only project found"

            projects = new string[] { "test.nativeproj", "test.vcproj" };
            extensionsToIgnore = new string[] { ".vcproj" };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.nativeproj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.nativeproj to be only project found"

            projects = new string[] { "test.proj", "test.sln" };
            extensionsToIgnore = new string[] { ".vcproj" };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.sln to be only solution found"

            projects = new string[] { "test.proj", "test.sln", "test.proj~", "test.sln~" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.sln to be only solution found"

            projects = new string[] { "test.proj" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.proj to be only project found"

            projects = new string[] { "test.proj", "test.proj~" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.proj to be only project found"

            projects = new string[] { "test.sln" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.sln to be only solution found"

            projects = new string[] { "test.sln", "test.sln~" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.sln to be only solution found"


            projects = new string[] { "test.sln~", "test.sln" };
            extensionsToIgnore = new string[] { };
            projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.sln", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.sln to be only solution found"
        }


        /// <summary>
        /// Ignore .sln and .vcproj files to replicate Building_DF_LKG functionality
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchReplicateBuildingDFLKG()
        {
            string[] projects = new string[] { "test.proj", "test.sln", "Foo.vcproj" };
            string[] extensionsToIgnore = { ".sln", ".vcproj" };
            IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
            Assert.Equal(0, String.Compare("test.proj", MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles), StringComparison.OrdinalIgnoreCase)); // "Expected test.proj to be only project found"
        }


        /// <summary>
        /// Test the case where we remove all of the project extensions that exist in the directory
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchRemovedAllprojects()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects;
                string[] extensionsToIgnore = null;
                projects = new string[] { "test.nativeproj", "test.vcproj" };
                extensionsToIgnore = new string[] { ".nativeproj", ".vcproj" };
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Test the case where there is a solution and a project in the same directory but they have different names
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchSlnProjDifferentNames()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "test.proj", "Different.sln" };
                string[] extensionsToIgnore = null;
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
                // Should not get here
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Test the case where we have two proj files in the same directory
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchTwoProj()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "test.proj", "Different.proj" };
                string[] extensionsToIgnore = null;
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);

                // Should not get here
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Test the case where we have two native project files in the same directory
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchTwoNative()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "test.nativeproj", "Different.nativeproj" };
                string[] extensionsToIgnore = null;
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);

                // Should not get here
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Test when there are two solutions in the smae directory
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchTwoSolutions()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "test.sln", "Different.sln" };
                string[] extensionsToIgnore = null;
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
                // Should not get here
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Check the case where there are more than two projects in the directory and one is a proj file
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchMoreThenTwoProj()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { "test.nativeproj", "Different.csproj", "Another.proj" };
                string[] extensionsToIgnore = null;
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
                // Should not get here
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Test what happens if there are no project or solution files in the directory
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchNoProjectOrSolution()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string[] projects = new string[] { };
                string[] extensionsToIgnore = null;
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(projects);
                MSBuildApp.ProcessProjectSwitch(new string[0] { }, extensionsToIgnore, projectHelper.GetFiles);
                // Should not get here
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Helper class to simulate directory work for ignore project extensions
        /// </summary>
        internal class IgnoreProjectExtensionsHelper
        {
            private List<string> _directoryFileNameList;

            /// <summary>
            /// Takes in a list of file names to simulate as being in a directory
            /// </summary>
            /// <param name="filesInDirectory"></param>
            internal IgnoreProjectExtensionsHelper(string[] filesInDirectory)
            {
                _directoryFileNameList = new List<string>();
                foreach (string file in filesInDirectory)
                {
                    _directoryFileNameList.Add(file);
                }
            }

            /// <summary>
            /// Mocks System.IO.GetFiles. Takes in known search patterns and returns files which
            /// are provided through the constructor
            /// </summary>
            /// <param name="path">not used</param>
            /// <param name="searchPattern">Pattern of files to return</param>
            /// <returns></returns>
            internal string[] GetFiles(string path, string searchPattern)
            {
                List<string> fileNamesToReturn = new List<string>();
                foreach (string file in _directoryFileNameList)
                {
                    if (String.Compare(searchPattern, "*.sln", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (String.Compare(Path.GetExtension(file), ".sln", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            fileNamesToReturn.Add(file);
                        }
                    }
                    else if (String.Compare(searchPattern, "*.*proj", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (Path.GetExtension(file).Contains("proj"))
                        {
                            fileNamesToReturn.Add(file);
                        }
                    }
                }
                return fileNamesToReturn.ToArray();
            }
        }

        /// <summary>
        /// Verifies that when a directory is specified that a project can be found.
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchDirectory()
        {
            string projectDirectory = Directory.CreateDirectory(Path.Combine(ObjectModelHelpers.TempProjectDir, Guid.NewGuid().ToString("N"))).FullName;

            try
            {
                string expectedProject = "project1.proj";
                string[] extensionsToIgnore = null;
                IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(new[] { expectedProject });
                string actualProject = MSBuildApp.ProcessProjectSwitch(new[] { projectDirectory }, extensionsToIgnore, projectHelper.GetFiles);

                Assert.Equal(expectedProject, actualProject);
            }
            finally
            {
                RobustDelete(projectDirectory);
            }
        }

        /// <summary>
        /// Verifies that when a directory is specified and there are multiple projects that the correct error is thrown.
        /// </summary>
        [Fact]
        public void TestProcessProjectSwitchDirectoryMultipleProjects()
        {
            string projectDirectory = Directory.CreateDirectory(Path.Combine(ObjectModelHelpers.TempProjectDir, Guid.NewGuid().ToString("N"))).FullName;

            try
            {
                InitializationException exception = Assert.Throws<InitializationException>(() =>
                {
                    string[] extensionsToIgnore = null;
                    IgnoreProjectExtensionsHelper projectHelper = new IgnoreProjectExtensionsHelper(new[] { "project1.proj", "project2.proj" });
                    MSBuildApp.ProcessProjectSwitch(new[] { projectDirectory }, extensionsToIgnore, projectHelper.GetFiles);
                });

                Assert.Equal(ResourceUtilities.FormatResourceString("AmbiguousProjectDirectoryError", projectDirectory), exception.Message);
            }
            finally
            {
                RobustDelete(projectDirectory);
            }
        }
#endregion

#region ProcessFileLoggerSwitches
        /// <summary>
        /// Test the case where no file logger switches are given, should be no file loggers attached
        /// </summary>
        [Fact]
        public void TestProcessFileLoggerSwitch1()
        {
            bool distributedFileLogger = false;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, distributedLoggerRecords.Count); // "Expected no distributed loggers to be attached"
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
        }

        /// <summary>
        /// Test the case where a central logger and distributed logger are attached
        /// </summary>
        [Fact]
        public void TestProcessFileLoggerSwitch2()
        {
            bool distributedFileLogger = true;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected one distributed loggers to be attached"
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
        }

        /// <summary>
        /// Test the case where a central file logger is attached but no distributed logger
        /// </summary>
        [Fact]
        public void TestProcessFileLoggerSwitch3()
        {
            bool distributedFileLogger = false;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, distributedLoggerRecords.Count); // "Expected no distributed loggers to be attached"
            Assert.Equal(0, loggers.Count); // "Expected a central loggers to be attached"

            // add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[1] { "Parameter" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, distributedLoggerRecords.Count); // "Expected no distributed loggers to be attached"
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"

            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "Parameter1", "Parameter" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, distributedLoggerRecords.Count); // "Expected no distributed loggers to be attached"
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
        }

        /// <summary>
        /// Test the case where a distributed file logger is attached but no central logger
        /// </summary>
        [Fact]
        public void TestProcessFileLoggerSwitch4()
        {
            bool distributedFileLogger = true;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected a distributed logger to be attached"
            Assert.Equal(0, string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, "logFile=" + Path.Combine(Directory.GetCurrentDirectory(), "MSBuild.log"), StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"

            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[1] { "verbosity=Normal;" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected a distributed logger to be attached"
            Assert.Equal(0, string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, fileLoggerParameters[0] + ";logFile=" + Path.Combine(Directory.GetCurrentDirectory(), "MSBuild.log"), StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"

            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "verbosity=Normal", "" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected a distributed logger to be attached"
            Assert.Equal(0, string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, fileLoggerParameters[0] + ";logFile=" + Path.Combine(Directory.GetCurrentDirectory(), "MSBuild.log"), StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"

            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "", "Parameter1" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected a distributed logger to be attached"
            Assert.Equal(0, string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, ";Parameter1;logFile=" + Path.Combine(Directory.GetCurrentDirectory(), "MSBuild.log"), StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"


            // Not add a set of parameters and make sure the logger has those parameters
            distributedLoggerRecords = new List<DistributedLoggerRecord>();

            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "Parameter1", "verbosity=Normal;logfile=" + (NativeMethodsShared.IsWindows ? "c:\\temp\\cat.log" : "/tmp/cat.log") };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected a distributed logger to be attached"
            Assert.Equal(0, string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, fileLoggerParameters[0] + ";" + fileLoggerParameters[1], StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"

            distributedLoggerRecords = new List<DistributedLoggerRecord>();
            loggers = new ArrayList();
            fileLoggerParameters = new string[2] { "Parameter1", "verbosity=Normal;logfile=" + Path.Combine("..", "cat.log") + ";Parameter1" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected a distributed logger to be attached"
            Assert.Equal(0, string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, "Parameter1;verbosity=Normal;logFile=" + Path.Combine(Directory.GetCurrentDirectory(), "..", "cat.log") +";Parameter1", StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"

            loggers = new ArrayList();
            distributedLoggerRecords = new List<DistributedLoggerRecord>();
            fileLoggerParameters = new string[6] { "Parameter1", ";Parameter;", "", ";", ";Parameter", "Parameter;" };
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           2
                       );
            Assert.Equal(0, string.Compare(((DistributedLoggerRecord)distributedLoggerRecords[0]).ForwardingLoggerDescription.LoggerSwitchParameters, "Parameter1;Parameter;;;Parameter;Parameter;logFile=" + Path.Combine(Directory.GetCurrentDirectory(), "msbuild.log"), StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"
        }

        /// <summary>
        /// Verify when in single proc mode the file logger enables mp logging and does not show eventId
        /// </summary>
        [Fact]
        public void TestProcessFileLoggerSwitch5()
        {
            bool distributedFileLogger = false;
            string[] fileLoggerParameters = null;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>();

            ArrayList loggers = new ArrayList();
            MSBuildApp.ProcessDistributedFileLogger
                       (
                           distributedFileLogger,
                           fileLoggerParameters,
                           distributedLoggerRecords,
                           loggers,
                           1
                       );
            Assert.Equal(0, distributedLoggerRecords.Count); // "Expected no distributed loggers to be attached"
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
        }
#endregion

#region ProcessConsoleLoggerSwitches
        [Fact]
        public void ProcessConsoleLoggerSwitches()
        {
            ArrayList loggers = new ArrayList();
            LoggerVerbosity verbosity = LoggerVerbosity.Normal;
            List<DistributedLoggerRecord> distributedLoggerRecords = new List<DistributedLoggerRecord>(); ;
            string[] consoleLoggerParameters = new string[6] { "Parameter1", ";Parameter;", "", ";", ";Parameter", "Parameter;" };

            MSBuildApp.ProcessConsoleLoggerSwitch
                       (
                           true,
                           consoleLoggerParameters,
                           distributedLoggerRecords,
                           verbosity,
                           1,
                           loggers
                       );
            Assert.Equal(0, loggers.Count); // "Expected no central loggers to be attached"
            Assert.Equal(0, distributedLoggerRecords.Count); // "Expected no distributed loggers to be attached"

            MSBuildApp.ProcessConsoleLoggerSwitch
                       (
                           false,
                           consoleLoggerParameters,
                           distributedLoggerRecords,
                           verbosity,
                           1,
                           loggers
                       );
            Assert.Equal(1, loggers.Count); // "Expected a central loggers to be attached"
            Assert.Equal(0, string.Compare(((ILogger)loggers[0]).Parameters, "EnableMPLogging;SHOWPROJECTFILE=TRUE;Parameter1;Parameter;;;parameter;Parameter", StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameters passed in"

            MSBuildApp.ProcessConsoleLoggerSwitch
                       (
                          false,
                          consoleLoggerParameters,
                          distributedLoggerRecords,
                          verbosity,
                          2,
                          loggers
                      );
            Assert.Equal(1, loggers.Count); // "Expected a central loggers to be attached"
            Assert.Equal(1, distributedLoggerRecords.Count); // "Expected a distributed logger to be attached"
            DistributedLoggerRecord distributedLogger = ((DistributedLoggerRecord)distributedLoggerRecords[0]);
            Assert.Equal(0, string.Compare(distributedLogger.CentralLogger.Parameters, "SHOWPROJECTFILE=TRUE;Parameter1;Parameter;;;parameter;Parameter", StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameters passed in"
            Assert.Equal(0, string.Compare(distributedLogger.ForwardingLoggerDescription.LoggerSwitchParameters, "SHOWPROJECTFILE=TRUE;Parameter1;Parameter;;;Parameter;Parameter", StringComparison.OrdinalIgnoreCase)); // "Expected parameter in logger to match parameter passed in"
        }
#endregion

        private string CopyMSBuild()
        {
            string dest = null;
            try
            {
                string source = Path.GetDirectoryName(RunnerUtilities.PathToCurrentlyRunningMsBuildExe);
                dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

                Directory.CreateDirectory(dest);

                // Copy MSBuild.exe & dependent files (they will not be in the GAC so they must exist next to msbuild.exe)
                var filesToCopy = Directory
                    .EnumerateFiles(source)
                    .Where(f=> f.EndsWith(".dll") || f.EndsWith(".tasks") || f.EndsWith(".exe") || f.EndsWith(".exe.config") || f.EndsWith(".runtimeconfig.json"));

                var directoriesToCopy = Directory
                    .EnumerateDirectories(source)
                    .Where(d => Directory.EnumerateFiles(d).Any(f => f.EndsWith("resources.dll")));  // Copy satellite assemblies

                foreach (var file in filesToCopy)
                {
                    File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
                }

                foreach (var directory in directoriesToCopy)
                {
                    foreach (var sourceFile in Directory.EnumerateFiles(directory, "*"))
                    {
                        var destinationFile = sourceFile.Replace(source, dest);

                        var directoryName = Path.GetDirectoryName(destinationFile);
                        Directory.CreateDirectory(directoryName);
                        
                        File.Copy(sourceFile, destinationFile);
                    }
                }

                return dest;
            }
            catch (Exception)
            {
                RobustDelete(dest);
                throw;
            }
        }
    }
}
