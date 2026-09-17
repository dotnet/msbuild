// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class XmlReaderExtension_Tests
    {
        private readonly ITestOutputHelper _output;

        public XmlReaderExtension_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsOnlyFact]
        public void ItemMetadataPreservesCrLfWhenLoadedFromDisk_Regression()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            TransientTestFile projectFile = env.CreateFile("metadata-newlines.proj", GetMultilineMetadataProjectContents());
            var pc = env.CreateProjectCollection();
            Project project = pc.Collection.LoadProject(projectFile.Path);
            string metadataValue = project.GetItems("I").Single().GetMetadataValue("M");

            metadataValue.ShouldBe(string.Join(Environment.NewLine,
            [
                "multiple",
                "lines",
                "in",
                "this",
                "metadatum",
            ]));
        }

        [WindowsOnlyFact]
        public void ItemMetadataPreservesCrLfWhenLoadedReadOnly_Regression()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            TransientTestFile projectFile = env.CreateFile("metadata-newlines-readonly.proj", GetMultilineMetadataProjectContents());

            using var projectCollection = new ProjectCollection(
                new Dictionary<string, string>(),
                loggers: null,
                remoteLoggers: null,
                ToolsetDefinitionLocations.Default,
                maxNodeCount: 1,
                onlyLogCriticalEvents: false,
                loadProjectsReadOnly: true);

            Project project = projectCollection.LoadProject(projectFile.Path);
            string metadataValue = project.GetItems("I").Single().GetMetadataValue("M");

            metadataValue.ShouldBe(string.Join(Environment.NewLine,
            [
                "multiple",
                "lines",
                "in",
                "this",
                "metadatum",
            ]));
        }

        private static string GetMultilineMetadataProjectContents() => "<Project>\r\n" +
            "  <ItemGroup>\r\n" +
            "    <I Include=\"I\">\r\n" +
            "      <M>multiple\r\n" +
            "lines\r\n" +
            "in\r\n" +
            "this\r\n" +
            "metadatum</M>\r\n" +
            "    </I>\r\n" +
            "  </ItemGroup>\r\n" +
            "</Project>";
    }
}
