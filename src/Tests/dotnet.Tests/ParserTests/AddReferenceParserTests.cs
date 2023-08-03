// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLineValidation;
using Microsoft.DotNet.Tools.Common;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class AddReferenceParserTests
    {
        private readonly ITestOutputHelper output;

        public AddReferenceParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void AddReferenceHasDefaultArgumentSetToCurrentDirectory()
        {
            var result = Parser.Instance.Parse("dotnet add reference my.csproj");

            result.GetValue<string>(AddCommandParser.ProjectArgument)
                .Should()
                .BeEquivalentTo(
                    PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
        }

        [Fact]
        public void AddReferenceHasInteractiveFlag()
        {
            var result = Parser.Instance.Parse("dotnet add reference my.csproj --interactive");

            result.GetValue<bool>(AddProjectToProjectReferenceParser.InteractiveOption)
                .Should().BeTrue();
        }

        [Fact]
        public void AddReferenceDoesNotHaveInteractiveFlagByDefault()
        {
            var result = Parser.Instance.Parse("dotnet add reference my.csproj");

            result.GetValue<bool>(AddProjectToProjectReferenceParser.InteractiveOption)
                .Should().BeFalse();
        }

        [Fact]
        public void AddReferenceWithoutArgumentResultsInAnError()
        {
            var result = Parser.Instance.Parse("dotnet add reference");

            result
                .Errors
                .Select(e => e.Message)
                .Should()
                .BeEquivalentTo(string.Format(LocalizableStrings.RequiredArgumentMissingForCommand, "'reference'."));
        }

        [Fact]
        public void EnumerablePackageIdFromQueryResponseResultsPackageIds()
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(_nugetResponseSample);
                writer.Flush();
                stream.Position = 0;

                AddPackageParser.EnumerablePackageIdFromQueryResponse(stream)
                    .Should()
                    .Contain(
                        new List<string>
                        { "System.Text.Json",
                            "System.Text.Json.Mobile" });
            }
        }

        private string _nugetResponseSample =
    @"{
    ""@context"": {
        ""@vocab"": ""http://schema.nuget.org/schema#""
    },
    ""totalHits"": 2,
    ""lastReopen"": ""2019-03-17T22:25:28.9238936Z"",
    ""index"": ""v3-lucene2-v2v3-20171018"",
    ""data"": [
        ""System.Text.Json"",
        ""System.Text.Json.Mobile""
    ]
}";
    }
}
