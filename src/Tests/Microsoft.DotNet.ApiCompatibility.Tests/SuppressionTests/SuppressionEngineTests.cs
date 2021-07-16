// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace Microsoft.DotNet.Compatibility.ErrorSuppression.Tests
{
    public class SuppressionEngineTests
    {
        [Fact]
        public void AddingASuppressionTwiceDoesntThrow()
        {
            var testEngine = SuppressionEngine.Create();
            AddSuppression(testEngine);
            AddSuppression(testEngine);

            static void AddSuppression(SuppressionEngine testEngine) => testEngine.AddSuppression("PKG004", "A.B()", "ref/net6.0/mylib.dll", "lib/net6.0/mylib.dll");
        }

        [Fact]
        public void SuppressionEngineCanParseInputSuppressionFile()
        {
            TestSuppressionEngine testEngine = TestSuppressionEngine.CreateTestSuppressionEngine();

            // Parsed the right ammount of suppressions
            Assert.Equal(9, testEngine.GetSuppressionCount());

            // Test IsErrorSuppressed string overload.
            Assert.True(testEngine.IsErrorSuppressed("CP0001", "T:A.B", "ref/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll"));
            Assert.False(testEngine.IsErrorSuppressed("CP0001", "T:A.C", "ref/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll"));
            Assert.False(testEngine.IsErrorSuppressed("CP0001", "T:A.B", "lib/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll"));
            Assert.True(testEngine.IsErrorSuppressed("PKV004", ".netframework,Version=v4.8"));
            Assert.False(testEngine.IsErrorSuppressed(string.Empty, string.Empty));
            Assert.False(testEngine.IsErrorSuppressed("PKV004", ".netframework,Version=v4.8", "lib/net6.0/mylib.dll"));
            Assert.False(testEngine.IsErrorSuppressed("PKV004", ".NETStandard,Version=v2.0"));
            Assert.True(testEngine.IsErrorSuppressed("CP123", "T:myValidation.Class1", isBaselineSuppression: true));
            Assert.False(testEngine.IsErrorSuppressed("CP123", "T:myValidation.Class1", isBaselineSuppression: false));

            // Test IsErrorSuppressed Suppression overload.
            Assert.True(testEngine.IsErrorSuppressed(new Suppression
            {
                DiagnosticId = "CP0001",
                Target = "T:A.B",
                Left = "ref/netstandard2.0/tempValidation.dll",
                Right = "lib/net6.0/tempValidation.dll"
            }));
        }

        [Fact]
        public void SuppressionEngineThrowsIfFileDoesNotExist()
        {
            Assert.Throws<FileNotFoundException>(() => SuppressionEngine.CreateFromFile("AFileThatDoesNotExist.xml"));
        }

        [Fact]
        public void SuppressionEngineDoesNotThrowOnEmptyFile()
        {
            SuppressionEngine _ = SuppressionEngine.CreateFromFile(string.Empty);
            _ = SuppressionEngine.CreateFromFile("      ");
        }

        [Fact]
        public void SuppressionEngineSuppressionsRoundTrip()
        {
            string output = string.Empty;
            TestSuppressionEngine engine = TestSuppressionEngine.CreateTestSuppressionEngine(
            (stream) =>
            {
                stream.Position = 0;
                using StreamReader reader = new(stream);
                output = reader.ReadToEnd();
            });
            string filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName(), "DummyFile.xml");
            engine.WriteSuppressionsToFile(filePath);

            Assert.Equal(engine.suppressionsFile.Trim(), output.Trim(), ignoreCase: true);
        }

        [Fact]
        public void SuppressionEngineSupportsGlobalCompare()
        {
            SuppressionEngine engine = SuppressionEngine.Create();
            // Engine has a suppression with no left and no right. This should be treated global for any left and any right.
            engine.AddSuppression("CP0001", "T:A.B");

            Assert.True(engine.IsErrorSuppressed("CP0001", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll"));
            Assert.True(engine.IsErrorSuppressed("CP0001", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", false));
            Assert.True(engine.IsErrorSuppressed("CP0001", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", true));
        }

        [Fact]
        public void BaseliningNewErrorsDoesntOverrideSuppressions()
        {
            using Stream stream = new MemoryStream();
            TestSuppressionEngine engine = TestSuppressionEngine.CreateTestSuppressionEngine(
            (s) =>
            {
                s.Position = 0;
                s.CopyTo(stream);
                stream.Position = 0;
            });

            Assert.Equal(9, engine.GetSuppressionCount());

            Suppression newSuppression = new()
            {
                DiagnosticId = "CP0002",
                Target = "F:MyNs.Class1.Field"
            };

            engine.AddSuppression(newSuppression);
            string filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName(), "DummyFile.xml");
            engine.WriteSuppressionsToFile(filePath);

            XmlSerializer xmlSerializer = new(typeof(Suppression[]), new XmlRootAttribute("Suppressions"));
            Suppression[] deserializedSuppressions = xmlSerializer.Deserialize(stream) as Suppression[];
            Assert.Equal(10, deserializedSuppressions.Length);

            Assert.Equal(new Suppression()
            {
                DiagnosticId = "CP0001",
                Target = "T:A.B",
                Left = "ref/netstandard2.0/tempValidation.dll",
                Right = "lib/net6.0/tempValidation.dll"
            }, deserializedSuppressions[0]);

            Assert.Equal(newSuppression, deserializedSuppressions[9]);
        }
    }

    public class TestSuppressionEngine : SuppressionEngine
    {
        private MemoryStream _stream;
        private StreamWriter _writer;
        public readonly string suppressionsFile = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Suppressions xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:A.B</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.Bar(System.Int32)</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.SomeOtherGenericMethod``1(``0)</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.SomeNewBreakingChange</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:tempValidation.SomeGenericType`1</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:A</Target>
    <Left>lib/netstandard1.3/tempValidation.dll</Left>
    <Right>lib/netstandard1.3/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:tempValidation.Class1</Target>
    <Left>lib/netstandard1.3/tempValidation.dll</Left>
    <Right>lib/netstandard1.3/tempValidation.dll</Right>
    <IsBaselineSuppression>true</IsBaselineSuppression>
  </Suppression>
  <Suppression>
    <DiagnosticId>PKV004</DiagnosticId>
    <Target>.NETFramework,Version=v4.8</Target>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP123</DiagnosticId>
    <Target>T:myValidation.Class1</Target>
    <IsBaselineSuppression>true</IsBaselineSuppression>
  </Suppression>
</Suppressions>";

        private MemoryStream _outputStream = new();
        private readonly Action<Stream> _callback;

        public TestSuppressionEngine(string baselineFile, Action<Stream> callback)
            : base(baselineFile)
        {
            if (callback == null)
            {
                callback = (s) => { };
            }
            _callback = callback;
        }

        public static TestSuppressionEngine CreateTestSuppressionEngine(Action<Stream> callback = null)
            => new("NonExistentFile.xml", callback);

        public int GetSuppressionCount() => _validationSuppressions.Count;

        protected override Stream GetReadableStream(string baselineFile)
        {
            // Not Disposing stream since it will be disposed by caller.
            _stream = new MemoryStream();
            _writer = new StreamWriter(_stream);
            _writer.Write(suppressionsFile);
            _writer.Flush();
            _stream.Position = 0;
            return _stream;
        }

        protected override Stream GetWritableStream(string validationSuppressionFile) => _outputStream;

        protected override void AfterWrittingSuppressionsCallback(Stream stream) => _callback(stream);
    }
}
