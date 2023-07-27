// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging.Tests
{
    internal class TestSuppressionEngine : SuppressionEngine
    {
        private MemoryStream _stream;
        private StreamWriter _writer;
        private readonly string _suppressionFileContent;
        // On .NET Framework the xsd element is written before the xsi element, where-as on modern .NET, it's the other way around.
        private static readonly string s_suppressionsHeader = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework") ?
            @"<Suppressions xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">" :
            @"<Suppressions xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">";

        public static readonly string SuppressionFileHeader = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<!--{DiagnosticIdDocumentationComment}-->
{s_suppressionsHeader}";

        public static readonly string SuppressionFileFooter = "</Suppressions>";

        public static readonly string SuppressionFileComment = @"
  <!-- Test comment -->";

        public static readonly string DefaultSuppressionFile = SuppressionFileHeader + @$"
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:A</Target>
    <Left>lib/netstandard1.3/tempValidation.dll</Left>
    <Right>lib/netstandard1.3/tempValidation.dll</Right>
  </Suppression>{SuppressionFileComment}
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:tempValidation.Class1</Target>
    <Left>lib/netstandard1.3/tempValidation.dll</Left>
    <Right>lib/netstandard1.3/tempValidation.dll</Right>
    <IsBaselineSuppression>true</IsBaselineSuppression>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:A.B</Target>
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
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.Bar(System.Int32)</Target>
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
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.SomeOtherGenericMethod``1(``0)</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP123</DiagnosticId>
    <Target>T:myValidation.Class1</Target>
    <IsBaselineSuppression>true</IsBaselineSuppression>
  </Suppression>
  <Suppression>
    <DiagnosticId>PKV004</DiagnosticId>
    <Target>.NETFramework,Version=v4.8</Target>
  </Suppression>
" + SuppressionFileFooter;

        private readonly MemoryStream _outputStream = new();
        private readonly Action<Stream> _callback;

        public TestSuppressionEngine(Action<Stream> callback = null, string suppressionFileContent = null, string noWarn = "")
            : base(noWarn)
        {
            callback ??= (s) => { };
            _callback = callback;
            _suppressionFileContent = suppressionFileContent ?? DefaultSuppressionFile;
        }

        protected override Stream GetReadableStream(string suppressionFile)
        {
            // Not Disposing stream since it will be disposed by caller.
            _stream = new MemoryStream();
            _writer = new StreamWriter(_stream);
            _writer.Write(_suppressionFileContent);
            _writer.Flush();
            _stream.Position = 0;
            return _stream;
        }

        protected override Stream GetWritableStream(string suppressionFile) => _outputStream;

        protected override void AfterWritingSuppressionsCallback(Stream stream) => _callback(stream);
    }
}
