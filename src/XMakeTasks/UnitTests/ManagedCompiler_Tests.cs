// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ManagedCompiler_Tests
    {
        #region Test DebugType and EmitDebugInformation settings
        // We are testing to verify the following functionality
        // Debug Symbols              DebugType   Desired Resilts
        //          True               Full        /debug+ /debug:full
        //          True               PdbOnly     /debug+ /debug:PdbOnly
        //          True               None        /debug-
        //          True               Blank       /debug+
        //          False              Full        /debug- /debug:full
        //          False              PdbOnly     /debug- /debug:PdbOnly
        //          False              None        /debug-
        //          False              Blank       /debug-
        //          Blank              Full                /debug:full
        //          Blank              PdbOnly             /debug:PdbOnly
        //          Blank              None        /debug-
        // Debug:   Blank              Blank       /debug+ //Microsof.common.targets will set DebugSymbols to true
        // Release: Blank              Blank       "Nothing for either switch"

        /// <summary>
        /// Verify the test matrix for DebugSymbols = true
        /// </summary>
        [TestMethod]
        public void TestDebugSymbolsTrue()
        {
            // Verify each of the DebugType settings when EmitDebugInformation is true
            MyManagedCompiler m = new MyManagedCompiler();
            m.DebugType = "Full";
            m.EmitDebugInformation = true;
            m.AddResponseFileCommands();
            // We expect to see only /debug+ on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == true, "Expected to find /debug+ on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == false, "Not expected to find /debug- on the commandline");
            // Expect to only find Full on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:Full") == true, "Expected to find /debug:Full on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:PdbOnly") == false, "Not expected to find /debug:PdbOnly on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = "PdbOnly";
            m.EmitDebugInformation = true;
            m.AddResponseFileCommands();
            // We expect to see only /debug+ on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == true, "Expected to find /debug+ on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == false, "Not expected to find /debug- on the commandline");
            // Expect to find only PdbOnly on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:PdbOnly") == true, "Expected to find /debug:PdbOnly on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:Full") == false, "Not expected to find /debug:Full on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = "none";
            m.EmitDebugInformation = true;
            m.AddResponseFileCommands();
            // We expect to see /debug- on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == true, "Expected to find /debug- on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == false, "Not expected to find /debug+ on the commandline");
            // We do not expect to see any /debug: on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:") == false, "Not expected to find /debug: on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = null;
            m.EmitDebugInformation = true;
            m.AddResponseFileCommands();
            // We expect to see only /debug+ on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == true, "Expected to find /debug+ on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == false, "Not expected to find /debug- on the commandline");
            // We expect to not find any /debug: on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:") == false, "Not expected to find /debug: on the commandline");
        }

        /// <summary>
        /// Verify the test matrix for DebugSymbols = false
        /// </summary>
        [TestMethod]
        public void DebugSymbolsFalse()
        {
            // Verify each of the DebugType settings when EmitDebugInformation is false
            MyManagedCompiler m = new MyManagedCompiler();
            m.DebugType = "Full";
            m.EmitDebugInformation = false;
            m.AddResponseFileCommands();
            // We expect to see /debug-
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == true, "Expected to find /debug- on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == false, "Not expected to find /debug+ on the commandline");
            // We expect to find /debug:Full
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:Full") == true, "Expected to find /debug:Full on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:PdbOnly") == false, "Not expected to find /debug:PdbOnly on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = "PdbOnly";
            m.EmitDebugInformation = false;
            m.AddResponseFileCommands();
            // We expect to see /debug-
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == true, "Expected to find /debug- on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == false, "Not expected to find /debug+ on the commandline");
            // We expect to find /debug:PdbOnly
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:PdbOnly") == true, "Expected to find /debug:PdbOnly on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:Full") == false, "Not expected to find /debug:Full on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = "none";
            m.EmitDebugInformation = false;
            m.AddResponseFileCommands();
            // We expect to see /debug- on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == true, "Expected to find /debug- on the commandline");
            // We do not expect to see andy /debug: on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:") == false, "Not expected to find /debug: on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = null;
            m.EmitDebugInformation = false;
            m.AddResponseFileCommands();
            // We expect to see /debug-
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == true, "Expected to find /debug- on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == false, "Not expected to find /debug+ on the commandline");
            // We do not expect to find ANY /debug: on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:") == false, "Not expected to find /debug: on the commandline");
        }

        /// <summary>
        /// Verify the test matrix for DebugSymbols when it is not set
        /// </summary>
        [TestMethod]
        public void TestDebugSymbolsNull()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.DebugType = "Full";
            m.AddResponseFileCommands();
            // We expect to not see /debug + or -
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == false, "Not expected to find /debug- on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == false, "Not expected to find /debug+ on the commandline");
            // We expect to find /debug:Full
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:Full") == true, "Expected to find /debug:Full on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:PdbOnly") == false, "Not expected to find /debug:PdbOnly on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = "PdbOnly";
            m.AddResponseFileCommands();
            // We do not expect to see /debug + or -
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == false, "Not expected to find /debug- on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == false, "Not expected to find /debug+ on the commandline");
            // We expect to find /debug:PdbOnly
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:PdbOnly") == true, "Expected to find /debug:PdbOnly on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:Full") == false, "Not expected to find /debug:Full on the commandline");

            m = new MyManagedCompiler();
            m.DebugType = "none";
            m.AddResponseFileCommands();
            // We expect to see /debug- on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == true, "Expected to find /debug- on the commandline");
            // We do not expect to see any /debug: on the commandline
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:") == false, "Not expected to find /debug: on the commandline");

            // The cases where DebugType and DebugSymbols are Blank(not set) is a special case because in microsoft.common.targets 
            // when the configuration is "debug" and both DebugType and DebugSymbols are blank DebugSymbols will be set to True.
            // In relase the DebugSymbols will remail blank
            // Debug:   Blank              Blank       /debug+ //Microsof.common.targets will set DebugSymbols to true.
            // This makes the case equal to the testing of EmitDebugSymbols=true and DebugType=null which is done in TestDebugSymbolsTrue above.
            // Release: Blank              Blank       "Nothing for either switch"
            m = new MyManagedCompiler();
            m.DebugType = null;
            m.AddResponseFileCommands();
            // We do not expect to find /debug+ or /debug-
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug+") == false, "Not expected to find /debug+ on the commandline");
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug-") == false, "Not expected to find /debug- on the commandline");
            // We do not expect to find /debug:
            Assert.IsTrue(m.VerifySwitchOnCommandLine("/debug:") == false, "Not expected to find /debug: on the commandline");
        }

        #endregion

        [TestMethod]
        public void DuplicateSources()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("foo"), new TaskItem("foo") };
            Assert.IsTrue(!m.AccessValidateParameters());
            ((MockEngine)m.BuildEngine).AssertLogContains("MSB3105");
        }

        [TestMethod]
        public void DuplicateResourcesWithNoLogicalNames()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("bar") };
            m.Resources = new ITaskItem[] { new TaskItem("foo.resources"), new TaskItem("foo.resources") };
            // This is an error
            Assert.IsTrue(!m.AccessValidateParameters());
            ((MockEngine)m.BuildEngine).AssertLogContains("MSB3105");
        }

        [TestMethod]
        public void DuplicateResourcesButWithDifferentLogicalNames()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("bar") };
            TaskItem resource1 = new TaskItem("foo.resources");
            resource1.SetMetadata("LogicalName", "value1");
            TaskItem resource2 = new TaskItem("foo.resources");
            resource2.SetMetadata("LogicalName", "value2");
            m.Resources = new ITaskItem[] { resource1, resource2 };
            // This is okay
            Assert.IsTrue(m.AccessValidateParameters());
            ((MockEngine)m.BuildEngine).AssertLogDoesntContain("MSB3105");
            ((MockEngine)m.BuildEngine).AssertLogDoesntContain("MSB3083");
        }

        [TestMethod]
        public void DefaultWin32ManifestEmbeddedInConsoleApp()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("bar") };
            m.TargetType = "EXE";

            Assert.IsTrue
            (
                m.AccessGetWin32ManifestSwitch(false, null).EndsWith("default.win32manifest", StringComparison.OrdinalIgnoreCase),
                "default.win32manifest should be embedded in a console exe!"
            );
        }

        [TestMethod]
        public void DefaultWin32ManifestEmbeddedInConsoleAppWhenTargetTypeInferred()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("bar") };

            Assert.IsTrue
            (
                m.AccessGetWin32ManifestSwitch(false, null).EndsWith("default.win32manifest", StringComparison.OrdinalIgnoreCase),
                "default.win32manifest should be embedded in a console exe!"
            );
        }

        [TestMethod]
        public void DefaultWin32ManifestNotEmbeddedInClassLibrary()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("bar") };
            m.TargetType = "LIBRary";

            Assert.IsTrue
            (
                String.IsNullOrEmpty(m.AccessGetWin32ManifestSwitch(false, null)),
                "default.win32manifest should NOT be embedded in a class library!"
            );
        }

        [TestMethod]
        public void DefaultWin32ManifestNotEmbeddedInNetModule()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("bar") };
            m.TargetType = "modULE";

            Assert.IsTrue
            (
                String.IsNullOrEmpty(m.AccessGetWin32ManifestSwitch(false, null)),
                "default.win32manifest should NOT be embedded in a net module!"
            );
        }

        [TestMethod]
        public void DuplicateResourcesWithSameLogicalNames()
        {
            MyManagedCompiler m = new MyManagedCompiler();
            m.BuildEngine = new MockEngine(true);
            m.Sources = new ITaskItem[] { new TaskItem("bar") };
            TaskItem resource1 = new TaskItem("foo.resources");
            resource1.SetMetadata("LogicalName", "value1");
            TaskItem resource2 = new TaskItem("foo.resources");
            resource2.SetMetadata("LogicalName", "value1");
            m.Resources = new ITaskItem[] { resource1, resource2 };
            // This is an error
            Assert.IsTrue(!m.AccessValidateParameters());
            ((MockEngine)m.BuildEngine).AssertLogContains("MSB3083");
        }
    }

    /// <summary>
    /// Class implementing ManagedCompiler so that its protected methods can
    /// be accessed
    /// </summary>
    internal class MyManagedCompiler : ManagedCompiler
    {
        private MyCommandLineBuilderExtension _commandLineBuilder = new MyCommandLineBuilderExtension();
        protected override string ToolName { get { return String.Empty; } }
        protected override string GenerateFullPathToTool() { return String.Empty; }

        public void AddResponseFileCommands()
        {
            base.AddResponseFileCommands(_commandLineBuilder);
        }

        public bool VerifySwitchOnCommandLine(string switchToVerify)
        {
            return _commandLineBuilder.LogContains(switchToVerify);
        }

        public bool AccessValidateParameters()
        {
            return base.ValidateParameters();
        }

        public string AccessGetWin32ManifestSwitch(bool noDefaultWin32Manifest, string win32Manifest)
        {
            return base.GetWin32ManifestSwitch(noDefaultWin32Manifest, win32Manifest);
        }
    }


    /// <summary>
    /// Class implementing CommandLineBuilderExtension so that its protected methods can
    /// be accessed
    /// </summary>
    internal class MyCommandLineBuilderExtension : CommandLineBuilderExtension
    {
        internal bool LogContains(string contains)
        {
            if (this.CommandLine != null)
            {
                string commandLineUpperInvariant = this.CommandLine.ToString().ToUpperInvariant();
                return commandLineUpperInvariant.Contains(contains.ToUpperInvariant());
            }
            else
            {
                if (contains == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
