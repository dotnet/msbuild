// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class BinlogPerformancePrototypeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MetadataCachePreservesBytesAcrossMutation(bool useTaskItem, bool useDefinitions)
    {
        using ProjectCollection collection = new();
        using ProjectFromString project = new("""
            <Project>
              <ItemDefinitionGroup>
                <Compile>
                  <Inherited>inherited%3Bvalue</Inherited>
                  <Override>definition</Override>
                </Compile>
              </ItemDefinitionGroup>
              <ItemGroup>
                <Compile Include="a%3Bb.cs">
                  <Override>item%25value</Override>
                  <Empty></Empty>
                </Compile>
                <WithoutDefinitions Include="plain.cs">
                  <Override>item%25value</Override>
                </WithoutDefinitions>
                <EmptyItem Include="empty.cs" />
              </ItemGroup>
            </Project>
            """, null, null, collection);
        ProjectInstance instance = project.Project.CreateProjectInstance();
        ProjectItemInstance projectItem = instance.GetItems(useDefinitions ? "Compile" : "WithoutDefinitions").Single();
        ProjectItemInstance emptyItem = instance.GetItems("EmptyItem").Single();
        ITaskItem item = useTaskItem ? new ProjectItemInstance.TaskItem(projectItem) : projectItem;
        using MemoryStream expected = new();
        using MemoryStream actual = new();
        using BinaryWriter expectedBinary = new(expected, Encoding.UTF8, leaveOpen: true);
        using BinaryWriter actualBinary = new(actual, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter expectedWriter = new(expectedBinary);
        BuildEventArgsWriter actualWriter = new(actualBinary, cacheMetadata: true);

        WriteBoth(true);
        WriteBoth(true);
        WriteBoth(false);
        item.SetMetadata("Override", "changed%3Bvalue");
        WriteBoth(true);
        item.SetMetadata("Added", "new%25value");
        WriteBoth(true);
        item.RemoveMetadata("Added");
        WriteBoth(true);
        item.ItemSpec = "renamed%3Bitem";
        WriteBoth(true);

        actual.ToArray().ShouldBe(expected.ToArray());

        void WriteBoth(bool logMetadata)
        {
            ITaskItem[] items = [item, item, emptyItem, emptyItem];
            TaskParameterEventArgs e = new(TaskParameterMessageKind.TaskInput, "Input", null,
                "Compile", items, logMetadata, DateTime.MinValue);
            expectedWriter.Write(e);
            actualWriter.Write(e);
        }
    }

    [Fact]
    public void MetadataCachePreservesMutableFallbackItems()
    {
        using MemoryStream expected = new();
        using MemoryStream actual = new();
        using BinaryWriter expectedBinary = new(expected);
        using BinaryWriter actualBinary = new(actual);
        BuildEventArgsWriter expectedWriter = new(expectedBinary);
        BuildEventArgsWriter actualWriter = new(actualBinary, cacheMetadata: true);
        TaskItemData item = new("item", new System.Collections.Generic.Dictionary<string, string> { ["Key"] = "first" });
        ITaskItem[] items = [item];
        TaskParameterEventArgs e = new(TaskParameterMessageKind.TaskInput, "Input", null,
            "Compile", items, true, DateTime.MinValue);
        expectedWriter.Write(e);
        actualWriter.Write(e);
        item.Metadata["Key"] = "second";
        expectedWriter.Write(e);
        actualWriter.Write(e);
        actual.ToArray().ShouldBe(expected.ToArray());
    }

    [Fact]
    public void PipelinePreservesOrderingAndFlushBoundaries()
    {
        byte[] bytes = new byte[5 * 1024 * 1024 + 17];
        new Random(42).NextBytes(bytes);
        MemoryStream destination = new();
        using (PipelinedWriteStream pipeline = new(destination))
        {
            pipeline.Write(bytes, 0, 13);
            pipeline.Flush();
            destination.ToArray().ShouldBe(bytes.Take(13).ToArray());
            pipeline.Write(bytes, 13, bytes.Length - 13);
            pipeline.Flush();
            destination.ToArray().ShouldBe(bytes);
            pipeline.WriteByte(123);
        }
        destination.ToArray().ShouldBe(bytes.Concat(new byte[] { 123 }).ToArray());
    }

    [Fact]
    public void PipelinePropagatesFinalizationFailure()
    {
        PipelinedWriteStream pipeline = new(new FinalizationFailingStream());
        pipeline.WriteByte(42);
        Should.Throw<IOException>(() => pipeline.Dispose()).Message.ShouldBe("expected finalization failure");
        pipeline.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PipelinePropagatesWorkerFailures(bool failOnFlush)
    {
        PipelinedWriteStream pipeline = new(new FailingStream(failOnFlush));
        byte[] bytes = new byte[5 * 1024 * 1024];
        Should.Throw<IOException>(() =>
        {
            pipeline.Write(bytes, 0, bytes.Length);
            pipeline.Flush();
        }).Message.ShouldBe("expected failure");
        Should.Throw<IOException>(() => pipeline.Dispose()).Message.ShouldBe("expected failure");
        pipeline.Dispose();
    }

    private sealed class FailingStream(bool failOnFlush) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!failOnFlush)
            {
                throw new IOException("expected failure");
            }
            base.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            if (failOnFlush)
            {
                throw new IOException("expected failure");
            }
            base.Flush();
        }
    }

    private sealed class FinalizationFailingStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            throw new IOException("expected finalization failure");
        }
    }
}
