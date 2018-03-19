// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.UnitTests;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace NuGet.MSBuildSdkResolver.UnitTests
{
    public class GlobalJsonReader_Tests
    {
        public static string WriteGlobalJson(string directory, Dictionary<string, string> sdkVersions, string additionalcontent = "")
        {
            string path = Path.Combine(directory, GlobalJsonReader.GlobalJsonFileName);

            using (StreamWriter writer = File.CreateText(path))
            {
                writer.WriteLine("{");
                if (sdkVersions != null)
                {
                    writer.WriteLine("    \"msbuild-sdks\": {");
                    writer.WriteLine(String.Join($",{Environment.NewLine}        ", sdkVersions.Select(i => $"\"{i.Key}\": \"{i.Value}\"")));
                    writer.WriteLine("    }");
                }

                if (!String.IsNullOrWhiteSpace(additionalcontent))
                {
                    writer.Write(additionalcontent);
                }

                writer.WriteLine("}");
            }

            return path;
        }

        [Fact]
        public void EmptyGlobalJson()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder();

                File.WriteAllText(Path.Combine(folder.FolderPath, GlobalJsonReader.GlobalJsonFileName), " { } ");

                MockSdkResolverContext context = new MockSdkResolverContext(Path.Combine(folder.FolderPath, "foo.proj"));

                GlobalJsonReader.GetMSBuildSdkVersions(context).ShouldBeNull();
            }
        }

        [Fact]
        public void InvalidJsonLogsMessage()
        {
            Dictionary<string, string> expectedVersions = new Dictionary<string, string>
            {
                {"foo", "1.0.0"},
                {"bar", "2.0.0"}
            };

            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectWithFiles = testEnvironment.CreateTestProjectWithFiles("");

                string globalJsonPath = WriteGlobalJson(projectWithFiles.TestRoot, expectedVersions, additionalcontent: ", abc");

                MockSdkResolverContext context = new MockSdkResolverContext(projectWithFiles.ProjectFile);

                GlobalJsonReader.GetMSBuildSdkVersions(context).ShouldBeNull();

                context.MockSdkLogger.LoggedMessages
                    .ShouldHaveSingleItem()
                    .Key
                    .ShouldBe($"Failed to parse \"{globalJsonPath}\". Invalid JavaScript property identifier character: }}. Path \'msbuild-sdks\', line 6, position 5.");
            }
        }

        [Fact]
        public void SdkVersionsAreSuccessfullyLoaded()
        {
            Dictionary<string, string> expectedVersions = new Dictionary<string, string>
            {
                {"foo", "1.0.0"},
                {"bar", "2.0.0"}
            };
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectWithFiles = testEnvironment.CreateTestProjectWithFiles("", relativePathFromRootToProject: @"a\b\c");

                WriteGlobalJson(projectWithFiles.TestRoot, expectedVersions);

                MockSdkResolverContext context = new MockSdkResolverContext(projectWithFiles.ProjectFile);

                GlobalJsonReader.GetMSBuildSdkVersions(context).ShouldBe(expectedVersions);
            }
        }
    }
}
