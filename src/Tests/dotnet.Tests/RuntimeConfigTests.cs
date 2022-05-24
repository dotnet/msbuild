// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class RuntimeConfigTests : SdkTest
    {
        public RuntimeConfigTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        void ParseBasicRuntimeConfig()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, Basic);
            var runtimeConfig = new RuntimeConfig(tempPath);
            Asset(runtimeConfig);
        }

        [Fact]
        void ParseRuntimeConfigWithTrailingComma()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, TrailingComma);
            var runtimeConfig = new RuntimeConfig(tempPath);
            Asset(runtimeConfig);
        }

        [Fact]
        void ParseRuntimeConfigWithComment()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, WithComment);
            var runtimeConfig = new RuntimeConfig(tempPath);
            Asset(runtimeConfig);
        }

        [Fact]
        void ParseRuntimeConfigWithDifferentOrder()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, Order);
            var runtimeConfig = new RuntimeConfig(tempPath);
            Asset(runtimeConfig);
        }

        [Fact]
        void ParseRuntimeConfigWithDifferentCasingOnNameAndVersionField()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, CasingOnNameAndVersionField);
            var runtimeConfig = new RuntimeConfig(tempPath);
            Asset(runtimeConfig);
        }

        [Fact]
        void ParseRuntimeConfigWithDifferentCasingOnFrameworkField()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, CasingOnFrameworkField);
            var runtimeConfig = new RuntimeConfig(tempPath);
            runtimeConfig.Framework.Should().BeNull();
        }

        [Fact]
        void ParseRuntimeConfigWithDifferentCasingOnRuntimeOptionsField()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, CasingOnRuntimeOptionsField);
            var runtimeConfig = new RuntimeConfig(tempPath);
            runtimeConfig.Framework.Should().BeNull();
        }

        [Fact]
        void ParseRuntimeConfigWithEmpty()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, "");
            Action a = () => new RuntimeConfig(tempPath);
            a.ShouldThrow<System.Text.Json.JsonException>();
        }

        [Fact]
        void ParseRuntimeConfigWithDifferentWithExtraField()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, ExtraField);
            var runtimeConfig = new RuntimeConfig(tempPath);
            Asset(runtimeConfig);
        }

        [Fact]
        void ParseRuntimeConfigWithDifferentWithNoFramework()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, NoFramework);
            var runtimeConfig = new RuntimeConfig(tempPath);
            runtimeConfig.Framework.Should().BeNull();
            runtimeConfig.IsPortable.Should().BeFalse();
        }

        [Fact]
        void ParseRuntimeConfigWithDifferentWithMissingField()
        {
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, Missing);
            var runtimeConfig = new RuntimeConfig(tempPath);
            Asset(runtimeConfig);
        }

        private static void Asset(RuntimeConfig runtimeConfig)
        {
            runtimeConfig.Framework.Version.Should().Be("2.1.0");
            runtimeConfig.Framework.Name.Should().Be("Microsoft.NETCore.App");
            runtimeConfig.IsPortable.Should().BeTrue();
        }

        private string GetTempPath([CallerMemberName] string callingMethod = null)
        {
            return Path.Combine(_testAssetsManager.CreateTestDirectory(callingMethod).Path, Path.GetTempFileName());
        }

        private const string Basic =
            $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""2.1.0""
    }}
  }}
}}";

        private const string TrailingComma =
            $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""2.1.0"",
    }}
  }}
}}";

        private const string WithComment =
            $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""2.1.0"" // with comment
    }}
  }}
}}";

        private const string Order =
            $@"{{
  ""runtimeOptions"": {{
    ""framework"": {{
      ""version"": ""2.1.0"",
      ""name"": ""Microsoft.NETCore.App""
    }},
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}""
  }}
}}";

        private const string CasingOnNameAndVersionField =
            $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""framework"": {{
      ""Name"": ""Microsoft.NETCore.App"",
      ""Version"": ""2.1.0""
    }}
  }}
}}";

        private const string CasingOnFrameworkField =
            $@"{{
     ""runtimeOptions"": {{
       ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
       ""Framework"": {{
         ""name"": ""Microsoft.NETCore.App"",
         ""version"": ""2.1.0""
       }}
     }}
   }}";

        private const string CasingOnRuntimeOptionsField =
            $@"{{
  ""RuntimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""2.1.0""
    }}
  }}
}}";

        private const string ExtraField =
            $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""2.1.0""
    }},
    ""extra"": ""field""
  }}
}}";

        private const string Missing =
            @"{
  ""runtimeOptions"": {
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""2.1.0""
    }
  }
}";

        private const string NoFramework =
            @"{
  ""runtimeOptions"": {
  }
}";

    }
}
