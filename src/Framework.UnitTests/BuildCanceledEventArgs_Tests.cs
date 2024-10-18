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
    public class BuildCanceledEventArgs_Tests
    {
        [Fact]
        public void SerializationDeserializationTest()
        {
            var message = "message";
            var datetime = DateTime.Today;

            BuildCanceledEventArgs args = new(
                message,
                datetime
                );
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(stream);
            args.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new BinaryReader(stream);
            BuildCanceledEventArgs argDeserialized = new("m");
            int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;

            argDeserialized.CreateFromStream(br, packetVersion);
            argDeserialized.Message.ShouldBe(message);
            argDeserialized.Timestamp.ShouldBe(datetime); 
        }
    }
}
