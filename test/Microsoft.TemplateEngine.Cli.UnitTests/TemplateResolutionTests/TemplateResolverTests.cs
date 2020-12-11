// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    // Implementation notes:
    // If a test is going to hit the secondary matching in the resolver, make sure to initialize the Tags & CacheParameters,
    //  otherwise an exception will be thrown in TemplateInfo.Parameters getter
    //  (just about every situation will get to the secondary matching)
    // MockNewCommandInput doesn't support everything in the interface, just enough for this type of testing.
    public class TemplateResolverTests
    {
        [Fact(DisplayName = nameof(TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity))]
        public void TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity()
        {
            List<ITemplateMatchInfo> templatesToCheck = new List<ITemplateMatchInfo>();

            templatesToCheck.Add(new TemplateMatchInfo(
                new MockTemplateInfo("Template1", name: "Template1", identity: "Template1", groupIdentity: "TestGroup", precedence: 10), null));
            templatesToCheck.Add(new TemplateMatchInfo(
                new MockTemplateInfo("Template2", name: "Template2", identity: "Template2", groupIdentity: "TestGroup", precedence: 20), null));
            templatesToCheck.Add(new TemplateMatchInfo(
                new MockTemplateInfo("Template3", name: "Template3", identity: "Template3", groupIdentity: "TestGroup", precedence: 0), null));

            ITemplateMatchInfo highestPrecedenceTemplate = TemplateResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(templatesToCheck);
            Assert.NotNull(highestPrecedenceTemplate);
            Assert.Equal("Template2", highestPrecedenceTemplate.Info.Identity);
            Assert.Equal(20, highestPrecedenceTemplate.Info.Precedence);
        }

        [Fact(DisplayName = nameof(TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullIfGroupsAreDifferent))]
        public void TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullIfGroupsAreDifferent()
        {
            List<ITemplateMatchInfo> templatesToCheck = new List<ITemplateMatchInfo>();
            templatesToCheck.Add(new TemplateMatchInfo(
                new MockTemplateInfo("Template1", name: "Template1", identity: "Template1", groupIdentity: "TestGroup", precedence: 10), null));
            templatesToCheck.Add(new TemplateMatchInfo(
                new MockTemplateInfo("Template2", name: "Template2", identity: "Template2", groupIdentity: "RealGroup", precedence: 20), null));

            ITemplateMatchInfo highestPrecedenceTemplate = TemplateResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(templatesToCheck);
            Assert.Null(highestPrecedenceTemplate);
        }

        [Fact(DisplayName = nameof(TestPerformAllTemplatesInContextQuery))]
        public void TestPerformAllTemplatesInContextQuery()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("Template1", name: "Template1", identity: "Template1")
                                    .WithTag("type", "project"));
            templatesToSearch.Add(new MockTemplateInfo("Template2", name: "Template2", identity: "Template2")
                                    .WithTag("type", "item"));
            templatesToSearch.Add(new MockTemplateInfo("Template3", name: "Template3", identity: "Template3")
                                    .WithTag("type", "myType"));
            templatesToSearch.Add(new MockTemplateInfo("Template4", name: "Template4", identity: "Template4")
                                    .WithTag("type", "project"));
            templatesToSearch.Add(new MockTemplateInfo("Template5", name: "Template5", identity: "Template5")
                                     .WithTag("type", "project"));

            IHostSpecificDataLoader hostDataLoader = new MockHostSpecificDataLoader();

            IReadOnlyCollection<ITemplateMatchInfo> projectTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "project");
            Assert.Equal(3, projectTemplates.Count);
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template1", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template4", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template5", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<ITemplateMatchInfo> itemTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "item");
            Assert.Equal(1, itemTemplates.Count);
            Assert.True(itemTemplates.Where(x => string.Equals(x.Info.Identity, "Template2", StringComparison.Ordinal)).Any());

            //Visual Studio only supports "project" and "item", so using other types is no longer allowed, therefore "other" handling is removed.
            //support of match on custom type still remains
            IReadOnlyCollection<ITemplateMatchInfo> otherTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "other");
            Assert.Equal(0, otherTemplates.Count);
            Assert.False(otherTemplates.Where(x => string.Equals(x.Info.Identity, "Template3", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<ITemplateMatchInfo> customTypeTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "myType");
            Assert.Equal(1, customTypeTemplates.Count);
            Assert.True(customTypeTemplates.Where(x => string.Equals(x.Info.Identity, "Template3", StringComparison.Ordinal)).Any());
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_UniqueNameMatchesCorrectly))]
        public void TestPerformCoreTemplateQuery_UniqueNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("Template1", name: "Long name of Template1", identity: "Template1"));
            templatesToSearch.Add(new MockTemplateInfo("Template2", name: "Long name of Template2", identity: "Template2"));

            INewCommandInput userInputs = new MockNewCommandInput("Template2");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);

            Assert.Equal(1, matchResult.GetBestTemplateMatchList().Count);
            Assert.Equal("Template2", matchResult.GetBestTemplateMatchList()[0].Info.Identity);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("Template2", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_DefaultLanguageDisambiguates))]
        public void TestPerformCoreTemplateQuery_DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Description of foo Perl template", identity: "foo.test.Perl", groupIdentity: "foo.test.template")
                                      .WithTag("language", "Perl"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Description of foo LISP template", identity: "foo.test.Lisp", groupIdentity: "foo.test.template")
                                      .WithTag("language", "LISP"));

            INewCommandInput userInputs = new MockNewCommandInput("foo");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "Perl");
            Assert.Equal(1, matchResult.GetBestTemplateMatchList().Count);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.Perl", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_InputLanguageIsPreferredOverDefault))]
        public void TestPerformCoreTemplateQuery_InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Description of foo Perl template", identity: "foo.test.Perl", groupIdentity: "foo.test.template")
                                      .WithTag("language", "Perl"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Description of foo LISP template", identity: "foo.test.Lisp", groupIdentity: "foo.test.template")
                                      .WithTag("language", "LISP"));

            INewCommandInput userInputs = new MockNewCommandInput("foo", "LISP");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "Perl");

            Assert.Equal(1, matchResult.GetBestTemplateMatchList().Count);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.Lisp", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_GroupIsFound))]
        public void TestPerformCoreTemplateQuery_GroupIsFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template old", identity: "foo.test.old", groupIdentity: "foo.test.template", precedence: 100));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template new", identity: "foo.test.new", groupIdentity: "foo.test.template", precedence: 200));
            templatesToSearch.Add(new MockTemplateInfo("bar", name: "Bar template", identity: "bar.test", groupIdentity: "bar.test.template", precedence: 100));

            INewCommandInput userInputs = new MockNewCommandInput("foo");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Contains(unambiguousGroup, x => string.Equals(x.Info.Identity, "foo.test.old"));
            Assert.Contains(unambiguousGroup, x => string.Equals(x.Info.Identity, "foo.test.new"));
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_ParameterNameDisambiguates))]
        public void TestPerformCoreTemplateQuery_ParameterNameDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template old", identity: "foo.test.old", groupIdentity: "foo.test.template").WithParameters("bar"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template new", identity: "foo.test.new", groupIdentity: "foo.test.template").WithParameters("baz"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("baz", "whatever");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.new", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_ParameterValueDisambiguates))]
        public void TestPerformCoreTemplateQuery_ParameterValueDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template old", identity: "foo.test.old", groupIdentity: "foo.test.template", precedence: 100).WithTag("framework", "netcoreapp1.0", "netcoreapp1.1"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template new", identity: "foo.test.new", groupIdentity: "foo.test.template", precedence: 200).WithTag("framework", "netcoreapp2.0"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("framework", "netcoreapp1.0");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.old", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_UnknownParameterNameInvalidatesMatch))]
        public void TestPerformCoreTemplateQuery_UnknownParameterNameInvalidatesMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test", groupIdentity: "foo.test.template", precedence: 100).WithParameters("bar"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("baz");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);

            Assert.False(TemplateResolver.ValidateRemainingParameters(unambiguousGroup[0], out IReadOnlyList<string> invalidParams));
            Assert.Equal(1, invalidParams.Count);
            Assert.Equal("baz", invalidParams[0]);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_InvalidChoiceValueInvalidatesMatch))]
        public void TestPerformCoreTemplateQuery_InvalidChoiceValueInvalidatesMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test.1x", groupIdentity: "foo.test.template", precedence: 100).WithTag("framework", "netcoreapp1.0", "netcoreapp1.1"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test.2x", groupIdentity: "foo.test.template", precedence: 200).WithTag("framework", "netcoreapp2.0"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("framework", "netcoreapp3.0");


            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.Contains(unambiguousGroup[0].MatchDisposition, x => x.Kind == MatchKind.InvalidParameterValue);
            Assert.Contains(unambiguousGroup[1].MatchDisposition, x => x.Kind == MatchKind.InvalidParameterValue);
        }
    }
}
