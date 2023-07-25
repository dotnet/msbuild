// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Xunit;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Engine.UnitTests
{
    public class BuildEventArgsWriter_Tests
    {
        [Fact]
        public void WriteBlobFromStream()
        {
            byte[] bytes = new byte[] { 1, 2, 3, 4, 5 };
            MemoryStream inputStream = new MemoryStream(bytes);

            MemoryStream outputStream = new MemoryStream();
            using BinaryWriter binaryWriter = new BinaryWriter(outputStream);
            BuildEventArgsWriter writer = new BuildEventArgsWriter(binaryWriter);
            
            writer.WriteBlob(BinaryLogRecordKind.ProjectImportArchive, inputStream);
            binaryWriter.Flush();

            outputStream.Position = 0;
            BinaryReader binaryReader = new BinaryReader(outputStream);
            Assert.Equal(BinaryLogRecordKind.ProjectImportArchive, (BinaryLogRecordKind)binaryReader.Read7BitEncodedInt());
            Assert.Equal(bytes.Length, binaryReader.Read7BitEncodedInt());
            Assert.Equal(bytes, binaryReader.ReadBytes(bytes.Length));
        }
    }
}
