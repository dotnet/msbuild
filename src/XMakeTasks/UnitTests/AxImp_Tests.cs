// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Microsoft.Build.UnitTests.AxTlbImp_Tests
{
    [TestClass]
    sealed public class AxImp_Tests
    {
        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [TestMethod]
        public void ActiveXControlName()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.IsNull(t.ActiveXControlName, "ActiveXControlName should be null by default");

            t.ActiveXControlName = testParameterValue;
            Assert.AreEqual(testParameterValue, t.ActiveXControlName, "New ActiveXControlName value should be set");
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [TestMethod]
        public void ActiveXControlNameWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.IsNull(t.ActiveXControlName, "ActiveXControlName should be null by default");

            t.ActiveXControlName = testParameterValue;
            Assert.AreEqual(testParameterValue, t.ActiveXControlName, "New ActiveXControlName value should be set");
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /source switch
        /// </summary>
        [TestMethod]
        public void GenerateSource()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.GenerateSource, "GenerateSource should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/source", false /* no response file */);

            t.GenerateSource = true;
            Assert.IsTrue(t.GenerateSource, "GenerateSource should be true");
            CommandLine.ValidateHasParameter(t, @"/source", false /* no response file */);
        }

        /// <summary>
        /// Tests the /nologo switch
        /// </summary>
        [TestMethod]
        public void NoLogo()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.NoLogo, "NoLogo should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/nologo", false /* no response file */);

            t.NoLogo = true;
            Assert.IsTrue(t.NoLogo, "NoLogo should be true");
            CommandLine.ValidateHasParameter(t, @"/nologo", false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch
        /// </summary>
        [TestMethod]
        public void OutputAssembly()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.IsNull(t.OutputAssembly, "OutputAssembly should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/out:", false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.OutputAssembly, "New OutputAssembly value should be set");
            CommandLine.ValidateHasParameter(t, @"/out:" + testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch, with a space in the output file
        /// </summary>
        [TestMethod]
        public void OutputAssemblyWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.IsNull(t.OutputAssembly, "OutputAssembly should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/out:", false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.OutputAssembly, "New OutputAssembly value should be set");
            CommandLine.ValidateHasParameter(t, @"/out:" + testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /rcw: switch
        /// </summary>
        [TestMethod]
        public void RuntimeCallableWrapper()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "Interop.Foo.dll";

            Assert.IsNull(t.RuntimeCallableWrapperAssembly, "RuntimeCallableWrapper should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/rcw:", false /* no response file */);

            t.RuntimeCallableWrapperAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.RuntimeCallableWrapperAssembly, "New RuntimeCallableWrapper value should be set");
            CommandLine.ValidateHasParameter(t, @"/rcw:" + testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /rcw: switch with a space in the filename
        /// </summary>
        [TestMethod]
        public void RuntimeCallableWrapperWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"C:\Program Files\Microsoft Visual Studio 10.0\Interop.Foo.dll";

            Assert.IsNull(t.RuntimeCallableWrapperAssembly, "RuntimeCallableWrapper should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/rcw:", false /* no response file */);

            t.RuntimeCallableWrapperAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.RuntimeCallableWrapperAssembly, "New RuntimeCallableWrapper value should be set");
            CommandLine.ValidateHasParameter(t, @"/rcw:" + testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /silent switch
        /// </summary>
        [TestMethod]
        public void Silent()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.Silent, "Silent should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/silent", false /* no response file */);

            t.Silent = true;
            Assert.IsTrue(t.Silent, "Silent should be true");
            CommandLine.ValidateHasParameter(t, @"/silent", false /* no response file */);
        }

        /// <summary>
        /// Tests the /verbose switch
        /// </summary>
        [TestMethod]
        public void Verbose()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.Verbose, "Verbose should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/verbose", false /* no response file */);

            t.Verbose = true;
            Assert.IsTrue(t.Verbose, "Verbose should be true");
            CommandLine.ValidateHasParameter(t, @"/verbose", false /* no response file */);
        }

        /// <summary>
        /// Tests that task does the right thing (fails) when no .ocx file is passed to it
        /// </summary>
        [TestMethod]
        public void TaskFailsWithNoInputs()
        {
            var t = new ResolveComReference.AxImp();

            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxImp.NoInputFileSpecified");
        }
    }
}
