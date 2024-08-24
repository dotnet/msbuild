// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests
{
    public class BufferedBinaryReader_Tests
    {
        /// <summary>
        /// Test ReadString
        /// </summary>
        [Fact]
        public void Test_ReadString()
        {
            var testString = new string[] { "foobar", "catbar", "dogbar" };
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream);
            foreach (string test in testString)
            {
                string result = reader.ReadString();
                Assert.Equal(test, result);
            }
        }

        /// <summary>
        /// Test ReadString support strings that are larger than the internal buffer.
        /// </summary>
        [Fact]
        public void Test_ReadString_LongString()
        {
            var testString = new string[]
            {
                "FoobarCatbarDogbarDiveBarSandBar",
                "FoobarCatbarDogbarDiveBarSandBar2",
                "FoobarCatbarDogbarDiveBarSandBar3",
            };

            var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream, bufferCapacity: 10);
            foreach (string test in testString)
            {
                string result = reader.ReadString();
                Assert.Equal(test, result);
            }
        }

        /// <summary>
        /// Test ReadInt64.
        /// </summary>
        [Fact]
        public void Test_ReadInt64()
        {
            Int64 test = Int64.MaxValue;
            var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            writer.Write(test);

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream);
            var result = reader.ReadInt64();

            Assert.Equal(test, result);
        }

        /// <summary>
        /// Test Read 7BitEncoded Integer.
        /// </summary>
        [Fact]
        public void Test_Read7BitEncodedInt()
        {
            int test = 100;
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            writer.Write7BitEncodedInt(test);

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream);
            var result = reader.Read7BitEncodedInt();

            Assert.Equal(test, result);
        }

        /// <summary>
        /// Test Read7BitEncodedInt with varied length.
        /// </summary>
        [Fact]
        public void Test_Read7BitEncodedInt_VariedLength()
        {
            int[] ints = new[] { 0, 1, 10, 254, 255, 256, 500, 1024, 1025, 100_000, 100_000_000, int.MaxValue };
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            foreach (int number in ints)
            {
                writer.Write7BitEncodedInt(number);
            }

            stream.Position = 0;

            int result = 0;

            using var reader = new BufferedBinaryReader(stream);
            foreach (int number in ints)
            {
                result = reader.Read7BitEncodedInt();
                Assert.Equal(number, result);
            }
        }

        /// <summary>
        /// Test Reading multiple 7BitEncoded Integer.
        /// </summary>
        [Fact]
        public void Test_BulkRead7Bit()
        {
            int initialCount = BufferedBinaryReader.MaxBulkRead7BitLength;
            int test = initialCount;
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            while (test > 0)
            {
                writer.Write7BitEncodedInt(test);
                test--;
            }

            stream.Position = 0;
            test = initialCount;

            using var reader = new BufferedBinaryReader(stream);
            int[] results = reader.BulkRead7BitEncodedInt(initialCount);

            foreach (int result in results)
            {
                Assert.Equal(test, result);
                test--;
            }
        }

        /// <summary>
        /// Test Reading multiple 7BitEncoded Integer.
        /// </summary>
        [Fact]
        public void Test_Read7BitArray_Looped()
        {
            int initialCount = BufferedBinaryReader.MaxBulkRead7BitLength * 100;
            int test = initialCount;
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            while (test > 0)
            {
                writer.Write7BitEncodedInt(test);
                test--;
            }

            stream.Position = 0;
            test = initialCount;

            using var reader = new BufferedBinaryReader(stream);

            do
            {
                int[] results = reader.BulkRead7BitEncodedInt(BufferedBinaryReader.MaxBulkRead7BitLength);

                foreach (int result in results)
                {
                    Assert.Equal(test, result);
                    test--;
                }
            } while (test > 0);
        }

        /// <summary>
        /// Test ReadInt64 that are larger than the internal buffer.
        /// </summary>
        [Fact]
        public void Test_FillBuffer_Int64()
        {
            Int64 initialCount = 200; // a large enough value to saturate a buffer
            Int64 test = initialCount;
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            while (test > 0)
            {
                writer.Write(test);
                test--;
            }

            stream.Position = 0;
            test = initialCount;
            Int64 result = 0;

            using var reader = new BufferedBinaryReader(stream, bufferCapacity: 20);  // Reduced buffer size
            while (test > 0)
            {
                result = reader.ReadInt64();
                Assert.Equal(test, result);
                test--;
            }
        }

        /// <summary>
        /// Test Read7BitEncodedInt that are larger than the internal buffer.
        /// </summary>
        [Fact]
        public void Test_FillBuffer_Read7Bit()
        {
            int initialCount = 200; // a large enough value to saturate a buffer
            int test = initialCount;
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            while (test > 0)
            {
                writer.Write7BitEncodedInt(test);
                test--;
            }

            stream.Position = 0;
            test = initialCount;
            int result = 0;

            using var reader = new BufferedBinaryReader(stream, bufferCapacity: 20);  // Reduced buffer size
            while (test > 0)
            {
                result = reader.Read7BitEncodedInt();
                Assert.Equal(test, result);
                test--;
            }
        }

        /// <summary>
        /// Test ReadString support strings that are larger than the internal buffer.
        /// </summary>
        [Fact]
        public void Test_FillBuffer_ReadString()
        {
            var testString = new string[] { "foobar", "catbar", "dogbar" };
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream, bufferCapacity: 10);
            foreach (string test in testString)
            {
                string result = reader.ReadString();
                Assert.Equal(test, result);
            }
        }

        /// <summary>
        /// Test ReadString support unicode string that are larger than the internal buffer.
        /// </summary>
        [Fact]
        public void Test_Unicode_ReadString()
        {
            var testString = new string[] { "가 각 갂 갃 간", "一 丁 丂 七 丄 丅", "豈 更 車 賈 滑", "ﻬ ﻭ ﻮ ﻯ ﻰ ﻱ" };
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            // Use a buffer size that is between code point.
            using var reader = new BufferedBinaryReader(stream, bufferCapacity: 7);
            foreach (string test in testString)
            {
                string result = reader.ReadString();
                Assert.Equal(test, result);
            }
        }

        /// <summary>
        /// Test Slice function to correctly stream with correct position.
        /// </summary>
        [Fact]
        public void Test_SliceBuffer()
        {
            var testString = new string[] { "foobar", "catbar", "dogbar" };
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream, bufferCapacity: 10);
            string firstResult = reader.ReadString();
            Assert.Equal(testString[0], firstResult);

            var sliceStream = reader.Slice(100);
            using var binaryReader = new BinaryReader(sliceStream);

            foreach (string test in testString.Skip(1))
            {
                string result = binaryReader.ReadString();
                Assert.Equal(test, result);
            }
        }

        /// <summary>
        /// Test Seek function to correctly seek stream with correct position.
        /// </summary>
        [Theory]
        [InlineData(10)]
        [InlineData(100)]

        public void Test_Seek(int bufferCapacity)
        {
            var testString = new string[] { "foobar", "catbar", "dogbar" };
            using var stream = new MemoryStream();

            using var writer = new BinaryWriter(stream);
            writer.Write(testString[0]);
            var offset1 = stream.Position;
            writer.Write(testString[1]);
            var offset2 = stream.Position;
            writer.Write(testString[2]);

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream, bufferCapacity: bufferCapacity);
            Assert.Equal(testString[0], reader.ReadString());

            // Seek to skip a string.
            reader.Seek((int)(offset2 - offset1), SeekOrigin.Current);

            Assert.Equal(testString[2], reader.ReadString());
        }

        /// <summary>
        /// Test ReadGuid 
        /// </summary>
        [Fact]
        public void Test_ReadGuid()
        {
            int testCount = 20;
            List<Guid> testGuids = new List<Guid>(testCount);

            for(int i = 0; i < testCount; i++)
            {
                testGuids.Add(Guid.NewGuid());
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            foreach (var guid in testGuids)
            {
                writer.Write(guid.ToByteArray());
            }

            stream.Position = 0;

            using var reader = new BufferedBinaryReader(stream, bufferCapacity: 20);
            foreach (var guid in testGuids)
            {
                Assert.Equal(guid, new Guid(reader.ReadGuid()));
            }
        }
    }
}
