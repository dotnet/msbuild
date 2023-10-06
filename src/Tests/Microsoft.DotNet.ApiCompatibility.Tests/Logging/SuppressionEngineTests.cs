// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Serialization;

namespace Microsoft.DotNet.ApiCompatibility.Logging.Tests
{
    public class SuppressionEngineTests
    {
        [Fact]
        public void SuppressionEngine_AddSuppression_AddingTwiceDoesntThrow()
        {
            SuppressionEngine suppressionEngine = new();
            Suppression suppression = new("PKG004", "A.B()", "ref/net6.0/mylib.dll", "lib/net6.0/mylib.dll");

            suppressionEngine.AddSuppression(suppression);
            suppressionEngine.AddSuppression(suppression);

            Assert.Single(suppressionEngine.Suppressions);
        }

        [Fact]
        public void SuppressionEngine_IsErrorSuppressed_CanParseInputSuppressionFile()
        {
            TestSuppressionEngine suppressionEngine = new();
            suppressionEngine.LoadSuppressions("NonExistentFile.xml");

            // Parsed the right amount of suppressions
            Assert.Equal(9, suppressionEngine.BaselineSuppressions.Count);

            Assert.True(suppressionEngine.IsErrorSuppressed(new Suppression("CP0001", "T:A.B", "ref/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll")));
            Assert.False(suppressionEngine.IsErrorSuppressed(new Suppression("CP0001", "T:A.C", "ref/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll")));
            Assert.False(suppressionEngine.IsErrorSuppressed(new Suppression("CP0001", "T:A.B", "lib/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll")));
            Assert.True(suppressionEngine.IsErrorSuppressed(new Suppression("PKV004", ".netframework,Version=v4.8")));
            Assert.False(suppressionEngine.IsErrorSuppressed(new Suppression(string.Empty, string.Empty)));
            Assert.False(suppressionEngine.IsErrorSuppressed(new Suppression("PKV004", ".netframework,Version=v4.8", "lib/net6.0/mylib.dll")));
            Assert.False(suppressionEngine.IsErrorSuppressed(new Suppression("PKV004", ".NETStandard,Version=v2.0")));
            Assert.True(suppressionEngine.IsErrorSuppressed(new Suppression("CP123", "T:myValidation.Class1", isBaselineSuppression: true)));
            Assert.False(suppressionEngine.IsErrorSuppressed(new Suppression("CP123", "T:myValidation.Class1", isBaselineSuppression: false)));
            Assert.True(suppressionEngine.IsErrorSuppressed(new Suppression("CP0001", "T:A.B", "ref/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll")));
        }

        [Fact]
        public void SuppressionEngine_LoadFiles_ThrowsIfFileDoesNotExist()
        {
            SuppressionEngine suppressionEngine = new();
            Assert.Throws<FileNotFoundException>(() => suppressionEngine.LoadSuppressions("AFileThatDoesNotExist.xml"));
        }

        [Fact]
        public void SuppressionEngine_WriteSuppressionsToFile_SuppressionsRoundTrip()
        {
            string output = string.Empty;
            TestSuppressionEngine suppressionEngine = new((stream) =>
            {
                stream.Position = 0;
                using StreamReader reader = new(stream);
                output = reader.ReadToEnd();
            });
            suppressionEngine.LoadSuppressions("NonExistentFile.xml");

            string filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName(), "DummyFile.xml");
            IReadOnlyCollection<Suppression> writtenSuppressions = suppressionEngine.WriteSuppressionsToFile(filePath, preserveUnnecessarySuppressions: true);

            Assert.NotEmpty(writtenSuppressions);

            // Trimming away the comment as the serializer doesn't preserve them.
            string expectedSuppressionFile = TestSuppressionEngine.DefaultSuppressionFile.Replace(TestSuppressionEngine.SuppressionFileComment, string.Empty);
            Assert.Equal(expectedSuppressionFile.Trim(), output.Trim(), ignoreCase: true);
        }

        [Fact]
        public void SuppressionEngine_GetUnnecessarySuppressions_NotEmptyWithUnnecessarySuppressions()
        {
            TestSuppressionEngine suppressionEngine = new();
            suppressionEngine.LoadSuppressions("NonExistentFile.xml");

            IReadOnlyCollection<Suppression> unnecessarySuppressions = suppressionEngine.GetUnnecessarySuppressions();

            Assert.NotEmpty(unnecessarySuppressions);
        }

        [Fact]
        public void SuppressionEngine_BaselineSuppressions_NotEmptyWithUnnecessarySuppressions()
        {
            TestSuppressionEngine suppressionEngine = new();
            suppressionEngine.LoadSuppressions("NonExistentFile.xml");

            Assert.Empty(suppressionEngine.Suppressions);
            Assert.NotEmpty(suppressionEngine.BaselineSuppressions);
        }

        [Fact]
        public void SuppressionEngine_WriteSuppressionsToFile_ReturnsEmptyWithAllUnnecessarySuppressions()
        {
            TestSuppressionEngine suppressionEngine = new();
            suppressionEngine.LoadSuppressions("NonExistentFile.xml");

            string filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName(), "DummyFile.xml");
            Assert.Empty(suppressionEngine.WriteSuppressionsToFile(filePath));
        }

        [Fact]
        public void SuppressionEngine_WriteSuppressionsToFile_UnnecessarySuppressionsOmitted()
        {
            Suppression usedSuppression = new("CP0001", "T:A", "lib/netstandard1.3/tempValidation.dll", "lib/netstandard1.3/tempValidation.dll");

            string suppressionFile = TestSuppressionEngine.SuppressionFileHeader + @$"
  <Suppression>
    <DiagnosticId>{usedSuppression.DiagnosticId}</DiagnosticId>
    <Target>{usedSuppression.Target}</Target>
    <Left>{usedSuppression.Left}</Left>
    <Right>{usedSuppression.Right}</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:tempValidation.Class1</Target>
    <Left>lib/netstandard1.3/tempValidation.dll</Left>
    <Right>lib/netstandard1.3/tempValidation.dll</Right>
    <IsBaselineSuppression>true</IsBaselineSuppression>
  </Suppression>
" + TestSuppressionEngine.SuppressionFileFooter;

            TestSuppressionEngine suppressionEngine = new(suppressionFileContent: suppressionFile);
            suppressionEngine.LoadSuppressions("NonExistentFile.xml");
            suppressionEngine.AddSuppression(usedSuppression);

            string filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName(), "DummyFile.xml");
            IReadOnlyCollection<Suppression> writtenSuppressions = suppressionEngine.WriteSuppressionsToFile(filePath);

            Assert.Equal(new Suppression[] { usedSuppression }, writtenSuppressions);
        }

        [Fact]
        public void SuppressionEngine_WriteSuppressionsToFile_ReturnsEmptyWithEmptyFilePath()
        {
            EmptyTestSuppressionEngine suppressionEngine = new();

            Assert.Equal(0, suppressionEngine.Suppressions.Count);
            Assert.Empty(suppressionEngine.WriteSuppressionsToFile(string.Empty, preserveUnnecessarySuppressions: true));
        }

        [Fact]
        public void SuppressionEngine_IsErrorSuppressed_SupportsGlobalSuppressions()
        {
            SuppressionEngine engine = new();

            // Engine has a suppression with no left and no right. This should be treated global for any left and any right.
            engine.AddSuppression(new Suppression("CP0001", "T:A.B", isBaselineSuppression: true));
            engine.AddSuppression(new Suppression("CP0001", "T:A.C"));

            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0001", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true)));
            Assert.False(engine.IsErrorSuppressed(new Suppression("CP0001", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false)));

            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0001", "T:A.C", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false)));
            Assert.False(engine.IsErrorSuppressed(new Suppression("CP0001", "T:A.C", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true)));

            // Engine has a suppression with no target. Should be treated globally for any target with that left and right.
            engine.AddSuppression(new Suppression("CP0003", null, "ref/net6.0/myleft.dll", "lib/net6.0/myright.dll", isBaselineSuppression: false));

            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0003", "T:A.B", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll")));
            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0003", "T:A.C", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll")));
            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0003", "T:A.D", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll")));
            Assert.False(engine.IsErrorSuppressed(new Suppression("CP0003", "T:A.D", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll", isBaselineSuppression: true)));

            // Engine has a suppression with no diagnostic id and target. Should be treated globally for any diagnostic id and target with that left and right.
            engine.AddSuppression(new Suppression(string.Empty, null, "ref/net8.0/left.dll", "lib/net8.0/left.dll", isBaselineSuppression: false));
            engine.AddSuppression(new Suppression(string.Empty, null, "ref/net8.0/left.dll", "lib/net8.0/left.dll", isBaselineSuppression: true));

            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0009", "T:A.B.C.D.E", "ref/net8.0/left.dll", "lib/net8.0/left.dll", isBaselineSuppression: false)));
            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0009", "T:A.B.C.D.E", "ref/net8.0/left.dll", "lib/net8.0/left.dll", isBaselineSuppression: true)));
        }

        [Fact]
        public void SuppressionEngine_BaseliningNewErrorsDoesNotOverrideSuppressions()
        {
            using Stream stream = new MemoryStream();
            TestSuppressionEngine suppressionEngine = new((s) =>
            {
                s.Position = 0;
                s.CopyTo(stream);
                stream.Position = 0;
            });
            suppressionEngine.LoadSuppressions("NonExistentFile.xml");

            Assert.Equal(9, suppressionEngine.BaselineSuppressions.Count);

            Suppression newSuppression = new("CP0002", "F:MyNs.Class1.Field");
            suppressionEngine.AddSuppression(newSuppression);

            string filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName(), "DummyFile.xml");
            Assert.NotEmpty(suppressionEngine.WriteSuppressionsToFile(filePath, preserveUnnecessarySuppressions: true));

            XmlSerializer xmlSerializer = new(typeof(Suppression[]), new XmlRootAttribute("Suppressions"));
            Suppression[] deserializedSuppressions = xmlSerializer.Deserialize(stream) as Suppression[];
            Assert.Equal(10, deserializedSuppressions.Length);

            Assert.Equal(new Suppression("CP0001")
            {
                Target = "T:A",
                Left = "lib/netstandard1.3/tempValidation.dll",
                Right = "lib/netstandard1.3/tempValidation.dll"
            }, deserializedSuppressions[0]);

            Assert.Equal(newSuppression, deserializedSuppressions[4]);
        }

        [Fact]
        public void SuppressionEngine_IsErrorSuppressed_NoWarnIsHonored()
        {
            SuppressionEngine engine = new(noWarn: "CP0001;CP0003;CP1111");

            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0001", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true)));
            Assert.False(engine.IsErrorSuppressed(new Suppression("CP1110", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false)));

            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0001", "T:A.C", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: false)));
            Assert.False(engine.IsErrorSuppressed(new Suppression("CP1000", "T:A.C", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll", isBaselineSuppression: true)));

            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0003", "T:A.B", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll")));
            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0003", "T:A.C", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll")));
            Assert.True(engine.IsErrorSuppressed(new Suppression("CP0003", "T:A.D", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll")));
            Assert.False(engine.IsErrorSuppressed(new Suppression("CP1232", "T:A.D", "ref/net6.0/myLeft.dll", "lib/net6.0/myRight.dll", isBaselineSuppression: true)));
        }
    }
}
