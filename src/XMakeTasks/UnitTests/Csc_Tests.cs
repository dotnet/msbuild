// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   CscTests
     *
     * Test the Csc task in various ways.
     *
     */
    [TestFixture]
    sealed public class CscTests
    {
        /// <summary>
        /// Tests the "References" parameter on the Csc task, and confirms that it sets
        /// the /reference switch on the command-line correctly.  The Csc task
        /// supports assembly aliases, so we want to make sure that we pass an assembly
        /// alias into csc.exe.
        /// </summary>
        [Test]
        public void SingleAliasOnAReference()
        {
            Csc t = new Csc();

            TaskItem reference = new TaskItem("System.Xml.dll");
            reference.SetMetadata("Aliases", "Foo");

            t.References = new TaskItem[] { reference };
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/reference:Foo=System.Xml.dll"));
        }

        /// <summary>
        /// Tests the "References" parameter on the Csc task, and confirms that it sets
        /// the /reference switch on the command-line correctly.  The Csc task
        /// supports assembly aliases, so we want to make sure that we pass an assembly
        /// alias into csc.exe.
        /// </summary>
        [Test]
        public void SingleAliasUnicodeOnAReference()
        {
            Csc t = new Csc();

            TaskItem reference = new TaskItem("System.Xml.dll");
            reference.SetMetadata("Aliases", "?");

            t.References = new TaskItem[] { reference };
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/reference:?=System.Xml.dll"));
        }


        /// <summary>
        /// Tests the "References" parameter on the Csc task, and confirms that it sets
        /// the /reference switch on the command-line correctly.  The Csc task
        /// supports assembly aliases, so we want to make sure that we pass an assembly
        /// alias into csc.exe.
        /// </summary>
        [Test]
        public void MultipleAliasesOnAReference()
        {
            Csc t = new Csc();

            TaskItem reference = new TaskItem("System.Xml.dll");
            reference.SetMetadata("Aliases", "Foo, Bar");

            t.References = new TaskItem[] { reference };
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/reference:Foo=System.Xml.dll"));
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/reference:Bar=System.Xml.dll"));
        }

        /// <summary>
        /// Tests the "References" parameter on the Csc task, and confirms that it sets
        /// the /reference switch on the command-line correctly.  The Csc task
        /// supports assembly aliases, so we want to make sure that we pass an assembly
        /// alias into csc.exe.
        /// </summary>
        [Test]
        public void NonAliasedReference1()
        {
            Csc t = new Csc();

            TaskItem reference = new TaskItem("System.Xml.dll");
            reference.SetMetadata("Aliases", "global");

            t.References = new TaskItem[] { reference };
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/reference:System.Xml.dll"));
        }

        /// <summary>
        /// Tests the "References" parameter on the Csc task, and confirms that it sets
        /// the /reference switch on the command-line correctly.  The Csc task
        /// supports assembly aliases, so we want to make sure that we pass an assembly
        /// alias into csc.exe.
        /// </summary>
        [Test]
        public void NonAliasedReference2()
        {
            Csc t = new Csc();

            TaskItem reference = new TaskItem("System.Xml.dll");

            t.References = new TaskItem[] { reference };
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/reference:System.Xml.dll"));
        }

        /// <summary>
        /// Tests the "References" parameter on the Csc task, and confirms that it sets
        /// the /reference switch on the command-line correctly.  The Csc task
        /// supports assembly aliases, so we want to make sure that we pass an assembly
        /// alias into csc.exe.
        /// </summary>
        [Test]
        public void GlobalAndExplicitAliasOnAReference()
        {
            Csc t = new Csc();

            TaskItem reference = new TaskItem("System.Xml.dll");
            reference.SetMetadata("Aliases", "global , Foo");

            t.References = new TaskItem[] { reference };
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/reference:System.Xml.dll"));
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/reference:Foo=System.Xml.dll"));
        }

        // Tests the "DefineConstants" parameter on the Csc task.  The task actually
        // needs to slightly munge the string that was passed in from the project file,
        // in order to maintain compatibility with VS.
        [Test]
        public void DefineConstants()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc t = new Csc();
            t.BuildEngine = mockEngine;

            // Perfectly valid, so no change expected.
            Assert.AreEqual("DEBUG;TRACE",
                t.GetDefineConstantsSwitch("DEBUG;TRACE"));

            // Spaces should be removed.
            Assert.AreEqual("DEBUG;TRACE",
                t.GetDefineConstantsSwitch("DEBUG; TRACE"));

            // Commas become semicolons.
            Assert.AreEqual("DEBUG;TRACE",
                t.GetDefineConstantsSwitch("DEBUG , TRACE"));

            // We ignore anything that has quotes.
            Assert.AreEqual("DEBUG",
                t.GetDefineConstantsSwitch("DEBUG , \"TRACE\""));

            // We ignore anything that has an equals sign.
            Assert.AreEqual("DEBUG;TRACE",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE=MYVALUE"));

            // Since we split on space and comma, what seems like a value actually
            // becomes a new constant.  Yes, this is really what happens in
            // Everett VS.
            Assert.AreEqual("DEBUG;TRACE;MYDEFINE;MYVALUE",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE = MYVALUE"));

            // Since we split on space and comma/semicolon, what seems like a value actually
            // becomes a new constant.  Yes, this is really what happens in
            // Everett VS.
            Assert.AreEqual("DEBUG;TRACE;MYDEFINE;MY;VALUE",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE = MY VALUE"));

            // Even if the comma is inside quotes, we still split on it.  Yup, this
            // is what VS did in Everett.
            Assert.AreEqual("DEBUG;TRACE;MYDEFINE;WEIRD",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE = \"MY,WEIRD,VALUE\""));

            // Once again, quotes aren't allowed.
            Assert.AreEqual("DEBUG;TRACE;MYDEFINE",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE = \"MY VALUE\""));

            // (,),@,%,$ aren't allowed, and spaces are a valid delimiter.
            Assert.AreEqual("DEBUG;TRACE;a;b",
                t.GetDefineConstantsSwitch("DEBUG;TRACE;a b;(;);@;%;$"));

            // Dash is not allowed either.  It's not a valid character in an
            // identifier.
            Assert.AreEqual("DEBUG;TRACE;MYDEFINE",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE = -1"));

            // Identifiers cannot begin with numbers.
            Assert.AreEqual("DEBUG;TRACE;MYDEFINE",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE;123ABC"));

            // But identifiers can contain numbers.
            Assert.AreEqual("DEBUG;TRACE;MYDEFINE;ABC123",
                t.GetDefineConstantsSwitch("DEBUG,TRACE,MYDEFINE;ABC123"));

            // Identifiers can contain initial underscores and embedded underscores.
            Assert.AreEqual("_DEBUG;MY_DEFINE",
                t.GetDefineConstantsSwitch("_DEBUG, MY_DEFINE"));

            // We should get back "null" if there's nothing valid in there.
            Assert.AreEqual(null,
                t.GetDefineConstantsSwitch("DEBUG=\"myvalue\""));
        }

        // Tests the "DebugType" and "EmitDebuggingInformation" parameters on the Csc task,
        // and confirms that it sets the /debug switch on the command-line correctly.
        [Test]
        public void DebugType()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Ignore("The test does not make sense, except under Windows, as pdbonly value is not supported");
            }

            Csc t = new Csc();

            t.DebugType = "pdbonly";
            t.EmitDebugInformation = true;

            int firstParamLocation = CommandLine.ValidateHasParameter(t, "/debug+");
            int secondParamLocation = CommandLine.ValidateHasParameter(t, "/debug:pdbonly");

            Assert.IsTrue(secondParamLocation > firstParamLocation, "The order of the /debug switches is incorrect.");
        }

        // Tests the "LangVersion" parameter on the Csc task, and confirms that it sets
        // the /langversion switch on the command-line correctly.
        [Test]
        public void LangVersion()
        {
            Csc t = new Csc();

            t.LangVersion = "v7.1";
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/langversion:v7.1"));
        }

        // Tests the "AdditionalLibPaths" parameter on the Csc task, and confirms that it sets
        // the /lib switch on the command-line correctly.
        [Test]
        public void AdditionaLibPaths()
        {
            Csc t = new Csc();

            t.AdditionalLibPaths = new string[] { @"c:\xmake\", @"c:\msbuild" };
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/lib:c:\xmake\,c:\msbuild"));
        }

        // Tests the "PreferredUILang" parameter on the Csc task, and confirms that it sets
        // the /preferreduilang switch on the command-line correctly.
        [Test]
        public void PreferredUILang()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Ignore("The preferreduilang is not supported, except under Windows");
            }

            Csc t = new Csc();
            CommandLine.ValidateNoParameterStartsWith(t, @"/preferreduilang:");

            t.PreferredUILang = "en-US";
            CommandLine.ValidateHasParameter(t, @"/preferreduilang:en-US");
        }

        // Tests the "Platform" parameter on the Csc task, and confirms that it sets
        // the /platform switch on the command-line correctly.
        [Test]
        public void Platform()
        {
            Csc t = new Csc();

            t.Platform = "x86";
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:x86"));
        }

        // Tests the "Platform" and "Prefer32Bit" parameter combinations on the Csc task,
        // and confirms that it sets the /platform switch on the command-line correctly.
        [Test]
        public void PlatformAndPrefer32Bit()
        {
            // Implicit "anycpu"
            Csc t = new Csc();
            CommandLine.ValidateNoParameterStartsWith(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:"));
            t = new Csc();
            t.Prefer32Bit = false;
            CommandLine.ValidateNoParameterStartsWith(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:"));
            t = new Csc();
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/platform:anycpu32bitpreferred"));

            // Explicit "anycpu"
            t = new Csc();
            t.Platform = "anycpu";
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:anycpu"));
            t = new Csc();
            t.Platform = "anycpu";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:anycpu"));
            t = new Csc();
            t.Platform = "anycpu";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/platform:anycpu32bitpreferred"));

            // Explicit "x86"
            t = new Csc();
            t.Platform = "x86";
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:x86"));
            t = new Csc();
            t.Platform = "x86";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:x86"));
            t = new Csc();
            t.Platform = "x86";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch(@"/platform:x86"));
        }

        // Tests the "HighEntropyVA" parameter on the Csc task, and confirms that it
        // sets the /highentropyva switch on the command-line correctly.
        [Test]
        public void HighEntropyVA()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Ignore("The highentropyva is not supported, except under Windows");
            }

            // Implicit /highentropyva-
            Csc t = new Csc();
            CommandLine.ValidateNoParameterStartsWith(t, @"/highentropyva");

            // Explicit /highentropyva-
            t = new Csc();
            t.HighEntropyVA = false;
            CommandLine.ValidateHasParameter(t, @"/highentropyva-");

            // Explicit /highentropyva+
            t = new Csc();
            t.HighEntropyVA = true;
            CommandLine.ValidateHasParameter(t, @"/highentropyva+");
        }

        // Tests the "PdbFile" parameter on the Csc task, and confirms that it sets
        // the /pdb switch on the command-line correctly.
        [Test]
        public void Pdb()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Ignore("The pdb switch is not supported, except under Windows");
            }

            Csc t = new Csc();

            t.PdbFile = "foo.pdb";
            CommandLine.ValidateHasParameter(t, @"/pdb:foo.pdb");
        }

        // Tests the "SubsystemVersion" parameter on the Csc task, and confirms that it sets
        // the /subsystemversion switch on the command-line correctly.
        [Test]
        public void SubsystemVersion()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Ignore("The subsystemversion is not supported, except under MONO");
            }

            Csc t = new Csc();
            CommandLine.ValidateNoParameterStartsWith(t, @"/subsystemversion");

            t = new Csc();
            t.SubsystemVersion = "4.0";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:4.0");

            t = new Csc();
            t.SubsystemVersion = "5";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:5");

            t = new Csc();
            t.SubsystemVersion = "6.02";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:6.02");

            t = new Csc();
            t.SubsystemVersion = "garbage";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:garbage");
        }

        // Tests the "ApplicationConfiguration" parameter on the Csc task, and confirms that it sets
        // the /appconfig switch on the command-line correctly.
        [Test]
        public void ApplicationConfiguration()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Ignore("The appconfig switch is not supported, except under Windows");
            }

            Csc t = new Csc();

            t.ApplicationConfiguration = "ConsoleApplication1.exe.config";
            CommandLine.ValidateHasParameter(t, @"/appconfig:ConsoleApplication1.exe.config");
        }

        // Tests the "UnsafeBlocks" parameter on the Csc task, and confirms that it sets
        // the /unsafe switch on the command-line correctly.
        [Test]
        public void UnsafeBlocks()
        {
            Csc t = new Csc();
            t.AllowUnsafeBlocks = true;
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/unsafe+"));
            t.AllowUnsafeBlocks = false;
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/unsafe-"));
        }

        // Tests the "WarningsAsErrors" parameter on the Csc task, and confirms that it sets
        // the /warnaserror switch on the command-line correctly.
        [Test]
        public void WarningsAsErrors()
        {
            Csc t = new Csc();

            t.WarningsAsErrors = "1234 ;5678";
            t.TreatWarningsAsErrors = false;

            int firstParamLocation = CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/warnaserror-"));
            int secondParamLocation = CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/warnaserror+:1234,5678"));

            Assert.IsTrue(secondParamLocation > firstParamLocation, "The order of the /warnaserror switches is incorrect.");
        }

        // Check all parameters that are based on ints, bools and other value types.
        // This is because parameters with these types go through a not-so-typesafe check
        // for existence in the property bag.
        [Test]
        public void FlagsAndInts()
        {
            Csc t = new Csc();

            // From managed compiler
            t.CodePage = 5;
            t.EmitDebugInformation = true;
            t.DelaySign = true;
            t.FileAlignment = 9;
            t.NoLogo = true;
            t.Optimize = true;
            t.TreatWarningsAsErrors = true;
            t.Utf8Output = true;

            // From csc.
            t.AllowUnsafeBlocks = true;
            t.CheckForOverflowUnderflow = true;
            t.GenerateFullPaths = true;
            t.NoStandardLib = true;
            t.WarningLevel = 5;

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/codepage:5"));
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/debug+"));
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/delaysign+"));
            // The filealign, nologo, utf8output switches is not supported, except under Windows.
            if (NativeMethodsShared.IsWindows)
            {
                CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/filealign:9"));
                CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/nologo"));
                CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/utf8output"));
            }
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/optimize+"));
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/warnaserror+"));
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/unsafe+"));
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/checked+"));
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/fullpaths"));
            CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/warn:5"));
        }


        [Test]
        [Ignore("Disabled pending decision whether to support host objects")]
        public void CscHostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;
            MockCscHostObject cscHostObject = new MockCscHostObject();
            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsTrue(!cscHostObject.CompileMethodWasCalled);

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };
            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.IsTrue(cscHostObject.CompileMethodWasCalled);
        }

        [Test]
        [Ignore("Disabled pending decision whether to support host objects")]
        public void CscHostObject2()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;
            MockCscHostObject2 cscHostObject = new MockCscHostObject2();
            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsTrue(!cscHostObject.CompileMethodWasCalled);

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };
            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.IsTrue(cscHostObject.CompileMethodWasCalled);
        }

        [Test]
        [Ignore("Disabled pending decision whether to support host objects")]
        public void CscHostObject3()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;
            MockCscHostObject3 cscHostObject = new MockCscHostObject3();
            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsTrue(!cscHostObject.CompileMethodWasCalled);

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };
            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.IsTrue(cscHostObject.CompileMethodWasCalled);
        }

        [Test]
        public void CscHostObjectNotUsedIfToolNameSpecified()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;
            MockCscHostObject cscHostObject = new MockCscHostObject();
            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;
            csc.ToolExe = "csc_custom.exe";

            Assert.IsTrue(csc.UseAlternateCommandLineToolToExecute());
        }

        [Test]
        public void CscHostObjectNotUsedIfToolPathSpecified()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;
            MockCscHostObject cscHostObject = new MockCscHostObject();
            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;
            csc.ToolPath = "c:\\some\\custom\\path";

            Assert.IsTrue(csc.UseAlternateCommandLineToolToExecute());
        }


        /*
        * Class:   CscCrossParameterInjection
        *
        * Test the Csc task for cases where the parameters are passed in
        * with with whitespace and other special characters to try to fool
        * us into spawning Csc.exe while bypassing security
        */
        [TestFixture]
        sealed public class CscCrossParameterInjection
        {
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void Win32IconEmbeddedQuote()
            {
                Csc t = new Csc();
                t.Win32Icon = "MyFile.ico\\\" /out:c:\\windows\\system32\\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void Sources()
            {
                Csc t = new Csc();

                t.Sources = new TaskItem[]
                    {
                        new TaskItem("parm0.cs"),
                        new TaskItem("parm1 /out:c:\\windows\\system32\\notepad.exe")
                    };

                // If sources are specified, but not OutputAssembly is specified then the Csc task
                // will create an OutputAssembly that is the name of the first source file with the
                // extension replaced with .exe.
                CommandLine.ValidateHasParameter(t, CommandLineBuilder.FixCommandLineSwitch("/out:parm0.exe"));

                // Still, we don't want and additional /out added.
                CommandLine.ValidateNoParameterStartsWith(
                    t,
                    CommandLineBuilder.FixCommandLineSwitch("/out"),
                    CommandLineBuilder.FixCommandLineSwitch("/out:parm0.exe"));
            }

            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void SourcesEmbeddedQuote()
            {
                Csc t = new Csc();

                // Embedded quotes could be used to escape from our quoting. We throw an exception
                // when this happens.
                t.Sources = new TaskItem[]
                    {
                        new TaskItem("parm0\\\" /out:c:\\windows\\system32\\notepad.exe")
                    };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void Win32Icon()
            {
                Csc t = new Csc();
                t.Win32Icon = @"MyFile.ico /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, CommandLineBuilder.FixCommandLineSwitch("/out"));
                CommandLine.ValidateHasParameter(
                    t,
                    CommandLineBuilder.FixCommandLineSwitch(
                        @"/win32icon:MyFile.ico /out:c:\windows\system32\notepad.exe"));
            }

            [Test]
            public void AdditionalLibPaths()
            {
                Csc t = new Csc();
                t.AdditionalLibPaths = new string[] { @"parm /out:c:\windows\system32\notepad.exe" };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void AddModules()
            {
                Csc t = new Csc();
                t.AddModules = new string[] { @"parm /out:c:\windows\system32\notepad.exe" };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void BaseAddress()
            {
                Csc t = new Csc();
                t.BaseAddress = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void DebugType()
            {
                Csc t = new Csc();
                t.DebugType = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            [Ignore("Because constants may legitimately contains quotes _and_ we've cut security, we decided to let DefineConstants be passed through literally.")]
            public void DefineConstants()
            {
                Csc t = new Csc();
                t.DefineConstants = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void DisabledWarnings()
            {
                Csc t = new Csc();
                t.DisabledWarnings = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void DocumentationFile()
            {
                Csc t = new Csc();
                t.DocumentationFile = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void KeyContainer()
            {
                Csc t = new Csc();
                t.KeyContainer = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void KeyFile()
            {
                Csc t = new Csc();
                t.KeyFile = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, CommandLineBuilder.FixCommandLineSwitch("/out"));
            }

            [Test]
            public void LinkResources()
            {
                Csc t = new Csc();
                t.KeyFile = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, CommandLineBuilder.FixCommandLineSwitch("/out"));
            }

            [Test]
            public void MainEntryPoint()
            {
                Csc t = new Csc();
                t.MainEntryPoint = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, CommandLineBuilder.FixCommandLineSwitch("/out"));
            }

            [Test]
            public void OutputAssembly()
            {
                Csc t = new Csc();
                t.OutputAssembly = new TaskItem("parm1 /out:c:\\windows\\system32\\notepad.exe");
                CommandLine.ValidateHasParameter(
                    t,
                    CommandLineBuilder.FixCommandLineSwitch(
                        @"/out:parm1 /out:" + FileUtilities.FixFilePath(@"c:\windows\system32\notepad.exe")));
            }

            [Test]
            public void References()
            {
                Csc t = new Csc();
                t.References = new TaskItem[]
                    {
                        new TaskItem("parm0"),
                        new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe")
                    };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void Resources()
            {
                Csc t = new Csc();
                t.References = new TaskItem[]
                {
                    new TaskItem("parm0"),
                    new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe")
                };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void ResponseFiles()
            {
                Csc t = new Csc();
                t.ResponseFiles = new TaskItem[]
                {
                    new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe")
                };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void TargetType()
            {
                Csc t = new Csc();
                t.TargetType = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void ToolPath()
            {
                Csc t = new Csc();
                t.ToolPath = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [Test]
            public void WarningsAsErrors()
            {
                Csc t = new Csc();
                t.WarningsAsErrors = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }


            [Test]
            public void Win32Resource()
            {
                Csc t = new Csc();
                t.Win32Resource = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }
        }

        [Test]
        public void MultipleResponseFiles()
        {
            Csc t = new Csc();
            t.ResponseFiles = new TaskItem[]
                {
                    new TaskItem(@"1.rsp"),
                    new TaskItem(@"2.rsp"),
                    new TaskItem(@"3.rsp"),
                    new TaskItem(@"4.rsp")
                };
            CommandLine.ValidateContains(t, "@1.rsp @2.rsp @3.rsp @4.rsp", true);
        }

        [Test]
        public void SingleResponseFile()
        {
            Csc t = new Csc();
            t.ResponseFiles = new TaskItem[]
                {
                    new TaskItem(@"1.rsp")
                };
            CommandLine.ValidateHasParameter(t, "@1.rsp");
        }

        [Test]
        public void NoAnalyzers_CommandLine()
        {
            Csc csc = new Csc();

            CommandLine.ValidateNoParameterStartsWith(csc, "/analyzer");
        }

        [Test]
        public void Analyzer_CommandLine()
        {
            Csc csc = new Csc();
            csc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll")
            };

            CommandLine.ValidateHasParameter(csc, CommandLineBuilder.FixCommandLineSwitch("/analyzer:Foo.dll"));
        }

        [Test]
        public void MultipleAnalyzers_CommandLine()
        {
            Csc csc = new Csc();
            csc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll"),
                new TaskItem("Bar.dll")
            };

            CommandLine.ValidateHasParameter(csc, CommandLineBuilder.FixCommandLineSwitch("/analyzer:Foo.dll"));
            CommandLine.ValidateHasParameter(csc, CommandLineBuilder.FixCommandLineSwitch("/analyzer:Bar.dll"));
        }

        [Test]
        public void NoAnalyzer_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.Analyzers);

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.IsNull(cscHostObject.Analyzers);
        }

        [Test]
        public void Analyzer_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.Analyzers);

            csc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll")
            };

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.AreEqual(1, cscHostObject.Analyzers.Length);
            Assert.AreEqual("Foo.dll", cscHostObject.Analyzers[0].ItemSpec);
        }

        [Test]
        public void MultipleAnalyzers_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.Analyzers);

            csc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll"),
                new TaskItem("Bar.dll")
            };

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");

            Assert.AreEqual(2, cscHostObject.Analyzers.Length);
            Assert.AreEqual("Foo.dll", cscHostObject.Analyzers[0].ItemSpec);
            Assert.AreEqual("Bar.dll", cscHostObject.Analyzers[1].ItemSpec);
        }

        [Test]
        public void NoRuleSet_CommandLine()
        {
            Csc csc = new Csc();

            CommandLine.ValidateNoParameterStartsWith(csc, "/ruleset");
        }

        [Test]
        public void RuleSet_CommandLine()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Ignore("The ruleset is not supported, except under Windows");
            }

            Csc csc = new Csc();
            csc.CodeAnalysisRuleSet = "Bar.ruleset";

            CommandLine.ValidateHasParameter(csc, CommandLineBuilder.FixCommandLineSwitch("/ruleset:Bar.ruleset"));
        }

        [Test]
        public void NoRuleSet_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.RuleSet);

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.IsNull(cscHostObject.RuleSet);
        }

        [Test]
        public void RuleSet_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.RuleSet);

            csc.CodeAnalysisRuleSet = "Bar.ruleset";

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.AreEqual("Bar.ruleset", cscHostObject.RuleSet);
        }

        [Test]
        public void NoAdditionalFiles_CommandLine()
        {
            Csc csc = new Csc();

            CommandLine.ValidateNoParameterStartsWith(csc, "/additionalfile");
        }

        [Test]
        public void AdditionalFiles_CommandLine()
        {
            Csc csc = new Csc();
            csc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("web.config")
            };

            CommandLine.ValidateHasParameter(csc, CommandLineBuilder.FixCommandLineSwitch("/additionalfile:web.config"));
        }

        [Test]
        public void MultipleAdditionalFiles_CommandLine()
        {
            Csc csc = new Csc();
            csc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("app.config"),
                new TaskItem("web.config")
            };

            CommandLine.ValidateHasParameter(csc, CommandLineBuilder.FixCommandLineSwitch("/additionalfile:app.config"));
            CommandLine.ValidateHasParameter(csc, CommandLineBuilder.FixCommandLineSwitch("/additionalfile:web.config"));
        }

        [Test]
        public void NoAdditionalFile_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.AdditionalFiles);

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.IsNull(cscHostObject.AdditionalFiles);
        }

        [Test]
        public void AdditionalFile_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.AdditionalFiles);

            csc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("web.config")
            };

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");
            Assert.AreEqual(1, cscHostObject.AdditionalFiles.Length);
            Assert.AreEqual("web.config", cscHostObject.AdditionalFiles[0].ItemSpec);
        }

        [Test]
        public void MultipleAdditionalFiles_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Csc csc = new Csc();
            csc.BuildEngine = mockEngine;

            MockCscAnalyzerHostObject cscHostObject = new MockCscAnalyzerHostObject();
            cscHostObject.SetDesignTime(true);

            csc.HostObject = cscHostObject;
            csc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(cscHostObject.AdditionalFiles);

            csc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("web.config"),
                new TaskItem("app.config")
            };

            csc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool cscSuccess = csc.Execute();

            Assert.IsTrue(cscSuccess, "Csc task failed.");

            Assert.AreEqual(2, cscHostObject.AdditionalFiles.Length);
            Assert.AreEqual("web.config", cscHostObject.AdditionalFiles[0].ItemSpec);
            Assert.AreEqual("app.config", cscHostObject.AdditionalFiles[1].ItemSpec);
        }
    }
}