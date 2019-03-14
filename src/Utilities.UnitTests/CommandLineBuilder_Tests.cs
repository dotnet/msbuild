// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class CommandLineBuilderTest
    {
        /*
        * Method:   AppendSwitchSimple
        *
        * Just append a simple switch.
        */
        [Fact]
        public void AppendSwitchSimple()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/a");
            c.AppendSwitch("-b");
            c.ShouldBe("/a -b");
        }

        /*
        * Method:   AppendSwitchWithStringParameter
        *
        * Append a switch that has a string parameter.
        */
        [Fact]
        public void AppendSwitchWithStringParameter()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/animal:", "dog");
            c.ShouldBe("/animal:dog");
        }

        /*
        * Method:   AppendSwitchWithSpacesInParameter
        *
        * This should trigger implicit quoting.
        */
        [Fact]
        public void AppendSwitchWithSpacesInParameter()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/animal:", "dog and pony");
            c.ShouldBe("/animal:\"dog and pony\"");
        }

        /// <summary>
        /// Test for AppendSwitchIfNotNull for the ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSwitchWithSpacesInParameterTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/animal:", new TaskItem("dog and pony"));
            c.ShouldBe("/animal:\"dog and pony\"");
        }

        /*
        * Method:   AppendLiteralSwitchWithSpacesInParameter
        *
        * Implicit quoting should not happen.
        */
        [Fact]
        public void AppendLiteralSwitchWithSpacesInParameter()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchUnquotedIfNotNull("/animal:", "dog and pony");
            c.ShouldBe("/animal:dog and pony");
        }

        /*
        * Method:   AppendTwoStringsEnsureNoSpace
        *
        * When appending two comma-delimited strings, there should be no space before the comma.
        */
        [Fact]
        public void AppendTwoStringsEnsureNoSpace()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull(new[] { "Form1.resx", FileUtilities.FixFilePath("built\\Form1.resources") }, ",");

            // There shouldn't be a space before or after the comma
            // Tools like resgen require comma-delimited lists to be bumped up next to each other.
            c.ShouldBe(FileUtilities.FixFilePath(@"Form1.resx,built\Form1.resources"));
        }

        /*
        * Method:   AppendSourcesArray
        *
        * Append several sources files using JoinAppend
        */
        [Fact]
        public void AppendSourcesArray()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull(new[] { "Mercury.cs", "Venus.cs", "Earth.cs" }, " ");

            // Managed compilers use this function to append sources files.
            c.ShouldBe(@"Mercury.cs Venus.cs Earth.cs");
        }

        /*
        * Method:   AppendSourcesArrayWithDashes
        *
        * Append several sources files starting with dashes using JoinAppend
        */
        [Fact]
        public void AppendSourcesArrayWithDashes()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull(new[] { "-Mercury.cs", "-Venus.cs", "-Earth.cs" }, " ");

            // Managed compilers use this function to append sources files.
            c.ShouldBe($".{Path.DirectorySeparatorChar}-Mercury.cs .{Path.DirectorySeparatorChar}-Venus.cs .{Path.DirectorySeparatorChar}-Earth.cs");
        }

        /// <summary>
        /// Test AppendFileNamesIfNotNull, the ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSourcesArrayWithDashesTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull(new[] { new TaskItem("-Mercury.cs"), null, new TaskItem("Venus.cs"), new TaskItem("-Earth.cs") }, " ");

            // Managed compilers use this function to append sources files.
            c.ShouldBe($".{Path.DirectorySeparatorChar}-Mercury.cs  Venus.cs .{Path.DirectorySeparatorChar}-Earth.cs");
        }

        /*
        * Method:   JoinAppendEmpty
        *
        * Append an empty array. Result should be NOP.
        */
        [Fact]
        public void JoinAppendEmpty()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull(new[] { "" }, " ");

            // Managed compilers use this function to append sources files.
            c.ShouldBe("");
        }

        /*
        * Method:   JoinAppendNull
        *
        * Append an empty array. Result should be NOP.
        */
        [Fact]
        public void JoinAppendNull()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull((string[])null, " ");

            // Managed compilers use this function to append sources files.
            c.ShouldBe("");
        }

        /// <summary>
        /// Append a switch with parameter array, quoting
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayQuoting()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchIfNotNull("/switch:", new[] { "Mer cury.cs", "Ve nus.cs", "Ear th.cs" }, ",");

            // Managed compilers use this function to append sources files.
            c.ShouldBe("/something /switch:\"Mer cury.cs\",\"Ve nus.cs\",\"Ear th.cs\"");
        }

        /// <summary>
        /// Append a switch with parameter array, quoting, ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayQuotingTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchIfNotNull("/switch:", new[] { new TaskItem("Mer cury.cs"), null, new TaskItem("Ve nus.cs"), new TaskItem("Ear th.cs") }, ",");

            // Managed compilers use this function to append sources files.
            c.ShouldBe("/something /switch:\"Mer cury.cs\",,\"Ve nus.cs\",\"Ear th.cs\"");
        }

        /// <summary>
        /// Append a switch with parameter array, no quoting
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayNoQuoting()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchUnquotedIfNotNull("/switch:", new[] { "Mer cury.cs", "Ve nus.cs", "Ear th.cs" }, ",");

            // Managed compilers use this function to append sources files.
            c.ShouldBe("/something /switch:Mer cury.cs,Ve nus.cs,Ear th.cs");
        }

        /// <summary>
        /// Append a switch with parameter array, no quoting, ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayNoQuotingTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchUnquotedIfNotNull("/switch:", new[] { new TaskItem("Mer cury.cs"), null, new TaskItem("Ve nus.cs"), new TaskItem("Ear th.cs") }, ",");

            // Managed compilers use this function to append sources files.
            c.ShouldBe("/something /switch:Mer cury.cs,,Ve nus.cs,Ear th.cs");
        }

        /// <summary>
        /// Appends a single file name
        /// </summary>
        [Fact]
        public void AppendSingleFileName()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendFileNameIfNotNull("-Mercury.cs");
            c.AppendFileNameIfNotNull("Mercury.cs");
            c.AppendFileNameIfNotNull("Mer cury.cs");

            // Managed compilers use this function to append sources files.
            c.ShouldBe($"/something .{Path.DirectorySeparatorChar}-Mercury.cs Mercury.cs \"Mer cury.cs\"");
        }

        /// <summary>
        /// Appends a single file name, ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSingleFileNameTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendFileNameIfNotNull(new TaskItem("-Mercury.cs"));
            c.AppendFileNameIfNotNull(new TaskItem("Mercury.cs"));
            c.AppendFileNameIfNotNull(new TaskItem("Mer cury.cs"));

            // Managed compilers use this function to append sources files.
            c.ShouldBe($"/something .{Path.DirectorySeparatorChar}-Mercury.cs Mercury.cs \"Mer cury.cs\"");
        }

        /// <summary>
        /// Verify that we throw an exception correctly for the case where we don't have a switch name
        /// </summary>
        [Fact]
        public void AppendSingleFileNameWithQuotes()
        {
            Should.Throw<ArgumentException>(() =>
            {
                // Cannot have escaped quotes in a file name
                CommandLineBuilder c = new CommandLineBuilder();
                c.AppendFileNameIfNotNull("string with \"quotes\"");

                c.ShouldBe("\"string with \\\"quotes\\\"\"");
            }
           );
        }
        /// <summary>
        /// Trigger escaping of literal quotes.
        /// </summary>
        [Fact]
        public void AppendSwitchWithLiteralQuotesInParameter()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", "LSYSTEM_COMPATIBLE_ASSEMBLY_NAME=L\"Microsoft.Windows.SystemCompatible\"");
            c.ShouldBe("/D\"LSYSTEM_COMPATIBLE_ASSEMBLY_NAME=L\\\"Microsoft.Windows.SystemCompatible\\\"\"");
        }

        /// <summary>
        /// Trigger escaping of literal quotes.
        /// </summary>
        [Fact]
        public void AppendSwitchWithLiteralQuotesInParameter2()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"ASSEMBLY_KEY_FILE=""c:\\foo\\FinalKeyFile.snk""");
            c.ShouldBe(@"/D""ASSEMBLY_KEY_FILE=\""c:\\foo\\FinalKeyFile.snk\""""");
        }

        /// <summary>
        /// Trigger escaping of literal quotes. This time, a double set of literal quotes.
        /// </summary>
        [Fact]
        public void AppendSwitchWithLiteralQuotesInParameter3()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"""A B"" and ""C""");
            c.ShouldBe(@"/D""\""A B\"" and \""C\""""");
        }

        /// <summary>
        /// When a value contains a backslash, it doesn't normally need escaping.
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchContainingBackslash()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"A \B");
            c.ShouldBe(@"/D""A \B""");
        }

        /// <summary>
        /// Backslashes before quotes need escaping themselves.
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchContainingBackslashBeforeLiteralQuote()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"A"" \""B");
            c.ShouldBe(@"/D""A\"" \\\""B""");
        }

        /// <summary>
        /// Don't quote if not asked to
        /// </summary>
        [Fact]
        public void AppendSwitchUnquotedIfNotNull()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchUnquotedIfNotNull("/D", @"A"" \""B");
            c.ShouldBe(@"/DA"" \""B");
        }

        /// <summary>
        /// When a value ends with a backslash, that certainly should be escaped if it's
        /// going to be quoted.
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchEndingInBackslash()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"A B\");
            c.ShouldBe(@"/D""A B\\""");
        }

        /// <summary>
        /// Backslashes don't need to be escaped if the string isn't going to get quoted.
        /// </summary>
        [Fact]
        public void AppendNonQuotableSwitchEndingInBackslash()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"AB\");
            c.ShouldBe(@"/DAB\");
        }

        /// <summary>
        /// Quoting of hyphens
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchWithHyphen()
        {
            CommandLineBuilder c = new CommandLineBuilder(/* do not quote hyphens*/);
            c.AppendSwitchIfNotNull("/D", @"foo-bar");
            c.ShouldBe(@"/Dfoo-bar");
        }

        /// <summary>
        /// Quoting of hyphens 2
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchWithHyphenQuoting()
        {
            CommandLineBuilder c = new CommandLineBuilder(true /* quote hyphens*/);
            c.AppendSwitchIfNotNull("/D", @"foo-bar");
            c.ShouldBe(@"/D""foo-bar""");
        }

        /// <summary>
        /// Appends an ITaskItem item spec as a parameter
        /// </summary>
        [Fact]
        public void AppendSwitchTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder(true);
            c.AppendSwitchIfNotNull("/D", new TaskItem(@"foo-bar"));
            c.ShouldBe(@"/D""foo-bar""");
        }

        /// <summary>
        /// Appends an ITaskItem item spec as a parameter
        /// </summary>
        [Fact]
        public void AppendSwitchUnQuotedTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder(true);
            c.AppendSwitchUnquotedIfNotNull("/D", new TaskItem(@"foo-bar"));
            c.ShouldBe(@"/Dfoo-bar");
        }

        /// <summary>
        /// Ensure it's not an error to have an odd number of literal quotes. Sometimes
        /// it's a mistake on the programmer's side, but we cannot reject odd numbers of
        /// quotes in the general case because sometimes that's exactly what's needed (e.g.
        /// passing a string with a single embedded double-quote to a compiler).
        /// </summary>
        [Fact]
        public void AppendSwitchWithOddNumberOfLiteralQuotesInParameter()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"A='""'");     //   /DA='"'
            c.ShouldBe(@"/D""A='\""'"""); //   /D"A='\"'"
        }

        [Fact]
        public void UseNewLineSeparators()
        {
            CommandLineBuilder c = new CommandLineBuilder(quoteHyphensOnCommandLine: false, useNewLineSeparator: true);

            c.AppendSwitchIfNotNull("/foo:", "bar");
            c.AppendFileNameIfNotNull("18056896847C4FFC9706F1D585C077B4");
            c.AppendSwitch("/D:");
            c.AppendTextUnquoted("C7E1720B16E5477D8D15733006E68278");


            string[] actual = c.ToString().Split(MSBuildConstants.EnvironmentNewLine, StringSplitOptions.None);
            string[] expected = 
            {
                "/foo:bar",
                "18056896847C4FFC9706F1D585C077B4",
                "/D:C7E1720B16E5477D8D15733006E68278"
            };

            actual.ShouldBe(expected);
        }

        internal class TestCommandLineBuilder : CommandLineBuilder
        {
            internal void TestVerifyThrow(string switchName, string parameter)
            {
                VerifyThrowNoEmbeddedDoubleQuotes(switchName, parameter);
            }

            protected override void VerifyThrowNoEmbeddedDoubleQuotes(string switchName, string parameter)
            {
                base.VerifyThrowNoEmbeddedDoubleQuotes(switchName, parameter);
            }
        }

        /// <summary>
        /// Test the else of VerifyThrowNOEmbeddedDouble quotes where the switch name is not empty or null
        /// </summary>
        [Fact]
        public void TestVerifyThrowElse()
        {
            Should.Throw<ArgumentException>(() =>
            {
                TestCommandLineBuilder c = new TestCommandLineBuilder();
                c.TestVerifyThrow("SuperSwitch", @"Parameter");
                c.TestVerifyThrow("SuperSwitch", @"Para""meter");
            }
           );
        }

        
    }

    internal static class CommandLineBuilderExtensionMethods
    {
        public static void ShouldBe(this CommandLineBuilder commandLineBuilder, string expected)
        {
            commandLineBuilder.ToString().ShouldBe(expected);
        }
    }
}
