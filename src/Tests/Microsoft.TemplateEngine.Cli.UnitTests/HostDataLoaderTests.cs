// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class HostDataLoaderTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public HostDataLoaderTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public void CanLoadHostDataFile()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);
            Assert.True(engineEnvironmentSettings.TryGetMountPoint(Directory.GetCurrentDirectory(), out IMountPoint? mountPoint));
            Assert.NotNull(mountPoint);
            IFile? dataFile = mountPoint!.FileInfo("/Resources/dotnetcli.host.json");

            ITemplateInfo template = A.Fake<ITemplateInfo>();
            A.CallTo(() => template.MountPointUri).Returns(Directory.GetCurrentDirectory());
            A.CallTo(() => template.HostConfigPlace).Returns("/Resources/dotnetcli.host.json");

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.NotNull(data);

            Assert.False(data.IsHidden);
            Assert.Equal(2, data.UsageExamples?.Count);
            Assert.NotNull(data.UsageExamples);
            Assert.Contains("--framework netcoreapp3.1 --langVersion '9.0'", data.UsageExamples);
            Assert.Equal(4, data.SymbolInfo?.Count);
            Assert.Contains("TargetFrameworkOverride", data.HiddenParameterNames);
            Assert.Contains("Framework", data.ParametersToAlwaysShow);
            Assert.True(data.LongNameOverrides.ContainsKey("skipRestore"));
            Assert.Equal("no-restore", data.LongNameOverrides["skipRestore"]);
            Assert.True(data.ShortNameOverrides.ContainsKey("skipRestore"));
            Assert.Equal("", data.ShortNameOverrides["skipRestore"]);
            Assert.Equal("no-restore", data.DisplayNameForParameter("skipRestore"));
        }

        [Fact]
        public void CanReadHostDataFromITemplateInfo()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);
            Assert.True(engineEnvironmentSettings.TryGetMountPoint(Directory.GetCurrentDirectory(), out IMountPoint? mountPoint));
            Assert.NotNull(mountPoint);
            IFile? dataFile = mountPoint!.FileInfo("/Resources/dotnetcli.host.json");
            Assert.NotNull(dataFile);
            using Stream s = dataFile.OpenRead();
            using TextReader tr = new StreamReader(s, Encoding.UTF8, true);

            string json = tr.ReadToEnd();
            ITemplateInfo template = A.Fake<ITemplateInfo>(builder => builder.Implements<ITemplateInfoHostJsonCache>().Implements<ITemplateInfo>());
            A.CallTo(() => ((ITemplateInfoHostJsonCache)template).HostData).Returns(json);

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.NotNull(data);

            Assert.False(data.IsHidden);
            Assert.Equal(2, data.UsageExamples?.Count);
            Assert.NotNull(data.UsageExamples);
            Assert.Contains("--framework netcoreapp3.1 --langVersion '9.0'", data.UsageExamples);
            Assert.Equal(4, data.SymbolInfo?.Count);
            Assert.Contains("TargetFrameworkOverride", data.HiddenParameterNames);
            Assert.Contains("Framework", data.ParametersToAlwaysShow);
            Assert.True(data.LongNameOverrides.ContainsKey("skipRestore"));
            Assert.Equal("no-restore", data.LongNameOverrides["skipRestore"]);
            Assert.True(data.ShortNameOverrides.ContainsKey("skipRestore"));
            Assert.Equal("", data.ShortNameOverrides["skipRestore"]);
            Assert.Equal("no-restore", data.DisplayNameForParameter("skipRestore"));
        }

        [Fact]
        public void ReturnDefaultForInvalidEntry()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);

            ITemplateInfo template = A.Fake<ITemplateInfo>(builder => builder.Implements<ITemplateInfoHostJsonCache>().Implements<ITemplateInfo>());
            A.CallTo(() => ((ITemplateInfoHostJsonCache)template).HostData).Returns(null);

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.NotNull(data);
            Assert.Equal(HostSpecificTemplateData.Default, data);
        }

        [Fact]
        public void ReturnDefaultForInvalidFile()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);

            ITemplateInfo template = A.Fake<ITemplateInfo>();
            A.CallTo(() => template.MountPointUri).Returns(Directory.GetCurrentDirectory());
            A.CallTo(() => template.HostConfigPlace).Returns("unknown");

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.NotNull(data);
            Assert.Equal(HostSpecificTemplateData.Default, data);
        }

        [Fact]
        public void CanSerializeData()
        {
            var usageExamples = new[] { "example1" };
            var symbolInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>()
            {
                {
                    "param1",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "longParam1" },
                        { "shortName", "shortParam1" }
                    }
                },
                {
                    "param2",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "" },
                        { "shortName", "shortParam2" }
                    }
                },
                {
                    "param3",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "true" }
                    }
                }
            };
            var data = new HostSpecificTemplateData(symbolInfo, usageExamples, isHidden: true);
            var serialized = JObject.FromObject(data);

            Assert.NotNull(serialized);
            Assert.Equal(3, serialized.Children().Count());

            Assert.Single<JProperty>(serialized.Properties(), p => p.Name == "UsageExamples");
            Assert.Single<JProperty>(serialized.Properties(), p => p.Name == "SymbolInfo");
            Assert.Single<JProperty>(serialized.Properties(), p => p.Name == "IsHidden");
        }

        [Fact]
        public void CanSerializeData_SkipsEmpty()
        {
            var usageExamples = Array.Empty<string>();
            var symbolInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>()
            {
                {
                    "param1",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "longParam1" },
                        { "shortName", "shortParam1" }
                    }
                },
                {
                    "param2",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "" },
                        { "shortName", "shortParam2" }
                    }
                },
                {
                    "param3",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "true" }
                    }
                }
            };
            var data = new HostSpecificTemplateData(symbolInfo, usageExamples, isHidden: false);
            var serialized = JObject.FromObject(data);

            Assert.NotNull(serialized);
            Assert.Single(serialized.Children());

            Assert.Single<JProperty>(serialized.Properties(), p => p.Name == "SymbolInfo");

            var symbolInfoArray = serialized.Properties().Single().Value as JObject;
            Assert.NotNull(symbolInfoArray);
            //empty values should stay when deserializing symbol info
            Assert.Equal(3, ((JObject)symbolInfoArray!["param1"]!).Properties().Count());
            Assert.Equal("", symbolInfoArray!["param2"]!["longName"]);
            Assert.Equal(3, ((JObject)symbolInfoArray!["param2"]!).Properties().Count());
            Assert.Single(((JObject)symbolInfoArray!["param3"]!).Properties());

            Assert.DoesNotContain(serialized.Properties(), p => p.Name == "IsHidden");
            Assert.DoesNotContain(serialized.Properties(), p => p.Name == "UsageExamples");
        }
    }
}
