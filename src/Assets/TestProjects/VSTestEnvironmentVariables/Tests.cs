// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;

namespace TestNamespace
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestEnvironmentVariables()
        {
            foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                var (key, value) = ((string)env.Key, (string)env.Value);

                if (!key.StartsWith("__DOTNET_TEST_ENVIRONMENT_VARIABLE_"))
                    continue;

                Console.WriteLine($"{key}={value}");
            }

            // This project is compiled, and executed by the tests in "src/Tests/dotnet-test.Tests/GivenDotnetTestContainsEnvironmentVariables.cs"
            // The values are set there.
            AssertEnvironmentVariable("__DOTNET_TEST_ENVIRONMENT_VARIABLE_EMPTY", string.Empty);
            AssertEnvironmentVariable("__DOTNET_TEST_ENVIRONMENT_VARIABLE_1", "VALUE1");
            AssertEnvironmentVariable("__DOTNET_TEST_ENVIRONMENT_VARIABLE_2", "VALUE WITH SPACE");
        }

        private static void AssertEnvironmentVariable(string name, string expected)
        {
            var actual = Environment.GetEnvironmentVariable(name);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }
    }
}
