// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Shouldly;

namespace Microsoft.Build.UnitTests
{
    public class SGen_Tests
    {
#if RUNTIME_TYPE_NETCORE
        [Fact]
        public void TaskFailsOnCore()
        {
            using (TestEnvironment testenv = TestEnvironment.Create())
            {
                MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(@$"
<Project>
    <Target Name=""MyTarget"">
        <SGen
            BuildAssemblyName=""Foo""
            BuildAssemblyPath=""Foo""
            ShouldGenerateSerializer=""true""
            UseProxyTypes=""true""
            UseKeep=""true""
            References=""Foo""
            KeyContainer=""Foo""
            KeyFile=""Foo""
            DelaySign=""true""
            SerializationAssembly=""Foo""
            SdkToolsPath=""Foo""
            Platform=""Foo""
            Types=""Foo""
        />
    </Target>
</Project>");
                logger.ErrorCount.ShouldBe(1);
                logger.Errors.First().Code.ShouldBe("MSB3474");
            }
        }
#else
        internal class SGenExtension : SGen
        {
            internal string CommandLine()
            {
                return base.GenerateCommandLineCommands();
            }
        }

        [Fact]
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

            Assert.True(commandLine.IndexOf("/compiler:\"/keyfile:\\\"" + sgen.KeyFile + "\\\"\"", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void TestKeepFlagTrue()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = true;

            string commandLine = sgen.CommandLine();

            Assert.True(commandLine.IndexOf("/keep", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void TestKeepFlagFalse()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = false;

            string commandLine = sgen.CommandLine();

            Assert.True(commandLine.IndexOf("/keep", StringComparison.OrdinalIgnoreCase) < 0);
        }


        [Fact]
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
            Assert.Equal(1, engine.Errors);
        }

        [Fact]
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
            Assert.Equal(1, engine.Errors);
        }

        [Fact]
        public void TestInputChecks3()
        {
            Assert.Throws<ArgumentNullException>(() =>
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
           );
        }

        [Fact]
        public void TestInputChecks4()
        {
            Assert.Throws<ArgumentNullException>(() =>
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
           );
        }
        [Fact]
        public void TestInputPlatform()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.Platform = "x86";
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = NativeMethodsShared.IsUnixLike
                                         ? "/SomeFolder/MyAsm.dll"
                                         : "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();
            string targetCommandLine = "/assembly:\"" + sgen.BuildAssemblyPath + Path.DirectorySeparatorChar
                                       + "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /compiler:/platform:x86";

            Assert.Equal(targetCommandLine, commandLine);
        }

        [Fact]
        public void TestInputTypes()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.Types = new string[] { "System.String", "System.Boolean" };
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = NativeMethodsShared.IsUnixLike
                                         ? "/SomeFolder/MyAsm.dll"
                                         : "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();
            string targetCommandLine = "/assembly:\"" + sgen.BuildAssemblyPath + Path.DirectorySeparatorChar
                                       + "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /type:System.String /type:System.Boolean";

            Assert.Equal(targetCommandLine, commandLine);
        }

        [Fact]
        public void TestInputEmptyTypesAndPlatform()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = NativeMethodsShared.IsUnixLike ? "/SomeFolder/MyAsm.dll" : "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();
            string targetCommandLine = "/assembly:\"" + sgen.BuildAssemblyPath + Path.DirectorySeparatorChar
                                       + "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\"";

            Assert.Equal(targetCommandLine, commandLine);
        }

        [Fact]
        public void TestNullReferences()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = false;
            sgen.References = null;

            string commandLine = sgen.CommandLine();

            Assert.True(commandLine.IndexOf("/reference:", StringComparison.OrdinalIgnoreCase) < 0);
        }

        [Fact]
        public void TestEmptyReferences()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = false;
            sgen.References = new string[]{ };

            string commandLine = sgen.CommandLine();

            Assert.True(commandLine.IndexOf("/reference:", StringComparison.OrdinalIgnoreCase) < 0);
        }

        [Fact]
        public void TestReferencesCommandLine()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = false;
            sgen.References = new string[]{ "C:\\SomeFolder\\reference1.dll", "C:\\SomeFolder\\reference2.dll" };

            string commandLine = sgen.CommandLine();
            string targetCommandLine = "/assembly:\"" + sgen.BuildAssemblyPath + Path.DirectorySeparatorChar
                                       + "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /reference:\"C:\\SomeFolder\\reference1.dll,C:\\SomeFolder\\reference2.dll\"";
            
            Assert.Equal(targetCommandLine, commandLine);
        }
#endif
    }
}
