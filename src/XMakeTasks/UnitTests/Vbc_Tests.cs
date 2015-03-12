// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   VbcTests
     *
     * Test the Vbc task in various ways.
     *
     */
    [TestClass]
    sealed public class VbcTests
    {
        /// <summary>
        /// Tests the "References" parameter on the Vbc task, and confirms that it sets
        /// the /reference switch on the command-line correctly.  The Vbc task does
        /// not support assembly aliases, so we want to make sure that we don't try
        /// to pass an assembly alias into vbc.exe.
        /// </summary>
        [TestMethod]
        public void References()
        {
            Vbc t = new Vbc();

            TaskItem reference = new TaskItem("System.Xml.dll");
            reference.SetMetadata("Alias", "Foo");

            t.References = new TaskItem[] { reference };
            CommandLine.ValidateHasParameter(t, "/reference:System.Xml.dll");
        }

        // Tests the "BaseAddress" parameter on the Vbc task, and confirms that it sets
        // the /baseaddress switch on the command-line correctly.  The catch here is that
        // "vbc.exe" only supports passing in hex values, but the task may be passed
        // in hex or decimal.
        [TestMethod]
        public void BaseAddressHex1()
        {
            Vbc t = new Vbc();
            t.BaseAddress = "&H00001000";
            CommandLine.ValidateHasParameter(t, "/baseaddress:00001000");
        }

        // Tests the "BaseAddress" parameter on the Vbc task, and confirms that it sets
        // the /baseaddress switch on the command-line correctly.  The catch here is that
        // "vbc.exe" only supports passing in hex values, but the task may be passed
        // in hex or decimal.
        [TestMethod]
        public void BaseAddressHex2()
        {
            Vbc t = new Vbc();
            t.BaseAddress = "&h00001000";
            CommandLine.ValidateHasParameter(t, "/baseaddress:00001000");
        }

        // Tests the "BaseAddress" parameter on the Vbc task, and confirms that it sets
        // the /baseaddress switch on the command-line correctly.  The catch here is that
        // "vbc.exe" only supports passing in hex values, but the task may be passed
        // in hex or decimal.
        [TestMethod]
        public void BaseAddressHex3()
        {
            Vbc t = new Vbc();
            t.BaseAddress = "0x0000FFFF";
            CommandLine.ValidateHasParameter(t, "/baseaddress:0000FFFF");
        }

        // Tests the "BaseAddress" parameter on the Vbc task, and confirms that it sets
        // the /baseaddress switch on the command-line correctly.  The catch here is that
        // "vbc.exe" only supports passing in hex values, but the task may be passed
        // in hex or decimal.
        [TestMethod]
        public void BaseAddressHex4()
        {
            Vbc t = new Vbc();
            t.BaseAddress = "0X00001000";
            CommandLine.ValidateHasParameter(t, "/baseaddress:00001000");
        }

        // Tests the "BaseAddress" parameter on the Vbc task, and confirms that it sets
        // the /baseaddress switch on the command-line correctly.  The catch here is that
        // "vbc.exe" only supports passing in hex values, but the task may be passed
        // in hex or decimal.
        [TestMethod]
        public void BaseAddressDecimal()
        {
            Vbc t = new Vbc();
            t.BaseAddress = "285212672";
            CommandLine.ValidateHasParameter(t, "/baseaddress:11000000");
        }

        /// <summary>
        /// Test the hex parsing code with a large integer value (unsigned int in size)
        /// </summary>
        [TestMethod]
        public void BaseAddressLargeDecimal()
        {
            Vbc t = new Vbc();
            t.BaseAddress = "3555454580";
            CommandLine.ValidateHasParameter(t, "/baseaddress:D3EBEE74");
        }

        // Tests the "BaseAddress" parameter on the Vbc task, and confirms that it throws
        // an exception when an invalid string is passed in.
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void BaseAddressInvalid()
        {
            Vbc t = new Vbc();
            t.BaseAddress = "deadbeef";
            CommandLine.ValidateHasParameter(t, "WeShouldNeverGetThisFar");
        }

        // Tests the "GenerateDocumentation" and "DocumentationFile" parameters on the Vbc task,
        // and confirms that it sets the /doc switch on the command-line correctly.
        [TestMethod]
        public void DocumentationFile()
        {
            Vbc t = new Vbc();

            t.DocumentationFile = "foo.xml";
            t.GenerateDocumentation = true;

            int firstParamLocation = CommandLine.ValidateHasParameter(t, "/doc+");
            int secondParamLocation = CommandLine.ValidateHasParameter(t, "/doc:foo.xml");

            Assert.IsTrue(secondParamLocation > firstParamLocation, "The order of the /doc switches is incorrect.");
        }

        // Tests the "AdditionalLibPaths" parameter on the Vbc task, and confirms that it sets
        // the /lib switch on the command-line correctly.
        [TestMethod]
        public void AdditionaLibPaths()
        {
            Vbc t = new Vbc();

            t.AdditionalLibPaths = new string[] { @"c:\xmake\", @"c:\msbuild" };
            CommandLine.ValidateHasParameter(t, @"/libpath:c:\xmake\,c:\msbuild");
        }

        // Tests the "NoVBRuntimeReference" parameter on the Vbc task, and confirms that it sets
        // the /novbruntimeref switch on the command-line correctly.
        [TestMethod]
        public void NoVBRuntimeReference()
        {
            Vbc t = new Vbc();

            t.NoVBRuntimeReference = true;
            CommandLine.ValidateHasParameter(t, @"/novbruntimeref");

            Vbc t2 = new Vbc();

            t2.NoVBRuntimeReference = false;
            CommandLine.ValidateNoParameterStartsWith(t2, @"/novbruntimeref");
        }

        // Tests the "Verbosity" parameter on the Vbc task, and confirms that it sets
        // the /quiet switch on the command-line correctly.
        [TestMethod]
        public void VerbosityQuiet()
        {
            Vbc t = new Vbc();

            t.Verbosity = "QUIET";
            CommandLine.ValidateHasParameter(t, @"/QUIET");
        }

        // Tests the "Verbosity" parameter on the Vbc task, and confirms that it sets
        // the /verbose switch on the command-line correctly.
        [TestMethod]
        public void VerbosityVerbose()
        {
            Vbc t = new Vbc();

            t.Verbosity = "verbose";
            CommandLine.ValidateHasParameter(t, @"/verbose");
        }

        // Tests the "Platform" parameter on the Vbc task, and confirms that it sets
        // the /platform switch on the command-line correctly.
        [TestMethod]
        public void Platform()
        {
            Vbc t = new Vbc();

            t.Platform = "x86";
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
        }

        // Tests the "Platform" and "Prefer32Bit" parameter combinations on the Vbc task,
        // and confirms that it sets the /platform switch on the command-line correctly.
        [TestMethod]
        public void PlatformAndPrefer32Bit()
        {
            // Implicit "anycpu"
            Vbc t = new Vbc();
            CommandLine.ValidateNoParameterStartsWith(t, @"/platform:");
            t = new Vbc();
            t.Prefer32Bit = false;
            CommandLine.ValidateNoParameterStartsWith(t, @"/platform:");
            t = new Vbc();
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu32bitpreferred");

            // Explicit "anycpu"
            t = new Vbc();
            t.Platform = "anycpu";
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu");
            t = new Vbc();
            t.Platform = "anycpu";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu");
            t = new Vbc();
            t.Platform = "anycpu";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu32bitpreferred");

            // Explicit "x86"
            t = new Vbc();
            t.Platform = "x86";
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
            t = new Vbc();
            t.Platform = "x86";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
            t = new Vbc();
            t.Platform = "x86";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
        }

        // Tests the "HighEntropyVA" parameter on the Vbc task, and confirms that it
        // sets the /highentropyva switch on the command-line correctly.
        [TestMethod]
        public void HighEntropyVA()
        {
            // Implicit /highentropyva-
            Vbc t = new Vbc();
            CommandLine.ValidateNoParameterStartsWith(t, @"/highentropyva");

            // Explicit /highentropyva-
            t = new Vbc();
            t.HighEntropyVA = false;
            CommandLine.ValidateHasParameter(t, @"/highentropyva-");

            // Explicit /highentropyva+
            t = new Vbc();
            t.HighEntropyVA = true;
            CommandLine.ValidateHasParameter(t, @"/highentropyva+");
        }

        // Tests the "SubsystemVersion" parameter on the Vbc task, and confirms that it sets
        // the /subsystemversion switch on the command-line correctly.
        [TestMethod]
        public void SubsystemVersion()
        {
            Vbc t = new Vbc();
            CommandLine.ValidateNoParameterStartsWith(t, @"/subsystemversion");

            t = new Vbc();
            t.SubsystemVersion = "4.0";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:4.0");

            t = new Vbc();
            t.SubsystemVersion = "5";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:5");

            t = new Vbc();
            t.SubsystemVersion = "6.02";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:6.02");

            t = new Vbc();
            t.SubsystemVersion = "garbage";
            CommandLine.ValidateHasParameter(t, @"/subsystemversion:garbage");
        }

        // Tests the "Verbosity" parameter on the Vbc task, and confirms that it
        // does not add any command-line switches when verbosity is set to normal.
        [TestMethod]
        public void VerbosityNormal()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc t = new Vbc();
            t.BuildEngine = mockEngine;

            t.Verbosity = "Normal";
            t.Sources = new TaskItem[] { new TaskItem("a.vb") };
            Assert.IsTrue(CommandLine.CallValidateParameters(t), "Vbc task didn't accept 'normal' for the Verbosity parameter");
            CommandLine.ValidateNoParameterStartsWith(t, @"/quiet");
            CommandLine.ValidateNoParameterStartsWith(t, @"/verbose");
            CommandLine.ValidateNoParameterStartsWith(t, @"/normal");
        }

        // Tests the "Verbosity" parameter on the Vbc task, and confirms that it
        // throws an error when an invalid value is passed in.
        [TestMethod]
        public void VerbosityBogus()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc t = new Vbc();
            t.BuildEngine = mockEngine;

            t.Verbosity = "bogus";
            Assert.IsFalse(CommandLine.CallValidateParameters(t), "Bogus verbosity setting not caught as error.");
        }

        // Check all parameters that are based on ints, bools and other value types.
        // This is because parameters with these types go through a not-so-typesafe check
        // for existence in the property bag.
        [TestMethod]
        public void FlagsAndInts()
        {
            Vbc t = new Vbc();

            // From managed compiler
            t.CodePage = 5;
            t.EmitDebugInformation = true;
            t.DelaySign = true;
            t.FileAlignment = 9;
            t.NoLogo = true;
            t.Optimize = true;
            t.TreatWarningsAsErrors = true;
            t.Utf8Output = true;

            // From Vbc.
            t.GenerateDocumentation = true;
            t.NoWarnings = true;
            t.OptionExplicit = true;
            t.OptionStrict = true;
            t.RemoveIntegerChecks = true;
            t.TargetCompactFramework = true;
            t.OptionInfer = true;


            // Check the parameters.
            CommandLine.ValidateHasParameter(t, "/codepage:5");
            CommandLine.ValidateHasParameter(t, "/debug+");
            CommandLine.ValidateHasParameter(t, "/delaysign+");
            CommandLine.ValidateHasParameter(t, "/filealign:9");
            CommandLine.ValidateHasParameter(t, "/nologo");
            CommandLine.ValidateHasParameter(t, "/optimize+");
            CommandLine.ValidateHasParameter(t, "/warnaserror+");
            CommandLine.ValidateHasParameter(t, "/utf8output");

            CommandLine.ValidateHasParameter(t, "/doc+");
            CommandLine.ValidateHasParameter(t, "/nowarn");
            CommandLine.ValidateHasParameter(t, "/optionexplicit+");
            CommandLine.ValidateHasParameter(t, "/optionstrict+");
            CommandLine.ValidateHasParameter(t, "/removeintchecks+");
            CommandLine.ValidateHasParameter(t, "/netcf");
            CommandLine.ValidateHasParameter(t, "/optioninfer+");
        }

        /***********************************************************************
         * Test:            VbcHostObject
         *
         * Instantiates the Vbc task, sets a host object on it, and tries executing
         * the task to make sure the host object is called appropriately.
         *
         **********************************************************************/
        [TestMethod]
        [Ignore]
        public void VbcHostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;
            MockVbcHostObject vbcHostObject = new MockVbcHostObject5();
            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsTrue(!vbcHostObject.CompileMethodWasCalled);

            vbc.Sources = new TaskItem[] { new TaskItem("a.vb") };
            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.IsTrue(vbcHostObject.CompileMethodWasCalled);
        }

        [TestMethod]
        public void VbcHostObjectNotUsedIfToolNameSpecified()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;
            MockVbcHostObject vbcHostObject = new MockVbcHostObject();
            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;
            vbc.ToolExe = "vbc_custom.exe";

            Assert.IsTrue(vbc.UseAlternateCommandLineToolToExecute());
        }

        [TestMethod]
        public void VbcHostObjectNotUsedIfToolPathSpecified()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;
            MockVbcHostObject vbcHostObject = new MockVbcHostObject();
            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;
            vbc.ToolPath = "c:\\some\\custom\\path";

            Assert.IsTrue(vbc.UseAlternateCommandLineToolToExecute());
        }


        /*
         * Class:   VbcCrossParameterInjection
         *
         * Test the Vbc task for cases where the parameters are passed in
         * with with whitespace and other special characters to try to fool
         * us into spawning Vbc.exe while bypassing security
         */
        [TestClass]
        sealed public class VbcCrossParameterInjection
        {
            [TestMethod]
            public void Win32Icon()
            {
                Vbc t = new Vbc();
                t.Win32Icon = @"MyFile.ico /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void AdditionalLibPaths()
            {
                Vbc t = new Vbc();
                t.AdditionalLibPaths = new string[] { @"parm /out:c:\windows\system32\notepad.exe" };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void AddModules()
            {
                Vbc t = new Vbc();
                t.AddModules = new string[] { @"parm /out:c:\windows\system32\notepad.exe" };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            [ExpectedException(typeof(ArgumentException))]
            public void BaseAddress()
            {
                Vbc t = new Vbc();
                t.BaseAddress = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void DebugType()
            {
                Vbc t = new Vbc();
                t.DebugType = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            [Ignore] // Because constants may legitimately contains quotes _and_ we've cut security, we decided to let DefineConstants be passed through literally.
            public void DefineConstants()
            {
                Vbc t = new Vbc();
                t.DefineConstants = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void DocumentationFile()
            {
                Vbc t = new Vbc();
                t.DocumentationFile = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void KeyContainer()
            {
                Vbc t = new Vbc();
                t.KeyContainer = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void KeyFile()
            {
                Vbc t = new Vbc();
                t.KeyFile = @"parm /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void LinkResources()
            {
                Vbc t = new Vbc();
                t.KeyFile = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void MainEntryPoint()
            {
                Vbc t = new Vbc();
                t.MainEntryPoint = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void OutputAssembly()
            {
                Vbc t = new Vbc();
                t.OutputAssembly = new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe");
                CommandLine.ValidateHasParameter(t, @"/out:parm1 /out:c:\windows\system32\notepad.exe");
            }

            [TestMethod]
            public void References()
            {
                Vbc t = new Vbc();
                t.References = new TaskItem[]
                {
                    new TaskItem("parm0"),
                    new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe")
                };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void Resources()
            {
                Vbc t = new Vbc();
                t.References = new TaskItem[]
                {
                    new TaskItem("parm0"),
                    new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe")
                };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void ResponseFiles()
            {
                Vbc t = new Vbc();
                t.ResponseFiles = new TaskItem[]
                {
                    new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe")
                };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void TargetType()
            {
                Vbc t = new Vbc();
                t.TargetType = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void ToolPath()
            {
                Vbc t = new Vbc();
                t.ToolPath = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void Win32Resource()
            {
                Vbc t = new Vbc();
                t.Win32Resource = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void Imports()
            {
                Vbc t = new Vbc();
                t.Imports = new TaskItem[] { new TaskItem(@"parm1 /out:c:\windows\system32\notepad.exe") };
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void OptionCompare()
            {
                Vbc t = new Vbc();
                t.OptionCompare = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void RootNamespace()
            {
                Vbc t = new Vbc();
                t.RootNamespace = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }

            [TestMethod]
            public void SdkPath()
            {
                Vbc t = new Vbc();
                t.SdkPath = @"parm1 /out:c:\windows\system32\notepad.exe";
                CommandLine.ValidateNoParameterStartsWith(t, "/out");
            }
        }

        [TestMethod]
        public void OptionStrictOffNowarnsUndefined()
        {
            Vbc t = new Vbc();
            t.OptionStrict = false;
            t.DisabledWarnings = null;
            CommandLine.ValidateHasParameter(t, "/optionstrict:custom"); // we should be custom if no warnings are disabled
        }

        [TestMethod]
        public void OptionStrictOffNowarnsEmpty()
        {
            Vbc t = new Vbc();
            t.OptionStrict = false;
            t.DisabledWarnings = "";
            CommandLine.ValidateHasParameter(t, "/optionstrict:custom"); // we shuold be custom if no warnings are disabled
        }

        [TestMethod]
        public void OptionStrictOffNoWarnsPresent()
        {
            // When the below warnings are set to NONE, we are effectively Option Strict-.  But because we don't want the msbuild task
            // to have to know the current set of disabled warnings that implies option strict-, we just set option strict:custom
            // with the understanding that we get the same behavior as option strict- since we are passing the /nowarn line on that
            // contains all the warnings OptionStrict- would disable anyway.
            Vbc t = new Vbc();
            t.OptionStrict = false;
            t.DisabledWarnings = "41999,42016,42017,42018,42019,42020,42021,42022,42032,42036";
            CommandLine.ValidateHasParameter(t, "/optionstrict:custom");
            t.DisabledWarnings = "/nowarn:41999,42016,42017,42018,42019,42020,42021,42022,42032,42036";
        }

        [TestMethod]
        public void OptionStrictOnNoWarnsUndefined()
        {
            Vbc t = new Vbc();
            t.OptionStrict = true;
            t.DisabledWarnings = "";
            CommandLine.ValidateHasParameter(t, "/optionstrict+");
        }

        [TestMethod]
        public void OptionStrictOnNoWarnsPresent()
        {
            Vbc t = new Vbc();
            t.OptionStrict = true;
            t.DisabledWarnings = "41999";
            CommandLine.ValidateHasParameter(t, "/optionstrict+");
            t.DisabledWarnings = "/nowarn:41999";
        }

        [TestMethod]
        public void OptionStrictType1()
        {
            Vbc t = new Vbc();
            t.OptionStrict = true;
            t.OptionStrictType = "custom";
            CommandLine.ValidateContains(t, "/optionstrict+ /optionstrict:custom", true);
        }

        [TestMethod]
        public void OptionStrictType2()
        {
            Vbc t = new Vbc();
            t.OptionStrictType = "custom";
            CommandLine.ValidateContains(t, "/optionstrict:custom", true);
            CommandLine.ValidateDoesNotContain(t, "/optionstrict-", true);
        }

        [TestMethod]
        public void MultipleResponseFiles()
        {
            Vbc t = new Vbc();
            t.ResponseFiles = new TaskItem[]
                {
                    new TaskItem(@"1.rsp"),
                    new TaskItem(@"2.rsp"),
                    new TaskItem(@"3.rsp"),
                    new TaskItem(@"4.rsp")
                };
            CommandLine.ValidateContains(t, "@1.rsp @2.rsp @3.rsp @4.rsp", true);
        }

        [TestMethod]
        public void SingleResponseFile()
        {
            Vbc t = new Vbc();
            t.ResponseFiles = new TaskItem[]
                {
                    new TaskItem(@"1.rsp")
                };
            CommandLine.ValidateHasParameter(t, "@1.rsp");
        }

        [TestMethod]
        public void ParseError_StandardVbcError()
        {
            Vbc t = new Vbc();
            t.BuildEngine = new MockEngine();

            string error1 = "d:\\scratch\\607654\\Module1.vb(5) : error BC30451: Name 'Ed' is not declared.";
            string error2 = "";
            string error3 = "    Ed Sub";
            string error4 = "    ~~    ";

            t.ParseVBErrorOrWarning(error1, MessageImportance.High);
            t.ParseVBErrorOrWarning(error2, MessageImportance.High);
            t.ParseVBErrorOrWarning(error3, MessageImportance.High);
            t.ParseVBErrorOrWarning(error4, MessageImportance.High);

            Assert.IsTrue((t.BuildEngine as MockEngine).Errors >= 1, "Should be at least one error");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30451");
            (t.BuildEngine as MockEngine).AssertLogContains("(5,5)");
        }

        [TestMethod]
        public void ParseError_StandardVbcErrorWithColon()
        {
            Vbc t = new Vbc();
            t.BuildEngine = new MockEngine();

            string error1 = "d:\\scratch\\607654\\Module1.vb(5) : error BC30800: Method arguments must be enclosed in parentheses : Don't you think?";
            string error2 = "";
            string error3 = "    Ed Sub";
            string error4 = "       ~~~";

            t.ParseVBErrorOrWarning(error1, MessageImportance.High);
            t.ParseVBErrorOrWarning(error2, MessageImportance.High);
            t.ParseVBErrorOrWarning(error3, MessageImportance.High);
            t.ParseVBErrorOrWarning(error4, MessageImportance.High);

            Assert.IsTrue((t.BuildEngine as MockEngine).Errors >= 1, "Should be at least one error");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30800");
            (t.BuildEngine as MockEngine).AssertLogContains("(5,8)");
        }

        [TestMethod]
        public void ParseError_ProjectLevelVbcError()
        {
            Vbc t = new Vbc();
            t.BuildEngine = new MockEngine();

            string error = "vbc : error BC30573: Error in project-level import '<xmlns=\"hi\">' at '<xmlns=\"hi\">' : XML namespace prefix '' is already declared";

            t.ParseVBErrorOrWarning(error, MessageImportance.High);

            Assert.IsTrue((t.BuildEngine as MockEngine).Errors >= 1, "Should be at least one error");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30573");
        }

        [TestMethod]
        public void ParseError_ProjectLevelVbcErrorFollowedByStandard()
        {
            Vbc t = new Vbc();
            t.BuildEngine = new MockEngine();

            string error1 = "vbc : error BC30573: Error in project-level import '<xmlns=\"hi\">' at '<xmlns=\"hi\">' : XML namespace prefix '' is already declared";

            string error2 = "d:\\scratch\\607654\\Module1.vb(5) : error BC30800: Method arguments must be enclosed in parentheses : Don't you think?";
            string error3 = "";
            string error4 = "    Ed Sub";
            string error5 = "       ~~~";

            t.ParseVBErrorOrWarning(error1, MessageImportance.High);
            t.ParseVBErrorOrWarning(error2, MessageImportance.High);
            t.ParseVBErrorOrWarning(error3, MessageImportance.High);
            t.ParseVBErrorOrWarning(error4, MessageImportance.High);
            t.ParseVBErrorOrWarning(error5, MessageImportance.High);

            Assert.IsTrue((t.BuildEngine as MockEngine).Errors >= 2, "Should be at least two errors");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30573");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30800");
        }

        [TestMethod]
        public void ParseError_StandardVbcErrorFollowedByProjectLevel()
        {
            Vbc t = new Vbc();
            t.BuildEngine = new MockEngine();

            string error1 = "d:\\scratch\\607654\\Module1.vb(5) : error BC30800: Method arguments must be enclosed in parentheses : Don't you think?";
            string error2 = "";
            string error3 = "    Ed Sub";
            string error4 = "       ~~~";

            string error5 = "vbc : error BC30573: Error in project-level import '<xmlns=\"hi\">' at '<xmlns=\"hi\">' : XML namespace prefix '' is already declared";

            t.ParseVBErrorOrWarning(error1, MessageImportance.High);
            t.ParseVBErrorOrWarning(error2, MessageImportance.High);
            t.ParseVBErrorOrWarning(error3, MessageImportance.High);
            t.ParseVBErrorOrWarning(error4, MessageImportance.High);
            t.ParseVBErrorOrWarning(error5, MessageImportance.High);

            Assert.IsTrue((t.BuildEngine as MockEngine).Errors >= 2, "Should be at least two errors");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30573");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30800");
        }

        [TestMethod]
        public void ParseError_TwoProjectLevelErrors()
        {
            Vbc t = new Vbc();
            t.BuildEngine = new MockEngine();

            string error1 = "vbc : error BC30205: Error in project-level import '<xmlns='bye'>, <xmlns='byebye'>' at '<xmlns='bye'>, ' : End of statement expected.";
            string error2 = "vbc : error BC30573: Error in project-level import '<xmlns=\"hi\">' at '<xmlns=\"hi\">' : XML namespace prefix '' is already declared";

            t.ParseVBErrorOrWarning(error1, MessageImportance.High);
            t.ParseVBErrorOrWarning(error2, MessageImportance.High);

            Assert.IsTrue((t.BuildEngine as MockEngine).Errors >= 2, "Should be at least two errors");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30573");
            (t.BuildEngine as MockEngine).AssertLogContains("BC30205");
        }


        /// <summary>
        ///  Make sure that if we pass a name of a pdb file to the task that it corrrectly moves the file.
        /// </summary>
        [TestMethod]
        public void MovePDBFile_GoodName()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "MovePDBFile_GoodName");
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
                }

                Directory.CreateDirectory(tempDirectory);

                string outputAssemblyPath = Path.Combine(tempDirectory, "Out.dll");
                string newoutputAssemblyPath = Path.Combine(tempDirectory, "MyNewPDBFile.pdb");
                string outputPDBPath = Path.Combine(tempDirectory, "Out.pdb");
                File.WriteAllText(outputPDBPath, "Hello");
                File.WriteAllText(outputAssemblyPath, "Hello");

                Vbc t = new Vbc();
                t.BuildEngine = new MockEngine();
                t.PdbFile = newoutputAssemblyPath;
                t.MovePdbFileIfNecessary(outputAssemblyPath);

                FileInfo newPDBInfo = new FileInfo(newoutputAssemblyPath);
                FileInfo oldPDBInfo = new FileInfo(outputPDBPath);

                Assert.IsTrue(newPDBInfo.Exists);
                Assert.IsFalse(oldPDBInfo.Exists);
                ((MockEngine)t.BuildEngine).MockLogger.AssertNoErrors();
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
                }
            }
        }


        /// <summary>
        ///  Make sure if the file already exists that the move still happens.
        /// </summary>
        [TestMethod]
        public void MovePDBFile_SameNameandFileAlreadyExists()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "MovePDBFile_SmeNameandFileAlreadyExists");
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
                }

                Directory.CreateDirectory(tempDirectory);

                string outputAssemblyPath = Path.Combine(tempDirectory, "Out.dll");
                string newoutputAssemblyPath = Path.Combine(tempDirectory, "Out.pdb");

                File.WriteAllText(outputAssemblyPath, "Hello");
                File.WriteAllText(newoutputAssemblyPath, "Hello");

                Vbc t = new Vbc();
                t.BuildEngine = new MockEngine();
                t.PdbFile = newoutputAssemblyPath;
                t.MovePdbFileIfNecessary(outputAssemblyPath);

                FileInfo newPDBInfo = new FileInfo(newoutputAssemblyPath);

                Assert.IsTrue(newPDBInfo.Exists);
                ((MockEngine)t.BuildEngine).MockLogger.AssertNoErrors();
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
                }
            }
        }

        /// <summary>
        ///  Make sure that if we pass a name of a pdb file to the task that it corrrectly moves the file.
        /// </summary>
        [TestMethod]
        public void MovePDBFile_BadFileName()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "MovePDBFile_BadFileName");

            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
                }

                Directory.CreateDirectory(tempDirectory);

                string outputAssemblyPath = Path.Combine(tempDirectory, "Out.dll");
                string outputPDBPath = Path.Combine(tempDirectory, "Out.pdb");
                File.WriteAllText(outputPDBPath, "Hello");
                File.WriteAllText(outputAssemblyPath, "Hello");

                MockEngine engine = new MockEngine();
                Vbc t = new Vbc();
                t.BuildEngine = engine;
                t.PdbFile = "||{}}{<>?$$%^&*()!@#$%`~.pdb";
                t.MovePdbFileIfNecessary(outputAssemblyPath);

                FileInfo oldPDBInfo = new FileInfo(outputAssemblyPath);
                Assert.IsTrue(oldPDBInfo.Exists);

                Assert.IsTrue(engine.Errors >= 1, "Should be one error");
                (t.BuildEngine as MockEngine).AssertLogContains("MSB3402");
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(tempDirectory, true);
                }
            }
        }

        [TestMethod]
        public void NoAnalyzers_CommandLine()
        {
            Vbc vbc = new Vbc();

            CommandLine.ValidateNoParameterStartsWith(vbc, "/analyzer");
        }

        [TestMethod]
        public void Analyzer_CommandLine()
        {
            Vbc vbc = new Vbc();
            vbc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll")
            };

            CommandLine.ValidateHasParameter(vbc, "/analyzer:Foo.dll");
        }

        [TestMethod]
        public void MultipleAnalyzers_CommandLine()
        {
            Vbc vbc = new Vbc();
            vbc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll"),
                new TaskItem("Bar.dll")
            };

            CommandLine.ValidateHasParameter(vbc, "/analyzer:Foo.dll");
            CommandLine.ValidateHasParameter(vbc, "/analyzer:Bar.dll");
        }

        [TestMethod]
        public void NoAnalyzer_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.Analyzers);

            vbc.Sources = new TaskItem[] { new TaskItem("a.vb") };
            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.IsNull(vbcHostObject.Analyzers);
        }

        [TestMethod]
        public void Analyzer_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.Analyzers);

            vbc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll")
            };

            vbc.Sources = new TaskItem[] { new TaskItem("a.vb") };
            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.AreEqual(1, vbcHostObject.Analyzers.Length);
            Assert.AreEqual("Foo.dll", vbcHostObject.Analyzers[0].ItemSpec);
        }

        [TestMethod]
        public void MultipleAnalyzers_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.Analyzers);

            vbc.Analyzers = new TaskItem[]
            {
                new TaskItem("Foo.dll"),
                new TaskItem("Bar.dll")
            };

            vbc.Sources = new TaskItem[] { new TaskItem("a.vb") };
            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.AreEqual(2, vbcHostObject.Analyzers.Length);
            Assert.AreEqual("Foo.dll", vbcHostObject.Analyzers[0].ItemSpec);
            Assert.AreEqual("Bar.dll", vbcHostObject.Analyzers[1].ItemSpec);
        }

        [TestMethod]
        public void NoRuleSet_CommandLine()
        {
            Vbc vbc = new Vbc();

            CommandLine.ValidateNoParameterStartsWith(vbc, "/ruleset");
        }

        [TestMethod]
        public void RuleSet_CommandLine()
        {
            Vbc vbc = new Vbc();
            vbc.CodeAnalysisRuleSet = "Bar.ruleset";

            CommandLine.ValidateHasParameter(vbc, "/ruleset:Bar.ruleset");
        }

        [TestMethod]
        public void NoRuleSet_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.RuleSet);

            vbc.Sources = new TaskItem[] { new TaskItem("a.vb") };

            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.IsNull(vbcHostObject.RuleSet);
        }

        [TestMethod]
        public void RuleSet_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.RuleSet);

            vbc.CodeAnalysisRuleSet = "Bar.ruleset";

            vbc.Sources = new TaskItem[] { new TaskItem("a.vb") };

            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.AreEqual("Bar.ruleset", vbcHostObject.RuleSet);
        }

        [TestMethod]
        public void NoAdditionalFiles_CommandLine()
        {
            Vbc vbc = new Vbc();

            CommandLine.ValidateNoParameterStartsWith(vbc, "/additionalfile");
        }

        [TestMethod]
        public void AdditionalFiles_CommandLine()
        {
            Vbc vbc = new Vbc();
            vbc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("web.config")
            };

            CommandLine.ValidateHasParameter(vbc, "/additionalfile:web.config");
        }

        [TestMethod]
        public void MultipleAdditionalFiles_CommandLine()
        {
            Vbc vbc = new Vbc();
            vbc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("app.config"),
                new TaskItem("web.config")
            };

            CommandLine.ValidateHasParameter(vbc, "/additionalfile:app.config");
            CommandLine.ValidateHasParameter(vbc, "/additionalfile:web.config");
        }

        [TestMethod]
        public void NoAdditionalFile_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.AdditionalFiles);

            vbc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.IsNull(vbcHostObject.AdditionalFiles);
        }

        [TestMethod]
        public void AdditionalFile_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.AdditionalFiles);

            vbc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("web.config")
            };

            vbc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");
            Assert.AreEqual(1, vbcHostObject.AdditionalFiles.Length);
            Assert.AreEqual("web.config", vbcHostObject.AdditionalFiles[0].ItemSpec);
        }

        [TestMethod]
        public void MultipleAdditionalFiles_HostObject()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Vbc vbc = new Vbc();
            vbc.BuildEngine = mockEngine;

            MockVbcAnalyzerHostObject vbcHostObject = new MockVbcAnalyzerHostObject();
            vbcHostObject.SetDesignTime(true);

            vbc.HostObject = vbcHostObject;
            vbc.UseHostCompilerIfAvailable = true;

            Assert.IsNull(vbcHostObject.AdditionalFiles);

            vbc.AdditionalFiles = new TaskItem[]
            {
                new TaskItem("web.config"),
                new TaskItem("app.config")
            };

            vbc.Sources = new TaskItem[] { new TaskItem("a.cs") };

            bool vbcSuccess = vbc.Execute();

            Assert.IsTrue(vbcSuccess, "Vbc task failed.");

            Assert.AreEqual(2, vbcHostObject.AdditionalFiles.Length);
            Assert.AreEqual("web.config", vbcHostObject.AdditionalFiles[0].ItemSpec);
            Assert.AreEqual("app.config", vbcHostObject.AdditionalFiles[1].ItemSpec);
        }
    }
}





