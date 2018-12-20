// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class ConfigCache_Tests
    {
        public static IEnumerable<object[]> CacheSerializationTestData
        {
            get
            {
                yield return new[] { new ConfigCache() };

                var brq1 = new BuildRequestConfiguration(
                    1,
                    new BuildRequestData("path1", new Dictionary<string, string> {["a1"] = "b1"}, Constants.defaultToolsVersion, new[] {"target1"}, null),
                    Constants.defaultToolsVersion);

                var configCache1 = new ConfigCache();
                configCache1.AddConfiguration(brq1.ShallowCloneWithNewId(1));

                yield return new[] {configCache1};

                var brq2 = new BuildRequestConfiguration(
                    2,
                    new BuildRequestData("path2", new Dictionary<string, string> { ["a2"] = "b2" }, Constants.defaultToolsVersion, new[] { "target2" }, null),
                    Constants.defaultToolsVersion);

                var configCache2 = new ConfigCache();
                configCache2.AddConfiguration(brq1.ShallowCloneWithNewId(1));
                configCache2.AddConfiguration(brq2.ShallowCloneWithNewId(2));

                var brq3 = new BuildRequestConfiguration(
                    3,
                    new BuildRequestData("path3", new Dictionary<string, string> { ["a3"] = "b3" }, Constants.defaultToolsVersion, new[] { "target3" }, null),
                    Constants.defaultToolsVersion);

                brq3.ProjectDefaultTargets = new List<string>{"target3"};
                brq3.ProjectInitialTargets = new List<string>{"targetInitial"};

                var configCache3 = new ConfigCache();
                configCache3.AddConfiguration(brq3.ShallowCloneWithNewId(3));

                yield return new[] { configCache3 };
            }
        }

        [Theory]
        [MemberData(nameof(CacheSerializationTestData))]
        public void ConfigCacheShouldBeTranslatable(object obj)
        {
            var initial = (ConfigCache) obj;

            TranslationHelpers.GetWriteTranslator().Translate(ref initial);

            ConfigCache copy = null;

            TranslationHelpers.GetReadTranslator().Translate(ref copy);

            // test _configurations
            var initialConfigurations = initial.GetEnumerator().ToArray();
            var copiedConfigurations = copy.GetEnumerator().ToArray();

            Assert.Equal(copiedConfigurations, initialConfigurations, EqualityComparer<BuildRequestConfiguration>.Default);

            // test _configurationIdsByMetadata
            copiedConfigurations.ShouldAllBe(config => initial.GetMatchingConfiguration(new ConfigurationMetadata(config)).Equals(config));
            initialConfigurations.ShouldAllBe(config => copy.GetMatchingConfiguration(new ConfigurationMetadata(config)).Equals(config));

            // test relevant fields not covered by BuildRequestConfiguration.Equals
            foreach (var initialConfiguration in initial)
            {
                copy[initialConfiguration.ConfigurationId].ProjectDefaultTargets.ShouldBe(initialConfiguration.ProjectDefaultTargets);
                copy[initialConfiguration.ConfigurationId].ProjectInitialTargets.ShouldBe(initialConfiguration.ProjectInitialTargets);
            }
        }
    }
}
