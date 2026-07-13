// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Tasks;


#nullable disable

namespace Microsoft.Build.UnitTests.AxTlbImp_Tests
{
    [TestClass]
    public sealed class AxImp_Tests
    {
        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [MSBuildTestMethod]
        public void ActiveXControlName()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.IsNull(t.ActiveXControlName); // "ActiveXControlName should be null by default"

            t.ActiveXControlName = testParameterValue;
            Assert.AreEqual(testParameterValue, t.ActiveXControlName); // "New ActiveXControlName value should be set"
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [MSBuildTestMethod]
        public void ActiveXControlNameWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.IsNull(t.ActiveXControlName); // "ActiveXControlName should be null by default"

            t.ActiveXControlName = testParameterValue;
            Assert.AreEqual(testParameterValue, t.ActiveXControlName); // "New ActiveXControlName value should be set"
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /source switch
        /// </summary>
        [MSBuildTestMethod]
        public void GenerateSource()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.GenerateSource); // "GenerateSource should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/source",
                false /* no response file */);

            t.GenerateSource = true;
            Assert.IsTrue(t.GenerateSource); // "GenerateSource should be true"
            CommandLine.ValidateHasParameter(
                t,
                @"/source",
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /nologo switch
        /// </summary>
        [MSBuildTestMethod]
        public void NoLogo()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.NoLogo); // "NoLogo should be false by default"
            CommandLine.ValidateNoParameterStartsWith(t, @"/nologo", false /* no response file */);

            t.NoLogo = true;
            Assert.IsTrue(t.NoLogo); // "NoLogo should be true"
            CommandLine.ValidateHasParameter(t, @"/nologo", false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch
        /// </summary>
        [MSBuildTestMethod]
        public void OutputAssembly()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.IsNull(t.OutputAssembly); // "OutputAssembly should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/out:",
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.OutputAssembly); // "New OutputAssembly value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/out:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch, with a space in the output file
        /// </summary>
        [MSBuildTestMethod]
        public void OutputAssemblyWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.IsNull(t.OutputAssembly); // "OutputAssembly should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/out:",
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.OutputAssembly); // "New OutputAssembly value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/out:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /rcw: switch
        /// </summary>
        [MSBuildTestMethod]
        public void RuntimeCallableWrapper()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "Interop.Foo.dll";

            Assert.IsNull(t.RuntimeCallableWrapperAssembly); // "RuntimeCallableWrapper should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/rcw:",
                false /* no response file */);

            t.RuntimeCallableWrapperAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.RuntimeCallableWrapperAssembly); // "New RuntimeCallableWrapper value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/rcw:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /rcw: switch with a space in the filename
        /// </summary>
        [MSBuildTestMethod]
        public void RuntimeCallableWrapperWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"C:\Program Files\Microsoft Visual Studio 10.0\Interop.Foo.dll";

            Assert.IsNull(t.RuntimeCallableWrapperAssembly); // "RuntimeCallableWrapper should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/rcw:",
                false /* no response file */);

            t.RuntimeCallableWrapperAssembly = testParameterValue;
            Assert.AreEqual(testParameterValue, t.RuntimeCallableWrapperAssembly); // "New RuntimeCallableWrapper value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/rcw:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /silent switch
        /// </summary>
        [MSBuildTestMethod]
        public void Silent()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.Silent); // "Silent should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/silent",
                false /* no response file */);

            t.Silent = true;
            Assert.IsTrue(t.Silent); // "Silent should be true"
            CommandLine.ValidateHasParameter(
                t,
                @"/silent",
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /verbose switch
        /// </summary>
        [MSBuildTestMethod]
        public void Verbose()
        {
            var t = new ResolveComReference.AxImp();

            Assert.IsFalse(t.Verbose); // "Verbose should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/verbose",
                false /* no response file */);

            t.Verbose = true;
            Assert.IsTrue(t.Verbose); // "Verbose should be true"
            CommandLine.ValidateHasParameter(
                t,
                @"/verbose",
                false /* no response file */);
        }

        /// <summary>
        /// Tests that task does the right thing (fails) when no .ocx file is passed to it
        /// </summary>
        [MSBuildTestMethod]
        public void TaskFailsWithNoInputs()
        {
            var t = new ResolveComReference.AxImp();

            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxImp.NoInputFileSpecified");
        }
    }
}
