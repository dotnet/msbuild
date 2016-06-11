using System;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Loader.Tests
{
    public class ProjectLoadContextTest : TestBase
    {
        [Fact]
        public void LoadContextCanLoadProjectOutput()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectWithCultureSpecificResource")
                .WithLockFiles()
                .WithBuildArtifacts();

            var runtimeIdentifier = DependencyContext.Default.Target.Runtime;

            var context = ProjectContext.Create(testInstance.TestRoot, NuGetFramework.Parse("netcoreapp1.0"), new[] { runtimeIdentifier });
            var loadContext = context.CreateLoadContext(runtimeIdentifier, Constants.DefaultConfiguration);

            // Load the project assembly
            var assembly = loadContext.LoadFromAssemblyName(new AssemblyName("TestProjectWithCultureSpecificResource"));

            // Call Program.GetMessage() and assert the output
            var type = assembly.GetType("TestProjectWithCultureSpecificResource.Program");
            var message = (string)type.GetRuntimeMethod("GetMessage", Type.EmptyTypes).Invoke(null, new object[0]);
            Assert.Equal("Hello World!" + Environment.NewLine + "Bonjour!", message);
        }
    }
}
