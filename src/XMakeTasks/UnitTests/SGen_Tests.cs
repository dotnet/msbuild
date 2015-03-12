// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.IO;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class SGen_Tests
    {
        internal class SGenExtension : SGen
        {
            internal string CommandLine()
            {
                return base.GenerateCommandLineCommands();
            }
        }

        [TestMethod]
        public void KeyFileQuotedOnCommandLineIfNecessary()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;

            // This should result in a nested, quoted parameter on
            // the command line, which ultimately looks like this:
            //
            //   /compiler:"/keyfile:\"c:\Some Folder\MyKeyFile.snk\""
            //
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";

            string commandLine = sgen.CommandLine();

            Assert.IsTrue(commandLine.IndexOf("/compiler:\"/keyfile:\\\"" + sgen.KeyFile + "\\\"\"", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [TestMethod]
        public void TestKeepFlagTrue()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = true;

            string commandLine = sgen.CommandLine();

            Assert.IsTrue(commandLine.IndexOf("/keep", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        [TestMethod]
        public void TestKeepFlagFalse()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = false;

            string commandLine = sgen.CommandLine();

            Assert.IsTrue(commandLine.IndexOf("/keep", StringComparison.OrdinalIgnoreCase) < 0);
        }


        [TestMethod]
        public void TestInputChecks1()
        {
            MockEngine engine = new MockEngine();
            SGenExtension sgen = new SGenExtension();
            sgen.BuildEngine = engine;
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" + Path.GetInvalidPathChars()[0];
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            // This should result in a quoted parameter...
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";
            string commandLine = sgen.CommandLine();
            Assert.IsTrue(engine.Errors == 1);
        }

        [TestMethod]
        public void TestInputChecks2()
        {
            MockEngine engine = new MockEngine();
            SGenExtension sgen = new SGenExtension();
            sgen.BuildEngine = engine;
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll" + Path.GetInvalidPathChars()[0];
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            // This should result in a quoted parameter...
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";
            string commandLine = sgen.CommandLine();
            Assert.IsTrue(engine.Errors == 1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestInputChecks3()
        {
            MockEngine engine = new MockEngine();
            SGenExtension sgen = new SGenExtension();
            sgen.BuildEngine = engine;
            sgen.BuildAssemblyName = null;
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            // This should result in a quoted parameter...
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";
            string commandLine = sgen.CommandLine();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestInputChecks4()
        {
            MockEngine engine = new MockEngine();
            SGenExtension sgen = new SGenExtension();
            sgen.BuildEngine = engine;
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = null;
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            // This should result in a quoted parameter...
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";

            string commandLine = sgen.CommandLine();
        }

        [TestMethod]
        public void TestInputPlatform()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.Platform = "x86";
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();

            Assert.IsTrue(String.Equals(commandLine, "/assembly:\"C:\\SomeFolder\\MyAsm.dll\\MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /compiler:/platform:x86", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestInputTypes()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.Types = new string[] { "System.String", "System.Boolean" };
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();

            Assert.IsTrue(String.Equals(commandLine, "/assembly:\"C:\\SomeFolder\\MyAsm.dll\\MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /type:System.String /type:System.Boolean", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestInputEmptyTypesAndPlatform()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();

            Assert.IsTrue(String.Equals(commandLine, "/assembly:\"C:\\SomeFolder\\MyAsm.dll\\MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\"", StringComparison.OrdinalIgnoreCase));
        }
    }
}
