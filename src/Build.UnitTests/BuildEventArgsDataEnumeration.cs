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
            results[0].Value.ItemSpec.ShouldBe("TestItemSpec");
            results[0].Value.GetEvaluatedInclude().ShouldBe("TestItemSpec");
            results[0].Value.EnumerateMetadata().ShouldBeEmpty();

            results[1].Type.ShouldBe("Key2");
            results[1].Value.ItemSpec.ShouldBe("spec");
            results[1].Value.GetEvaluatedInclude().ShouldBe("spec");
            List<KeyValuePair<string, string>> metadata = results[1].Value.EnumerateMetadata().ToList();
            metadata.Count.ShouldBe(2);
            metadata[0].Key.ShouldBe("metadat1");
            metadata[0].Value.ShouldBe("val1");
            metadata[1].Key.ShouldBe("metadat2");
            metadata[1].Value.ShouldBe("val2");
        }
    }
}
