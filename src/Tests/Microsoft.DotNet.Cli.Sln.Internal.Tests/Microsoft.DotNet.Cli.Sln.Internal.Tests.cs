// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Sln.Internal.Tests
{
    public class GivenAnSlnFile : SdkTest
    {
        private const string SolutionModified = @"
Microsoft Visual Studio Solution File, Format Version 14.00
# Visual Studio 16
VisualStudioVersion = 16.0.26006.2
MinimumVisualStudioVersion = 11.0.40219.1
Project(""{7072A694-548F-4CAE-A58F-12D257D5F486}"") = ""AppModified"", ""AppModified\AppModified.csproj"", ""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = TRUE
	EndGlobalSection
EndGlobal
";

        private const string SolutionWithAppAndLibProjects = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{13B669BE-BB05-4DDF-9536-439F39A36129}"") = ""Lib"", ""..\Lib\Lib.csproj"", ""{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Debug|x64.ActiveCfg = Debug|x64
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Debug|x64.Build.0 = Debug|x64
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Debug|x86.ActiveCfg = Debug|x86
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Debug|x86.Build.0 = Debug|x86
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Release|Any CPU.Build.0 = Release|Any CPU
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Release|x64.ActiveCfg = Release|x64
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Release|x64.Build.0 = Release|x64
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Release|x86.ActiveCfg = Release|x86
		{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        public GivenAnSlnFile(ITestOutputHelper log) : base(log)
        {
        }

        private string CreateFile([CallerMemberName] string callerName = null, string identifier = null)
        {
            var folder = _testAssetsManager.CreateTestDirectory(testName: callerName + identifier);
            var filename = Path.Combine(folder.Path, Guid.NewGuid().ToString() + ".tmp");
            using (new FileStream(filename, FileMode.CreateNew)) { }
            return filename;
        }


        [Fact]
        public void WhenGivenAValidSlnFileItReadsAndVerifiesContents()
        {
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionWithAppAndLibProjects);

            SlnFile slnFile = SlnFile.Read(tmpFile);

            Console.WriteLine(new
            {
                slnFile_FormatVersion = slnFile.FormatVersion,
                slnFile_ProductDescription = slnFile.ProductDescription,
                slnFile_VisualStudioVersion = slnFile.VisualStudioVersion,
                slnFile_MinimumVisualStudioVersion = slnFile.MinimumVisualStudioVersion,
                slnFile_BaseDirectory = slnFile.BaseDirectory,
                slnFile_FullPath = slnFile.FullPath,
                tmpFilePath = tmpFile
            }.ToString());

            slnFile.FormatVersion.Should().Be("12.00");
            slnFile.ProductDescription.Should().Be("Visual Studio 15");
            slnFile.VisualStudioVersion.Should().Be("15.0.26006.2");
            slnFile.MinimumVisualStudioVersion.Should().Be("10.0.40219.1");
            slnFile.BaseDirectory.Should().Be(Path.GetDirectoryName(tmpFile));
            slnFile.FullPath.Should().Be(Path.GetFullPath(tmpFile));

            slnFile.Projects.Count.Should().Be(2);
            var project = slnFile.Projects[0];
            project.Id.Should().Be("{7072A694-548F-4CAE-A58F-12D257D5F486}");
            project.TypeGuid.Should().Be("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}");
            project.Name.Should().Be("App");
            project.FilePath.Should().Be(Path.Combine("App", "App.csproj"));
            project = slnFile.Projects[1];
            project.Id.Should().Be("{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}");
            project.TypeGuid.Should().Be("{13B669BE-BB05-4DDF-9536-439F39A36129}");
            project.Name.Should().Be("Lib");
            project.FilePath.Should().Be(Path.Combine("..", "Lib", "Lib.csproj"));

            slnFile.SolutionConfigurationsSection.Count.Should().Be(6);
            slnFile.SolutionConfigurationsSection
                .GetValue("Debug|Any CPU", string.Empty)
                .Should().Be("Debug|Any CPU");
            slnFile.SolutionConfigurationsSection
                .GetValue("Debug|x64", string.Empty)
                .Should().Be("Debug|x64");
            slnFile.SolutionConfigurationsSection
                .GetValue("Debug|x86", string.Empty)
                .Should().Be("Debug|x86");
            slnFile.SolutionConfigurationsSection
                .GetValue("Release|Any CPU", string.Empty)
                .Should().Be("Release|Any CPU");
            slnFile.SolutionConfigurationsSection
                .GetValue("Release|x64", string.Empty)
                .Should().Be("Release|x64");
            slnFile.SolutionConfigurationsSection
                .GetValue("Release|x86", string.Empty)
                .Should().Be("Release|x86");

            slnFile.ProjectConfigurationsSection.Count.Should().Be(2);
            var projectConfigSection = slnFile
                .ProjectConfigurationsSection
                .GetPropertySet("{7072A694-548F-4CAE-A58F-12D257D5F486}");
            projectConfigSection.Count.Should().Be(12);
            projectConfigSection
                .GetValue("Debug|Any CPU.ActiveCfg", string.Empty)
                .Should().Be("Debug|Any CPU");
            projectConfigSection
                .GetValue("Debug|Any CPU.Build.0", string.Empty)
                .Should().Be("Debug|Any CPU");
            projectConfigSection
                .GetValue("Debug|x64.ActiveCfg", string.Empty)
                .Should().Be("Debug|x64");
            projectConfigSection
                .GetValue("Debug|x64.Build.0", string.Empty)
                .Should().Be("Debug|x64");
            projectConfigSection
                .GetValue("Debug|x86.ActiveCfg", string.Empty)
                .Should().Be("Debug|x86");
            projectConfigSection
                .GetValue("Debug|x86.Build.0", string.Empty)
                .Should().Be("Debug|x86");
            projectConfigSection
                .GetValue("Release|Any CPU.ActiveCfg", string.Empty)
                .Should().Be("Release|Any CPU");
            projectConfigSection
                .GetValue("Release|Any CPU.Build.0", string.Empty)
                .Should().Be("Release|Any CPU");
            projectConfigSection
                .GetValue("Release|x64.ActiveCfg", string.Empty)
                .Should().Be("Release|x64");
            projectConfigSection
                .GetValue("Release|x64.Build.0", string.Empty)
                .Should().Be("Release|x64");
            projectConfigSection
                .GetValue("Release|x86.ActiveCfg", string.Empty)
                .Should().Be("Release|x86");
            projectConfigSection
                .GetValue("Release|x86.Build.0", string.Empty)
                .Should().Be("Release|x86");
            projectConfigSection = slnFile
                .ProjectConfigurationsSection
                .GetPropertySet("{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}");
            projectConfigSection.Count.Should().Be(12);
            projectConfigSection
                .GetValue("Debug|Any CPU.ActiveCfg", string.Empty)
                .Should().Be("Debug|Any CPU");
            projectConfigSection
                .GetValue("Debug|Any CPU.Build.0", string.Empty)
                .Should().Be("Debug|Any CPU");
            projectConfigSection
                .GetValue("Debug|x64.ActiveCfg", string.Empty)
                .Should().Be("Debug|x64");
            projectConfigSection
                .GetValue("Debug|x64.Build.0", string.Empty)
                .Should().Be("Debug|x64");
            projectConfigSection
                .GetValue("Debug|x86.ActiveCfg", string.Empty)
                .Should().Be("Debug|x86");
            projectConfigSection
                .GetValue("Debug|x86.Build.0", string.Empty)
                .Should().Be("Debug|x86");
            projectConfigSection
                .GetValue("Release|Any CPU.ActiveCfg", string.Empty)
                .Should().Be("Release|Any CPU");
            projectConfigSection
                .GetValue("Release|Any CPU.Build.0", string.Empty)
                .Should().Be("Release|Any CPU");
            projectConfigSection
                .GetValue("Release|x64.ActiveCfg", string.Empty)
                .Should().Be("Release|x64");
            projectConfigSection
                .GetValue("Release|x64.Build.0", string.Empty)
                .Should().Be("Release|x64");
            projectConfigSection
                .GetValue("Release|x86.ActiveCfg", string.Empty)
                .Should().Be("Release|x86");
            projectConfigSection
                .GetValue("Release|x86.Build.0", string.Empty)
                .Should().Be("Release|x86");

            slnFile.Sections.Count.Should().Be(3);
            var solutionPropertiesSection = slnFile.Sections.GetSection("SolutionProperties");
            solutionPropertiesSection.Properties.Count.Should().Be(1);
            solutionPropertiesSection.Properties
                .GetValue("HideSolutionNode", string.Empty)
                .Should().Be("FALSE");
        }

        [Fact]
        public void WhenGivenAValidReadOnlySlnFileItReadsContentsWithNoException()
        {
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionWithAppAndLibProjects);
            var attr = File.GetAttributes(tmpFile);
            attr = attr | FileAttributes.ReadOnly;
            File.SetAttributes(tmpFile, attr);

            Action act = () => SlnFile.Read(tmpFile);
            act.Should().NotThrow("Because readonly file is not being modified.");
        }

        [Fact]
        public void WhenGivenAValidSlnFileItModifiesSavesAndVerifiesContents()
        {
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionWithAppAndLibProjects);

            SlnFile slnFile = SlnFile.Read(tmpFile);

            slnFile.FormatVersion = "14.00";
            slnFile.ProductDescription = "Visual Studio 16";
            slnFile.VisualStudioVersion = "16.0.26006.2";
            slnFile.MinimumVisualStudioVersion = "11.0.40219.1";

            slnFile.Projects.Count.Should().Be(2);
            var project = slnFile.Projects[0];
            project.Id = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
            project.TypeGuid = "{7072A694-548F-4CAE-A58F-12D257D5F486}";
            project.Name = "AppModified";
            project.FilePath = Path.Combine("AppModified", "AppModified.csproj");
            slnFile.Projects.Remove(slnFile.Projects[1]);

            slnFile.SolutionConfigurationsSection.Count.Should().Be(6);
            slnFile.SolutionConfigurationsSection.Remove("Release|Any CPU");
            slnFile.SolutionConfigurationsSection.Remove("Release|x64");
            slnFile.SolutionConfigurationsSection.Remove("Release|x86");

            slnFile.ProjectConfigurationsSection.Count.Should().Be(2);
            var projectConfigSection = slnFile
                .ProjectConfigurationsSection
                .GetPropertySet("{21D9159F-60E6-4F65-BC6B-D01B71B15FFC}");
            slnFile.ProjectConfigurationsSection.Remove(projectConfigSection);

            slnFile.Sections.Count.Should().Be(3);
            var solutionPropertiesSection = slnFile.Sections.GetSection("SolutionProperties");
            solutionPropertiesSection.Properties.Count.Should().Be(1);
            solutionPropertiesSection.Properties.SetValue("HideSolutionNode", "TRUE");

            slnFile.Write();

            File.ReadAllText(tmpFile)
                .Should().Be(SolutionModified);
        }

        [Theory]
        [InlineData("Microsoft Visual Studio Solution File, Format Version ", 1)]
        [InlineData("First Line\nMicrosoft Visual Studio Solution File, Format Version ", 2)]
        [InlineData("First Line\nMicrosoft Visual Studio Solution File, Format Version \nThird Line", 2)]
        [InlineData("First Line\nSecondLine\nMicrosoft Visual Studio Solution File, Format Version \nFourth Line", 3)]
        public void WhenGivenASolutionWithMissingHeaderVersionItThrows(string fileContents, int lineNum)
        {
            var tmpFile = CreateFile(identifier: fileContents.GetHashCode().ToString());
            File.WriteAllText(tmpFile, fileContents);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(lineNum, LocalizableStrings.FileHeaderMissingVersionError));
        }

        [Theory]
        [InlineData("Invalid Solution")]
        [InlineData("Invalid Solution\nSpanning Multiple Lines")]
        [InlineData("Microsoft Visual\nStudio Solution File,\nFormat Version ")]
        public void WhenGivenASolutionWithMissingHeaderItThrows(string fileContents)
        {
            var tmpFile = CreateFile(identifier: fileContents.GetHashCode().ToString());
            File.WriteAllText(tmpFile, fileContents);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(LocalizableStrings.FileHeaderMissingError);
        }

        [Fact]
        public void WhenGivenASolutionWithMultipleGlobalSectionsItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Global
EndGlobal
Global
EndGlobal
";
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(5, LocalizableStrings.GlobalSectionMoreThanOnceError));
        }

        [Fact]
        public void WhenGivenASolutionWithGlobalSectionNotClosedItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Global
";
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(3, LocalizableStrings.GlobalSectionNotClosedError));
        }

        [Fact]
        public void WhenGivenASolutionWithProjectSectionNotClosedItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
";
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(3, LocalizableStrings.ProjectSectionNotClosedError));
        }

        [Fact]
        public void WhenGivenASolutionWithInvalidProjectSectionItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
";

            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(3, LocalizableStrings.ProjectParsingErrorFormatString, "(", 0));
        }

        [Fact]
        public void WhenGivenASolutionWithInvalidSectionTypeItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Global
	GlobalSection(SolutionConfigurationPlatforms) = thisIsUnknown
	EndGlobalSection
EndGlobal
";
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(4, LocalizableStrings.InvalidSectionTypeError, "thisIsUnknown"));
        }

        [Fact]
        public void WhenGivenASolutionWithMissingSectionIdTypeItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Global
	GlobalSection = preSolution
	EndGlobalSection
EndGlobal
";
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(4, LocalizableStrings.SectionIdMissingError));
        }

        [Fact]
        public void WhenGivenASolutionWithSectionNotClosedItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
EndGlobal
";
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                SlnFile.Read(tmpFile);
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(6, LocalizableStrings.ClosingSectionTagNotFoundError));
        }

        [Fact]
        public void WhenGivenASolutionWithInvalidPropertySetItThrows()
        {
            const string SolutionFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{7072A694-548F-4CAE-A58F-12D257D5F486}"") = ""AppModified"", ""AppModified\AppModified.csproj"", ""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}""
EndProject
Global
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486} Debug|Any CPU ActiveCfg = Debug|Any CPU
	EndGlobalSection
EndGlobal
";
            var tmpFile = CreateFile();
            File.WriteAllText(tmpFile, SolutionFile);

            Action action = () =>
            {
                var slnFile = SlnFile.Read(tmpFile);
                if (slnFile.ProjectConfigurationsSection.Count == 0)
                {
                    // Need to force loading of nested property sets
                }
            };

            action.Should().Throw<InvalidSolutionFormatException>()
                .WithMessage(FormatError(7, LocalizableStrings.InvalidPropertySetFormatString, "."));
        }

        private static string FormatError(int line, string format, params object[] args)
        {
            return string.Format(
                LocalizableStrings.ErrorMessageFormatString,
                line,
                string.Format(format, args));
        }
    }
}
