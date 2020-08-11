// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   CscTests
     *
     * Test the Csc task in various ways.
     *
     */
    sealed internal class CommandLine
    {
        /// <summary>
        /// Invokes the ValidateParameters on the given ToolTask instance. We need to use reflection since
        /// ValidateParameters is inaccessible to Tasks unit tests.
        /// </summary>
        /// <returns></returns>
        static internal bool CallValidateParameters(ToolTask task)
        {
            MethodInfo validateMethod = typeof(ToolTask).GetMethod("ValidateParameters", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)validateMethod.Invoke(task, null);
        }

        /*
         * Method:      ParseCommandLine
         *
         * Simulates the parsing of a command line, taking into account quoting.
         *
         */
        private static string[] Parse(string cl)
        {
            int emptySplits;
            string[] pieces = (string[])QuotingUtilities.SplitUnquoted(cl, int.MaxValue, false, true, out emptySplits, ' ', '\n').ToArray(typeof(string));
            return pieces;
        }

        /*
         * Method:      ValidateHasParameter
         *
         * Validates that the given ToolTaskExtension's command line contains the indicated
         * parameter.  Returns the index of the parameter that matched.
         *
         */
        internal static int ValidateHasParameter(ToolTaskExtension t, string parameter)
        {
            return ValidateHasParameter(t, parameter, true /* use response file */);
        }

        /*
         * Method:      ValidateHasParameter
         *
         * Validates that the given ToolTaskExtension's command line contains the indicated
         * parameter.  Returns the index of the parameter that matched.
         *
         */
        internal static int ValidateHasParameter(ToolTaskExtension t, string parameter, bool useResponseFile)
        {
            CommandLineBuilderExtension b = new CommandLineBuilderExtension();

            if (useResponseFile)
                t.AddResponseFileCommands(b);
            else
                t.AddCommandLineCommands(b);

            string cl = b.ToString();
            string msg = String.Format("Command-line = [{0}]\r\n", cl);
            msg += String.Format(" Searching for [{0}]\r\n", parameter);

            string[] pieces = Parse(cl);

            int i = 0;
            foreach (string s in pieces)
            {
                msg += String.Format(" Parm = [{0}]\r\n", s);
                if (s == parameter)
                {
                    return i;
                }

                i++;
            }

            msg += "Not found!\r\n";
            Console.WriteLine(msg);
            Assert.True(false, msg); // Could not find the parameter.

            return 0;
        }

        /// <summary>
        /// Validates that the given ToolTaskExtension's command line does not contain 
        /// any parameter starting with the given string.
        /// </summary>
        /// <param name="t">task to get the command line from</param>
        /// <param name="startsWith">string to look for in the command line</param>
        /// <param name="useResponseFile">if true, use the response file cmd line, else use regular cmd line</param>
        internal static void ValidateNoParameterStartsWith(ToolTaskExtension t, string startsWith, bool useResponseFile)
        {
            ValidateNoParameterStartsWith(t, startsWith, "", useResponseFile);
        }

        /// <summary>
        /// Validates that the given ToolTaskExtension's command line does not contain 
        /// any parameter starting with the given string.
        /// </summary>
        /// <param name="t">task to get the command line from</param>
        /// <param name="startsWith">string to look for in the command line</param>
        internal static void ValidateNoParameterStartsWith(ToolTaskExtension t, string startsWith)
        {
            ValidateNoParameterStartsWith(t, startsWith, true);
        }

        /// <summary>
        /// Validates that the given ToolTaskExtension's command line does not contain 
        /// any parameter starting with the given string.
        /// </summary>
        /// <param name="t">task to get the command line from</param>
        /// <param name="startsWith">string to look for in the command line</param>
        /// <param name="except">only find strings that don't contain this argument</param>
        internal static void ValidateNoParameterStartsWith(ToolTaskExtension t, string startsWith, string except)
        {
            ValidateNoParameterStartsWith(t, startsWith, except, true);
        }

        /// <summary>
        /// Validates that the given ToolTaskExtension's command line does not contain 
        /// any parameter starting with the given string.
        /// </summary>
        /// <param name="t">task to get the command line from</param>
        /// <param name="startsWith">string to look for in the command line</param>
        /// <param name="except">only find strings that don't contain this argument</param>
        /// <param name="useResponseFile">if true, use the response file cmd line, else use regular cmd line</param>
        internal static void ValidateNoParameterStartsWith(
            ToolTaskExtension t,
            string startsWith,
            string except,
            bool useResponseFile
        )
        {
            CommandLineBuilderExtension b = new CommandLineBuilderExtension();

            if (useResponseFile)
                t.AddResponseFileCommands(b);
            else
                t.AddCommandLineCommands(b);

            string cl = b.ToString();

            string msg = String.Format("Command-line = [{0}]\r\n", cl);
            msg += String.Format(" Searching for something that starts with [{0}]\r\n", startsWith);
            msg += String.Format(" that doesn't contain [{0}]\r\n", except);

            string[] pieces = Parse(cl);

            foreach (string s in pieces)
            {
                msg += String.Format(" Parm = [{0}]\r\n", s);

                if (s.Length < startsWith.Length)
                {
                    // Skip anything shorter than the compare string.
                    continue;
                }
                if (String.Equals(s.Substring(0, startsWith.Length), startsWith, StringComparison.OrdinalIgnoreCase))
                {
                    // If this doesn't match the 'except' then this is an error.
                    if (!String.Equals(s, except, StringComparison.Ordinal))
                    {
                        msg += String.Format(" Found something!\r\n");
                        Console.WriteLine(msg);
                        Assert.True(false, msg); // Found the startsWith but shouldn't have.
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if command line generated by task contains given string.
        /// This is used to verify that the stuff that should get quoted actually does.
        /// </summary>
        /// <param name="t">task to get the command line from</param>
        /// <param name="lookFor">string to look for in the command line</param>
        /// <param name="useResponseFile">if true, use the response file cmd line, else use regular cmd line</param>
        internal static void ValidateContains(ToolTaskExtension t, string lookFor, bool useResponseFile)
        {
            CommandLineBuilderExtension b = new CommandLineBuilderExtension();

            if (useResponseFile)
                t.AddResponseFileCommands(b);
            else
                t.AddCommandLineCommands(b);

            string cl = b.ToString();
            string msg = String.Format("Command-line = [{0}]\r\n", cl);

            msg += String.Format(" Searching for [{0}]\r\n", lookFor);

            if (cl.IndexOf(lookFor) == -1)
            {
                msg += "Not found!\r\n";
                Console.WriteLine(msg);
                Assert.True(false, msg);
            }
        }

        /// <summary>
        /// Checks if command line generated by task contains given string.
        /// This is used to verify that the stuff that should get quoted actually does.
        /// </summary>
        /// <param name="t">task to get the command line from</param>
        /// <param name="lookFor">string to look for in the command line</param>
        /// <param name="useResponseFile">if true, use the response file cmd line, else use regular cmd line</param>
        internal static void ValidateDoesNotContain(ToolTaskExtension t, string lookFor, bool useResponseFile)
        {
            CommandLineBuilderExtension b = new CommandLineBuilderExtension();

            if (useResponseFile)
                t.AddResponseFileCommands(b);
            else
                t.AddCommandLineCommands(b);

            string cl = b.ToString();
            string msg = String.Format("Command-line = [{0}]\r\n", cl);

            msg += String.Format(" Searching for [{0}]\r\n", lookFor);
            if (cl.IndexOf(lookFor) != -1)
            {
                msg += "Found!\r\n";
                Console.WriteLine(msg);
                Assert.True(false, msg);
            }
        }

        /// <summary>
        /// Checks if command line generated by task matches the given string.
        /// </summary>
        /// <param name="t">task to get the command line from</param>
        /// <param name="lookFor">string to look for in the command line</param>
        /// <param name="useResponseFile">if true, use the response file cmd line, else use regular cmd line</param>
        internal static void ValidateEquals(ToolTaskExtension t, string lookFor, bool useResponseFile)
        {
            CommandLineBuilderExtension b = new CommandLineBuilderExtension();

            if (useResponseFile)
                t.AddResponseFileCommands(b);
            else
                t.AddCommandLineCommands(b);

            string cl = b.ToString();
            string msg = String.Format("Command-line = [{0}]\r\n", cl);
            msg += String.Format("Expected     = [{0}]\r\n", lookFor);

            if (cl != lookFor)
            {
                msg += "Does not match!\r\n";
                Console.WriteLine(msg);
                Assert.True(false, msg);
            }
        }

        internal static string GetCommandLine(ToolTaskExtension t, bool useResponseFile)
        {
            CommandLineBuilderExtension b = new CommandLineBuilderExtension();

            if (useResponseFile)
                t.AddResponseFileCommands(b);
            else
                t.AddCommandLineCommands(b);

            return b.ToString();
        }
    }
}
