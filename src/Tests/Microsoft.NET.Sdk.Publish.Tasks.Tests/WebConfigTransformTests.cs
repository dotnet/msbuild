// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Publish.Tasks;
using Microsoft.NET.Sdk.Publish.Tasks.Tests;

// Some of the tests Copied from https://raw.githubusercontent.com/aspnet/IISIntegration/50f066579a96c6f2b2a4c47524c684e1ef3dfdf0/test/Microsoft.AspNetCore.Server.IISIntegration.Tools.Tests/WebConfigTransformFacts.cs

namespace Microsoft.Net.Sdk.Publish.Tasks.Tests
{
    public class WebConfigTransformTests
    {
        [Fact]
        public void WebConfigTransform_creates_new_config_if_one_does_not_exist()
        {
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName:null, projectFullPath: null)));

            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplatePortable,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: false, extension: null, aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [Fact]
        public void WebConfigTransform_creates_ProcessPath_WithCorrectExtension()
        {
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));

            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplateWithOutExe,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: null, aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [Fact]
        public void WebConfigTransform_creates_new_config_if_one_has_unexpected_format()
        {
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(XDocument.Parse("<unexpected />"), "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [Theory]
        [InlineData(new object[] { new[] { "system.webServer" } })]
        [InlineData(new object[] { new[] { "add" } })]
        [InlineData(new object[] { new[] { "handlers" } })]
        [InlineData(new object[] { new[] { "aspNetCore" } })]
        [InlineData(new object[] { new[] { "environmentVariables" } })]
        [InlineData(new object[] { new[] { "environmentVariable" } })]
        [InlineData(new object[] { new[] { "handlers", "aspNetCore", "environmentVariables" } })]
        public void WebConfigTransform_adds_missing_elements(string[] elementNames)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            foreach (var elementName in elementNames)
            {
                input.Descendants(elementName).Remove();
            }

            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [Theory]
        [InlineData("add", "path", "test")]
        [InlineData("add", "verb", "test")]
        [InlineData("add", "modules", "mods")]
        [InlineData("add", "resourceType", "Either")]
        [InlineData("aspNetCore", "stdoutLogEnabled", "true")]
        [InlineData("aspNetCore", "startupTimeLimit", "1200")]
        [InlineData("aspNetCore", "arguments", "arg1")]
        [InlineData("aspNetCore", "stdoutLogFile", "logfile")]
        public void WebConfigTransform_wont_override_custom_values(string elementName, string attributeName, string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants(elementName).Single().SetAttributeValue(attributeName, attributeValue);

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.Equal(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }


        [Theory]
        [InlineData("aspNetCore", "hostingModel", "inprocess")]
        [InlineData("aspNetCore", "hostingModel", "InProcess")]
        [InlineData("aspNetCore", "hostingModel", "outofprocess")]
        [InlineData("aspNetCore", "hostingModel", "OutOfProcess")]
        public void WebConfigTransform_will_UseHostingModel_FromWebConfigIfPresent(string elementName, string attributeName, string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants(elementName).Single().SetAttributeValue(attributeName, attributeValue);

            var guid = Guid.NewGuid();
            string projectPath = Path.Combine(Path.GetTempPath(), guid.ToString(), "sample.csproj");
            string projectDirectory = Path.GetDirectoryName(projectPath);
            if (!Directory.Exists(projectDirectory))
            {
                Directory.CreateDirectory(projectDirectory);
            }
            string webConfigPath = Path.Combine(projectDirectory, "web.config");
            File.WriteAllText(webConfigPath, input.ToString());

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: "foo", environmentName: null, projectFullPath: projectPath);
            Assert.Equal(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [Theory]
        [InlineData("add", "modules", "AspNetCoreModuleV2")]
        [InlineData("add", "modules", "AspNetCoreModule")]
        public void WebConfigTransform_UsesAspNetCoreHostingVersion_ForHostingModule(string elementName, string attributeName, string attributeValue)
        {
            var output = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: attributeValue, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.Equal(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [Theory]
        [InlineData("add", "modules", "AspNetCoreModuleV2")]
        [InlineData("add", "modules", "AspNetCoreModule")]
        [InlineData("add", "modules", "UnKnownValue")]
        public void WebConfigTransform_UsesAspNetCoreHostingVersion_FromWebConfigIfPresent(string elementName, string attributeName, string attributeValue)
        {
            var output = WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: attributeValue, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.Equal("AspNetCoreModule", (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [Theory]
        [InlineData("aspNetCore", "hostingModel", "outofprocess")]
        [InlineData("aspNetCore", "hostingModel", "OutOfProcess")]
        public void WebConfigTransform_UsingAspNetCoreModule_SupportsOutOfProc(string elementName, string attributeName, string attributeValue)
        {
            // Template uses AspNetCoreModuleV1
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null);
            Assert.Equal(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [Theory]
        [InlineData("inprocess")]
        [InlineData("InProcess")]
        public void WebConfigTransform_Throws_ForInValidHostingModel(string attributeValue)
        {
            // Template uses AspNetCoreModuleV1
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            Assert.Throws<Exception>(() => WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null));
        }

        [Theory]
        [InlineData("aspNetCore", "hostingModel", "inprocess", "AspNetCoreModuleV2")]
        [InlineData("aspNetCore", "hostingModel", "InProcess", "AspNetCoreModuleV2")]
        [InlineData("aspNetCore", "hostingModel", "outofprocess", "AspNetCoreModuleV2")]
        [InlineData("aspNetCore", "hostingModel", "OutOfProcess", "AspNetCoreModuleV2")]
        [InlineData("aspNetCore", "hostingModel", "outofprocess", "AspNetCoreModule")]
        [InlineData("aspNetCore", "hostingModel", "OutOfProcess", "AspNetCoreModule")]
        public void WebConfigTransform_UsesAspNetCoreHostingModelValue_ForHostingModel(string elementName, string attributeName, string attributeValue, string aspNetCoreModuleName)
        {
            var output = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: aspNetCoreModuleName, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null);
            Assert.Equal(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [Theory]
        [InlineData("foo")]
        public void WebConfigTransform_Throws_IfHostingModelValueIsUndefined(string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            Assert.Throws<Exception>(() => WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null));
        }

        [Theory]
        [InlineData("aspNetCore", "hostingModel", "")]
        public void WebConfigTransform_DoesNotSet_HostingModelIfEmpty(string elementName, string attributeName, string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null);
            Assert.Null((string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [Fact]
        public void WebConfigTransform_will_append_Env_IfPassed()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
 
            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Production", projectFullPath: null);
            Assert.True(XNode.DeepEquals(output, WebConfigTransformTemplates.WebConfigTemplateWithEnvironmentVariable));
        }

        [Fact]
        public void WebConfigTransform_will_Override_Env_IfUpdated()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplateWithEnvironmentVariable;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Staging", projectFullPath: null);
            Assert.Equal("Staging", (string)output.Descendants("environmentVariable").Single().Attribute("value"));
        }


        private static readonly List<object[]> testData = new List<object[]>
        {
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithoutLocation) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithNonRelevantLocationFirst) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithNonRelevantLocationLast) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithRelevantLocationFirst) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithRelevantLocationLast) },
        };

        public static IEnumerable<object[]> TemplatesToTest
        {
            get { return testData; }
        }

        [Theory]
        [MemberData(nameof(TemplatesToTest))]
        public void WebConfigTransform_HandlesLocations_Correctly(XDocument template)
        {
            var input = template;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.True(XNode.DeepEquals(output, template));
        }


        [Fact]
        public void WebConfigTransform_overwrites_processPath()
        {
            var newProcessPath =
                (string)WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "app.exe", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single().Attribute("processPath");

            Assert.Equal(@".\app.exe", newProcessPath);
        }

        [Fact]
        public void WebConfigTransform_fixes_aspnetcore_casing()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("add").Single().SetAttributeValue("name", "aspnetcore");

            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [Fact]
        public void WebConfigTransform_does_not_remove_children_of_aspNetCore_element()
        {
            var envVarElement =
                new XElement("environmentVariable", new XAttribute("name", "ENVVAR"), new XAttribute("value", "123"));

            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Single().Add(envVarElement);

            Assert.True(XNode.DeepEquals(envVarElement,
                WebConfigTransform.Transform(input, "app.exe", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Test", projectFullPath: null)
                    .Descendants("environmentVariable").SingleOrDefault(e => (string)e.Attribute("name") == "ENVVAR")));

            var output = WebConfigTransform.Transform(input, "app.exe", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Test", projectFullPath: null);

            Assert.Equal("Test", (string)output.Descendants("environmentVariable").SingleOrDefault(e => (string)e.Attribute("name") == "ASPNETCORE_ENVIRONMENT").Attribute("value"));
        }

        [Fact]
        public void WebConfigTransform_adds_stdoutLogEnabled_if_attribute_is_missing()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Attributes("stdoutLogEnabled").Remove();

            Assert.Equal(
                "false",
                (string)WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants().Attributes("stdoutLogEnabled").Single());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("false")]
        [InlineData("true")]
        public void WebConfigTransform_adds_stdoutLogFile_if_attribute_is_missing(string stdoutLogFile)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            var aspNetCoreElement = input.Descendants("aspNetCore").Single();
            aspNetCoreElement.Attribute("stdoutLogEnabled").Remove();
            if (stdoutLogFile != null)
            {
                aspNetCoreElement.SetAttributeValue("stdoutLogEnabled", stdoutLogFile);
            }

            Assert.Equal(
                @".\logs\stdout",
                (string)WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants().Attributes("stdoutLogFile").Single());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("true")]
        [InlineData("false")]
        public void WebConfigTransform_does_not_change_existing_stdoutLogEnabled(string stdoutLogEnabledValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            var aspNetCoreElement = input.Descendants("aspNetCore").Single();

            aspNetCoreElement.SetAttributeValue("stdoutLogFile", "mylog.txt");
            aspNetCoreElement.Attributes("stdoutLogEnabled").Remove();
            if (stdoutLogEnabledValue != null)
            {
                input.Descendants("aspNetCore").Single().SetAttributeValue("stdoutLogEnabled", stdoutLogEnabledValue);
            }

            Assert.Equal(
                "mylog.txt",
                (string)WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants().Attributes("stdoutLogFile").Single());
        }

        [Fact]
        public void WebConfigTransform_correctly_configures_for_Azure()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Attributes().Remove();

            var aspNetCoreElement = WebConfigTransform.Transform(input, "test.dll", configureForAzure: true, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                .Descendants("aspNetCore").Single();
            aspNetCoreElement.Elements().Remove();

            Assert.True(XNode.DeepEquals(
                XDocument.Parse(@"<aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false""
                    stdoutLogFile=""\\?\%home%\LogFiles\stdout"" />").Root,
                aspNetCoreElement));
        }

        [Fact]
        public void WebConfigTransform_overwrites_stdoutLogPath_for_Azure()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: true, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);

            Assert.Equal(
                @"\\?\%home%\LogFiles\stdout",
                (string)output.Descendants("aspNetCore").Single().Attribute("stdoutLogFile"));
        }

        [Fact]
        public void WebConfigTransform_configures_portable_apps_correctly()
        {
            var aspNetCoreElement =
                WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "test.dll", configureForAzure: false, useAppHost: false, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.True(XNode.DeepEquals(
                XDocument.Parse(@"<aspNetCore processPath=""dotnet"" arguments="".\test.dll"" stdoutLogEnabled=""false""
                     stdoutLogFile="".\logs\stdout"" />").Root,
                aspNetCoreElement));
        }

        [Fact]
        public void WebConfigTransform_configures_full_framework_apps_correctly()
        {
            var aspNetCoreElement =
                WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "test.exe", configureForAzure: false, useAppHost: false, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.True(XNode.DeepEquals(
                XDocument.Parse(@"<aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false""
                     stdoutLogFile="".\logs\stdout"" />").Root,
                aspNetCoreElement));
        }

        [Theory]
        [InlineData("%LAUNCHER_ARGS%", "")]
        [InlineData(" %launcher_ARGS%", "")]
        [InlineData("%LAUNCHER_args% ", "")]
        [InlineData("%LAUNCHER_ARGS% %launcher_args%", "")]
        [InlineData(" %LAUNCHER_ARGS% %launcher_args% ", "")]
        [InlineData(" %launcher_args% -my-switch", "-my-switch")]
        [InlineData("-my-switch %LaUnChEr_ArGs%", "-my-switch")]
        [InlineData("-switch-1 %LAUNCHER_ARGS% -switch-2", "-switch-1  -switch-2")]
        [InlineData("%LAUNCHER_ARGS% -switch %launcher_args%", "-switch")]
        [InlineData("-argFile IISExeLauncherArgs.txt", "")]
        [InlineData(" -argFile IISExeLauncherArgs.txt", "")]
        [InlineData("-argFile IISExeLauncherArgs.txt ", "")]
        [InlineData("-argFile IISExeLauncherArgs.txt -argFile IISExeLauncherArgs.txt", "")]
        [InlineData(" -argFile IISExeLauncherArgs.txt %launcher_args% ", "")]
        [InlineData(" -argFile IISExeLauncherArgs.txt -my-switch", "-my-switch")]
        [InlineData("-my-switch -argFile IISExeLauncherArgs.txt", "-my-switch")]
        [InlineData("%launcher_args% -switch-1 -argFile IISExeLauncherArgs.txt -switch-2", "-switch-1  -switch-2")]
        [InlineData("-argFile IISExeLauncherArgs.txt -switch -argFile IISExeLauncherArgs.txt %launcher_args%", "-switch")]
        public void WebConfigTransform_removes_LAUNCHER_ARGS_from_arguments_for_standalone_apps(string inputArguments, string outputArguments)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Single().SetAttributeValue("arguments", inputArguments);

            var aspNetCoreElement =
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.Equal(outputArguments, (string)aspNetCoreElement.Attribute("arguments"));
        }

        [Theory]
        [InlineData("", ".\\myapp.dll")]
        [InlineData("%LAUNCHER_ARGS%", ".\\myapp.dll")]
        [InlineData("%LAUNCHER_ARGS% %launcher_args%", ".\\myapp.dll")]
        [InlineData("-my-switch", ".\\myapp.dll -my-switch")]
        [InlineData(" %launcher_args% -my-switch", ".\\myapp.dll -my-switch")]
        [InlineData("-my-switch %LaUnChEr_ArGs%", ".\\myapp.dll -my-switch")]
        [InlineData("-switch-1 -switch-2", ".\\myapp.dll -switch-1 -switch-2")]
        [InlineData("-switch-1 %LAUNCHER_ARGS% -switch-2", ".\\myapp.dll -switch-1  -switch-2")]
        [InlineData("%LAUNCHER_ARGS% -switch %launcher_args%", ".\\myapp.dll -switch")]
        [InlineData("%LAUNCHER_ARGS% -argFile IISExeLauncherArgs.txt", ".\\myapp.dll")]
        [InlineData("-my-switch %LaUnChEr_ArGs% -argFile iisexelauncherargs.txt", ".\\myapp.dll -my-switch")]
        [InlineData("-argFile iisexelauncherargs.txt", ".\\myapp.dll")]
        [InlineData("-argFile iisexelauncherargs.txt -argFile iisexelauncherargs.txt", ".\\myapp.dll")]
        public void WebConfigTransform_wont_override_existing_args_for_portable_apps(string inputArguments, string outputArguments)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Single().SetAttributeValue("arguments", inputArguments);

            var aspNetCoreElement =
                WebConfigTransform.Transform(input, "myapp.dll", configureForAzure: false, useAppHost: false, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.Equal(outputArguments, (string)aspNetCoreElement.Attribute("arguments"));
        }


        private bool VerifyMissingElementCreated(params string[] elementNames)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            foreach (var elementName in elementNames)
            {
                input.Descendants(elementName).Remove();
            }

            return XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null));
        }

        [Theory]
        [InlineData("66964EC2-712A-451A-AB4F-33F18D8F54F1")]
        [InlineData("  66964EC2-712A-451A-AB4F-33F18D8F54F1  ")]
        [InlineData("{ 66964EC2-712A-451A-AB4F-33F18D8F54F1 }")]
        [InlineData("{66964EC2-712A-451A-AB4F-33F18D8F54F1}")]
        [InlineData("( 66964EC2-712A-451A-AB4F-33F18D8F54F1 )")]
        [InlineData("(66964EC2-712A-451A-AB4F-33F18D8F54F1)")]

        public void WebConfigTransform_Adds_ProjectGuid_IfNotPresent(string projectGuid)
        {
            // Arrange
            XDocument transformedWebConfig = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfig));

            // Act
            XDocument transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, projectGuid, false);

            // Assert
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplateWithProjectGuid, transformedWebConfigWithGuid));
        }

        [Theory]
        [InlineData("66964EC2-712A-451A-AB4F-33F18D8F54F1")]
        [InlineData(" 66964EC2-712A-451A-AB4F-33F18D8F54F1 ")]
        [InlineData("{ 66964EC2-712A-451A-AB4F-33F18D8F54F1 }")]
        [InlineData("{66964EC2-712A-451A-AB4F-33F18D8F54F1}")]
        [InlineData("( 66964EC2-712A-451A-AB4F-33F18D8F54F1 )")]
        [InlineData("(66964EC2-712A-451A-AB4F-33F18D8F54F1)")]

        public void WebConfigTransform_Removes_ProjectGuid_IfIgnorePropertyIsSet(string projectGuid)
        {
            // Arrange
            XDocument transformedWebConfig = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfig));
            XDocument transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, projectGuid, false);
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplateWithProjectGuid, transformedWebConfigWithGuid));

            // Act
            transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, projectGuid, true);

            //Assert
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfigWithGuid));

        }

        [Fact]
        public void WebConfigTransform_DoesNothingWithProjectGuid_IfAbsent()
        {
            // Arrange
            XDocument transformedWebConfig = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfig));

            // Act
            XDocument transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, null, false);

            // Assert
            Assert.True(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfigWithGuid));
        }
    }
}
