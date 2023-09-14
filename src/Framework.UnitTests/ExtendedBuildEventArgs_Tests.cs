// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class ExtendedBuildEventArgs_Tests
{
    [InlineData(true)]
    [InlineData(false)]
    [Theory]
    public void ExtendedCustomBuildEventArgs_SerializationDeserialization(bool withOptionalData)
    {
        ExtendedCustomBuildEventArgs arg = new(
            type: "TypeOfExtendedCustom",
            message: withOptionalData ? "a message with args {0} {1}" : null,
            helpKeyword: withOptionalData ? "MSBT123" : null,
            senderName: withOptionalData ? $"UnitTest {Guid.NewGuid()}" : null,
            eventTimestamp: withOptionalData ? DateTime.Parse("3/1/2017 11:11:56 AM") : DateTime.Now,
            messageArgs: withOptionalData ? new object[] { "arg0val", "arg1val" } : null)
            {
                ExtendedData = withOptionalData ? "{'long-json':'mostly-strings'}" : null,
                ExtendedMetadata = withOptionalData ? new Dictionary<string, string?> { {"m1", "v1" }, { "m2", "v2" } } : null,
                BuildEventContext = withOptionalData ? new BuildEventContext(1, 2, 3, 4, 5, 6, 7) : null,
            };

        using MemoryStream stream = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(stream);
        arg.WriteToStream(bw);

        stream.Position = 0;
        using BinaryReader br = new BinaryReader(stream);
        ExtendedCustomBuildEventArgs argDeserialized = new();
        argDeserialized.CreateFromStream(br, 80);

        argDeserialized.Should().BeEquivalentTo(arg);
    }

    [InlineData(true)]
    [InlineData(false)]
    [Theory]
    public void ExtendedErrorEventArgs_SerializationDeserialization(bool withOptionalData)
    {
        ExtendedBuildErrorEventArgs arg = new(
            type: "TypeOfExtendedCustom",
            subcategory: withOptionalData ? "sub-type" : null,
            code: withOptionalData ? "a-code" : null,
            file: withOptionalData ? ".\\dev\\my.csproj" : null,
            lineNumber: withOptionalData ? 1 : default,
            columnNumber: withOptionalData ? 2 : default,
            endLineNumber: withOptionalData ? 3 : default,
            endColumnNumber: withOptionalData ? 4 : default,
            message: withOptionalData ? "a message with args {0} {1}" : null,
            helpKeyword: withOptionalData ? "MSBT123" : null,
            senderName: withOptionalData ? $"UnitTest {Guid.NewGuid()}" : null,
            helpLink: withOptionalData ? "(001)2234456" : null,
            eventTimestamp: withOptionalData ? DateTime.Parse("3/1/2017 11:11:56 AM") : DateTime.Now,
            messageArgs: withOptionalData ? new object[] { "arg0val", "arg1val" } : null)
        {
            ExtendedData = withOptionalData ? "{'long-json':'mostly-strings'}" : null,
            ExtendedMetadata = withOptionalData ? new Dictionary<string, string?> { { "m1", "v1" }, { "m2", "v2" } } : null,
            BuildEventContext = withOptionalData ? new BuildEventContext(1, 2, 3, 4, 5, 6, 7) : null,
        };

        using MemoryStream stream = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(stream);
        arg.WriteToStream(bw);

        stream.Position = 0;
        using BinaryReader br = new BinaryReader(stream);
        ExtendedBuildErrorEventArgs argDeserialized = new();
        argDeserialized.CreateFromStream(br, 80);

        argDeserialized.Should().BeEquivalentTo(arg);
    }


    [InlineData(true)]
    [InlineData(false)]
    [Theory]
    public void ExtendedWarningEventArgs_SerializationDeserialization(bool withOptionalData)
    {
        ExtendedBuildWarningEventArgs arg = new(
            type: "TypeOfExtendedCustom",
            subcategory: withOptionalData ? "sub-type" : null,
            code: withOptionalData ? "a-code" : null,
            file: withOptionalData ? ".\\dev\\my.csproj" : null,
            lineNumber: withOptionalData ? 1 : default,
            columnNumber: withOptionalData ? 2 : default,
            endLineNumber: withOptionalData ? 3 : default,
            endColumnNumber: withOptionalData ? 4 : default,
            message: withOptionalData ? "a message with args {0} {1}" : null,
            helpKeyword: withOptionalData ? "MSBT123" : null,
            senderName: withOptionalData ? $"UnitTest {Guid.NewGuid()}" : null,
            helpLink: withOptionalData ? "(001)2234456" : null,
            eventTimestamp: withOptionalData ? DateTime.Parse("3/1/2017 11:11:56 AM") : DateTime.Now,
            messageArgs: withOptionalData ? new object[] { "arg0val", "arg1val" } : null)
        {
            ExtendedData = withOptionalData ? "{'long-json':'mostly-strings'}" : null,
            ExtendedMetadata = withOptionalData ? new Dictionary<string, string?> { { "m1", "v1" }, { "m2", "v2" } } : null,
            BuildEventContext = withOptionalData ? new BuildEventContext(1, 2, 3, 4, 5, 6, 7) : null,
        };

        using MemoryStream stream = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(stream);
        arg.WriteToStream(bw);

        stream.Position = 0;
        using BinaryReader br = new BinaryReader(stream);
        ExtendedBuildWarningEventArgs argDeserialized = new();
        argDeserialized.CreateFromStream(br, 80);

        argDeserialized.Should().BeEquivalentTo(arg);
    }

    [InlineData(true)]
    [InlineData(false)]
    [Theory]
    public void ExtendedMessageEventArgs_SerializationDeserialization(bool withOptionalData)
    {
        ExtendedBuildMessageEventArgs arg = new(
            type: "TypeOfExtendedCustom",
            subcategory: withOptionalData ? "sub-type" : null,
            code: withOptionalData ? "a-code" : null,
            file: withOptionalData ? ".\\dev\\my.csproj" : null,
            lineNumber: withOptionalData ? 1 : default,
            columnNumber: withOptionalData ? 2 : default,
            endLineNumber: withOptionalData ? 3 : default,
            endColumnNumber: withOptionalData ? 4 : default,
            message: withOptionalData ? "a message with args {0} {1}" : null,
            helpKeyword: withOptionalData ? "MSBT123" : null,
            senderName: withOptionalData ? $"UnitTest {Guid.NewGuid()}" : null,
            importance: withOptionalData ? MessageImportance.Normal : default,
            eventTimestamp: withOptionalData ? DateTime.Parse("3/1/2017 11:11:56 AM") : DateTime.Now,
            messageArgs: withOptionalData ? new object[] { "arg0val", "arg1val" } : null)
        {
            ExtendedData = withOptionalData ? "{'long-json':'mostly-strings'}" : null,
            ExtendedMetadata = withOptionalData ? new Dictionary<string, string?> { { "m1", "v1" }, { "m2", "v2" } } : null,
            BuildEventContext = withOptionalData ? new BuildEventContext(1, 2, 3, 4, 5, 6, 7) : null,
        };

        using MemoryStream stream = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(stream);
        arg.WriteToStream(bw);

        stream.Position = 0;
        using BinaryReader br = new BinaryReader(stream);
        ExtendedBuildMessageEventArgs argDeserialized = new();
        argDeserialized.CreateFromStream(br, 80);

        argDeserialized.Should().BeEquivalentTo(arg);
    }

    [Fact]
    public void ExtendedCustomBuildEventArgs_Ctors()
    {
        var ea = new ExtendedCustomBuildEventArgs();
        ea = new ExtendedCustomBuildEventArgs("type");
        ea = new ExtendedCustomBuildEventArgs("type", "Message {0}", "Help", "sender");
        ea = new ExtendedCustomBuildEventArgs("type", "Message {0}", "Help", "sender", DateTime.Now);
        ea = new ExtendedCustomBuildEventArgs("type", "Message {0}", "Help", "sender", DateTime.Now, "arg1");
        ea = new ExtendedCustomBuildEventArgs("type");
        ea = new ExtendedCustomBuildEventArgs("type", null, null, null);
        ea = new ExtendedCustomBuildEventArgs("type", null, null, null, default(DateTime));
        ea = new ExtendedCustomBuildEventArgs("type", null, null, null, default(DateTime), null);
    }

    [Fact]
    public void ExtendedBuildErrorEventArgs_Ctors()
    {
        var ea = new ExtendedBuildErrorEventArgs();
        ea = new ExtendedBuildErrorEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender");
        ea = new ExtendedBuildErrorEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender", DateTime.Now);
        ea = new ExtendedBuildErrorEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "sender", DateTime.Now, "Message");
        ea = new ExtendedBuildErrorEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "sender", "HelpLink", DateTime.Now, "Message");
        ea = new ExtendedBuildErrorEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null);
        ea = new ExtendedBuildErrorEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null, DateTime.Now);
        ea = new ExtendedBuildErrorEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null, null, DateTime.Now, null);
    }

    [Fact]
    public void ExtendedBuildWarningEventArgs_Ctors()
    {
        var ea = new ExtendedBuildWarningEventArgs();
        ea = new ExtendedBuildWarningEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender");
        ea = new ExtendedBuildWarningEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender", DateTime.Now);
        ea = new ExtendedBuildWarningEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "sender", DateTime.Now, "Message");
        ea = new ExtendedBuildWarningEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "sender", "HelpLink", DateTime.Now, "Message");
        ea = new ExtendedBuildWarningEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null);
        ea = new ExtendedBuildWarningEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null, DateTime.Now);
        ea = new ExtendedBuildWarningEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null, null, DateTime.Now, null);
    }

    [Fact]
    public void ExtendedBuildMessageEventArgs_Ctors()
    {
        var ea = new ExtendedBuildMessageEventArgs();
        ea = new ExtendedBuildMessageEventArgs("type");
        ea = new ExtendedBuildMessageEventArgs("type", "Message", "HelpKeyword", "sender", MessageImportance.High);
        ea = new ExtendedBuildMessageEventArgs("type", "Message", "HelpKeyword", "sender", MessageImportance.High, DateTime.Now);
        ea = new ExtendedBuildMessageEventArgs("type", "Message", "HelpKeyword", "sender", MessageImportance.High, DateTime.Now, "arg1");
        ea = new ExtendedBuildMessageEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender", MessageImportance.High);
        ea = new ExtendedBuildMessageEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender", MessageImportance.High, DateTime.Now);
        ea = new ExtendedBuildMessageEventArgs("type", "Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "sender", MessageImportance.High, DateTime.Now, "Message");
        ea = new ExtendedBuildMessageEventArgs("type");
        ea = new ExtendedBuildMessageEventArgs("type", null, null, null, default);
        ea = new ExtendedBuildMessageEventArgs("type", null, null, null, default, DateTime.Now);
        ea = new ExtendedBuildMessageEventArgs("type", null, null, null, default, default, null);
        ea = new ExtendedBuildMessageEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null, default);
        ea = new ExtendedBuildMessageEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null, default, DateTime.Now);
        ea = new ExtendedBuildMessageEventArgs("type", null, null, null, 1, 2, 3, 4, null, null, null, default, DateTime.Now, null);
    }
}
