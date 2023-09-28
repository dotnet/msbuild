// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests
{
    public class AspNetCoreOnFullFramework : SdkTest
    {
        public AspNetCoreOnFullFramework(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyTheory]
        [InlineData("1.1.2")]
        [InlineData("2.0.4")]
        public void It_discovers_assembly_parts(string aspnetVersion)
        {
            var testProject = new TestProject()
            {
                Name = "AssemblyPartDiscovery",
                TargetFrameworks = "net462",
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = @"
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyModel;
using System.IO;
using System.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var parts = DefaultAssemblyPartDiscoveryProvider.DiscoverAssemblyParts(""" + testProject.Name + @""");
        foreach (var item in parts)
        {
            System.Console.WriteLine(item.Name);
        }
    }
}";

            TestProject referencedProjectWithPart = new()
            {
                Name = "ReferencedProjectWithPart",
                TargetFrameworks = "net462",
                IsExe = false
            };


            referencedProjectWithPart.References.Add("System.ServiceModel");

            referencedProjectWithPart.SourceFiles["Class1.cs"] = @"
class Class1
{
    public string X => typeof(System.ServiceModel.AddressFilterMode).ToString();
}";

            TestProject referencedProjectWithMvc = new()
            {
                Name = "ReferencedProjectWithMVC",
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                TargetFrameworks = "net462",
                IsExe = false
            };

            referencedProjectWithMvc.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Mvc", aspnetVersion));

            testProject.ReferencedProjects.Add(referencedProjectWithPart);
            testProject.ReferencedProjects.Add(referencedProjectWithMvc);

            var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject, identifier: aspnetVersion);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            string outputPath = buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName;

            string exePath = Path.Combine(outputPath, testProject.Name + ".exe");

            var toolCommandSpec = new SdkCommandSpec()
            {
                FileName = exePath
            };
            TestContext.Current.AddTestEnvironmentVariables(toolCommandSpec.Environment);

            ICommand toolCommand = toolCommandSpec.ToCommand().CaptureStdOut();

            var toolResult = toolCommand.Execute();

            toolResult.Should().Pass();
        }
    }
}
