// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class TemplateMatchInfoExtensionTests
    {

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_ShortNameMismatchAndClassificationExact))]
        public void TestHasClassificationMatchAndNameMismatch_ShortNameMismatchAndClassificationExact()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.ShortName,
                Kind = MatchKind.Mismatch
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Exact
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.True(testTemplate.HasClassificationMatchAndNameMismatch());
        }

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_NameMismatchAndClassificationExact))]
        public void TestHasClassificationMatchAndNameMismatch_NameMismatchAndClassificationExact()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Name,
                Kind = MatchKind.Mismatch
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Exact
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.True(testTemplate.HasClassificationMatchAndNameMismatch());
        }

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_NameMismatchAndClassificationPartial))]
        public void TestHasClassificationMatchAndNameMismatch_NameMismatchAndClassificationPartial()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Name,
                Kind = MatchKind.Mismatch
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Partial
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.True(testTemplate.HasClassificationMatchAndNameMismatch());
        }

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_NameExactAndClassificationPartial))]
        public void TestHasClassificationMatchAndNameMismatch_NameExactAndClassificationPartial()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Name,
                Kind = MatchKind.Exact
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Partial
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.False(testTemplate.HasClassificationMatchAndNameMismatch());
        }

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_ShortNameExactAndClassificationPartial))]
        public void TestHasClassificationMatchAndNameMismatch_ShortNameExactAndClassificationPartial()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.ShortName,
                Kind = MatchKind.Exact
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Partial
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.False(testTemplate.HasClassificationMatchAndNameMismatch());
        }

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_ShortNameExactNameMismatchAndClassificationPartial))]
        public void TestHasClassificationMatchAndNameMismatch_ShortNameExactNameMismatchAndClassificationPartial()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.ShortName,
                Kind = MatchKind.Exact
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Name,
                Kind = MatchKind.Mismatch
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Partial
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.False(testTemplate.HasClassificationMatchAndNameMismatch());
        }

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_NameMismatchClassificationPartialLanguageMismatch))]
        public void TestHasClassificationMatchAndNameMismatch_NameMismatchClassificationPartialLanguageMismatch()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Name,
                Kind = MatchKind.Mismatch
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Partial
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Language,
                Kind = MatchKind.Mismatch
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.False(testTemplate.HasClassificationMatchAndNameMismatch());
        }

        [Fact(DisplayName = nameof(TestHasClassificationMatchAndNameMismatch_NameMismatchClassificationPartialOtherStartsWith))]
        public void TestHasClassificationMatchAndNameMismatch_NameMismatchClassificationPartialOtherStartsWith()
        {
            List<MatchInfo> testDispositions = new List<MatchInfo>();
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Name,
                Kind = MatchKind.Mismatch
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.Classification,
                Kind = MatchKind.Partial
            });
            testDispositions.Add(new MatchInfo()
            {
                Location = MatchLocation.OtherParameter,
                Kind = MatchKind.SingleStartsWith
            });

            ITemplateMatchInfo testTemplate = new TemplateMatchInfo(new MockTemplateInfo("Template1", groupIdentity: "TestGroup"), testDispositions);

            Assert.True(testTemplate.HasClassificationMatchAndNameMismatch());
        }
    }
}
