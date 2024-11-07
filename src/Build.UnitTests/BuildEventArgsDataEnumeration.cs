// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class BuildEventArgsDataEnumeration
    {
        [Fact]
        public void SamplePropertiesEnumeration()
        {
            var projectFile = @"C:\foo\bar.proj";
            var args = new ProjectEvaluationFinishedEventArgs(
                ResourceUtilities.GetResourceString("EvaluationFinished"),
                projectFile)
            {
                BuildEventContext = BuildEventContext.Invalid,
                ProjectFile = @"C:\foo\bar.proj",
                GlobalProperties = new Dictionary<string, string>() { { "GlobalKey", "GlobalValue" } },
                Properties = new List<object>()
                {
                    new DictionaryEntry("Key", "Value"),
                    ProjectPropertyInstance.Create("prop", "val"),
                    new KeyValuePair<string, string>("foo","bar")
                },
                Items = null
            };

            List<PropertyData> results = args.EnumerateProperties().ToList();
            results.Count.ShouldBe(3);
            results[0].ShouldBe(new("Key", "Value"));
            results[1].ShouldBe(new("prop", "val"));
            results[2].ShouldBe(new("foo", "bar"));
        }

        [Fact]
        public void SampleItemsEnumeration()
        {
            string projectFile = @"C:\foo\bar.proj";
            ProjectEvaluationFinishedEventArgs args = new ProjectEvaluationFinishedEventArgs(
                ResourceUtilities.GetResourceString("EvaluationFinished"),
                projectFile)
            {
                BuildEventContext = BuildEventContext.Invalid,
                ProjectFile = @"C:\foo\bar.proj",
                GlobalProperties = new Dictionary<string, string>() { { "GlobalKey", "GlobalValue" } },
                Properties = null,
                Items = new List<DictionaryEntry>()
                {
                    new DictionaryEntry("Key", new MyTaskItem() { ItemSpec = "TestItemSpec" }),
                    new DictionaryEntry("Key2",
                        new TaskItemData("spec",
                            new Dictionary<string, string>() { { "metadat1", "val1" }, { "metadat2", "val2" } })),
                }
            };

            List<ItemData> results = args.EnumerateItems().ToList();

            results.Count.ShouldBe(2);
            results[0].Type.ShouldBe("Key");
            results[0].EvaluatedInclude.ShouldBe("TestItemSpec");
            results[0].EnumerateMetadata().ShouldBeEmpty();

            results[1].Type.ShouldBe("Key2");
            results[1].EvaluatedInclude.ShouldBe("spec");
            List<KeyValuePair<string, string>> metadata = results[1].EnumerateMetadata().ToList();
            metadata.Count.ShouldBe(2);
            metadata[0].Key.ShouldBe("metadat1");
            metadata[0].Value.ShouldBe("val1");
            metadata[1].Key.ShouldBe("metadat2");
            metadata[1].Value.ShouldBe("val2");
        }

        [Fact]
        public void SampleFilteredItemsEnumeration()
        {
            string projectFile = @"C:\foo\bar.proj";
            ProjectEvaluationFinishedEventArgs args = new ProjectEvaluationFinishedEventArgs(
                ResourceUtilities.GetResourceString("EvaluationFinished"),
                projectFile)
            {
                BuildEventContext = BuildEventContext.Invalid,
                ProjectFile = @"C:\foo\bar.proj",
                GlobalProperties = new Dictionary<string, string>() { { "GlobalKey", "GlobalValue" } },
                Properties = null,
                Items = new List<DictionaryEntry>()
                {
                    new DictionaryEntry("Key", new MyTaskItem() { ItemSpec = "TestItemSpec" }),
                    new DictionaryEntry("Key2",
                        new TaskItemData("spec",
                            new Dictionary<string, string>() { { "metadat1", "val1" }, { "metadat2", "val2" } })),
                    new DictionaryEntry("Key2", new MyTaskItem() { ItemSpec = "TestItemSpec3" }),
                    new DictionaryEntry("Key",
                        new TaskItemData("spec4",
                            new Dictionary<string, string>() { { "metadat41", "val41" }, { "metadat42", "val42" } })),
                }
            };

            List<ItemData> results = args.EnumerateItemsOfType("Key").ToList();

            results.Count.ShouldBe(2);
            results[0].Type.ShouldBe("Key");
            results[0].EvaluatedInclude.ShouldBe("TestItemSpec");
            results[0].EnumerateMetadata().ShouldBeEmpty();

            results[1].Type.ShouldBe("Key");
            results[1].EvaluatedInclude.ShouldBe("spec4");
            List<KeyValuePair<string, string>> metadata = results[1].EnumerateMetadata().ToList();
            metadata.Count.ShouldBe(2);
            metadata[0].Key.ShouldBe("metadat41");
            metadata[0].Value.ShouldBe("val41");
            metadata[1].Key.ShouldBe("metadat42");
            metadata[1].Value.ShouldBe("val42");

            results = args.EnumerateItemsOfType("Key2").ToList();

            results.Count.ShouldBe(2);

            results[0].Type.ShouldBe("Key2");
            results[0].EvaluatedInclude.ShouldBe("spec");
            metadata = results[0].EnumerateMetadata().ToList();
            metadata.Count.ShouldBe(2);
            metadata[0].Key.ShouldBe("metadat1");
            metadata[0].Value.ShouldBe("val1");
            metadata[1].Key.ShouldBe("metadat2");
            metadata[1].Value.ShouldBe("val2");

            results[1].Type.ShouldBe("Key2");
            results[1].EvaluatedInclude.ShouldBe("TestItemSpec3");
            results[1].EnumerateMetadata().ShouldBeEmpty();
        }
    }
}
