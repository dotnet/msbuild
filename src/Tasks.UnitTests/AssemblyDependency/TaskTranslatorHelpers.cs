using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    public class TaskTranslatorHelpers
    {
        MemoryStream _serializationStream;

        [Fact]
        public void NullFrameworkName()
        {
            FrameworkName value = null;

            GetWriteTranslator().Translate(ref value);
            GetReadTranslator().Translate(ref value);

            value.ShouldBeNull();
        }

        [Theory]
        [MemberData(nameof(SampleFrameworkNames))]
        public void ValidFrameworkName(FrameworkName value)
        {
            FrameworkName deserialized = null;

            GetWriteTranslator().Translate(ref value);
            GetReadTranslator().Translate(ref deserialized);

            deserialized.ShouldNotBeNull();
            deserialized.ShouldBe(value);
        }

        public static IEnumerable<object[]> SampleFrameworkNames =>
            new List<object[]>
            {
                new object[] { new FrameworkName("X, Version=3.4.5") },
                new object[] { new FrameworkName("X, Version=3.4, Profile=Compact") },
                new object[] { new FrameworkName("Y", new Version(1, 2, 3)) },
                new object[] { new FrameworkName("Z", new Version(1, 2, 3), "P") },
            };

        private ITranslator GetReadTranslator()
        {
            if (_serializationStream == null)
                throw new InvalidOperationException("GetWriteTranslator has to be called before GetReadTranslator");

            _serializationStream.Seek(0, SeekOrigin.Begin);
            return BinaryTranslator.GetReadTranslator(_serializationStream, null);
        }

        private ITranslator GetWriteTranslator()
        {
            _serializationStream = new MemoryStream();
            return BinaryTranslator.GetWriteTranslator(_serializationStream);
        }
    }
}
