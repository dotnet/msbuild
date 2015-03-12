// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.Hosting;
using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests
{
    internal class MockCscHostObject : ICscHostObject
    {
        private bool _compileMethodWasCalled = false;
        private bool _designTime = false;

        /// <summary>
        /// Construct
        /// </summary>
        public MockCscHostObject()
        {
        }

        /// <summary>
        /// Allows unit tests to control whether or not the host object is running
        /// in "design time".
        /// </summary>
        /// <param name="designTime"></param>
        public void SetDesignTime(bool designTime)
        {
            _designTime = designTime;
        }

        /// <summary>
        /// Returns true if the "Compile()" method has ever been called on this
        /// instance of this object, false otherwise.
        /// </summary>
        /// <value></value>
        public bool CompileMethodWasCalled
        {
            get { return _compileMethodWasCalled; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        bool ICscHostObject.IsDesignTime()
        {
            return _designTime;
        }

        /// <summary>
        /// This is a method from the ICscHostObject interface.  This will get
        /// called during the build as the Vbc task is executing.
        /// </summary>
        /// <returns></returns>
        bool ICscHostObject.Compile()
        {
            _compileMethodWasCalled = true;
            return true;
        }

        void ICscHostObject.BeginInitialization() { return; }

        bool ICscHostObject.SetAdditionalLibPaths(string[] additionalLibPaths) { return true; }
        bool ICscHostObject.SetAddModules(string[] addModules) { return true; }
        bool ICscHostObject.SetCodePage(int codePage) { return true; }
        bool ICscHostObject.SetDefineConstants(string defineConstants) { return true; }
        bool ICscHostObject.SetDisabledWarnings(string disabledWarnings) { return true; }
        bool ICscHostObject.SetDocumentationFile(string documentationFile) { return true; }
        bool ICscHostObject.SetErrorReport(string errorReport) { return true; }
        bool ICscHostObject.SetFileAlignment(int fileAlignment) { return true; }
        bool ICscHostObject.SetKeyContainer(string keyContainer) { return true; }
        bool ICscHostObject.SetKeyFile(string keyFile) { return true; }
        bool ICscHostObject.SetLinkResources(ITaskItem[] linkResources) { return true; }
        bool ICscHostObject.SetModuleAssemblyName(string moduleAssemblyName) { return true; }
        bool ICscHostObject.SetNoConfig(bool noConfig) { return true; }
        bool ICscHostObject.SetNoStandardLib(bool noStandardLib) { return true; }
        bool ICscHostObject.SetOptimize(bool optimize) { return true; }
        bool ICscHostObject.SetOutputAssembly(string outputAssembly) { return true; }
        bool ICscHostObject.SetPdbFile(string pdbFile) { return true; }
        bool ICscHostObject.SetPlatform(string platform) { return true; }
        bool ICscHostObject.SetReferences(ITaskItem[] references) { return true; }
        bool ICscHostObject.SetResources(ITaskItem[] resources) { return true; }
        bool ICscHostObject.SetResponseFiles(ITaskItem[] responseFiles) { return true; }
        bool ICscHostObject.SetSources(ITaskItem[] sources) { return true; }
        bool ICscHostObject.SetTargetType(string targetType) { return true; }
        bool ICscHostObject.SetTreatWarningsAsErrors(bool treatWarningsAsErrors) { return true; }
        bool ICscHostObject.SetWarningsAsErrors(string warningsAsErrors) { return true; }
        bool ICscHostObject.SetWarningsNotAsErrors(string warningsNotAsErrors) { return true; }
        bool ICscHostObject.SetWin32Icon(string win32Icon) { return true; }
        bool ICscHostObject.SetWin32Resource(string win32Resource) { return true; }


        bool ICscHostObject.EndInitialization(out string a, out int b) { a = null; b = 0; return true; }
        bool ICscHostObject.SetAllowUnsafeBlocks(bool a) { return true; }
        bool ICscHostObject.SetBaseAddress(string a) { return true; }
        bool ICscHostObject.SetCheckForOverflowUnderflow(bool a) { return true; }
        bool ICscHostObject.SetDebugType(string a) { return true; }
        bool ICscHostObject.SetDelaySign(bool a, bool b) { return true; }
        bool ICscHostObject.SetEmitDebugInformation(bool a) { return true; }
        bool ICscHostObject.SetGenerateFullPaths(bool a) { return true; }
        bool ICscHostObject.SetLangVersion(string a) { return true; }
        bool ICscHostObject.SetMainEntryPoint(string a, string b) { return true; }
        bool ICscHostObject.SetWarningLevel(int a) { return true; }

        bool ICscHostObject.IsUpToDate() { return false; }
    }

    internal class MockCscHostObject2 : MockCscHostObject, ICscHostObject2
    {
        bool ICscHostObject2.SetWin32Manifest(string win32Manifest) { return true; }
    }

    internal class MockCscHostObject3 : MockCscHostObject2, ICscHostObject3
    {
        bool ICscHostObject3.SetApplicationConfiguration(string applicationConfiguration) { return true; }
    }

    internal class MockCscHostObject4 : MockCscHostObject3, ICscHostObject4
    {
        bool ICscHostObject4.SetPlatformWith32BitPreference(string platformWith32BitPreference) { return true; }
        bool ICscHostObject4.SetHighEntropyVA(bool highEntropyVA) { return true; }
        bool ICscHostObject4.SetSubsystemVersion(string subsystemVersion) { return true; }
    }

    internal class MockCscAnalyzerHostObject : MockCscHostObject4, IAnalyzerHostObject
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
