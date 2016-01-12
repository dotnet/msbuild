// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public class DthStartupTests : IClassFixture<TestHelper>
    {
        private readonly TestHelper _testHelper;

        public DthStartupTests(TestHelper helper)
        {
            _testHelper = helper;
        }

        [Fact]
        public void DthStartup_GetProjectInformation()
        {
            var projectPath = _testHelper.FindSampleProject("EmptyConsoleApp");
            Assert.NotNull(projectPath);

            using (var server = new DthTestServer(_testHelper.LoggerFactory))
            using (var client = new DthTestClient(server))
            {
                client.Initialize(projectPath);

                var projectInformation = client.DrainTillFirst(MessageTypes.ProjectInformation)
                                               .EnsureSource(server, client)
                                               .RetrievePayloadAs<JObject>()
                                               .AssertProperty("Name", "EmptyConsoleApp");

                projectInformation.RetrievePropertyAs<JArray>("Configurations")
                                  .AssertJArrayCount(2)
                                  .AssertJArrayContains("Debug")
                                  .AssertJArrayContains("Release");

                var frameworkShortNames = projectInformation.RetrievePropertyAs<JArray>("Frameworks")
                                                            .AssertJArrayCount(2)
                                                            .Select(f => f["ShortName"].Value<string>());

                Assert.Contains("dnxcore50", frameworkShortNames);
                Assert.Contains("dnx451", frameworkShortNames);
            }
        }

        [Theory]
        [InlineData(4, 4)]
        [InlineData(5, 4)]
        [InlineData(3, 3)]
        public void DthStartup_ProtocolNegotiation(int requestVersion, int expectVersion)
        {
            using (var server = new DthTestServer(_testHelper.LoggerFactory))
            using (var client = new DthTestClient(server))
            {
                client.SetProtocolVersion(requestVersion);

                var response = client.DrainTillFirst(MessageTypes.ProtocolVersion, TimeSpan.FromDays(1));
                response.EnsureSource(server, client);

                Assert.Equal(expectVersion, response.Payload["Version"]?.Value<int>());
            }
        }

        [Fact]
        public void DthStartup_ProtocolNegotiation_ZeroIsNoAllowed()
        {
            using (var server = new DthTestServer(_testHelper.LoggerFactory))
            using (var client = new DthTestClient(server))
            {
                client.SetProtocolVersion(0);

                Assert.Throws<TimeoutException>(() =>
                {
                    client.DrainTillFirst(MessageTypes.ProtocolVersion, timeout: TimeSpan.FromSeconds(1));
                });
            }
        }

        [Theory]
        [InlineData("Project", "UnresolvedProjectSample", "EmptyLibrary", "Project")]
        [InlineData("Package", "UnresolvedPackageSample", "NoSuchPackage", null)]
        [InlineData("Package", "IncompatiblePackageSample", "Newtonsoft.Json", "Package")]
        public void DthCompilation_Initialize_UnresolvedDependency(string referenceType,
                                                                   string testProjectName,
                                                                   string expectedUnresolvedDependency,
                                                                   string expectedUnresolvedType)
        {
            var projectPath = _testHelper.FindSampleProject(testProjectName);
            Assert.NotNull(projectPath);

            using (var server = new DthTestServer(_testHelper.LoggerFactory))
            using (var client = new DthTestClient(server))
            {
                client.Initialize(projectPath);

                var unresolveDependency = client.DrainTillFirst("Dependencies")
                                                .EnsureSource(server, client)
                                                .RetrieveDependency(expectedUnresolvedDependency);

                unresolveDependency.AssertProperty("Name", expectedUnresolvedDependency)
                                   .AssertProperty("DisplayName", expectedUnresolvedDependency)
                                   .AssertProperty("Resolved", false)
                                   .AssertProperty("Type", expectedUnresolvedType);

                if (expectedUnresolvedType == "Project")
                {
                    unresolveDependency.AssertProperty("Path", Path.Combine(Path.GetDirectoryName(projectPath),
                                                                            expectedUnresolvedDependency,
                                                                            Project.FileName));
                }
                else
                {
                    Assert.False(unresolveDependency["Path"].HasValues);
                }

                var referencesMessage = client.DrainTillFirst("References", TimeSpan.FromDays(1))
                                              .EnsureSource(server, client);

                if (referenceType == "Project")
                {
                    var expectedUnresolvedProjectPath = Path.Combine(Path.GetDirectoryName(projectPath),
                                                                     expectedUnresolvedDependency,
                                                                     Project.FileName);

                    referencesMessage.RetrievePayloadAs<JObject>()
                                     .RetrievePropertyAs<JArray>("ProjectReferences")
                                     .AssertJArrayCount(1)
                                     .RetrieveArraryElementAs<JObject>(0)
                                     .AssertProperty("Name", expectedUnresolvedDependency)
                                     .AssertProperty("Path", expectedUnresolvedProjectPath)
                                     .AssertProperty<JToken>("WrappedProjectPath", prop => !prop.HasValues);
                }
                else if (referenceType == "Package")
                {
                    referencesMessage.RetrievePayloadAs<JObject>()
                                     .RetrievePropertyAs<JArray>("ProjectReferences")
                                     .AssertJArrayCount(0);
                }
            }
        }

        [Fact]
        public void DthNegative_BrokenProjectPathInLockFile()
        {
            using (var server = new DthTestServer(_testHelper.LoggerFactory))
            using (var client = new DthTestClient(server))
            {
                // After restore the project is copied to another place so that
                // the relative path in project lock file is invalid.
                var movedProjectPath = _testHelper.MoveProject("BrokenProjectPathSample");

                client.Initialize(movedProjectPath);

                client.DrainTillFirst("DependencyDiagnostics")
                      .RetrieveDependencyDiagnosticsCollection()
                      .RetrieveDependencyDiagnosticsErrorAt(0)
                      .AssertProperty<string>("FormattedMessage", message => message.Contains("error NU1002"))
                      .RetrievePropertyAs<JObject>("Source")
                      .AssertProperty("Name", "EmptyLibrary");

                client.DrainTillFirst("Dependencies")
                      .RetrieveDependency("EmptyLibrary")
                      .AssertProperty<JArray>("Errors", errorsArray => errorsArray.Count == 1)
                      .AssertProperty<JArray>("Warnings", warningsArray => warningsArray.Count == 0)
                      .AssertProperty("Name", "EmptyLibrary")
                      .AssertProperty("Resolved", false);
            }
        }

        [Fact(Skip = "Require dotnet restore integration test")]
        public void DthDependencies_UpdateGlobalJson_RefreshDependencies()
        {
            var projectPath = _testHelper.CreateSampleProject("DthUpdateSearchPathSample");
            Assert.True(Directory.Exists(projectPath));

            using (var server = new DthTestServer(_testHelper.LoggerFactory))
            using (var client = new DthTestClient(server))
            {
                var testProject = Path.Combine(projectPath, "home", "src", "MainProject");

                client.Initialize(testProject);

                client.DrainTillFirst("ProjectInformation")
                      .RetrievePayloadAs<JObject>()
                      .RetrievePropertyAs<JArray>("ProjectSearchPaths")
                      .AssertJArrayCount(2);

                client.DrainTillFirst("DependencyDiagnostics")
                      .RetrievePayloadAs<JObject>()
                      .AssertProperty<JArray>("Errors", array => array.Count == 0)
                      .AssertProperty<JArray>("Warnings", array => array.Count == 0);

                client.DrainTillFirst("Dependencies")
                      .RetrieveDependency("Newtonsoft.Json")
                      .AssertProperty("Type", "Project")
                      .AssertProperty("Resolved", true)
                      .AssertProperty<JArray>("Errors", array => array.Count == 0, _ => "Dependency shouldn't contain any error.");

                // Overwrite the global.json to remove search path to ext
                File.WriteAllText(
                    Path.Combine(projectPath, "home", GlobalSettings.FileName),
                    JsonConvert.SerializeObject(new { project = new string[] { "src" } }));

                client.SendPayLoad(testProject, "RefreshDependencies");

                client.DrainTillFirst("ProjectInformation")
                      .RetrievePayloadAs<JObject>()
                      .RetrievePropertyAs<JArray>("ProjectSearchPaths")
                      .AssertJArrayCount(1)
                      .AssertJArrayElement(0, Path.Combine(projectPath, "home", "src"));

                client.DrainTillFirst("DependencyDiagnostics")
                      .RetrieveDependencyDiagnosticsCollection()
                      .RetrieveDependencyDiagnosticsErrorAt<JObject>(0)
                      .AssertProperty("ErrorCode", "NU1010");

                client.DrainTillFirst("Dependencies")
                      .RetrieveDependency("Newtonsoft.Json")
                      .AssertProperty("Type", "")
                      .AssertProperty("Resolved", false)
                      .RetrievePropertyAs<JArray>("Errors")
                      .AssertJArrayCount(1)
                      .RetrieveArraryElementAs<JObject>(0)
                      .AssertProperty("ErrorCode", "NU1010");
            }
        }
    }
}
