// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Framework.UnitTests
{
    public class EnvironmentVariableClassifier_Tests
    {
        [Fact]
        public void IsImmutable_CustomImmutableVariables_ExactMatchWorks()
        {
            var classifier = new EnvironmentVariableClassifier([
                "test_env_var_1",
                "test_env_var_2"
            ], null);

            classifier.IsImmutable("test_env_var_1").ShouldBeTrue();
            classifier.IsImmutable("test_env_var_2").ShouldBeTrue();
            classifier.IsImmutable("test_env_var_3").ShouldBeFalse();
            classifier.IsImmutable("non_immutable_var_1").ShouldBeFalse();
        }

        [Fact]
        public void IsImmutable_CustomPrefixes_WorksCorrectly()
        {
            var classifier = new EnvironmentVariableClassifier([], ["prefix_1", "prefix_2"]);

            // Custom prefixes should work
            classifier.IsImmutable("prefix_1").ShouldBeTrue();
            classifier.IsImmutable("prefix_1_var").ShouldBeTrue();
            classifier.IsImmutable("prefix_2_var").ShouldBeTrue();

            // Non-matching prefixes should not work
            classifier.IsImmutable("test_prefix_1_var").ShouldBeFalse();
            classifier.IsImmutable("non_immutable_var_1").ShouldBeFalse();
        }

        [Fact]
        public void IsImmutable_EmptyOrNullNames_ReturnsFalse()
        {
            var classifier = new EnvironmentVariableClassifier([
                "test_env_var_1"
            ], null);

            classifier.IsImmutable(null).ShouldBeFalse();
            classifier.IsImmutable("").ShouldBeFalse();
            classifier.IsImmutable(string.Empty).ShouldBeFalse();
        }

        [WindowsOnlyFact]
        public void IsImmutable_CaseSensitivityBehavior_Windows()
        {
            var classifier = new EnvironmentVariableClassifier([
                "test_env_var_1",
            ], ["prefix_1"]);

            // On Windows, environment variables should be case-insensitive
            classifier.IsImmutable("test_env_var_1").ShouldBeTrue();
            classifier.IsImmutable("TEST_ENV_VAR_1").ShouldBeTrue();
            classifier.IsImmutable("Test_Env_Var_1").ShouldBeTrue();

            // Custom prefixes should also be case-insensitive
            classifier.IsImmutable("prefix_1").ShouldBeTrue();
            classifier.IsImmutable("PREFIX_1").ShouldBeTrue();
            classifier.IsImmutable("prefix_1_var").ShouldBeTrue();
            classifier.IsImmutable("PREFIX_1_VAR").ShouldBeTrue();
            classifier.IsImmutable("Prefix_1_Var").ShouldBeTrue();
        }

        [UnixOnlyFact]
        public void IsImmutable_CaseSensitivityBehavior_Unix()
        {
            var classifier = new EnvironmentVariableClassifier([
                "test_env_var_1",
            ], ["prefix_1"]);

            // On Unix, environment variables should be case-sensitive
            classifier.IsImmutable("test_env_var_1").ShouldBeTrue();
            classifier.IsImmutable("TEST_ENV_VAR_1").ShouldBeFalse();
            classifier.IsImmutable("Test_Env_Var_1").ShouldBeFalse();

            // Custom prefixes should also be case-sensitive
            classifier.IsImmutable("prefix_1").ShouldBeTrue();
            classifier.IsImmutable("prefix_1_var").ShouldBeTrue();
            classifier.IsImmutable("PREFIX_1").ShouldBeFalse();
            classifier.IsImmutable("PREFIX_1_VAR").ShouldBeFalse();
            classifier.IsImmutable("Prefix_1_Var").ShouldBeFalse();
        }
    }
}
