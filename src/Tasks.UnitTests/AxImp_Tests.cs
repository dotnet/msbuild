// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;

using Xunit;

namespace Microsoft.Build.UnitTests.AxTlbImp_Tests
{
    sealed public class AxImp_Tests
    {
        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [Fact]
        public void ActiveXControlName()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.Null(t.ActiveXControlName); // "ActiveXControlName should be null by default"

            t.ActiveXControlName = testParameterValue;
            Assert.Equal(testParameterValue, t.ActiveXControlName); // "New ActiveXControlName value should be set"
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests that the assembly being imported is passed to the command line
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void ActiveXControlNameWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.Null(t.ActiveXControlName); // "ActiveXControlName should be null by default"

            t.ActiveXControlName = testParameterValue;
            Assert.Equal(testParameterValue, t.ActiveXControlName); // "New ActiveXControlName value should be set"
            CommandLine.ValidateHasParameter(t, testParameterValue, false /* no response file */);
        }

        /// <summary>
        /// Tests the /source switch
        /// </summary>
        [Fact]
        public void GenerateSource()
        {
            var t = new ResolveComReference.AxImp();

            Assert.False(t.GenerateSource); // "GenerateSource should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/source",
                false /* no response file */);

            t.GenerateSource = true;
            Assert.True(t.GenerateSource); // "GenerateSource should be true"
            CommandLine.ValidateHasParameter(
                t,
                @"/source",
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /nologo switch
        /// </summary>
        [Fact]
        public void NoLogo()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "The /nologo switch is not available on Mono"
            }

            var t = new ResolveComReference.AxImp();

            Assert.False(t.NoLogo); // "NoLogo should be false by default"
            CommandLine.ValidateNoParameterStartsWith(t, @"/nologo", false /* no response file */);

            t.NoLogo = true;
            Assert.True(t.NoLogo); // "NoLogo should be true"
            CommandLine.ValidateHasParameter(t, @"/nologo", false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch
        /// </summary>
        [Fact]
        public void OutputAssembly()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "AxInterop.Foo.dll";

            Assert.Null(t.OutputAssembly); // "OutputAssembly should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/out:",
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.Equal(testParameterValue, t.OutputAssembly); // "New OutputAssembly value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/out:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /out: switch, with a space in the output file
        /// </summary>
        [Fact]
        public void OutputAssemblyWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"c:\Program Files\AxInterop.Foo.dll";

            Assert.Null(t.OutputAssembly); // "OutputAssembly should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/out:",
                false /* no response file */);

            t.OutputAssembly = testParameterValue;
            Assert.Equal(testParameterValue, t.OutputAssembly); // "New OutputAssembly value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/out:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /rcw: switch
        /// </summary>
        [Fact]
        public void RuntimeCallableWrapper()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = "Interop.Foo.dll";

            Assert.Null(t.RuntimeCallableWrapperAssembly); // "RuntimeCallableWrapper should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/rcw:",
                false /* no response file */);

            t.RuntimeCallableWrapperAssembly = testParameterValue;
            Assert.Equal(testParameterValue, t.RuntimeCallableWrapperAssembly); // "New RuntimeCallableWrapper value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/rcw:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /rcw: switch with a space in the filename
        /// </summary>
        [Fact]
        public void RuntimeCallableWrapperWithSpaces()
        {
            var t = new ResolveComReference.AxImp();
            string testParameterValue = @"C:\Program Files\Microsoft Visual Studio 10.0\Interop.Foo.dll";

            Assert.Null(t.RuntimeCallableWrapperAssembly); // "RuntimeCallableWrapper should be null by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/rcw:",
                false /* no response file */);

            t.RuntimeCallableWrapperAssembly = testParameterValue;
            Assert.Equal(testParameterValue, t.RuntimeCallableWrapperAssembly); // "New RuntimeCallableWrapper value should be set"
            CommandLine.ValidateHasParameter(
                t,
                @"/rcw:" + testParameterValue,
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /silent switch
        /// </summary>
        [Fact]
        public void Silent()
        {
            var t = new ResolveComReference.AxImp();

            Assert.False(t.Silent); // "Silent should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/silent",
                false /* no response file */);

            t.Silent = true;
            Assert.True(t.Silent); // "Silent should be true"
            CommandLine.ValidateHasParameter(
                t,
                @"/silent",
                false /* no response file */);
        }

        /// <summary>
        /// Tests the /verbose switch
        /// </summary>
        [Fact]
        public void Verbose()
        {
            var t = new ResolveComReference.AxImp();

            Assert.False(t.Verbose); // "Verbose should be false by default"
            CommandLine.ValidateNoParameterStartsWith(
                t,
                @"/verbose",
                false /* no response file */);

            t.Verbose = true;
            Assert.True(t.Verbose); // "Verbose should be true"
            CommandLine.ValidateHasParameter(
                t,
                @"/verbose",
                false /* no response file */);
        }

        /// <summary>
        /// Tests that task does the right thing (fails) when no .ocx file is passed to it
        /// </summary>
        [Fact]
        public void TaskFailsWithNoInputs()
        {
            var t = new ResolveComReference.AxImp();

            Utilities.ExecuteTaskAndVerifyLogContainsErrorFromResource(t, "AxImp.NoInputFileSpecified");
        }
    }
}
