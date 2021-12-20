// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public partial class InstantiateTests
    {
        private static readonly string NewLine = Environment.NewLine;

        public static IEnumerable<object[]> GetInvalidParametersTestData()
        {
            yield return new object[]
            {
                "foo --framework netcoreapp3.0",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                new string[][]
                {
                    new string[] { "value", "framework", "--framework", "netcoreapp3.0", $"'netcoreapp3.0' is not a valid value for --framework. The possible values are:{NewLine}   net5.0       {NewLine}   netcoreapp2.1{NewLine}   netcoreapp3.1" }
                }
            };

            yield return new object[]
            {
                "foo --framework net",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                new string[][]
                {
                    new string[] { "value", "framework", "--framework", "net", $"'net' is not a valid value for --framework. The possible values are:{NewLine}   net5.0       {NewLine}   netcoreapp2.1{NewLine}   netcoreapp3.1" }
                }
            };

            yield return new object[]
            {
                "foo --framework net",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp3.1"),
                },
                new string[][]
                {
                    new string[] { "value", "framework", "--framework", "net", $"'net' is not a valid value for --framework. The possible values are:{NewLine}   net5.0       {NewLine}   netcoreapp2.1{NewLine}   netcoreapp3.1" }
                }
            };

            yield return new object[]
            {
                "foo --framework net --fake fake --OtherChoice fake",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1").WithChoiceParameter("OtherChoice", "val1", "val2"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0").WithChoiceParameter("OtherChoice", "val1", "val2"),
                },
                new string?[][]
                {
                    new string?[] { "value", "framework", "--framework", "net", $"'net' is not a valid value for --framework. The possible values are:{NewLine}   net5.0       {NewLine}   netcoreapp2.1{NewLine}   netcoreapp3.1" },
                    new string?[] { "name", null, "--fake", null },
                    new string?[] { "name", null, "fake", null },
                    new string?[] { "value", "OtherChoice", "--OtherChoice", "fake", $"'fake' is not a valid value for --OtherChoice. The possible values are:{NewLine}   val1{NewLine}   val2" }
                }
            };
            yield return new object[]
            {
                "foo --int fake",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                        .WithParameter("int", paramType: "integer")
                },
                new string[][]
                {
                    new string[] { "value", "int", "--int", "fake", "Cannot parse argument 'fake' for option '--int' as expected type Int64." },
                }
            };
            yield return new object[]
            {
                "foo --int",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                        .WithParameter("int", paramType: "integer", defaultIfNoOptionValue: "fake")
                },
                new string?[][]
                {
                    new string?[] { "value", "int", "--int", null, "Cannot parse default if option without value 'fake' for option '--int' as expected type Int64." },
                }
            };
            //TODO: does not work, see https://github.com/dotnet/command-line-api/issues/1474
            //yield return new object[]
            //{
            //    "foo",
            //    new MockTemplateInfo[]
            //    {
            //        new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
            //            .WithParameter("int", paramType: "integer", defaultValue: "fake")
            //    },
            //    new string[][]
            //    {
            //        new string[] { "value", "int", "--int", "fake", "expected-error" },
            //    }
            //};

            yield return new object[]
            {
                "foo --langVersion",
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithParameter("langVersion"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                },
                new string?[][]
                {
                    new string?[] { "value", "langVersion", "--langVersion", null, "Required argument missing for option: '--langVersion'." }
                }
            };
        }

        [Theory]
        [MemberData(nameof(GetInvalidParametersTestData))]
        // invalid params:
        // [0] name / value - Kind
        // [1] canonical
        // [2] input format
        // [3] param value
        // [4] error message
        internal void CanEvaluateInvalidParameters(string command, MockTemplateInfo[] templates, string?[][] expectedInvalidParams)
        {
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(templates, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new {command}");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var templateCommands = instantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Empty(templateCommands);

            var templateMatchInfos = instantiateCommand.CollectTemplateMatchInfo(args, settings, templatePackageManager, templateGroup);
            var invalidOptions = InstantiateCommand.GetInvalidOptions(templateMatchInfos);
            Assert.Equal(expectedInvalidParams.Length, invalidOptions.Count);

            foreach (var invalidParam in expectedInvalidParams)
            {
                InvalidTemplateOptionResult.Kind expectedErrorKind = invalidParam[0] == "name"
                    ? InvalidTemplateOptionResult.Kind.InvalidName
                    : InvalidTemplateOptionResult.Kind.InvalidValue;

                string? expectedCanonicalName = invalidParam[1];
                string expectedInputFormat = invalidParam[2] ?? throw new Exception("Input Format cannot be null");
                string? expectedSpecifiedValue = invalidParam[3];
                string? expectedErrorMessage = null;
                if (invalidParam.Length == 5)
                {
                    expectedErrorMessage = invalidParam[4];
                }

                var actualParam = invalidOptions.Single(param => param.InputFormat == expectedInputFormat);

                Assert.Equal(expectedErrorKind, actualParam.ErrorKind);
                Assert.Equal(expectedCanonicalName, actualParam.TemplateOption?.TemplateParameter.Name);
                Assert.Equal(expectedInputFormat, actualParam.InputFormat);
                Assert.Equal(expectedSpecifiedValue, actualParam.SpecifiedValue);
                Assert.Equal(expectedErrorMessage, actualParam.ErrorMessage);
            }
        }
    }
}
