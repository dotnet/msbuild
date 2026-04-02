// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Xunit;
using Xunit.NetCore.Extensions;
using Xunit.Sdk;
using Xunit.v3;

#nullable disable

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

[assembly: AssemblyFixture(typeof(MSBuildTestAssemblyFixture))]

// Wrap a TestEnvironment around each test method and class so if invariants have changed we will know where
[assembly: TestFramework(typeof(MSBuildTestFramework))]

namespace Microsoft.Build.UnitTests
{
    public class MSBuildTestFramework : XunitTestFramework
    {
        private sealed class MSBuildDiscoverer : ITestFrameworkDiscoverer
        {
            private readonly ITestFrameworkDiscoverer _wrapped;

            public MSBuildDiscoverer(ITestFrameworkDiscoverer wrapped)
                => _wrapped = wrapped;

            public ITestAssembly TestAssembly => _wrapped.TestAssembly;

            public ValueTask Find(Func<ITestCase, ValueTask<bool>> callback, ITestFrameworkDiscoveryOptions discoveryOptions, Type[] types = null, CancellationToken? cancellationToken = null)
                => _wrapped.Find(testCase => callback(new MSBuildTestCase((IXunitTestCase)testCase)), discoveryOptions, types, cancellationToken);
        }

        private sealed class MSBuildTestCase : IXunitTestCase, ISelfExecutingXunitTestCase
        {
            private readonly IXunitTestCase _wrapped;

            public MSBuildTestCase(IXunitTestCase wrapped)
                => _wrapped = wrapped;

            public Type[] SkipExceptions => _wrapped.SkipExceptions;

            public string SkipReason => _wrapped.SkipReason;

            public Type SkipType => _wrapped.SkipType;

            public string SkipUnless => _wrapped.SkipUnless;

            public string SkipWhen => _wrapped.SkipWhen;

            public IXunitTestClass TestClass => _wrapped.TestClass;

            public int TestClassMetadataToken => _wrapped.TestClassMetadataToken;

            public string TestClassName => _wrapped.TestClassName;

            public string TestClassSimpleName => _wrapped.TestClassSimpleName;

            public IXunitTestCollection TestCollection => _wrapped.TestCollection;

            public IXunitTestMethod TestMethod => _wrapped.TestMethod;

            public int TestMethodMetadataToken => _wrapped.TestMethodMetadataToken;

            public string TestMethodName => _wrapped.TestMethodName;

            public string[] TestMethodParameterTypesVSTest => _wrapped.TestMethodParameterTypesVSTest;

            public string TestMethodReturnTypeVSTest => _wrapped.TestMethodReturnTypeVSTest;

            public int Timeout => _wrapped.Timeout;

            public bool Explicit => _wrapped.Explicit;

            public string SourceFilePath => _wrapped.SourceFilePath;

            public int? SourceLineNumber => _wrapped.SourceLineNumber;

            public string TestCaseDisplayName => _wrapped.TestCaseDisplayName;

            public string TestClassNamespace => _wrapped.TestClassNamespace;

            public int? TestMethodArity => _wrapped.TestMethodArity;

            public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits => _wrapped.Traits;

            public string UniqueID => _wrapped.UniqueID;

            ITestClass ITestCase.TestClass => TestClass;

            ITestCollection ITestCase.TestCollection => TestCollection;

            ITestMethod ITestCase.TestMethod => TestMethod;

            int? ITestCaseMetadata.TestClassMetadataToken => TestClassMetadataToken;

            int? ITestCaseMetadata.TestMethodMetadataToken => TestMethodMetadataToken;

            public ValueTask<IReadOnlyCollection<IXunitTest>> CreateTests() => _wrapped.CreateTests();

            public void PostInvoke() => _wrapped.PostInvoke();

            public void PreInvoke() => _wrapped.PreInvoke();
            public ValueTask<RunSummary> Run(ExplicitOption explicitOption, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                using var _ = TestEnvironment.Create();
                return XunitRunnerHelper.RunXunitTestCase(
                    this,
                    messageBus,
                    cancellationTokenSource,
                    aggregator.Clone(),
                    explicitOption,
                    constructorArguments);
            }
        }

        protected override ITestFrameworkDiscoverer CreateDiscoverer(Assembly assembly) => new MSBuildDiscoverer(base.CreateDiscoverer(assembly));

        protected override ITestFrameworkExecutor CreateExecutor(Assembly assembly) => base.CreateExecutor(assembly);
    }

    public class MSBuildTestAssemblyFixture : IDisposable
    {
        private bool _disposed;
        private TestEnvironment _testEnvironment;

        public MSBuildTestAssemblyFixture()
        {
            // Set field to indicate tests are running in the TestInfo class in Microsoft.Build.Framework.
            //  See the comments on the TestInfo class for an explanation of why it works this way.
            var frameworkAssembly = typeof(ITask).Assembly;
            var testInfoType = frameworkAssembly.GetType("Microsoft.Build.Framework.TestInfo");
            var runningTestsField = testInfoType.GetField("s_runningTests", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            runningTestsField.SetValue(null, true);

            // Set the field in BuildEnvironmentState - as it might have been already preintialized by the data preparation of data driven tests
            testInfoType = frameworkAssembly.GetType("Microsoft.Build.Framework.BuildEnvironmentState");
            runningTestsField = testInfoType.GetField("s_runningTests", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            runningTestsField.SetValue(null, true);

            // BuildEnvironment instance may be initialized in some tests' static members before s_runningTests is set
            // So reset the instance with running tests enabled
            var currentBuildEnvironment = BuildEnvironmentHelper.Instance;
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(
                new BuildEnvironment(
                    currentBuildEnvironment.Mode,
                    currentBuildEnvironment.CurrentMSBuildExePath,
                    runningTests: true,
                    currentBuildEnvironment.RunningInMSBuildExe,
                    currentBuildEnvironment.RunningInVisualStudio,
                    currentBuildEnvironment.VisualStudioInstallRootDirectory));

            // Note: build error files will be initialized in test environments for particular tests, also we don't have output to report error files into anyway...
            _testEnvironment = TestEnvironment.Create(output: null, ignoreBuildErrorFiles: true);

            _testEnvironment.DoNotLaunchDebugger();

            var bootstrapCorePath = Path.Combine(Path.Combine(RunnerUtilities.BootstrapRootPath, "core"), Constants.DotnetProcessName);
            _testEnvironment.SetEnvironmentVariable(Constants.DotnetHostPathEnvVarName, bootstrapCorePath);

            // Reset the VisualStudioVersion environment variable.  This will be set if tests are run from a VS command prompt.  However,
            //  if the environment variable is set, it will interfere with tests which set the SubToolsetVersion
            //  (VerifySubToolsetVersionSetByConstructorOverridable), as the environment variable would take precedence.
            _testEnvironment.SetEnvironmentVariable("VisualStudioVersion", null);

            // Prevent test assemblies from logging any performance info.
            // https://github.com/dotnet/msbuild/pull/6274
            _testEnvironment.SetEnvironmentVariable("DOTNET_PERFLOG_DIR", null);

            // Use a project-specific temporary path
            //  This is so multiple test projects can be run in parallel without sharing the same temp directory
            var subdirectory = Path.GetRandomFileName();

            string newTempPath = Path.Combine(Path.GetTempPath(), subdirectory);
            var assemblyTempFolder = _testEnvironment.CreateFolder(newTempPath);

            _testEnvironment.SetTempPath(assemblyTempFolder.Path);

            // Lets clear FileUtilities.TempFileDirectory in case it was already initialized by other code, so it picks up new TempPath
            FileUtilities.ClearTempFileDirectory();

            _testEnvironment.CreateFile(
                transientTestFolder: assemblyTempFolder,
                fileName: "MSBuild_Tests.txt",
                contents: $"Temporary test folder for tests from {AppContext.BaseDirectory}");

            // Ensure that we stop looking for a D.B.rsp at the root of the test temp
            _testEnvironment.CreateFile(
                transientTestFolder: assemblyTempFolder,
                fileName: "Directory.Build.rsp",
                contents: string.Empty);

            _testEnvironment.CreateFile(
                transientTestFolder: assemblyTempFolder,
                fileName: "Directory.Build.props",
                contents: "<Project />");

            _testEnvironment.CreateFile(
                transientTestFolder: assemblyTempFolder,
                fileName: "Directory.Build.targets",
                contents: "<Project />");
        }
 
        public void Dispose()
        {
            if (!_disposed)
            {
                _testEnvironment.Dispose();

                _disposed = true;
            }
        }
    }
}
