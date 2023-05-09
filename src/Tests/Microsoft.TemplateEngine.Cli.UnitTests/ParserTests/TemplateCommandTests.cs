// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [UsesVerify]
    public class TemplateCommandTests
    {
        [Fact]
        public Task CannotCreateCommandForInvalidParameter()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameters("have:colon", "n1", "n2");

            var paramSymbolInfo = new Dictionary<string, string>()
            {
                { "longName", "name" },
                { "shortName", "n" }
            };
            var symbolInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                { "n1", paramSymbolInfo },
                { "n2", paramSymbolInfo }
            };

            IHostSpecificDataLoader hostDataLoader = A.Fake<IHostSpecificDataLoader>();
            A.CallTo(() => hostDataLoader.ReadHostSpecificTemplateData(template)).Returns(new HostSpecificTemplateData(symbolInfo));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, hostDataLoader))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new foo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            try
            {
                _ = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            }
            catch (InvalidTemplateParametersException e)
            {
                Assert.Equal(2, e.ParameterErrors.Count);
                Assert.Equal(templateGroup.Templates.Single(), e.Template);

                return Verify(e.Message);
            }

            Assert.True(false, "should not land here");
            return Task.FromResult(1);

        }

        [Fact]
        public async Task Constraints_WhenTheTemplateIsAllowed()
        {
            MockTemplateInfo template = new MockTemplateInfo(shortName: "test", identity: "testId1").WithConstraints(new TemplateConstraintInfo("test", "yes"));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var templateConstraintManager = new TemplateConstraintManager(settings);

            Assert.Empty(await TemplateCommand.ValidateConstraintsAsync(templateConstraintManager, template, default).ConfigureAwait(false));
        }

        [Fact]
        public async Task Constraints_WhenTheTemplateIsRestricted()
        {
            MockTemplateInfo template = new MockTemplateInfo(shortName: "test").WithConstraints(new TemplateConstraintInfo("test", "no"));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var templateConstraintManager = new TemplateConstraintManager(settings);

            Assert.NotEmpty(await TemplateCommand.ValidateConstraintsAsync(templateConstraintManager, template, default).ConfigureAwait(false));
        }

        [Fact]
        public async Task Constraints_WhenTheConstraintCannotBeEvaluated()
        {
            MockTemplateInfo template = new MockTemplateInfo(shortName: "test").WithConstraints(new TemplateConstraintInfo("test", "bad-arg"));
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var templateConstraintManager = new TemplateConstraintManager(settings);

            Assert.NotEmpty(await TemplateCommand.ValidateConstraintsAsync(templateConstraintManager, template, default).ConfigureAwait(false));
        }

    }
}
