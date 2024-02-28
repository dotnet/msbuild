// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using CommunicationsUtilities = Microsoft.Build.Internal.CommunicationsUtilities;

namespace Microsoft.Build.UnitTests
{
    public class CommunicationUtilitiesTests
    {
        /// <summary>
        /// Verify our custom way of getting env vars gives the same results as the BCL.
        /// </summary>
        [Fact]
        public void GetEnvVars()
        {
            IDictionary<string, string> envVars = CommunicationsUtilities.GetEnvironmentVariables();
            IDictionary referenceVars = Environment.GetEnvironmentVariables();
            IDictionary<string, string> referenceVars2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry item in referenceVars)
            {
                referenceVars2.Add((string)item.Key!, (string)item.Value!);
            }

            Helpers.AssertCollectionsValueEqual(envVars, referenceVars2);
        }

        /// <summary>
        /// Verify that we correctly restore environment variables.
        /// </summary>
        [Fact]
        public void RestoreEnvVars()
        {
            string testName1 = "_MSBUILD_TEST_ENV_VAR1";
            string testName2 = "_MSBUILD_TEST_ENV_VAR2";

            // A long value exceeding the former limit of 32,767 characters.
            string testValue = new string('a', 1_000_000);

            CommunicationsUtilities.SetEnvironmentVariable(testName1, testValue);
            try
            {
                IDictionary<string, string> envVars = CommunicationsUtilities.GetEnvironmentVariables();

                CommunicationsUtilities.SetEnvironmentVariable(testName1, null);
                CommunicationsUtilities.SetEnvironmentVariable(testName2, testValue);

                CommunicationsUtilities.SetEnvironment(envVars);

                Environment.GetEnvironmentVariable(testName1).ShouldBe(testValue);
                Environment.GetEnvironmentVariable(testName2).ShouldBe(null);
            }
            finally
            {
                CommunicationsUtilities.SetEnvironmentVariable(testName1, null);
                CommunicationsUtilities.SetEnvironmentVariable(testName2, null);
            }
        }
    }
}
