// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ToolTaskExtensionTests
    {
        /// <summary>
        /// Verify that the resources in ToolTask/derived classes work correctly (are accessible with correct resource managers)
        /// With moving ToolTask into Utilities, tasks inheriting from it now have to deal with 3 (THREE!) resource streams,
        /// which has a lot of potential for breaking. Make sure that tasks can access all of them using the correct logger helpers.
        /// </summary>
        [Fact]
        public void TestResourceAccess()
        {
            MyToolTaskExtension t = new MyToolTaskExtension();
            MockEngine engine = new MockEngine();

            t.BuildEngine = engine;

            // No need to actually check the outputted strings. We only care that this doesn't throw, which means that 
            // the resource strings were reachable.

            // Normal CSC messages first, from private XMakeTasks resources. They should be accessible with t.Log
            t.Log.LogErrorWithCodeFromResources("Csc.AssemblyAliasContainsIllegalCharacters", "PlanetSide", "Knights of the Old Republic");
            t.Log.LogWarningWithCodeFromResources("Csc.InvalidParameter");
            t.Log.LogMessageFromResources("Vbc.ParameterHasInvalidValue", "Rome Total War", "Need for Speed Underground");

            // Now shared messages. Should be accessible with the private LogShared property
            PropertyInfo logShared = typeof(ToolTask).GetProperty("LogShared", BindingFlags.Instance | BindingFlags.NonPublic);
            TaskLoggingHelper log = (TaskLoggingHelper)logShared.GetValue(t, null);
            log.LogWarningWithCodeFromResources("Shared.FailedCreatingTempFile", "Gothic II");
            log.LogMessageFromResources("Shared.CannotConvertStringToBool", "foo");

            // Now private Utilities messages. Should be accessible with the private LogPrivate property
            PropertyInfo logPrivate = typeof(ToolTask).GetProperty("LogPrivate", BindingFlags.Instance | BindingFlags.NonPublic);
            log = (TaskLoggingHelper)logPrivate.GetValue(t, null);
            log.LogErrorWithCodeFromResources("ToolTask.CommandTooLong", "Painkiller");
            log.LogWarningWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", "Fallout Tactics", "Fallout 2");
            log.LogMessageFromResources("ToolsLocationHelper.InvalidRedistFile", "Deus Ex", "Fallout");
        }

        /// <summary>
        /// Verify that the above method actually tests something, that is make sure that non-existent resources throw
        /// </summary>
        [Fact]
        public void ResourceAccessSanityCheck()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                MyToolTaskExtension t = new MyToolTaskExtension();
                MockEngine engine = new MockEngine();

                t.BuildEngine = engine;
                t.Log.LogErrorFromResources("Beyond Good and Evil");
            }
           );
        }
        /// <summary>
        /// Retrieve a non-existent value but ask for a default.
        /// </summary>
        [Fact]
        public void GetNonExistentBoolWithDefault()
        {
            MyToolTaskExtension t = new MyToolTaskExtension();
            Assert.Equal(5, t.GetIntParameterWithDefault("Key", 5));
        }

        /// <summary>
        /// Retrieve a value that exists, but ask for a default. We expect the
        /// real value to win.
        /// </summary>
        [Fact]
        public void GetBoolWithDefault()
        {
            MyToolTaskExtension t = new MyToolTaskExtension();
            t.Bag["Key"] = true;

            Assert.True(t.GetBoolParameterWithDefault("Key", false));
        }

        /// <summary>
        /// Retrieve a value that exists, but ask for a default. We expect the
        /// real value to win.
        /// </summary>
        [Fact]
        public void GetIntWithDefault()
        {
            MyToolTaskExtension t = new MyToolTaskExtension();
            t.Bag["Key"] = 5;

            Assert.Equal(5, t.GetIntParameterWithDefault("Key", 9));
        }

        [Fact]
        public void UseNewLineSeparatorseInResponseFile()
        {
            Action<CommandLineBuilderExtension> addResponseFileCommands = (commandLineBuilder) =>
            {
                commandLineBuilder.AppendSwitchIfNotNull("/A:", "D66B977148114482A88B0EFC1E531F02");
                commandLineBuilder.AppendSwitchIfNotNull("/B:", "F9E03765A87543F4B385664B8DB7619D");
            };

            MyToolTaskExtension t = new MyToolTaskExtension(useNewLineSeparators: true, addResponseFileCommands: addResponseFileCommands);
            
            string[] actual = t.GetResponseFileCommands().Split(MSBuildConstants.EnvironmentNewLine, StringSplitOptions.None);
            string[] expected =
            {
                "/A:D66B977148114482A88B0EFC1E531F02",
                "/B:F9E03765A87543F4B385664B8DB7619D"
            };

            Assert.Equal(expected, actual);
        }

        private class MyToolTaskExtension : ToolTaskExtension
        {
            private readonly Action<CommandLineBuilderExtension> _addResponseFileCommands;

            public MyToolTaskExtension(bool useNewLineSeparators = false, Action<CommandLineBuilderExtension> addResponseFileCommands = null)
            {
                _addResponseFileCommands = addResponseFileCommands;
                UseNewLineSeparatorInResponseFile = useNewLineSeparators;
            }
            protected override string ToolName
            {
                get { return "toolname"; }
            }

            protected override string GenerateFullPathToTool()
            {
                return "fullpath";
            }

            protected override bool UseNewLineSeparatorInResponseFile { get; }

            public string GetResponseFileCommands()
            {
                return GenerateResponseFileCommands();
            }

            protected internal override void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
            {
                _addResponseFileCommands?.Invoke(commandLine);

                base.AddResponseFileCommands(commandLine);
            }
        }
    }
}
