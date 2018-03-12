
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Build.UnitTests;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

//  Register test framework for assembly fixture
[assembly: TestFramework("Xunit.NetCore.Extensions.XunitTestFrameworkWithAssemblyFixture", "Xunit.NetCore.Extensions")]

[assembly: AssemblyFixture(typeof(MSBuildTestAssemblyFixture))]

//  Wrap a TestEnvironment around each test method and class so if invariants have changed we will know where
[assembly: AssemblyFixture(typeof(MSBuildTestEnvironmentFixture), LifetimeScope = AssemblyFixtureAttribute.Scope.Class)]
[assembly: AssemblyFixture(typeof(MSBuildTestEnvironmentFixture), LifetimeScope = AssemblyFixtureAttribute.Scope.Method)]

public class MSBuildTestAssemblyFixture : IDisposable
{
    bool _disposed;
    private TestEnvironment _testEnvironment;

    public MSBuildTestAssemblyFixture()
    {
        //  Set field to indicate tests are running in the TestInfo class in Microsoft.Build.Framework.
        //  See the comments on the TestInfo class for an explanation of why it works this way.
        var frameworkAssembly = typeof(Microsoft.Build.Framework.ITask).Assembly;
        var testInfoType = frameworkAssembly.GetType("Microsoft.Build.Framework.TestInfo");
        var runningTestsField = testInfoType.GetField("s_runningTests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        runningTestsField.SetValue(null, true);

        _testEnvironment = TestEnvironment.Create();

        //  Reset the VisualStudioVersion environment variable.  This will be set if tests are run from a VS command prompt.  However,
        //  if the environment variable is set, it will interfere with tests which set the SubToolsetVersion
        //  (VerifySubToolsetVersionSetByConstructorOverridable), as the environment variable would take precedence.
        _testEnvironment.SetEnvironmentVariable("VisualStudioVersion", string.Empty);

        SetDotnetHostPath(_testEnvironment);

        //  Use a project-specific temporary path
        //  This is so multiple test projects can be run in parallel without sharing the same temp directory
        string newTempPath = Path.Combine(AppContext.BaseDirectory, "TestTemp");
        _testEnvironment.CreateFolder(newTempPath);

        _testEnvironment.SetTempPath(newTempPath);

        // Most places that use this variable fallback to temp, but setting it here makes it explicit.
        _testEnvironment.SetEnvironmentVariable("MSBUILDDEBUGPATH", newTempPath);
    }

    /// <summary>
    /// Find correct version of "dotnet", and set DOTNET_HOST_PATH so that the Roslyn tasks will use the right host
    /// </summary>
    /// <param name="testEnvironment"></param>
    private static void SetDotnetHostPath(TestEnvironment testEnvironment)
    {
        var currentFolder = System.AppContext.BaseDirectory;

        while (currentFolder != null)
        {
            string potentialVersionsPropsPath = Path.Combine(currentFolder, "build", "Versions.props");
            if (File.Exists(potentialVersionsPropsPath))
            {
                var doc = XDocument.Load(potentialVersionsPropsPath);
                var ns = doc.Root.Name.Namespace;
                var cliVersionElement = doc.Root.Elements(ns + "PropertyGroup").Elements(ns + "DotNetCliVersion").FirstOrDefault();
                if (cliVersionElement != null)
                {
                    string cliVersion = cliVersionElement.Value;
                    string dotnetPath = Path.Combine(currentFolder, "artifacts", ".dotnet", cliVersion, "dotnet");

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        dotnetPath += ".exe";
                    }

                    testEnvironment.SetEnvironmentVariable("DOTNET_HOST_PATH", dotnetPath);
                }

                break;
            }

            currentFolder = Directory.GetParent(currentFolder)?.FullName;
        }
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

public class MSBuildTestEnvironmentFixture : IDisposable
{
    bool _disposed;
    private TestEnvironment _testEnvironment;

    public MSBuildTestEnvironmentFixture()
    {
        _testEnvironment = TestEnvironment.Create();
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
