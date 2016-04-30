// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class GivenAnAppWithRedirectsAndExecutableDependency : TestBase, IClassFixture<TestSetupFixture>
    {
        private const string ExecutableDependency = "dotnet-desktop-binding-redirects.exe";
        private const string ExecutableDependencyCommand = "desktop-binding-redirects";
        private TestSetupFixture _testSetup;
        public string _appWithConfigProjectRoot;
        public string _appWithoutConfigProjectRoot;
        private string _appWithConfigBuildOutput;
        private string _appWithoutConfigBuildOutput;
        private string _appWithConfigPublishOutput;
        private string _appWithoutConfigPublishOutput;
        private string _executableDependencyBuildOutput;
        private string _executableDependencyPublishOutput;

        public GivenAnAppWithRedirectsAndExecutableDependency(TestSetupFixture testSetup)
        {
            _testSetup = testSetup;
            _appWithConfigProjectRoot = _testSetup.AppWithConfigProjectRoot;
            _appWithConfigBuildOutput = _testSetup.AppWithConfigBuildOutput;
            _appWithConfigPublishOutput = _testSetup.AppWithConfigPublishOutput;
            _appWithoutConfigProjectRoot = _testSetup.AppWithoutConfigProjectRoot;
            _appWithoutConfigBuildOutput = _testSetup.AppWithoutConfigBuildOutput;
            _appWithoutConfigPublishOutput = _testSetup.AppWithoutConfigPublishOutput;
            _executableDependencyBuildOutput = Path.Combine(Path.GetDirectoryName(_appWithConfigBuildOutput), ExecutableDependency);
            _executableDependencyPublishOutput = Path.Combine(Path.GetDirectoryName(_appWithConfigPublishOutput), ExecutableDependency);
        }

        private static List<string> BindingsAppNoConfig
        {
            get
            {
                List<string> bindings = new List<string>()
                {
                    @"<dependentAssembly xmlns=""urn:schemas-microsoft-com:asm.v1"">
                        <assemblyIdentity name=""Newtonsoft.Json"" publicKeyToken=""30ad4fe6b2a6aeed"" culture=""neutral"" />
                        <bindingRedirect oldVersion=""4.5.0.0"" newVersion=""8.0.0.0"" />
                        <bindingRedirect oldVersion=""6.0.0.0"" newVersion=""8.0.0.0"" />
                      </dependentAssembly>",
                    @"<dependentAssembly xmlns=""urn:schemas-microsoft-com:asm.v1"">
                        <assemblyIdentity name=""System.Web.Mvc"" publicKeyToken=""31bf3856ad364e35"" culture=""neutral"" />
                        <bindingRedirect oldVersion=""4.0.0.0"" newVersion=""3.0.0.1"" />
                      </dependentAssembly>"
                };

                return bindings;
            }
        }

        private static List<string> BindingsAppWithConfig
        {
            get
            {
                List<string> bindings = new List<string>()
                {
                    @"<dependentAssembly xmlns=""urn:schemas-microsoft-com:asm.v1"">
                        <assemblyIdentity name=""Newtonsoft.Json"" publicKeyToken=""30ad4fe6b2a6aeed"" culture=""neutral"" />
                        <bindingRedirect oldVersion=""3.5.0.0"" newVersion=""8.0.0.0"" />
                        <bindingRedirect oldVersion=""4.5.0.0"" newVersion=""8.0.0.0"" />
                        <bindingRedirect oldVersion=""6.0.0.0"" newVersion=""8.0.0.0"" />
                      </dependentAssembly>",
                    @"<dependentAssembly xmlns=""urn:schemas-microsoft-com:asm.v1"">
                        <assemblyIdentity name=""Some.Foo.Assembly"" publicKeyToken=""814f48568d36eed5"" culture=""neutral"" />
                        <bindingRedirect oldVersion=""3.0.0.0"" newVersion=""5.5.5.1"" />
                      </dependentAssembly>",
                    @"<dependentAssembly xmlns=""urn:schemas-microsoft-com:asm.v1"">
                        <assemblyIdentity name=""System.Web.Mvc"" publicKeyToken=""31bf3856ad364e35"" culture=""neutral"" />
                        <bindingRedirect oldVersion=""4.0.0.0"" newVersion=""3.0.0.1"" />
                      </dependentAssembly>"
                };

                return bindings;
            }
        }

        private static List<XElement> ExpectedBindingsAppNoConfig
        {
            get
            {
                List<XElement> bindingElements = new List<XElement>();

                foreach (var binding in BindingsAppNoConfig)
                {
                    bindingElements.Add(XElement.Parse(binding));
                }

                return bindingElements;
            }
        }

        private static List<XElement> ExpectedBindingsAppWithConfig
        {
            get
            {
                List<XElement> bindingElements = new List<XElement>();

                foreach (var binding in BindingsAppWithConfig)
                {
                    bindingElements.Add(XElement.Parse(binding));
                }

                return bindingElements;
            }
        }

        private static Dictionary<string, string> ExpectedAppSettings
        {
            get
            {
                Dictionary<string, string> appSettings = new Dictionary<string, string>()
                {
                    {"Setting1", "Hello"},
                    {"Setting2", "World"}
                };

                return appSettings;
            }
        }

        private IEnumerable<XElement> GetRedirects(string exePath)
        {
            var configFile = exePath + ".config";
            File.Exists(configFile).Should().BeTrue($"Config file not found - {configFile}");
            var config = ConfigurationManager.OpenExeConfiguration(exePath);
            var runtimeSectionXml = config.Sections["runtime"].SectionInformation.GetRawXml();
            var runtimeSectionElement = XElement.Parse(runtimeSectionXml);
            var redirects = runtimeSectionElement.Elements()
                                .Where(e => e.Name.LocalName == "assemblyBinding").Elements()
                                .Where(e => e.Name.LocalName == "dependentAssembly");
            return redirects;
        }

        private void VerifyRedirects(IEnumerable<XElement> redirects, IEnumerable<XElement> generatedBindings)
        {
            foreach (var binding in generatedBindings)
            {
                var redirect = redirects.SingleOrDefault(r => /*XNode.DeepEquals(r, binding)*/ r.ToString() == binding.ToString());

                redirect.Should().NotBeNull($"Binding not found in runtime section : {Environment.NewLine}{binding}");
            }
        }

        private void VerifyAppSettings(string exePath)
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(exePath);
            foreach (var appSetting in ExpectedAppSettings)
            {
                var value = configFile.AppSettings.Settings[appSetting.Key];
                value.Should().NotBeNull($"AppSetting with key '{appSetting.Key}' not found in config file.");
                value.Value.Should().Be(appSetting.Value, $"For AppSetting '{appSetting.Key}' - Expected Value '{appSetting.Value}', Actual '{ value.Value}'");
            }
        }

        [Fact]
        public void Build_Generates_Redirects_For_App_Without_Config()
        {
            var redirects = GetRedirects(_appWithoutConfigBuildOutput);
            VerifyRedirects(redirects, ExpectedBindingsAppNoConfig);

            var commandResult = new TestCommand(_appWithoutConfigBuildOutput)
                                    .Execute();
            commandResult.Should().Pass();
        }

        [Fact]
        public void Publish_Generates_Redirects_For_App_Without_Config()
        {
            var redirects = GetRedirects(_appWithoutConfigPublishOutput);
            VerifyRedirects(redirects, ExpectedBindingsAppNoConfig);

            var commandResult = new TestCommand(_appWithoutConfigPublishOutput)
                                    .Execute();
            commandResult.Should().Pass();
        }

        [Fact]
        public void Build_Generates_Redirects_For_Executable_Dependency()
        {
            var redirects = GetRedirects(_executableDependencyBuildOutput);
            VerifyRedirects(redirects, ExpectedBindingsAppNoConfig);

            var commandResult = new TestCommand(_executableDependencyBuildOutput)
                                    .Execute();
            commandResult.Should().Pass();
        }

        [Fact(Skip = "https://github.com/dotnet/cli/issues/2632")]
        public void Publish_Generates_Redirects_For_Executable_Dependency()
        {
            var redirects = GetRedirects(_executableDependencyPublishOutput);
            VerifyRedirects(redirects, ExpectedBindingsAppNoConfig);

            var commandResult = new TestCommand(_executableDependencyPublishOutput)
                                    .Execute();
            commandResult.Should().Pass();
        }

        [Fact]
        public void Build_Generates_Redirects_For_App_With_Config()
        {
            var redirects = GetRedirects(_appWithConfigBuildOutput);
            VerifyRedirects(redirects, ExpectedBindingsAppWithConfig);
            VerifyAppSettings(_appWithConfigBuildOutput);

            var commandResult = new TestCommand(_appWithConfigBuildOutput)
                                    .Execute();
            commandResult.Should().Pass();
        }

        [Fact]
        public void Publish_Generates_Redirects_For_App_With_Config()
        {
            var redirects = GetRedirects(_appWithConfigPublishOutput);
            VerifyRedirects(redirects, ExpectedBindingsAppWithConfig);
            VerifyAppSettings(_appWithConfigPublishOutput);

            var commandResult = new TestCommand(_appWithConfigPublishOutput)
                                    .Execute();
            commandResult.Should().Pass();
        }

        [Fact]
        public void Tool_Command_Runs_Executable_Dependency_For_App_With_Config()
        {
            var commandResult = new DependencyToolInvokerCommand { WorkingDirectory = _appWithConfigProjectRoot }
                                        .Execute("desktop-binding-redirects", "net451", "");
            commandResult.Should().Pass();
        }

        [Fact]
        public void Tool_Command_Runs_Executable_Dependency_For_App_Without_Config()
        {
            var appDirectory = Path.GetDirectoryName(_appWithoutConfigProjectRoot);
            var commandResult = new DependencyToolInvokerCommand { WorkingDirectory = _appWithoutConfigProjectRoot }
                                        .Execute("desktop-binding-redirects", "net451", "");
            commandResult.Should().Pass();
        }
    }
}
