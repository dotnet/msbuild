// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    public class BinaryLogger_PathTraversal_Tests
    {
        [Theory]
        [InlineData("..")]
        [InlineData("../etc/passwd")]
        [InlineData("..\\Windows\\System32\\evil.dll")]
        [InlineData("C/Users/jan/sub/../../../escape.txt")]
        [InlineData("dir/../../up")]
        [InlineData("a\\..\\b")]
        public void IsPathTraversal_WithDotDotSegment_ReturnsTrue(string entryPath)
        {
            BuildEventArgsReader.IsPathTraversal(entryPath).ShouldBeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("C/Users/jan/project.csproj")]
        [InlineData("Program Files/dotnet/sdk/Sdk.props")]
        [InlineData("a/b/c.targets")]
        [InlineData("file..with..dots.txt")]
        [InlineData("..hidden")]
        [InlineData("trailing..")]
        public void IsPathTraversal_WithLegitimateSegments_ReturnsFalse(string? entryPath)
        {
            BuildEventArgsReader.IsPathTraversal(entryPath).ShouldBeFalse();
        }

        /// <summary>
        /// Exercises the actual call site in <see cref="BuildEventArgsReader.ReadEmbeddedContent"/>:
        /// a crafted binlog stream whose embedded archive contains a path-traversal entry must be
        /// rejected with an <see cref="InvalidDataException"/> before the entry is surfaced to a
        /// subscriber. This guards against the security check being accidentally removed from the
        /// read loop (which the isolated <see cref="BuildEventArgsReader.IsPathTraversal"/> tests
        /// would not catch).
        /// </summary>
        [Fact]
        public void ReadEmbeddedContent_WithPathTraversalEntry_ThrowsInvalidDataException()
        {
            byte[] archiveBytes = CreateArchiveWithEntry("../../escape.txt", "malicious");

            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            using var binaryReader = new BinaryReader(memoryStream);

            // A single embedded-archive (ProjectImportArchive) record: kind, blob length, blob bytes.
            binaryWriter.Write7BitEncodedInt((int)BinaryLogRecordKind.ProjectImportArchive);
            binaryWriter.Write7BitEncodedInt(archiveBytes.Length);
            binaryWriter.Write(archiveBytes);
            binaryWriter.Flush();
            memoryStream.Position = 0;

            using var reader = new BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion);

            bool entrySurfaced = false;
            // The embedded-content branch is only taken when ArchiveFileEncountered is subscribed.
            reader.ArchiveFileEncountered += _ => entrySurfaced = true;

            InvalidDataException exception = Should.Throw<InvalidDataException>(() => reader.Read());
            exception.Message.ShouldContain("../../escape.txt");
            entrySurfaced.ShouldBeFalse("the traversal entry must be rejected before it is surfaced to a subscriber");
        }

        private static byte[] CreateArchiveWithEntry(string entryName, string content)
        {
            using var archiveStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry entry = zipArchive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(content);
            }

            return archiveStream.ToArray();
        }
    }
}
