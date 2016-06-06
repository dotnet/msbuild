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
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectWithResource")
                .WithLockFiles()
                .WithBuildArtifacts();

            var rid = DependencyContext.Default.Target.Runtime;

            var context = ProjectContext.Create(testInstance.TestRoot, NuGetFramework.Parse("netcoreapp1.0"), new[] { rid });
            var loadContext = context.CreateLoadContext(rid, Constants.DefaultConfiguration);

            // Load the project assembly
            var asm = loadContext.LoadFromAssemblyName(new AssemblyName("TestProjectWithResource"));

            // Call Program.GetMessage() and assert the output
            var message = (string)asm.GetType("TestProjectWithCultureSpecificResource").GetRuntimeMethod("GetMessage", Type.EmptyTypes).Invoke(null, new object[0]);
            Assert.Equal("Hello World!" + Environment.NewLine + "Bonjour!", message);
        }
    }
}
