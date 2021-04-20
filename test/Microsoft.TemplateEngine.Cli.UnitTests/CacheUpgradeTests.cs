// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class CacheUpgradeTests
    {
        [Fact(DisplayName = nameof(CanReadUnversionedCache))]
        public void CanReadUnversionedCache()
        {
            IEngineEnvironmentSettings mockEnvironmentSettings = new MockEngineEnvironmentSettings();
            TemplateCache cache = new TemplateCache(mockEnvironmentSettings, CacheDataOriginalStyle);

            Assert.Equal(3, cache.TemplateInfo.Count);
        }

        [Fact(DisplayName = nameof(CanReadVersion1000Cache))]
        public void CanReadVersion1000Cache()
        {
            IEngineEnvironmentSettings mockEnvironmentSettings = new MockEngineEnvironmentSettings();
            TemplateCache cache = new TemplateCache(mockEnvironmentSettings, CacheDataVersion1000);

            Assert.Equal(3, cache.TemplateInfo.Count);
        }

        private static JObject CacheDataVersion1000
        {
            get
            {
                string configString = @"{
  ""Version"": ""1.0.0.0"",
  ""TemplateInfo"": [
    {
      ""ConfigMountPointId"": ""23c0e74b-9815-4e6b-bf19-c8a6efa8ba85"",
      ""Author"": ""Microsoft"",
      ""Classifications"": [
        ""Config""
      ],
      ""DefaultName"": null,
      ""Description"": """",
      ""Identity"": ""Microsoft.Standard.QuickStarts.Nuget.Config"",
      ""GeneratorId"": ""0c434df7-e2cb-4dee-b216-d7c58c8eb4b3"",
      ""GroupIdentity"": ""ItemNugetConfig"",
      ""Precedence"": 100,
      ""Name"": ""NuGet Config"",
      ""ShortName"": ""nugetconfig"",
      ""Tags"": {
                ""type"": {
                    ""Description"": null,
          ""ChoicesAndDescriptions"": {
                        ""item"": """"
          },
          ""DefaultValue"": ""item""
                }
            },
      ""CacheParameters"": { },
      ""ConfigPlace"": ""/content/Nuget/.template.config/template.json"",
      ""LocaleConfigMountPointId"": ""00000000-0000-0000-0000-000000000000"",
      ""LocaleConfigPlace"": null,
      ""HostConfigMountPointId"": ""23c0e74b-9815-4e6b-bf19-c8a6efa8ba85"",
      ""HostConfigPlace"": ""/content/Nuget/.template.config/dotnetcli.host.json""
    },
    {
      ""ConfigMountPointId"": ""c22eeb1a-4fb0-4c52-991f-8d1b4f2448c3"",
      ""Author"": ""Microsoft"",
      ""Classifications"": [
        ""Common"",
        ""Library""
      ],
      ""DefaultName"": null,
      ""Description"": """",
      ""Identity"": ""Microsoft.Common.Library.CSharp"",
      ""GeneratorId"": ""0c434df7-e2cb-4dee-b216-d7c58c8eb4b3"",
      ""GroupIdentity"": ""Microsoft.Common.Library"",
      ""Precedence"": 100,
      ""Name"": ""Class Library"",
      ""ShortName"": ""classlib"",
      ""Tags"": {
        ""language"": {
          ""Description"": null,
          ""ChoicesAndDescriptions"": {
            ""C#"": """"
          },
          ""DefaultValue"": ""C#""
        },
        ""type"": {
          ""Description"": null,
          ""ChoicesAndDescriptions"": {
            ""project"": """"
          },
          ""DefaultValue"": ""project""
        },
        ""Framework"": {
          ""Description"": """",
          ""ChoicesAndDescriptions"": {
            ""netcoreapp1.0"": ""Target netcoreapp1.0"",
            ""netcoreapp1.1"": ""Target netcoreapp1.1"",
            ""netstandard1.0"": ""Target netstandard1.0"",
            ""netstandard1.1"": ""Target netstandard1.1"",
            ""netstandard1.2"": ""Target netstandard1.2"",
            ""netstandard1.3"": ""Target netstandard1.3"",
            ""netstandard1.4"": ""Target netstandard1.4"",
            ""netstandard1.5"": ""Target netstandard1.5"",
            ""netstandard1.6"": ""Target netstandard1.6""
          },
          ""DefaultValue"": ""netstandard1.4""
        }
      },
      ""CacheParameters"": {},
      ""ConfigPlace"": ""/content/ClassLibrary-CSharp/.template.config/template.json"",
      ""LocaleConfigMountPointId"": ""00000000-0000-0000-0000-000000000000"",
      ""LocaleConfigPlace"": null,
      ""HostConfigMountPointId"": ""c22eeb1a-4fb0-4c52-991f-8d1b4f2448c3"",
      ""HostConfigPlace"": ""/content/ClassLibrary-CSharp/.template.config/dotnetcli.host.json""
    },
    {
      ""ConfigMountPointId"": ""c22eeb1a-4fb0-4c52-991f-8d1b4f2448c3"",
      ""Author"": ""Microsoft"",
      ""Classifications"": [
        ""Common"",
        ""Library""
      ],
      ""DefaultName"": null,
      ""Description"": """",
      ""Identity"": ""Microsoft.Common.Library.FSharp"",
      ""GeneratorId"": ""0c434df7-e2cb-4dee-b216-d7c58c8eb4b3"",
      ""GroupIdentity"": ""Microsoft.Common.Library"",
      ""Precedence"": 100,
      ""Name"": ""Class Library"",
      ""ShortName"": ""classlib"",
      ""Tags"": {
        ""language"": {
          ""Description"": null,
          ""ChoicesAndDescriptions"": {
            ""F#"": """"
          },
          ""DefaultValue"": ""F#""
        },
        ""type"": {
          ""Description"": null,
          ""ChoicesAndDescriptions"": {
            ""project"": """"
          },
          ""DefaultValue"": ""project""
        }
      },
      ""CacheParameters"": {},
      ""ConfigPlace"": ""/content/ClassLibrary-FSharp/.template.config/template.json"",
      ""LocaleConfigMountPointId"": ""00000000-0000-0000-0000-000000000000"",
      ""LocaleConfigPlace"": null,
      ""HostConfigMountPointId"": ""c22eeb1a-4fb0-4c52-991f-8d1b4f2448c3"",
      ""HostConfigPlace"": ""/content/ClassLibrary-FSharp/.template.config/dotnetcli.host.json""
    }
  ],
  ""CacheVersion"": ""1.0.0.0""
}";
                return JObject.Parse(configString);
            }
        }

        private static JObject CacheDataOriginalStyle
        {
            get
            {
                string configString = @"{
  ""TemplateInfo"": [
    {
      ""ConfigMountPointId"": ""23c0e74b-9815-4e6b-bf19-c8a6efa8ba85"",
      ""Author"": ""Microsoft"",
      ""Classifications"": [
        ""Config""
      ],
      ""DefaultName"": null,
      ""Description"": """",
      ""Identity"": ""Microsoft.Standard.QuickStarts.Nuget.Config"",
      ""GeneratorId"": ""0c434df7-e2cb-4dee-b216-d7c58c8eb4b3"",
      ""GroupIdentity"": ""ItemNugetConfig"",
      ""Name"": ""NuGet Config"",
      ""ShortName"": ""nugetconfig"",
      ""Tags"": {
                ""type"": ""item""
      },
      ""ConfigPlace"": ""/content/Nuget/.template.config/template.json"",
      ""LocaleConfigMountPointId"": ""00000000-0000-0000-0000-000000000000"",
      ""LocaleConfigPlace"": null,
    },
    {
      ""ConfigMountPointId"": ""c22eeb1a-4fb0-4c52-991f-8d1b4f2448c3"",
      ""Author"": ""Microsoft"",
      ""Classifications"": [
        ""Common"",
        ""Library""
      ],
      ""DefaultName"": null,
      ""Description"": """",
      ""Identity"": ""Microsoft.Common.Library.CSharp"",
      ""GeneratorId"": ""0c434df7-e2cb-4dee-b216-d7c58c8eb4b3"",
      ""GroupIdentity"": ""Microsoft.Common.Library"",
      ""Name"": ""Class Library"",
      ""ShortName"": ""classlib"",
      ""Tags"": {
        ""language"": ""C#"",
        ""type"": ""project""
      },
      ""ConfigPlace"": ""/content/ClassLibrary-CSharp/.template.config/template.json"",
      ""LocaleConfigMountPointId"": ""00000000-0000-0000-0000-000000000000"",
      ""LocaleConfigPlace"": null,
    },
    {
      ""ConfigMountPointId"": ""c22eeb1a-4fb0-4c52-991f-8d1b4f2448c3"",
      ""Author"": ""Microsoft"",
      ""Classifications"": [
        ""Common"",
        ""Library""
      ],
      ""DefaultName"": null,
      ""Description"": """",
      ""Identity"": ""Microsoft.Common.Library.FSharp"",
      ""GeneratorId"": ""0c434df7-e2cb-4dee-b216-d7c58c8eb4b3"",
      ""GroupIdentity"": ""Microsoft.Common.Library"",
      ""Name"": ""Class Library"",
      ""ShortName"": ""classlib"",
      ""Tags"": {
        ""language"": ""F#"",
        ""type"": ""project""
      },
      ""ConfigPlace"": ""/content/ClassLibrary-FSharp/.template.config/template.json"",
      ""LocaleConfigMountPointId"": ""00000000-0000-0000-0000-000000000000"",
      ""LocaleConfigPlace"": null,
    }
  ]
}";
                return JObject.Parse(configString);
            }
        }
    }
}
