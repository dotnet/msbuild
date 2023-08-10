// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToControlGeneratedAssemblyInfo : SdkTest
    {
        public GivenThatWeWantToControlGeneratedAssemblyInfo(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("AssemblyInformationVersionAttribute")]
        [InlineData("AssemblyFileVersionAttribute")]
        [InlineData("AssemblyVersionAttribute")]
        [InlineData("AssemblyCompanyAttribute")]
        [InlineData("AssemblyConfigurationAttribute")]
        [InlineData("AssemblyCopyrightAttribute")]
        [InlineData("AssemblyDescriptionAttribute")]
        [InlineData("AssemblyTitleAttribute")]
        [InlineData("AssemblyTrademarkAttribute")]
        [InlineData("NeutralResourcesLanguageAttribute")]
        [InlineData("All")]
        public void It_respects_opt_outs(string attributeToOptOut)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: Path.DirectorySeparatorChar + attributeToOptOut)
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(
                    "/p:Version=1.2.3-beta",
                    "/p:FileVersion=4.5.6.7",
                    "/p:AssemblyVersion=8.9.10.11",
                    "/p:Company=TestCompany",
                    "/p:Configuration=Release",
                    "/p:Copyright=TestCopyright",
                    "/p:Description=TestDescription",
                    "/p:Product=TestProduct",
                    "/p:AssemblyTitle=TestTitle",
                    "/p:Trademark=TestTrademark",
                    "/p:NeutralLanguage=fr",
                    attributeToOptOut == "All" ?
                        "/p:GenerateAssemblyInfo=false" :
                        $"/p:Generate{attributeToOptOut}=false"
                    )
                .Should()
                .Pass();

            var expectedInfo = new SortedDictionary<string, string>
            {
                { "AssemblyInformationalVersionAttribute", "1.2.3-beta" },
                { "AssemblyFileVersionAttribute", "4.5.6.7" },
                { "AssemblyVersionAttribute", "8.9.10.11" },
                { "AssemblyCompanyAttribute", "TestCompany" },
                { "AssemblyConfigurationAttribute", "Release" },
                { "AssemblyCopyrightAttribute", "TestCopyright" },
                { "AssemblyDescriptionAttribute", "TestDescription" },
                { "AssemblyProductAttribute", "TestProduct" },
                { "AssemblyTitleAttribute", "TestTitle" },
                { "AssemblyTrademarkAttribute", "TestTrademark" },
                { "NeutralResourcesLanguageAttribute", "fr" },
            };

            if (attributeToOptOut == "All")
            {
                expectedInfo.Clear();
            }
            else
            {
                expectedInfo.Remove(attributeToOptOut);
            }

            expectedInfo.Add("TargetFrameworkAttribute", $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}");

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, "Release").FullName, "HelloWorld.dll");
            var actualInfo = AssemblyInfo.Get(assemblyPath);

            actualInfo.Should().Equal(expectedInfo);
        }

        [Fact]
        public void It_does_not_include_source_revision_id_if_initialize_source_control_target_not_available()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().Should().BeEquivalentTo(new[] { "1.0.0" });
        }

        [Fact]
        public void It_does_not_include_source_revision_id_if_source_revision_id_not_set()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", ""))));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true")));
                });

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().Should().BeEquivalentTo(new[] { "1.0.0" });
        }

        [Fact]
        public void It_does_not_include_source_revision_id_if_disabled()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "xyz"))));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true"),
                            new XElement("IncludeSourceRevisionInInformationalVersion", "false")));
                });

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().Should().BeEquivalentTo(new[] { "1.0.0" });
        }

        [Fact]
        public void It_includes_source_revision_id_if_available__version_without_plus()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "_SetSourceRevisionId"),
                            new XAttribute("BeforeTargets", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "xyz"))));

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation")));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true")));
                });

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().Should().BeEquivalentTo(new[] { "1.0.0+xyz" });
        }

        [Fact]
        public void It_includes_source_revision_id_if_available__version_with_plus()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProjectWithSourceRevisionId",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "_SetSourceRevisionId"),
                            new XAttribute("BeforeTargets", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "xyz"))));

                    project.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation")));

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true"),
                            new XElement("InformationalVersion", "1.2.3+abc")));
                });

            var command = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, valueName: "InformationalVersion");
            command.Execute().Should().Pass();

            command.GetValues().Should().BeEquivalentTo(new[] { "1.2.3+abc.xyz" });
        }

        [WindowsOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        [InlineData("net45")]
        public void It_respects_version_prefix(string targetFramework)
        {
            if (targetFramework == "net45")
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute($"/p:OutputType=Library", $"/p:TargetFramework={targetFramework}", $"/p:VersionPrefix=1.2.3")
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, "HelloWorld.dll");
            var info = AssemblyInfo.Get(assemblyPath);

            info["AssemblyVersionAttribute"].Should().Be("1.2.3.0");
            info["AssemblyFileVersionAttribute"].Should().Be("1.2.3.0");
            info["AssemblyInformationalVersionAttribute"].Should().Be("1.2.3");
        }

        [WindowsOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        [InlineData("net45")]
        public void It_respects_version_changes_on_incremental_build(string targetFramework)
        {
            if (targetFramework == "net45")
            {
                return;
            }

            // Given a project that has already been built
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource();
            BuildProject(versionPrefix: "1.2.3");

            // When the same project is built again using a different VersionPrefix property
            var incrementalBuildCommand = BuildProject(versionPrefix: "1.2.4");

            // Then the version of the built assembly shall match the provided VersionPrefix
            var assemblyPath = Path.Combine(incrementalBuildCommand.GetOutputDirectory(targetFramework).FullName, "HelloWorld.dll");
            var info = AssemblyInfo.Get(assemblyPath);
            info["AssemblyVersionAttribute"].Should().Be("1.2.4.0");

            BuildCommand BuildProject(string versionPrefix)
            {
                var command = new BuildCommand(testAsset);
                command.Execute($"/p:OutputType=Library", $"/p:TargetFramework={targetFramework}", $"/p:VersionPrefix={versionPrefix}")
                       .Should()
                       .Pass();
                return command;
            }
        }

        [Fact]
        public void It_respects_custom_assembly_attribute_items_on_incremental_build()
        {
            var targetFramework = "netstandard1.5";
            var testAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink", identifier: targetFramework)
                .WithSource();

            var firstBuildCommand = BuildProject(buildNumber: "1");
            var assemblyPath = Path.Combine(firstBuildCommand.GetOutputDirectory(targetFramework).FullName, "TestLibrary.dll");
            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("BuildNumber:1");

            var firstWriteTime = File.GetLastWriteTimeUtc(assemblyPath);

            // When rebuilding with the same value
            BuildProject(buildNumber: "1");

            // the build should no-op.
            File.GetLastWriteTimeUtc(assemblyPath).Should().Be(firstWriteTime);

            // When the same project is built again using a different build number
            BuildProject(buildNumber: "2");

            // the file should change
            File.GetLastWriteTimeUtc(assemblyPath).Should().NotBe(firstWriteTime);

            // and the custom assembly should be generated with the updated value.
            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("BuildNumber:2");

            BuildCommand BuildProject(string buildNumber)
            {
                var command = new BuildCommand(testAsset, "TestLibrary");
                command.Execute($"/p:BuildNumber={buildNumber}")
                       .Should()
                       .Pass();
                return command;
            }
        }

        [Fact]
        public void It_includes_internals_visible_to()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netstandard2.0")
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "InternalsVisibleTo",
                                new XAttribute("Include", "Tests"))));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "HelloWorld.dll");

            AssemblyInfo.Get(assemblyPath)["InternalsVisibleToAttribute"].Should().Be("Tests");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(true, true, "net5.0")]
        [InlineData(true, true, ToolsetInfo.CurrentTargetFramework)]
        [InlineData(true, false, ToolsetInfo.CurrentTargetFramework)]
        [InlineData(false, false, ToolsetInfo.CurrentTargetFramework)]
        public void TestPreviewFeatures(bool enablePreviewFeatures, bool generateRequiresPreviewFeaturesAttribute, string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: $"{enablePreviewFeatures}${generateRequiresPreviewFeaturesAttribute}${targetFramework}")
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement(ns + "EnablePreviewFeatures", $"{enablePreviewFeatures}")));

                    if (enablePreviewFeatures && !generateRequiresPreviewFeaturesAttribute)
                    {
                        project.Root.Add(
                            new XElement(ns + "PropertyGroup",
                                new XElement(ns + "GenerateRequiresPreviewFeaturesAttribute", $"False")));
                    }
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, "HelloWorld.dll");

            var parameterlessAttributes = AssemblyInfo.GetParameterlessAttributes(assemblyPath);
            bool contains = false;
            foreach (var attribute in parameterlessAttributes)
            {
                if (attribute.Equals("RequiresPreviewFeaturesAttribute", System.StringComparison.Ordinal))
                {
                    contains = true;
                    break;
                }
            }

            var getValuesCommand = new GetValuesCommand(testAsset, "LangVersion", targetFramework: targetFramework);
            getValuesCommand.Execute().Should().Pass();

            var values = getValuesCommand.GetValues();
            var langVersion = values.FirstOrDefault() ?? string.Empty;

            if (enablePreviewFeatures && generateRequiresPreviewFeaturesAttribute)
            {
                if (targetFramework == ToolsetInfo.CurrentTargetFramework)
                {
                    Assert.Equal("Preview", langVersion);
                    Assert.True(contains);
                }
                else
                {
                    // The assembly level attribute is generated only for the latest TFM for the given sdk
                    Assert.False(contains);
                    Assert.NotEqual("Preview", langVersion);
                }
            }

            if (!generateRequiresPreviewFeaturesAttribute)
            {
                Assert.False(contains);
            }
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_doesnt_includes_requires_preview_features()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement(ns + "EnablePreviewFeatures", "false")));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework).FullName, "HelloWorld.dll");

            var parameterlessAttributes = AssemblyInfo.GetParameterlessAttributes(assemblyPath);
            bool contains = false;
            foreach (var attribute in parameterlessAttributes)
            {
                if (attribute.Equals("RequiresPreviewFeaturesAttribute", System.StringComparison.Ordinal))
                {
                    contains = true;
                    break;
                }
            }

            Assert.False(contains);
        }

        [Theory]
        [InlineData(true, true, "net6.0", false)]
        [InlineData(true, false, "net6.0", false)]
        [InlineData(true, null, "net6.0", false)]
        [InlineData(false, false, "net6.0", false)]
        [InlineData(false, null, "net6.0", false)]
        [InlineData(true, true, "net7.0", true)]
        [InlineData(true, false, "net7.0", false)]
        [InlineData(true, null, "net7.0", true)]
        [InlineData(false, false, "net7.0", false)]
        [InlineData(false, null, "net7.0", false)]
        [InlineData(true, true, ToolsetInfo.CurrentTargetFramework, true)]
        [InlineData(true, false, ToolsetInfo.CurrentTargetFramework, false)]
        [InlineData(true, null, ToolsetInfo.CurrentTargetFramework, true)]
        [InlineData(false, false, ToolsetInfo.CurrentTargetFramework, false)]
        [InlineData(false, null, ToolsetInfo.CurrentTargetFramework, false)]
        public void TestDisableRuntimeMarshalling(bool disableRuntimeMarshalling, bool? generateDisableRuntimeMarshallingAttribute, string targetFramework, bool shouldHaveAttribute)
        {
            var testProject = new TestProject()
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties["DisableRuntimeMarshalling"] = disableRuntimeMarshalling.ToString();
            if (generateDisableRuntimeMarshallingAttribute.HasValue)
            {
                testProject.AdditionalProperties["GenerateDisableRuntimeMarshallingAttribute"] = generateDisableRuntimeMarshallingAttribute.Value.ToString();
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: disableRuntimeMarshalling.ToString() + "_" + generateDisableRuntimeMarshallingAttribute + "_" + targetFramework);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, "HelloWorld.dll");

            var parameterlessAttributes = AssemblyInfo.GetParameterlessAttributes(assemblyPath);
            bool contains = false;
            foreach (var attribute in parameterlessAttributes)
            {
                if (attribute.Equals("DisableRuntimeMarshallingAttribute", System.StringComparison.Ordinal))
                {
                    contains = true;
                    break;
                }
            }

            if (shouldHaveAttribute)
            {
                Assert.True(contains);
            }
            else
            {
                // The assembly level attribute is generated only for .NET 7 and newer
                Assert.False(contains);
            }
        }

        [Fact]
        public void It_respects_out_out_of_internals_visible_to()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netstandard2.0")
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement(ns + "GenerateInternalsVisibleToAttributes", "false")),
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "InternalsVisibleTo",
                                new XAttribute("Include", "Tests"))));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "HelloWorld.dll");

            Assert.False(AssemblyInfo.Get(assemblyPath).ContainsKey("InternalsVisibleToAttribute"));
        }

        [Fact]
        public void It_includes_internals_visible_to_with_key()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netstandard2.0")
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "InternalsVisibleTo",
                                new XAttribute("Include", "Tests"),
                                new XAttribute("Key", "00240000048000009400000006020000002400005253413100040000010001001d3e6bbb36e11ea61ceff6e1022b23dd779fc6230838db2d25a2c7c8433b3fcf86b16c25b281fc3db1027c0675395e7d0548e6add88b6a811962bf958101fa9e243b1618313bee11f5e3b3fefda7b1d1226311b6cc2d07e87ff893ba6890b20082df34a0aac14b605b8be055e81081a626f8c69e9ed4bbaa4eae9f94a35accd2"))));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "HelloWorld.dll");

            AssemblyInfo.Get(assemblyPath)["InternalsVisibleToAttribute"].Should().Be("Tests, PublicKey=00240000048000009400000006020000002400005253413100040000010001001d3e6bbb36e11ea61ceff6e1022b23dd779fc6230838db2d25a2c7c8433b3fcf86b16c25b281fc3db1027c0675395e7d0548e6add88b6a811962bf958101fa9e243b1618313bee11f5e3b3fefda7b1d1226311b6cc2d07e87ff893ba6890b20082df34a0aac14b605b8be055e81081a626f8c69e9ed4bbaa4eae9f94a35accd2");
        }

        [Fact]
        public void It_includes_internals_visible_to_with_project_publickey()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netstandard2.0")
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement(ns + "PublicKey", "00240000048000009400000006020000002400005253413100040000010001001d3e6bbb36e11ea61ceff6e1022b23dd779fc6230838db2d25a2c7c8433b3fcf86b16c25b281fc3db1027c0675395e7d0548e6add88b6a811962bf958101fa9e243b1618313bee11f5e3b3fefda7b1d1226311b6cc2d07e87ff893ba6890b20082df34a0aac14b605b8be055e81081a626f8c69e9ed4bbaa4eae9f94a35accd2")),
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "InternalsVisibleTo",
                                new XAttribute("Include", "Tests"))));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "HelloWorld.dll");

            AssemblyInfo.Get(assemblyPath)["InternalsVisibleToAttribute"].Should().Be("Tests, PublicKey=00240000048000009400000006020000002400005253413100040000010001001d3e6bbb36e11ea61ceff6e1022b23dd779fc6230838db2d25a2c7c8433b3fcf86b16c25b281fc3db1027c0675395e7d0548e6add88b6a811962bf958101fa9e243b1618313bee11f5e3b3fefda7b1d1226311b6cc2d07e87ff893ba6890b20082df34a0aac14b605b8be055e81081a626f8c69e9ed4bbaa4eae9f94a35accd2");
        }

        [Fact]
        public void It_includes_assembly_metadata()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netstandard2.0")
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "AssemblyMetadata",
                                new XAttribute("Include", "MetadataKey"),
                                new XAttribute("Value", "MetadataValue"))));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "HelloWorld.dll");

            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("MetadataKey:MetadataValue");
        }

        [Fact]
        public void It_respects_out_out_of_assembly_metadata()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netstandard2.0")
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement(ns + "GenerateAssemblyMetadataAttributes", "false")),
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "AssemblyMetadata",
                                new XAttribute("Include", "MetadataKey"),
                                new XAttribute("Value", "MetadataValue"))));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "HelloWorld.dll");

            Assert.False(AssemblyInfo.Get(assemblyPath).ContainsKey("AssemblyMetadataAttribute"));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void GenerateUserSecrets(bool referenceAspNetCore, bool referenceExtensionsUserSecrets, bool shouldHaveAttribute)
        {
            var testProject = new TestProject()
            {
                Name = "UserSecretTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            testProject.AdditionalProperties["UserSecretsId"] = "SecretsIdValue";

            if (referenceAspNetCore)
            {
                testProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");
            }
            if (referenceExtensionsUserSecrets)
            {
                testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Extensions.Configuration.UserSecrets", "3.0.0"));
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: referenceAspNetCore.ToString() + referenceExtensionsUserSecrets.ToString())
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName, testProject.Name + ".dll");

            if (shouldHaveAttribute)
            {
                AssemblyInfo.Get(assemblyPath)["UserSecretsIdAttribute"].Should().Be("SecretsIdValue");
            }
            else
            {
                AssemblyInfo.Get(assemblyPath).Should().NotContainKey("SecretsIdValue");
            }
        }

        [Fact]
        public void GenerateUserSecretsForTestProject()
        {
            //  Test the scenario where a test project references a web app and uses user secrets.
            var testProject = new TestProject()
            {
                Name = "WebApp",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };
            testProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");

            var testTestProject = new TestProject()
            {
                Name = "WebAppTests",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                ReferencedProjects = { testProject }
            };

            var testAsset = _testAssetsManager.CreateTestProject(testTestProject);

            File.WriteAllText(Path.Combine(testAsset.TestRoot, "Directory.Build.props"), @"
<Project>
  <PropertyGroup>
    <UserSecretsId>SecretsIdValue</UserSecretsId>
  </PropertyGroup>

</Project>
");
            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute("/restore")
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(testTestProject.TargetFrameworks).FullName, testTestProject.Name + ".dll");

            AssemblyInfo.Get(assemblyPath)["UserSecretsIdAttribute"].Should().Be("SecretsIdValue");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_includes_repository_url(bool privateRepo)
        {
            var fakeUrl = "fakeUrl";
            var testProject = new TestProject()
            {
                Name = "RepoUrlProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            if (privateRepo)
            {
                testProject.AdditionalProperties["PublishRepositoryUrl"] = "true";
                testProject.AdditionalProperties["PrivateRepositoryUrl"] = fakeUrl;
            }
            else
            {
                testProject.AdditionalProperties["RepositoryUrl"] = fakeUrl;
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: privateRepo.ToString());

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName, testProject.Name + ".dll");

            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("RepositoryUrl:" + fakeUrl);
        }

        [Theory]
        [InlineData("net40", false)]
        [InlineData("net45", true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        public void It_does_not_write_to_undefined_assembly_metadata_attribute(string targetFramework, bool containsAttribute)
        {
            var fakeUrl = "fakeUrl";
            var testProject = new TestProject()
            {
                Name = "RepoUrlProject",
                TargetFrameworks = targetFramework
            };

            testProject.AdditionalProperties["RepositoryUrl"] = fakeUrl;

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, testProject.Name + ".dll");

            if (containsAttribute)
            {
                AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("RepositoryUrl:" + fakeUrl);
            }
            else
            {
                AssemblyInfo.Get(assemblyPath).ContainsKey("AssemblyMetadataAttribute").Should().Be(false);
            }
        }

        [Theory]
        [InlineData("netcoreapp3.1", ".NET Core 3.1")]
        [InlineData("netcoreapp2.1", ".NET Core 2.1")]
        [InlineData("netstandard2.1", ".NET Standard 2.1")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, $".NET {ToolsetInfo.CurrentTargetFrameworkVersion}")]
        public void CheckTargetFrameworkDisplayName(string targetFrameworkVersion, string expectedFrameworkDisplayName)
        {
            TestProject libraryProject = new TestProject()
            {
                Name = "LibraryProject",
                TargetFrameworks = targetFrameworkVersion
            };
            libraryProject.AdditionalProperties["NoWarn"] = "NETSDK1138";
            libraryProject.SourceFiles["Class.cs"] = @"
public class LibraryClass{}
";

            TestProject testProject = new TestProject()
            {
                Name = "HelloWorld",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.ReferencedProjects.Add(libraryProject);
            testProject.SourceFiles["Program.cs"] = @"
using System;
using System.Reflection;
using System.Runtime.Versioning;

class Program
{
    static void Main(string[] args)
    {
        var str = typeof(LibraryClass).Assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkDisplayName;
        Console.WriteLine(str);
    }
}";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFrameworkVersion);
            var buildCommand = new BuildCommand(testAsset);
            buildCommand.WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(Path.Combine(testAsset.Path, testProject.Name))
                .Execute();
            result.Should().Pass();
            result.StdOut.Should().BeEquivalentTo(expectedFrameworkDisplayName);

        }

    }
}
