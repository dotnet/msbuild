// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class GeneratedFileUsedEventArgs_Tests
    {
        [Fact]
        public void SerializationDeserializationTest()
        {
            string filePath = "path";
            string content = "content";
            GeneratedFileUsedEventArgs arg = new(filePath, content);

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(stream);
            arg.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new BinaryReader(stream);
            GeneratedFileUsedEventArgs argDeserialized = new();
            int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;
            argDeserialized.CreateFromStream(br, packetVersion);

            argDeserialized.FilePath.ShouldBe(filePath);
            argDeserialized.Content.ShouldBe(content);
        }
    }
}
