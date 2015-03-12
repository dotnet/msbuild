// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.Hosting;
using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests
{
    internal class MockVbcHostObject : IVbcHostObject
    {
        private bool _compileMethodWasCalled = false;
        private bool _designTime = false;

        /***********************************************************************
         * Constructor:     MockVbcHostObject
         *
         **********************************************************************/
        public MockVbcHostObject()
        {
        }

        /***********************************************************************
         * Method:          MockVbcHostObject.SetDesignTime
         *
         * Allows unit tests to control whether or not the host object is running
         * in "design time".
         **********************************************************************/
        public void SetDesignTime(bool designTime)
        {
            _designTime = designTime;
        }

        /***********************************************************************
         * Method:          MockVbcHostObject.CompileMethodWasCalled
         *
         * Returns true if the "Compile()" method has ever been called on this
         * instance of this object, false otherwise.
         *
         **********************************************************************/
        public bool CompileMethodWasCalled
        {
            get { return _compileMethodWasCalled; }
            set { _compileMethodWasCalled = value; }
        }

        /***********************************************************************
         * Method:          MockVbcHostObject.IsDesignTime
         *
         **********************************************************************/
        bool IVbcHostObject.IsDesignTime()
        {
            return _designTime;
        }

        /***********************************************************************
         * Method:          MockVbcHostObject.Compile
         *
         * This is a method from the IVbcHostObject interface.  This will get
         * called during the build as the Vbc task is executing.
         *
         **********************************************************************/
        bool IVbcHostObject.Compile()
        {
            _compileMethodWasCalled = true;
            return true;
        }

        void IVbcHostObject.BeginInitialization() { return; }
        void IVbcHostObject.EndInitialization() { return; }

        bool IVbcHostObject.SetAdditionalLibPaths(string[] additionalLibPaths) { return true; }
        bool IVbcHostObject.SetAddModules(string[] addModules) { return true; }
        bool IVbcHostObject.SetBaseAddress(string targetType, string baseAddress) { return true; }
        bool IVbcHostObject.SetCodePage(int codePage) { return true; }
        bool IVbcHostObject.SetDebugType(bool emitDebugInformation, string debugType) { return true; }
        bool IVbcHostObject.SetDefineConstants(string defineConstants) { return true; }
        bool IVbcHostObject.SetDelaySign(bool delaySign) { return true; }
        bool IVbcHostObject.SetDisabledWarnings(string disabledWarnings) { return true; }
        bool IVbcHostObject.SetDocumentationFile(string documentationFile) { return true; }
        bool IVbcHostObject.SetErrorReport(string errorReport) { return true; }
        bool IVbcHostObject.SetFileAlignment(int fileAlignment) { return true; }
        bool IVbcHostObject.SetGenerateDocumentation(bool generateDocumentation) { return true; }
        bool IVbcHostObject.SetImports(ITaskItem[] imports) { return true; }
        bool IVbcHostObject.SetKeyContainer(string keyContainer) { return true; }
        bool IVbcHostObject.SetKeyFile(string keyFile) { return true; }
        bool IVbcHostObject.SetLinkResources(ITaskItem[] linkResources) { return true; }
        bool IVbcHostObject.SetMainEntryPoint(string mainEntryPoint) { return true; }
        bool IVbcHostObject.SetNoConfig(bool noConfig) { return true; }
        bool IVbcHostObject.SetNoStandardLib(bool noStandardLib) { return true; }
        bool IVbcHostObject.SetNoWarnings(bool noWarnings) { return true; }
        bool IVbcHostObject.SetOptimize(bool optimize) { return true; }
        bool IVbcHostObject.SetOptionCompare(string optionCompare) { return true; }
        bool IVbcHostObject.SetOptionExplicit(bool optionExplicit) { return true; }
        bool IVbcHostObject.SetOptionStrict(bool optionStrict) { return true; }
        bool IVbcHostObject.SetOptionStrictType(string optionStrictType) { return true; }
        bool IVbcHostObject.SetOutputAssembly(string outputAssembly) { return true; }
        bool IVbcHostObject.SetPlatform(string platform) { return true; }
        bool IVbcHostObject.SetReferences(ITaskItem[] references) { return true; }
        bool IVbcHostObject.SetRemoveIntegerChecks(bool removeIntegerChecks) { return true; }
        bool IVbcHostObject.SetResources(ITaskItem[] resources) { return true; }
        bool IVbcHostObject.SetResponseFiles(ITaskItem[] responseFiles) { return true; }
        bool IVbcHostObject.SetRootNamespace(string rootNamespace) { return true; }
        bool IVbcHostObject.SetSdkPath(string sdkPath) { return true; }
        bool IVbcHostObject.SetSources(ITaskItem[] sources) { return true; }
        bool IVbcHostObject.SetTargetCompactFramework(bool targetCompactFramework) { return true; }
        bool IVbcHostObject.SetTargetType(string targetType) { return true; }
        bool IVbcHostObject.SetTreatWarningsAsErrors(bool treatWarningsAsErrors) { return true; }
        bool IVbcHostObject.SetWarningsAsErrors(string warningsAsErrors) { return true; }
        bool IVbcHostObject.SetWarningsNotAsErrors(string warningsNotAsErrors) { return true; }
        bool IVbcHostObject.SetWin32Icon(string win32Icon) { return true; }
        bool IVbcHostObject.SetWin32Resource(string win32Resource) { return true; }

        bool IVbcHostObject.IsUpToDate() { return false; }
    }

    internal class MockVbcHostObject2 : MockVbcHostObject, IVbcHostObject2
    {
        bool IVbcHostObject2.SetOptionInfer(bool optionInfer) { return true; }
        bool IVbcHostObject2.SetModuleAssemblyName(string moduleAssemblyName) { return true; }
        bool IVbcHostObject2.SetWin32Manifest(string win32Manifest) { return true; }
    }

    internal class MockVbcHostObject3 : MockVbcHostObject2, IVbcHostObject3
    {
        bool IVbcHostObject3.SetLanguageVersion(string languageVersion) { return true; }
    }

    internal class MockVbcHostObject4 : MockVbcHostObject3, IVbcHostObject4
    {
        bool IVbcHostObject4.SetVBRuntime(string VBRuntime) { return true; }
    }

    internal class MockVbcHostObject5 : MockVbcHostObject4, IVbcHostObject5
    {
        IVbcHostObjectFreeThreaded IVbcHostObject5.GetFreeThreadedHostObject() { return new MockVbcHostObjectFreeThreaded(this); }
        int IVbcHostObject5.CompileAsync(out IntPtr buildSucceededEvent, out IntPtr buildFailedEvent) { buildSucceededEvent = IntPtr.Zero; buildFailedEvent = IntPtr.Zero; return 0; }
        int IVbcHostObject5.EndCompile(bool buildSuccess) { return 0; }
        bool IVbcHostObject5.SetPlatformWith32BitPreference(string platformWith32BitPreference) { return true; }
        bool IVbcHostObject5.SetHighEntropyVA(bool highEntropyVA) { return true; }
        bool IVbcHostObject5.SetSubsystemVersion(string subsystemVersion) { return true; }
    }

    internal class MockVbcHostObjectFreeThreaded : IVbcHostObjectFreeThreaded
    {
        private MockVbcHostObject5 _mock;
        internal MockVbcHostObjectFreeThreaded(MockVbcHostObject5 mock) { _mock = mock; }
        bool IVbcHostObjectFreeThreaded.Compile() { _mock.CompileMethodWasCalled = true; return true; }
    }

    internal class MockVbcAnalyzerHostObject : MockVbcHostObject5, IAnalyzerHostObject
    {
        public ITaskItem[] Analyzers { get; private set; }
        public string RuleSet { get; private set; }
        public ITaskItem[] AdditionalFiles { get; private set; }

        bool IAnalyzerHostObject.SetAnalyzers(ITaskItem[] analyzers)
        {
            this.Analyzers = analyzers;
            return true;
        }

        bool IAnalyzerHostObject.SetRuleSet(string ruleSetFile)
        {
            this.RuleSet = ruleSetFile;
            return true;
        }

        bool IAnalyzerHostObject.SetAdditionalFiles(ITaskItem[] additionalFiles)
        {
            this.AdditionalFiles = additionalFiles;
            return true;
        }
    }
}
