// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.Globalization;
using Xunit;

namespace Microsoft.Build.UnitTests.AxTlbImp_Tests
{
    sealed public class TlbImp_Tests
    {
        /// <summary>
        /// Tests that /machine flag will be set.
        /// </summary>
        [Fact]
        public void Machine()
        {
            var t = new ResolveComReference.TlbImp();
            Assert.Null(t.Machine); // "Machine should be null by default"

            t.Machine = "Agnostic";
            Assert.Equal("Agnostic", t.Machine); // "New TypeLibName value should be set");
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch("/machine:Agnostic"),
                false /* no response file */);
        }

        /// <summary>
        /// Check ReferenceFiles
        /// </summary>
        [Fact]
        public void ReferenceFiles()
        {
            var t = new ResolveComReference.TlbImp();
            Assert.Null(t.ReferenceFiles); // "ReferenceFiles should be null by default"

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
        [Fact]
        public void TypeLibName()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = "Interop.Foo.dll";

            Assert.Null(t.TypeLibName); // "TypeLibName should be null by default"

            t.TypeLibName = testParameterValue;
            Assert.Equal(testParameterValue, t.TypeLibName); // "New TypeLibName value should be set"
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [Fact]
        public void TypeLibNameWithSpaces()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = @"c:\Program Files\Interop.Foo.dll";

            Assert.Null(t.TypeLibName); // "TypeLibName should be null by default"

            t.TypeLibName = testParameterValue;
            Assert.Equal(testParameterValue, t.TypeLibName); // "New TypeLibName value should be set"
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /namespace: command line option
        /// </summary>
        [Fact]
        public void AssemblyNamespace()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = "Microsoft.Build.Foo";

            Assert.Null(t.AssemblyNamespace); // "AssemblyNamespace should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/namespace:"),
                false /* no response file */);

            t.AssemblyNamespace = testParameterValue;
            Assert.Equal(testParameterValue, t.AssemblyNamespace); // "New AssemblyNamespace value should be set"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/namespace:") + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /asmversion: command line option
        /// </summary>
        [Fact]
        public void AssemblyVersion()
        {
            var t = new ResolveComReference.TlbImp();
            Version testParameterValue = new Version(2, 12);

            Assert.Null(t.AssemblyVersion); // "AssemblyVersion should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/asmversion:"),
                false /* no response file */);

            t.AssemblyVersion = testParameterValue;
            Assert.Equal(testParameterValue, t.AssemblyVersion); // "New AssemblyNamespace value should be set"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/asmversion:") + testParameterValue.ToString(),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /nologo switch
        /// </summary>
        [Fact]
        public void NoLogo()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.False(t.NoLogo); // "NoLogo should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/nologo"),
                false /* no response file */);

            t.NoLogo = true;
            Assert.True(t.NoLogo); // "NoLogo should be true"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/nologo"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch
        /// </summary>
        [Fact]
        public void OutputAssembly()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.Null(t.OutputAssembly); // "OutputAssembly should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:"),
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.Equal(testParameterValue, t.OutputAssembly); // "New OutputAssembly value should be set"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:") + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch, with a space in the output file
        /// </summary>
        [Fact]
        public void OutputAssemblyWithSpaces()
        {
            var t = new ResolveComReference.TlbImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.Null(t.OutputAssembly); // "OutputAssembly should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:"),
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.Equal(testParameterValue, t.OutputAssembly); // "New OutputAssembly value should be set"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/out:") + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /noclassmembers switch
        /// </summary>
        [Fact]
        public void PreventClassMembers()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.False(t.PreventClassMembers); // "PreventClassMembers should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/noclassmembers"),
                false /* no response file */);

            t.PreventClassMembers = true;
            Assert.True(t.PreventClassMembers); // "PreventClassMembers should be true"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/noclassmembers"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /sysarray switch
        /// </summary>
        [Fact]
        public void SafeArrayAsSystemArray()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.False(t.SafeArrayAsSystemArray); // "SafeArrayAsSystemArray should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/sysarray"),
                false /* no response file */);

            t.SafeArrayAsSystemArray = true;
            Assert.True(t.SafeArrayAsSystemArray); // "SafeArrayAsSystemArray should be true"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/sysarray"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /silent switch
        /// </summary>
        [Fact]
        public void Silent()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.False(t.Silent); // "Silent should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/silent"),
                false /* no response file */);

            t.Silent = true;
            Assert.True(t.Silent); // "Silent should be true"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/silent"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /transform: switch
        /// </summary>
        [Fact]
        public void Transform()
        {
            var t = new ResolveComReference.TlbImp();

            var dispRet = ResolveComReference.TlbImpTransformFlags.TransformDispRetVals;
            var serialize = ResolveComReference.TlbImpTransformFlags.SerializableValueClasses;
            var both = ResolveComReference.TlbImpTransformFlags.TransformDispRetVals | ResolveComReference.TlbImpTransformFlags.SerializableValueClasses;

            t.TypeLibName = "SomeRandomControl.tlb";
            t.ToolPath = Path.GetTempPath();

            Assert.Equal(ResolveComReference.TlbImpTransformFlags.None, t.Transform); // "Transform should be TlbImpTransformFlags.None by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/transform:"),
                false /* no response file */);

            t.Transform = dispRet;
            Assert.Equal(dispRet, t.Transform); // "New Transform value should be set"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/transform:DispRet"),
                false /* no response file */);


            t.Transform = serialize;
            Assert.Equal(serialize, t.Transform); // "New Transform value should be set"
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
        [Fact]
        public void Verbose()
        {
            var t = new ResolveComReference.TlbImp();

            Assert.False(t.Verbose); // "Verbose should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/verbose"),
                false /* no response file */);


            t.Verbose = true;
            Assert.True(t.Verbose); // "Verbose should be true"
            CommandLine.ValidateHasParameter(
                t,
                CommandLineBuilder.FixCommandLineSwitch(@"/verbose"),
                false /* no response file */);
        }

        /// <summary>
        /// Tests that task does the right thing (fails) when no .ocx file is passed to it
        /// </summary>
        [Fact]
        public void TaskFailsWithNoInputs()
        {
            var t = new ResolveComReference.TlbImp();

            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "TlbImp.NoInputFileSpecified");
        }
    }
}
