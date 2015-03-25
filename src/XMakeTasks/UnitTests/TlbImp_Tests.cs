// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests.AxTlbImp_Tests
{
    [TestFixture]
    sealed public class TlbImp_Tests
    {
        /// <summary>
        /// Tests that /machine flag will be set.
        /// </summary>
        [Test]
        public void Machine()
        {
            var t = new ResolveComReference.TlbImp();
            Assert.IsNull(t.Machine, "Machine should be null by default");

            t.Machine = "Agnostic";
            Assert.AreEqual("Agnostic", t.Machine, "New TypeLibName value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/machine:Agnostic"),
                false /* no response file */);
        }

        /// <summary>
        /// Check ReferenceFiles
        /// </summary>
        [Test]
        public void ReferenceFiles()
        {
            var t = new ResolveComReference.TlbImp();
            Assert.IsNull(t.ReferenceFiles, "ReferenceFiles should be null by default");

            t.ReferenceFiles = new string[] { "File1.dll", "File2.dll" };
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/reference:File1.dll"),
                false /* no response file */);
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/reference:File2.dll"),
                false /* no response file */);
        }
        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [Test]
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
        [Test]
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
        [Test]
        public void AssemblyNamespace()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = "Microsoft.Build.Foo";

            Assert.IsNull(t.AssemblyNamespace, "AssemblyNamespace should be null by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/namespace:"),
                false /* no response file */);

            t.AssemblyNamespace = testParameterValue;
            Assert.AreEqual(testParameterValue, t.AssemblyNamespace, "New AssemblyNamespace value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/namespace:") + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /asmversion: command line option
        /// </summary>
        [Test]
        public void AssemblyVersion()
        {
            var t = new ResolveComReference.TlbImp();
            Version testParameterValue = new Version(2, 12);

            Assert.IsNull(t.AssemblyVersion, "AssemblyVersion should be null by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/asmversion:"),
                false /* no response file */);

            t.AssemblyVersion = testParameterValue;
            Assert.AreEqual(testParameterValue, t.AssemblyVersion, "New AssemblyNamespace value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/asmversion:") + testParameterValue.ToString(),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /nologo switch
        /// </summary>
        [Test]
        public void NoLogo()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.NoLogo, "NoLogo should be false by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/nologo"),
                false /* no response file */);

            t.NoLogo = true;
            Assert.IsTrue(t.NoLogo, "NoLogo should be true");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/nologo"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch
        /// </summary>
        [Test]
        public void OutputAssembly()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.IsNull(t.OutputAssembly, "OutputAssembly should be null by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:"),
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.OutputAssembly, "New OutputAssembly value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:") + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch, with a space in the output file
        /// </summary>
        [Test]
        public void OutputAssemblyWithSpaces()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.IsNull(t.OutputAssembly, "OutputAssembly should be null by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:"),
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.OutputAssembly, "New OutputAssembly value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:") + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /noclassmembers switch
        /// </summary>
        [Test]
        public void PreventClassMembers()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.PreventClassMembers, "PreventClassMembers should be false by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/noclassmembers"),
                false /* no response file */);

            t.PreventClassMembers = true;
            Assert.IsTrue(t.PreventClassMembers, "PreventClassMembers should be true");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/noclassmembers"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /sysarray switch
        /// </summary>
        [Test]
        public void SafeArrayAsSystemArray()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.SafeArrayAsSystemArray, "SafeArrayAsSystemArray should be false by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/sysarray"),
                false /* no response file */);

            t.SafeArrayAsSystemArray = true;
            Assert.IsTrue(t.SafeArrayAsSystemArray, "SafeArrayAsSystemArray should be true");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/sysarray"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /silent switch
        /// </summary>
        [Test]
        public void Silent()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.Silent, "Silent should be false by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/silent"),
                false /* no response file */);

            t.Silent = true;
            Assert.IsTrue(t.Silent, "Silent should be true");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/silent"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /transform: switch
        /// </summary>
        [Test]
        public void Transform()
        {
            var t = new ResolveComReference.TlbImp();

            var dispRet = ResolveComReference.TlbImpTransformFlags.TransformDispRetVals;
            var serialize = ResolveComReference.TlbImpTransformFlags.SerializableValueClasses;
            var both = ResolveComReference.TlbImpTransformFlags.TransformDispRetVals | ResolveComReference.TlbImpTransformFlags.SerializableValueClasses;

            t.TypeLibName = "SomeRandomControl.tlb";
            t.ToolPath = Path.GetTempPath();

            Assert.AreEqual(ResolveComReference.TlbImpTransformFlags.None, t.Transform, "Transform should be TlbImpTransformFlags.None by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/transform:"),
                false /* no response file */);

            t.Transform = dispRet;
            Assert.AreEqual(dispRet, t.Transform, "New Transform value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/transform:DispRet"),
                false /* no response file */);

            t.Transform = serialize;
            Assert.AreEqual(serialize, t.Transform, "New Transform value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/transform:SerializableValueClasses"),
                false /* no response file */);

            t.Transform = both;
            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "TlbImp.InvalidTransformParameter", t.Transform);
        }

        /// <summary>
        /// Tests the /verbose switch
        /// </summary>
        [Test]
        public void Verbose()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.IsFalse(t.Verbose, "Verbose should be false by default");
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/verbose"),
                false /* no response file */);

            t.Verbose = true;
            Assert.IsTrue(t.Verbose, "Verbose should be true");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/verbose"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests that task does the right thing (fails) when no .ocx file is passed to it
        /// </summary>
        [Test]
        public void TaskFailsWithNoInputs()
        {
            var t = new ResolveComReference.TlbImp();

            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "TlbImp.NoInputFileSpecified");
        }
    }
}
