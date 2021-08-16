// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class CommonTemplatesTests : IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _fixture;
        private readonly ITestOutputHelper _log;

        public CommonTemplatesTests(SharedHomeDirectory fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            _log = log;
        }

        [Theory]
        [InlineData("Console Application", "console")]
        [InlineData("Console Application", "console", "C#")]
        [InlineData("Console Application", "console", "F#")]
        [InlineData("Console Application", "console", "VB")]
        [InlineData("Console Application", "console", "C#", "net6.0")]
        [InlineData("Console Application", "console", "F#", "net6.0")]
        [InlineData("Console Application", "console", "VB", "net6.0")]
        [InlineData("Console Application", "console", "C#", "net5.0")]
        [InlineData("Console Application", "console", "F#", "net5.0")]
        [InlineData("Console Application", "console", "VB", "net5.0")]
        [InlineData("Console Application", "console", "C#", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "F#", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "VB", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "C#", "netcoreapp2.1")]
        [InlineData("Console Application", "console", "F#", "netcoreapp2.1")]
        [InlineData("Console Application", "console", "VB", "netcoreapp2.1")]

        [InlineData("Class Library", "classlib")]
        [InlineData("Class Library", "classlib", "C#")]
        [InlineData("Class Library", "classlib", "F#")]
        [InlineData("Class Library", "classlib", "VB")]
        [InlineData("Class Library", "classlib", "C#", "net6.0")]
        [InlineData("Class Library", "classlib", "F#", "net6.0")]
        [InlineData("Class Library", "classlib", "VB", "net6.0")]
        [InlineData("Class Library", "classlib", "C#", "net5.0")]
        [InlineData("Class Library", "classlib", "F#", "net5.0")]
        [InlineData("Class Library", "classlib", "VB", "net5.0")]
        [InlineData("Class Library", "classlib", "C#", "netcoreapp3.1")]
        [InlineData("Class Library", "classlib", "F#", "netcoreapp3.1")]
        [InlineData("Class Library", "classlib", "VB", "netcoreapp3.1")]
        [InlineData("Class Library", "classlib", "C#", "netcoreapp2.1")]
        [InlineData("Class Library", "classlib", "F#", "netcoreapp2.1")]
        [InlineData("Class Library", "classlib", "VB", "netcoreapp2.1")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.0")]

        public void AllCommonProjectsCreateRestoreAndBuild(string expectedTemplateName, string templateShortName, string? language = null, string? framework = null, string? langVersion = null)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string workingDirName = Path.GetFileName(workingDir);
            string extension = language switch
            {
                "F#" => "fsproj",
                "VB" => "vbproj",
                _ => "csproj"
            };
            string finalProjectName = Regex.Escape(Path.Combine(workingDir, $"{workingDirName}.{extension}"));
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //on OSX path in restore starts from /private for some reason
                finalProjectName = "/private" + finalProjectName;
            }
            Console.WriteLine($"Expected project location: {finalProjectName}");

            List<string> args = new List<string>() { templateShortName };
            if (!string.IsNullOrWhiteSpace(language))
            {
                args.Add("--language");
                args.Add(language);
            }
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                args.Add("--langVersion");
                args.Add(langVersion);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching(
$@"The template ""{expectedTemplateName}"" was created successfully\.

Processing post-creation actions\.\.\.
Running 'dotnet restore' on ({finalProjectName})\.\.\.
  Determining projects to restore\.\.\.
  Restored ({finalProjectName}) \(in \d{{1,3}} ms|\d(\.\d{{1,3}}){{0,1}} sec\)\.

Restore succeeded\.");

            new DotnetCommand(_log, "restore")
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }

        [Theory]
        [InlineData("Console Application", "console")]
        [InlineData("Console Application", "console", "C#")]
        [InlineData("Console Application", "console", "F#")]
        [InlineData("Console Application", "console", "VB")]
        [InlineData("Console Application", "console", "C#", "net6.0")]
        [InlineData("Console Application", "console", "F#", "net6.0")]
        [InlineData("Console Application", "console", "VB", "net6.0")]
        [InlineData("Console Application", "console", "C#", "net5.0")]
        [InlineData("Console Application", "console", "F#", "net5.0")]
        [InlineData("Console Application", "console", "VB", "net5.0")]
        [InlineData("Console Application", "console", "C#", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "F#", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "VB", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "C#", "netcoreapp2.1")]
        [InlineData("Console Application", "console", "F#", "netcoreapp2.1")]
        [InlineData("Console Application", "console", "VB", "netcoreapp2.1")]

        [InlineData("Class Library", "classlib")]
        [InlineData("Class Library", "classlib", "C#")]
        [InlineData("Class Library", "classlib", "F#")]
        [InlineData("Class Library", "classlib", "VB")]
        [InlineData("Class Library", "classlib", "C#", "net6.0")]
        [InlineData("Class Library", "classlib", "F#", "net6.0")]
        [InlineData("Class Library", "classlib", "VB", "net6.0")]
        [InlineData("Class Library", "classlib", "C#", "net5.0")]
        [InlineData("Class Library", "classlib", "F#", "net5.0")]
        [InlineData("Class Library", "classlib", "VB", "net5.0")]
        [InlineData("Class Library", "classlib", "C#", "netcoreapp3.1")]
        [InlineData("Class Library", "classlib", "F#", "netcoreapp3.1")]
        [InlineData("Class Library", "classlib", "VB", "netcoreapp3.1")]
        [InlineData("Class Library", "classlib", "C#", "netcoreapp2.1")]
        [InlineData("Class Library", "classlib", "F#", "netcoreapp2.1")]
        [InlineData("Class Library", "classlib", "VB", "netcoreapp2.1")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.0")]
        public void AllCommonProjectsCreate_NoRestore(string expectedTemplateName, string templateShortName, string? language = null, string? framework = null)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            List<string> args = new List<string>() { templateShortName, "--no-restore" };
            if (!string.IsNullOrWhiteSpace(language))
            {
                args.Add("--language");
                args.Add(language);
            }
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut($@"The template ""{expectedTemplateName}"" was created successfully.");

            Directory.Delete(workingDir, true);
        }

        [Theory]
        [InlineData("dotnet gitignore file", "gitignore")]
        [InlineData("global.json file", "globaljson")]
        [InlineData("NuGet Config", "nugetconfig")]
        [InlineData("Solution File", "sln")]
        [InlineData("Dotnet local tool manifest file", "tool-manifest")]
        [InlineData("Web Config", "webconfig")]
        public void AllCommonItemsCreate(string expectedTemplateName, string templateShortName)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, templateShortName)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut($@"The template ""{expectedTemplateName}"" was created successfully.");

            Directory.Delete(workingDir, true);
        }

        [Fact]
        public void EditorConfigTests()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "editorconfig")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut($@"The template ""EditorConfig file"" was created successfully.");

            string path = Path.Combine(workingDir, ".editorconfig");
            string editorConfigContent = File.ReadAllText(path);
            Assert.Contains("dotnet_naming_rule", editorConfigContent);
            Assert.Contains("dotnet_style_", editorConfigContent);
            Assert.Contains("dotnet_naming_symbols", editorConfigContent);
            File.Delete(path);

            new DotnetNewCommand(_log, "editorconfig", "--empty")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut($@"The template ""EditorConfig file"" was created successfully.");

            editorConfigContent = File.ReadAllText(path);
            Assert.DoesNotContain("dotnet_naming_rule", editorConfigContent);
            Assert.DoesNotContain("dotnet_style_", editorConfigContent);
            Assert.DoesNotContain("dotnet_naming_symbols", editorConfigContent);
            Assert.Contains("root = true", editorConfigContent);
            Directory.Delete(workingDir, true);
        }

        [Theory]
        [InlineData(
@"{
  ""sdk"": {
    ""version"": ""5.0.200""
  }
}",
            "globaljson",
            "--sdk-version",
            "5.0.200")]
        [InlineData(
@"{
  ""sdk"": {
    ""rollForward"": ""major"",
    ""version"": ""5.0.200""
  }
}",
            "globaljson",
            "--sdk-version",
            "5.0.200",
            "--roll-forward",
            "major")]
        public void GlobalJsonTests( string expectedContent, params string[] parameters)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, parameters)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut($@"The template ""global.json file"" was created successfully.");

            string globalJsonConent = File.ReadAllText(Path.Combine(workingDir, "global.json"));
            Assert.Equal(expectedContent.Replace("\r\n", "\n"), globalJsonConent.Replace("\r\n", "\n"));
            Directory.Delete(workingDir, true);
        }

        #region Project templates language features tests

        /// <summary>
        /// Creates all possible combinations for supported templates, language versions and frameworks.
        /// </summary>
        public static IEnumerable<object?[]> TopLevelProgramSupport_Data()
        {
            var templatesToTest = new[]
            {
                new { Name = "console",  Frameworks = new[] { null, "net6.0" } }
            };

            string[] unsupportedLanguageVersions = { "1", "ISO-1" };
            string?[] supportedLanguageVersions = { null, "ISO-2", "2", "3", "4", "5", "6", "7", "7.1", "7.2", "7.3", "8.0", "9.0", "10.0", "latest", "latestMajor", "default", "preview" };

            string?[] topLevelStatementSupport = { null, "9.0", "10.0", "latest", "latestMajor", "default", "preview" };

            foreach (var template in templatesToTest)
            {
                foreach (var langVersion in unsupportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[]
                        {
                            template.Name,
                            false, //dotnet build should fail
                            framework,
                            langVersion,
                            topLevelStatementSupport.Contains(langVersion)
                        };
                    }
                }
                foreach (var langVersion in supportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[]
                        {
                            template.Name,
                            true, //dotnet build should pass
                            framework,
                            langVersion,
                            topLevelStatementSupport.Contains(langVersion)
                        };
                    }
                }
            }
        }

        [Theory]
        //creates all possible combinations for supported templates, language versions and frameworks
        [MemberData(nameof(TopLevelProgramSupport_Data))]
        public void TopLevelProgramSupport(string name, bool buildPass, string? framework, string? langVersion, bool supportsFeature)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            List<string> args = new List<string>() { name, "-o", "MyProject" };
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                args.Add("--langVersion");
                args.Add(langVersion);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            var buildResult = new DotnetCommand(_log, "build", "MyProject")
                .WithWorkingDirectory(workingDir)
                .Execute();

            if (buildPass)
            {
                buildResult.Should().ExitWith(0).And.NotHaveStdErr();
            }
            else
            {
                buildResult.Should().Fail();
                return;
            }

            string programFileContent = File.ReadAllText(Path.Combine(workingDir, "MyProject", "Program.cs"));
            string unexpectedTopLevelContent =
@"namespace MyProject
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello, World!"");
        }
    }
}
";
            if (supportsFeature)
            {
                Assert.Contains("Console.WriteLine(\"Hello, World!\")", programFileContent);
                Assert.Contains("// See https://aka.ms/new-console-template for more information", programFileContent);
                Assert.DoesNotContain(unexpectedTopLevelContent, programFileContent);
            }
            else
            {
                Assert.DoesNotContain("// See https://aka.ms/new-console-template for more information", programFileContent);
                Assert.Contains(unexpectedTopLevelContent, programFileContent);
            }
        }

        /// <summary>
        /// Creates all possible combinations for supported templates, language versions and frameworks.
        /// </summary>
        public static IEnumerable<object?[]> NullableSupport_Data()
        {
            var templatesToTest = new[]
            {
                new { Template = "console",  Frameworks = new[] { null, "net6.0" } },
                new { Template = "classlib", Frameworks = new[] { null, "net6.0", "netstandard2.0", "netstandard2.1" } }
            };

            string[] unsupportedLanguageVersions = { "1", "ISO-1" };
            string?[] supportedLanguageVersions = { null, "ISO-2", "2", "3", "4", "5", "6", "7", "7.1", "7.2", "7.3", "8.0", "9.0", "10.0", "latest", "latestMajor", "default", "preview" };

            string?[] supportedInFrameworkByDefault = { null, "net6.0", "netstandard2.1" };
            string?[] supportedInLanguageVersion = { "8.0", "9.0", "10.0", "latest", "latestMajor", "default", "preview" };

            foreach (var template in templatesToTest)
            {
                foreach (var langVersion in unsupportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[]
                        {
                            template.Template,
                            false,  //dotnet build should fail
                            framework,
                            langVersion,
                            supportedInLanguageVersion.Contains(langVersion)
                                || langVersion == null && supportedInFrameworkByDefault.Contains(framework)
                        };
                    }
                }
                foreach (var langVersion in supportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[]
                        {
                            template.Template,
                            true,   //dotnet build should pass
                            framework,
                            langVersion,
                            supportedInLanguageVersion.Contains(langVersion)
                                || langVersion == null && supportedInFrameworkByDefault.Contains(framework)
                        };
                    }
                }
            }
        }

        [Theory]
        //creates all possible combinations for supported templates, language versions and frameworks
        [MemberData(nameof(NullableSupport_Data))]
        public void NullableSupport(string name, bool buildPass, string? framework, string? langVersion, bool supportsFeature)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            List<string> args = new List<string>() { name, "-o", "MyProject" };
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                args.Add("--langVersion");
                args.Add(langVersion);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            var buildResult = new DotnetCommand(_log, "build", "MyProject")
                .WithWorkingDirectory(workingDir)
                .Execute();

            if (buildPass)
            {
                buildResult.Should().ExitWith(0).And.NotHaveStdErr();
            }
            else
            {
                buildResult.Should().Fail();
                return;
            }

            XDocument projectXml = XDocument.Load(Path.Combine(workingDir, "MyProject", "MyProject.csproj"));
            XNamespace ns = projectXml.Root?.Name.Namespace ?? throw new Exception("Unexpected project file format");
            if (supportsFeature)
            {
                Assert.Equal("enable", projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + "Nullable")?.Value);
            }
            else
            {
                Assert.Null(projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + "Nullable"));
            }
        }

        /// <summary>
        /// Creates all possible combinations for supported templates, language versions and frameworks.
        /// </summary>
        public static IEnumerable<object?[]> ImplicitUsingsSupport_Data()
        {
            var templatesToTest = new[]
            {
                new { Template = "console",  Frameworks = new[] { null, "net6.0" } },
                new { Template = "classlib", Frameworks = new[] { null, "net6.0", "netstandard2.0", "netstandard2.1" } }
            };
            string[] unsupportedLanguageVersions = { "1", "ISO-1" };
            string?[] supportedLanguageVersions = { null, "ISO-2", "2", "3", "4", "5", "6", "7", "7.1", "7.2", "7.3", "8.0", "9.0", "10.0", "latest", "latestMajor", "default", "preview" };

            string?[] supportedInFramework = { null, "net6.0" };
            string?[] supportedInLangVersion = { null, "10.0", "latest", "latestMajor", "default", "preview" };

            foreach (var template in templatesToTest)
            {
                foreach (var langVersion in unsupportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[]
                        {
                            template.Template,
                            false,  //dotnet build should fail
                            framework,
                            langVersion,
                            supportedInLangVersion.Contains(langVersion) && supportedInFramework.Contains(framework)
                        };
                    }
                }
                foreach (var langVersion in supportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[]
                        {
                            template.Template,
                            true, //dotnet build should pass
                            framework,
                            langVersion,
                            supportedInLangVersion.Contains(langVersion) && supportedInFramework.Contains(framework)
                        };
                    }
                }
            }
        }

        [Theory]
        //creates all possible combinations for supported templates, language versions and frameworks
        [MemberData(nameof(ImplicitUsingsSupport_Data))]
        public void ImplicitUsingsSupport(string name, bool buildPass, string? framework, string? langVersion, bool supportsFeature)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            List<string> args = new List<string>() { name, "-o", "MyProject" };
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                args.Add("--langVersion");
                args.Add(langVersion);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            var buildResult = new DotnetCommand(_log, "build", "MyProject")
                .WithWorkingDirectory(workingDir)
                .Execute();

            if (buildPass)
            {
                buildResult.Should().ExitWith(0).And.NotHaveStdErr();
            }
            else
            {
                buildResult.Should().Fail();
                return;
            }
            string codeFileName = name == "console" ? "Program.cs" : "Class1.cs";
            string programFileContent = File.ReadAllText(Path.Combine(workingDir, "MyProject", codeFileName));
            XDocument projectXml = XDocument.Load(Path.Combine(workingDir, "MyProject", "MyProject.csproj"));
            XNamespace ns = projectXml.Root?.Name.Namespace ?? throw new Exception("Unexpected project file format");
            if (supportsFeature)
            {
                Assert.DoesNotContain("using System;", programFileContent);
                Assert.Equal("enable", projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + "ImplicitUsings")?.Value);
            }
            else
            {
                Assert.Contains("using System;", programFileContent);
                Assert.Null(projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + "ImplicitUsings"));
            }
        }

        public static IEnumerable<object?[]> FileScopedNamespacesSupport_Data()
        {
            var templatesToTest = new[]
            {
                new { Template = "classlib", Frameworks = new[] { null, "net6.0", "netstandard2.0", "netstandard2.1" } }
            };
            string[] unsupportedLanguageVersions = { "1", "ISO-1" };
            string?[] supportedLanguageVersions = { null, "ISO-2", "2", "3", "4", "5", "6", "7", "7.1", "7.2", "7.3", "8.0", "9.0", "10.0", "latest", "latestMajor", "default", "preview" };

            string?[] supportedFrameworks = { null, "net6.0" };
            string?[] fileScopedNamespacesSupportedLanguages = { "10.0", "latest", "latestMajor", "default", "preview" };

            foreach (var template in templatesToTest)
            {
                foreach (var langVersion in unsupportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[] { template.Template, false, framework, langVersion, fileScopedNamespacesSupportedLanguages.Contains(langVersion) || langVersion == null && supportedFrameworks.Contains(framework) };
                    }
                }
                foreach (var langVersion in supportedLanguageVersions)
                {
                    foreach (var framework in template.Frameworks)
                    {
                        yield return new object?[] { template.Template, true, framework, langVersion, fileScopedNamespacesSupportedLanguages.Contains(langVersion) || langVersion == null && supportedFrameworks.Contains(framework) };
                    }
                }
            }
        }

        [Theory]
        //creates all possible combinations for supported templates, language versions and frameworks 
        [MemberData(nameof(FileScopedNamespacesSupport_Data))]
        public void FileScopedNamespacesSupport(string name, bool pass, string? framework, string? langVersion, bool supportsFeature)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();

            List<string> args = new List<string>() { name, "-o", "MyProject" };
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                args.Add("--langVersion");
                args.Add(langVersion);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            var buildResult = new DotnetCommand(_log, "build", "MyProject")
                .WithWorkingDirectory(workingDir)
                .Execute();

            if (pass)
            {
                buildResult.Should().ExitWith(0).And.NotHaveStdErr();
            }
            else
            {
                buildResult.Should().Fail();
                return;
            }
            string codeFileName = name == "console" ? "Program.cs" : "Class1.cs";
            string programFileContent = File.ReadAllText(Path.Combine(workingDir, "MyProject", codeFileName));

            string supportedContent =
@"namespace MyProject;
public class Class1
{

}";
            string unsupportedContent =
@"namespace MyProject
{
    public class Class1
    {

    }
}";

            if (supportsFeature)
            {
                Assert.DoesNotContain(unsupportedContent, programFileContent);
                Assert.Contains(supportedContent, programFileContent);
            }
            else
            {
                Assert.DoesNotContain(supportedContent, programFileContent);
                Assert.Contains(unsupportedContent, programFileContent);
            }
        }
        #endregion

        [Theory]
        [InlineData("Nullable", "enable", "Console Application", "console", null, null)]
        [InlineData("CheckForOverflowUnderflow", null, "Console Application", "console", null, null)]
        [InlineData("LangVersion", null, "Console Application", "console", null, null)]
        [InlineData("TargetFramework", "net6.0", "Console Application", "console", null, null)]
        [InlineData("Nullable", null, "Console Application", "console", null, "net5.0")]
        [InlineData("Nullable", null, "Console Application", "console", null, "netcoreapp3.1")]
        [InlineData("Nullable", null, "Console Application", "console", null, "netcoreapp2.1")]

        [InlineData("Nullable", null, "Console Application", "console", "F#", null)]
        [InlineData("CheckForOverflowUnderflow", null, "Console Application", "console", "F#", null)]
        [InlineData("LangVersion", null, "Console Application", "console", "F#", null)]
        [InlineData("TargetFramework", "net6.0", "Console Application", "console", "F#", null)]
        [InlineData("GenerateDocumentationFile", null, "Console Application", "console", "F#", null)]

        [InlineData("Nullable", null, "Console Application", "console", "VB", null)]
        [InlineData("CheckForOverflowUnderflow", null, "Console Application", "console", "VB", null)]
        [InlineData("LangVersion", null, "Console Application", "console", "VB", null)]
        [InlineData("TargetFramework", "net6.0", "Console Application", "console", "VB", null)]

        [InlineData("Nullable", "enable", "Class Library", "classlib", null, null)]
        [InlineData("CheckForOverflowUnderflow", null, "Class Library", "classlib", null, null)]
        [InlineData("LangVersion", null, "Class Library", "classlib", null, null)]
        [InlineData("TargetFramework", "net6.0", "Class Library", "classlib", null, null)]
        [InlineData("Nullable", null, "Class Library", "classlib", null, "netstandard2.0")]
        [InlineData("Nullable", "enable", "Class Library", "classlib", null, "netstandard2.1")]

        [InlineData("Nullable", null, "Class Library", "classlib", "F#", null)]
        [InlineData("CheckForOverflowUnderflow", null, "Class Library", "classlib", "F#", null)]
        [InlineData("LangVersion", null, "Class Library", "classlib", "F#", null)]
        [InlineData("TargetFramework", "net6.0", "Class Library", "classlib", "F#", null)]
        [InlineData("GenerateDocumentationFile", "true", "Class Library", "classlib", "F#", null)]
        [InlineData("Nullable", null, "Class Library", "classlib", "F#", "netstandard2.0")]

        [InlineData("Nullable", null, "Class Library", "classlib", "VB", null)]
        [InlineData("CheckForOverflowUnderflow", null, "Class Library", "classlib", "VB", null)]
        [InlineData("LangVersion", null, "Class Library", "classlib", "VB", null)]
        [InlineData("TargetFramework", "net6.0", "Class Library", "classlib", "VB", null)]
        [InlineData("Nullable", null, "Class Library", "classlib", "VB", "netstandard2.0")]

        public void SetPropertiesByDefault(string propertyName, string? propertyValue, string expectedTemplateName, string templateShortName, string? language, string? framework)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            List<string> args = new List<string>() { templateShortName, "--no-restore" };
            if (!string.IsNullOrWhiteSpace(language))
            {
                args.Add("--language");
                args.Add(language);
            }
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut($@"The template ""{expectedTemplateName}"" was created successfully.");

            string expectedExtension = language switch
            {
                "C#" => "*.csproj",
                "F#" => "*.fsproj",
                "VB" => "*.vbproj",
                _ => "*.csproj"
            };
            string projectFile = Directory.GetFiles(workingDir, expectedExtension).Single();
            XDocument projectXml = XDocument.Load(projectFile);
            XNamespace ns = projectXml.Root?.Name.Namespace ?? throw new Exception("Unexpected project file format");
            if (propertyValue != null)
            {
                Assert.Equal(propertyValue, projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + propertyName)?.Value);
            }
            else
            {
                Assert.Null(projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + propertyName));
            }
            Directory.Delete(workingDir, true);
        }

        [Theory]
        //language version
        [InlineData("LangVersion", "9.0", "--langVersion", "9.0", "Console Application", "console", null, null)]
        [InlineData("LangVersion", "9.0", "--langVersion", "9.0", "Console Application", "console", "VB", null)]
        [InlineData("LangVersion", "9.0", "--langVersion", "9.0", "Class Library", "classlib", null, null)]
        [InlineData("LangVersion", "9.0", "--langVersion", "9.0", "Class Library", "classlib", "VB", null)]

        //framework
        [InlineData("TargetFramework", "net5.0", "--framework", "net5.0", "Console Application", "console", null, null)]
        [InlineData("TargetFramework", "net5.0", "--framework", "net5.0", "Console Application", "console", "VB", null)]
        [InlineData("TargetFramework", "net5.0", "--framework", "net5.0", "Console Application", "console", "F#", null)]
        [InlineData("TargetFramework", "net5.0", "--framework", "net5.0", "Class Library", "classlib", null, null)]
        [InlineData("TargetFramework", "net5.0", "--framework", "net5.0", "Class Library", "classlib", "VB", null)]
        [InlineData("TargetFramework", "net5.0", "--framework", "net5.0", "Class Library", "classlib", "F#", null)]

        [InlineData("TargetFramework", "net5.0", "-f", "net5.0", "Console Application", "console", null, null)]
        [InlineData("TargetFramework", "net5.0", "-f", "net5.0", "Console Application", "console", "VB", null)]
        [InlineData("TargetFramework", "net5.0", "-f", "net5.0", "Console Application", "console", "F#", null)]
        [InlineData("TargetFramework", "net5.0", "-f", "net5.0", "Class Library", "classlib", null, null)]
        [InlineData("TargetFramework", "net5.0", "-f", "net5.0", "Class Library", "classlib", "VB", null)]
        [InlineData("TargetFramework", "net5.0", "-f", "net5.0", "Class Library", "classlib", "F#", null)]
        public void CanSetProperty(string propertyName, string? propertyValue, string argName, string argValue, string expectedTemplateName, string templateShortName, string? language, string? framework)
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            List<string> args = new List<string>() { templateShortName, "--no-restore" };
            if (!string.IsNullOrWhiteSpace(language))
            {
                args.Add("--language");
                args.Add(language);
            }
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(argName))
            {
                args.Add(argName);
                args.Add(argValue);
            }

            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut($@"The template ""{expectedTemplateName}"" was created successfully.");

            string expectedExtension = language switch
            {
                "C#" => "*.csproj",
                "F#" => "*.fsproj",
                "VB" => "*.vbproj",
                _ => "*.csproj"
            };
            string projectFile = Directory.GetFiles(workingDir, expectedExtension).Single();
            XDocument projectXml = XDocument.Load(projectFile);
            XNamespace ns = projectXml.Root?.Name.Namespace ?? throw new Exception("Unexpected project file format");
            if (propertyValue != null)
            {
                Assert.Equal(propertyValue, projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + propertyName)?.Value);
            }
            else
            {
                Assert.Null(projectXml.Root?.Element(ns + "PropertyGroup")?.Element(ns + propertyName));
            }
            Directory.Delete(workingDir, true);
        }
    }
}
