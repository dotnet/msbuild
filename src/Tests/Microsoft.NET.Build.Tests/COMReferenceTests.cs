// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class COMReferenceTests : SdkTest
    {
        public COMReferenceTests(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyTheory()]
        [InlineData(true)]
        [InlineData(false)]
        public void COMReferenceBuildsAndRuns(bool embedInteropTypes)
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;

            var testProject = new TestProject
            {
                Name = "UseComReferences",
                TargetFrameworks = targetFramework,
                IsExe = true,
                SourceFiles =
                    {
                        ["Program.cs"] = @"
                            class Program
                            {
                                static void Main(string[] args)
                                {
                                    System.Console.WriteLine(typeof(VSLangProj.VSProject));
                                }
                            }
                        ",
                }
            };

            var reference = new XElement("ItemGroup",
                new XElement("COMReference",
                    new XAttribute("Include", "VSLangProj.dll"),
                    new XElement("Guid", "49a1950e-3e35-4595-8cb9-920c64c44d67"),
                    new XElement("VersionMajor", "7"),
                    new XElement("VersionMinor", "0"),
                    new XElement("WrapperTool", "tlbimp"),
                    new XElement("Lcid", "0"),
                    new XElement("Isolated", "false"),
                    new XElement("EmbedInteropTypes", embedInteropTypes)));

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: embedInteropTypes.ToString())
                .WithProjectChanges(doc => doc.Root.Add(reference));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();
            
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var runCommand = new RunExeCommand(Log, outputDirectory.File("UseComReferences.exe").FullName);
            runCommand.Execute().Should().Pass();
        }

        [FullMSBuildOnlyFact]
        public void COMReferenceProperlyPublish()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;

            var testProject = new TestProject
            {
                Name = "MultiComReference",
                TargetFrameworks = targetFramework,
                IsExe = true,
                SourceFiles =
                    {
                        ["Program.cs"] = @"
                            class Program
                            {
                                static void Main(string[] args)
                                {
                                }
                            }
                        "
                }
            };

            var vslangProj70ComRef = "VSLangProj.dll";
            var reference1 = new XElement("ItemGroup",
                new XElement("COMReference",
                    new XAttribute("Include", vslangProj70ComRef),
                    new XElement("Guid", "49a1950e-3e35-4595-8cb9-920c64c44d67"),
                    new XElement("VersionMajor", "7"),
                    new XElement("VersionMinor", "0"),
                    new XElement("WrapperTool", "tlbimp"),
                    new XElement("Lcid", "0"),
                    new XElement("Isolated", "false"),
                    new XElement("EmbedInteropTypes", "false")));

            var vslangProj80ComRef = "VSLangProj80.dll";
            var reference2 = new XElement("ItemGroup",
                new XElement("COMReference",
                    new XAttribute("Include", vslangProj80ComRef),
                    new XElement("Guid", "307953c0-7973-490a-a4a7-25999e023be8"),
                    new XElement("VersionMajor", "8"),
                    new XElement("VersionMinor", "0"),
                    new XElement("WrapperTool", "tlbimp"),
                    new XElement("Lcid", "0"),
                    new XElement("Isolated", "false"),
                    new XElement("EmbedInteropTypes", "false")));

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject)
                .WithProjectChanges(doc => doc.Root.Add(new[] { reference1, reference2 }));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            // COM References by default adds the 'Interop.' prefix.
            Assert.True(outputDirectory.File($"Interop.{vslangProj70ComRef}").Exists);
            Assert.True(outputDirectory.File($"Interop.{vslangProj80ComRef}").Exists);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            outputDirectory = publishCommand.GetOutputDirectory(targetFramework);

            // COM References by default adds the 'Interop.' prefix.
            Assert.True(outputDirectory.File($"Interop.{vslangProj70ComRef}").Exists);
            Assert.True(outputDirectory.File($"Interop.{vslangProj80ComRef}").Exists);
        }
    }
}
