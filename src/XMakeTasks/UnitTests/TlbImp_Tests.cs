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
    sealed public class TlbImp_Tests
    {
        /// <summary>
        /// Tests that /machine flag will be set.
        /// </summary>
        [TestMethod]
        public void Machine()
        {
            var t = new ResolveComReference.TlbImp();
            Assert.IsNull(t.Machine, "Machine should be null by default");

            t.Machine = "Agnostic";
            Assert.AreEqual("Agnostic", t.Machine, "New TypeLibName value should be set");
            CommandLine.ValidateHasParameter(t, "/machine:Agnostic", false /* no response file */);
        }

        /// <summary>
        /// Check ReferenceFiles
        /// </summary>
        [TestMethod]
        public void ReferenceFiles()
        {
            var t = new ResolveComReference.TlbImp();
            Assert.IsNull(t.ReferenceFiles, "ReferenceFiles should be null by default");

            t.ReferenceFiles = new string[] { "File1.dll", "File2.dll" };
            CommandLine.ValidateHasParameter(t, "/reference:File1.dll", false /* no response file */);
            CommandLine.ValidateHasParameter(t, "/reference:File2.dll", false /* no response file */);
        }
        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [TestMethod]
        public void TypeLibName()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = "Interop.Foo.dll";

            Assert.IsNull(t.TypeLibName, "TypeLibName should be null by default");

            t.TypeLibName = testParameterValue;
            Assert.AreEqual(testParameterValue, t.TypeLibName, "New TypeLibName value should be set");
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [TestMethod]
        public void TypeLibNameWithSpaces()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = @"c:\Program Files\Interop.Foo.dll";

            Assert.IsNull(t.TypeLibName, "TypeLibName should be null by default");

            t.TypeLibName = testParameterValue;
            Assert.AreEqual(testParameterValue, t.TypeLibName, "New TypeLibName value should be set");
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /namespace: command line option
        /// </summary>
        [TestMethod]
        public void AssemblyNamespace()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = "Microsoft.Build.Foo";

            Assert.IsNull(t.AssemblyNamespace, "AssemblyNamespace should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/namespace:", false /* no response file */);

            t.AssemblyNamespace = testParameterValue;
            Assert.AreEqual(testParameterValue, t.AssemblyNamespace, "New AssemblyNamespace value should be set");
            CommandLine.ValidateHasParameter(t, @"/namespace:" + testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /asmversion: command line option
        /// </summary>
        [TestMethod]
        public void AssemblyVersion()
        {
            var t = new ResolveComReference.TlbImp();
            Version testParameterValue = new Version(2, 12);

            Assert.IsNull(t.AssemblyVersion, "AssemblyVersion should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/asmversion:", false /* no response file */);

            t.AssemblyVersion = testParameterValue;
            Assert.AreEqual(testParameterValue, t.AssemblyVersion, "New AssemblyNamespace value should be set");
            CommandLine.ValidateHasParameter(t, @"/asmversion:" + testParameterValue.ToString(), false /* no response file */);
        }

        /// <summary>
        /// Tests the /nologo switch
        /// </summary>
        [TestMethod]
        public void NoLogo()
        {
            var t = new ResolveComReference.TlbImp();

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
            var t = new ResolveComReference.TlbImp();
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
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.IsNull(t.OutputAssembly, "OutputAssembly should be null by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/out:", false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.OutputAssembly, "New OutputAssembly value should be set");
            CommandLine.ValidateHasParameter(t, @"/out:" + testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /noclassmembers switch
        /// </summary>
        [TestMethod]
        public void PreventClassMembers()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.PreventClassMembers, "PreventClassMembers should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/noclassmembers", false /* no response file */);

            t.PreventClassMembers = true;
            Assert.IsTrue(t.PreventClassMembers, "PreventClassMembers should be true");
            CommandLine.ValidateHasParameter(t, @"/noclassmembers", false /* no response file */);
        }

        /// <summary>
        /// Tests the /sysarray switch
        /// </summary>
        [TestMethod]
        public void SafeArrayAsSystemArray()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.SafeArrayAsSystemArray, "SafeArrayAsSystemArray should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/sysarray", false /* no response file */);

            t.SafeArrayAsSystemArray = true;
            Assert.IsTrue(t.SafeArrayAsSystemArray, "SafeArrayAsSystemArray should be true");
            CommandLine.ValidateHasParameter(t, @"/sysarray", false /* no response file */);
        }

        /// <summary>
        /// Tests the /silent switch
        /// </summary>
        [TestMethod]
        public void Silent()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.Silent, "Silent should be false by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/silent", false /* no response file */);

            t.Silent = true;
            Assert.IsTrue(t.Silent, "Silent should be true");
            CommandLine.ValidateHasParameter(t, @"/silent", false /* no response file */);
        }

        /// <summary>
        /// Tests the /transform: switch
        /// </summary>
        [TestMethod]
        public void Transform()
        {
            var t = new ResolveComReference.TlbImp();

            var dispRet = ResolveComReference.TlbImpTransformFlags.TransformDispRetVals;
            var serialize = ResolveComReference.TlbImpTransformFlags.SerializableValueClasses;
            var both = ResolveComReference.TlbImpTransformFlags.TransformDispRetVals | ResolveComReference.TlbImpTransformFlags.SerializableValueClasses;

            t.TypeLibName = "SomeRandomControl.tlb";
            t.ToolPath = Path.GetTempPath();

            Assert.AreEqual(ResolveComReference.TlbImpTransformFlags.None, t.Transform, "Transform should be TlbImpTransformFlags.None by default");
            CommandLine.ValidateNoParameterStartsWith(t, @"/transform:", false /* no response file */);

            t.Transform = dispRet;
            Assert.AreEqual(dispRet, t.Transform, "New Transform value should be set");
            CommandLine.ValidateHasParameter(t, @"/transform:DispRet", false /* no response file */);

            t.Transform = serialize;
            Assert.AreEqual(serialize, t.Transform, "New Transform value should be set");
            CommandLine.ValidateHasParameter(t, @"/transform:SerializableValueClasses", false /* no response file */);

            t.Transform = both;
            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "TlbImp.InvalidTransformParameter", t.Transform);
        }

        /// <summary>
        /// Tests the /verbose switch
        /// </summary>
        [TestMethod]
        public void Verbose()
        {
            var t = new ResolveComReference.TlbImp();

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
            var t = new ResolveComReference.TlbImp();

            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "TlbImp.NoInputFileSpecified");
        }
    }
}
