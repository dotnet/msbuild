// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class CommandLineBuilderTest
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
            Assert.Equal(
                "/a " + "-b",
                c.ToString());
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
            Assert.Equal("/animal:dog", c.ToString());
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
            Assert.Equal("/animal:\"dog and pony\"", c.ToString());
        }

        /// <summary>
        /// Test for AppendSwitchIfNotNull for the ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSwitchWithSpacesInParameterTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/animal:", (ITaskItem)new TaskItem("dog and pony"));
            Assert.Equal("/animal:\"dog and pony\"", c.ToString());
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
            Assert.Equal("/animal:dog and pony", c.ToString());
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
            c.AppendFileNamesIfNotNull(new string[] { "Form1.resx", FileUtilities.FixFilePath("built\\Form1.resources") }, ",");

            // There shouldn't be a space before or after the comma
            // Tools like resgen require comma-delimited lists to be bumped up next to each other.
            Assert.Equal(FileUtilities.FixFilePath(@"Form1.resx,built\Form1.resources"), c.ToString());
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
            c.AppendFileNamesIfNotNull(new string[] { "Mercury.cs", "Venus.cs", "Earth.cs" }, " ");

            // Managed compilers use this function to append sources files.
            Assert.Equal(@"Mercury.cs Venus.cs Earth.cs", c.ToString());
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
            c.AppendFileNamesIfNotNull(new string[] { "-Mercury.cs", "-Venus.cs", "-Earth.cs" }, " ");

            // Managed compilers use this function to append sources files.
            Assert.Equal("." + Path.DirectorySeparatorChar + "-Mercury.cs ." +
                Path.DirectorySeparatorChar + "-Venus.cs ." +
                Path.DirectorySeparatorChar + "-Earth.cs", c.ToString());
        }

        /// <summary>
        /// Test AppendFileNamesIfNotNull, the ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSourcesArrayWithDashesTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull(new TaskItem[] { new TaskItem("-Mercury.cs"), null, new TaskItem("Venus.cs"), new TaskItem("-Earth.cs") }, " ");

            // Managed compilers use this function to append sources files.
            Assert.Equal("." + Path.DirectorySeparatorChar + "-Mercury.cs  Venus.cs ." + Path.DirectorySeparatorChar + "-Earth.cs", c.ToString());
        }

        /*
        * Method:   JoinAppendEmpty
        *
        * Append append and empty array. Result should be NOP.
        */
        [Fact]
        public void JoinAppendEmpty()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull(new string[] { "" }, " ");

            // Managed compilers use this function to append sources files.
            Assert.Equal(@"", c.ToString());
        }

        /*
        * Method:   JoinAppendNull
        *
        * Append append and empty array. Result should be NOP.
        */
        [Fact]
        public void JoinAppendNull()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendFileNamesIfNotNull((string[])null, " ");

            // Managed compilers use this function to append sources files.
            Assert.Equal(@"", c.ToString());
        }

        /// <summary>
        /// Append a switch with parameter array, quoting
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayQuoting()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchIfNotNull("/switch:", new string[] { "Mer cury.cs", "Ve nus.cs", "Ear th.cs" }, ",");

            // Managed compilers use this function to append sources files.
            Assert.Equal(
                "/something "
                + "/switch:\"Mer cury.cs\",\"Ve nus.cs\",\"Ear th.cs\"",
                c.ToString());
        }

        /// <summary>
        /// Append a switch with parameter array, quoting, ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayQuotingTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchIfNotNull("/switch:", new TaskItem[] { new TaskItem("Mer cury.cs"), null, new TaskItem("Ve nus.cs"), new TaskItem("Ear th.cs") }, ",");

            // Managed compilers use this function to append sources files.
            Assert.Equal(
                "/something "
                + "/switch:\"Mer cury.cs\",,\"Ve nus.cs\",\"Ear th.cs\"",
                c.ToString());
        }

        /// <summary>
        /// Append a switch with parameter array, no quoting
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayNoQuoting()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchUnquotedIfNotNull("/switch:", new string[] { "Mer cury.cs", "Ve nus.cs", "Ear th.cs" }, ",");

            // Managed compilers use this function to append sources files.
            Assert.Equal(
                "/something "
                + "/switch:Mer cury.cs,Ve nus.cs,Ear th.cs",
                c.ToString());
        }

        /// <summary>
        /// Append a switch with parameter array, no quoting, ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSwitchWithParameterArrayNoQuotingTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendSwitchUnquotedIfNotNull("/switch:", new TaskItem[] { new TaskItem("Mer cury.cs"), null, new TaskItem("Ve nus.cs"), new TaskItem("Ear th.cs") }, ",");

            // Managed compilers use this function to append sources files.
            Assert.Equal(
                "/something "
                + "/switch:Mer cury.cs,,Ve nus.cs,Ear th.cs",
                c.ToString());
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
            Assert.Equal(
                "/something ." + Path.DirectorySeparatorChar + "-Mercury.cs Mercury.cs \"Mer cury.cs\"",
                c.ToString());
        }

        /// <summary>
        /// Appends a single file name, ITaskItem version
        /// </summary>
        [Fact]
        public void AppendSingleFileNameTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitch("/something");
            c.AppendFileNameIfNotNull((ITaskItem)new TaskItem("-Mercury.cs"));
            c.AppendFileNameIfNotNull((ITaskItem)new TaskItem("Mercury.cs"));
            c.AppendFileNameIfNotNull((ITaskItem)new TaskItem("Mer cury.cs"));

            // Managed compilers use this function to append sources files.
            Assert.Equal(
                "/something ." + Path.DirectorySeparatorChar + "-Mercury.cs Mercury.cs \"Mer cury.cs\"",
                c.ToString());
        }

        /// <summary>
        /// Verify that we throw an exception correctly for the case where we don't have a switch name
        /// </summary>
        [Fact]
        public void AppendSingleFileNameWithQuotes()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                // Cannot have escaped quotes in a file name
                CommandLineBuilder c = new CommandLineBuilder();
                c.AppendFileNameIfNotNull("string with \"quotes\"");

                Assert.Equal("\"string with \\\"quotes\\\"\"", c.ToString());
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
            Assert.Equal(
                    "/D\"LSYSTEM_COMPATIBLE_ASSEMBLY_NAME=L\\\"Microsoft.Windows.SystemCompatible\\\"\"",
                c.ToString());
        }

        /// <summary>
        /// Trigger escaping of literal quotes.
        /// </summary>
        [Fact]
        public void AppendSwitchWithLiteralQuotesInParameter2()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"ASSEMBLY_KEY_FILE=""c:\\foo\\FinalKeyFile.snk""");
            Assert.Equal(
                @"/D""ASSEMBLY_KEY_FILE=\""c:\\foo\\FinalKeyFile.snk\""""",
                c.ToString());
        }

        /// <summary>
        /// Trigger escaping of literal quotes. This time, a double set of literal quotes.
        /// </summary>
        [Fact]
        public void AppendSwitchWithLiteralQuotesInParameter3()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"""A B"" and ""C""");
            Assert.Equal(@"/D""\""A B\"" and \""C\""""", c.ToString());
        }

        /// <summary>
        /// When a value contains a backslash, it doesn't normally need escaping.
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchContainingBackslash()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"A \B");
            Assert.Equal(@"/D""A \B""", c.ToString());
        }

        /// <summary>
        /// Backslashes before quotes need escaping themselves.
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchContainingBackslashBeforeLiteralQuote()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"A"" \""B");
            Assert.Equal(@"/D""A\"" \\\""B""", c.ToString());
        }

        /// <summary>
        /// Don't quote if not asked to
        /// </summary>
        [Fact]
        public void AppendSwitchUnquotedIfNotNull()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchUnquotedIfNotNull("/D", @"A"" \""B");
            Assert.Equal(@"/DA"" \""B", c.ToString());
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
            Assert.Equal(@"/D""A B\\""", c.ToString());
        }

        /// <summary>
        /// Backslashes don't need to be escaped if the string isn't going to get quoted.
        /// </summary>
        [Fact]
        public void AppendNonQuotableSwitchEndingInBackslash()
        {
            CommandLineBuilder c = new CommandLineBuilder();
            c.AppendSwitchIfNotNull("/D", @"AB\");
            Assert.Equal(@"/DAB\", c.ToString());
        }

        /// <summary>
        /// Quoting of hyphens
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchWithHyphen()
        {
            CommandLineBuilder c = new CommandLineBuilder(/* do not quote hyphens*/);
            c.AppendSwitchIfNotNull("/D", @"foo-bar");
            Assert.Equal(@"/Dfoo-bar", c.ToString());
        }

        /// <summary>
        /// Quoting of hyphens 2
        /// </summary>
        [Fact]
        public void AppendQuotableSwitchWithHyphenQuoting()
        {
            CommandLineBuilder c = new CommandLineBuilder(true /* quote hyphens*/);
            c.AppendSwitchIfNotNull("/D", @"foo-bar");
            Assert.Equal(@"/D""foo-bar""", c.ToString());
        }

        /// <summary>
        /// Appends an ITaskItem item spec as a parameter
        /// </summary>
        [Fact]
        public void AppendSwitchTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder(true);
            c.AppendSwitchIfNotNull("/D", new TaskItem(@"foo-bar"));
            Assert.Equal(@"/D""foo-bar""", c.ToString());
        }

        /// <summary>
        /// Appends an ITaskItem item spec as a parameter
        /// </summary>
        [Fact]
        public void AppendSwitchUnQuotedTaskItem()
        {
            CommandLineBuilder c = new CommandLineBuilder(true);
            c.AppendSwitchUnquotedIfNotNull("/D", new TaskItem(@"foo-bar"));
            Assert.Equal(@"/Dfoo-bar", c.ToString());
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
            Assert.Equal(@"/D""A='\""'""", c.ToString()); //   /D"A='\"'"
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
            Assert.Throws<ArgumentException>(() =>
            {
                TestCommandLineBuilder c = new TestCommandLineBuilder();
                c.TestVerifyThrow("SuperSwitch", @"Parameter");
                c.TestVerifyThrow("SuperSwitch", @"Para""meter");
            }
           );
        }
    }
}
