// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.ResourceHandling;
using Microsoft.Build.Tasks.UnitTests.ResourceHandling;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests.GenerateResource
{
    public class MSBuildResXReader_Tests
    {
        private readonly ITestOutputHelper _output;

        public MSBuildResXReader_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ParsesSingleStringAsString()
        {
            var resxWithSingleString = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
                    @"<data name=""StringResource"" xml:space=""preserve"">
    <value>StringValue</value>
    <comment>Comment</comment>
  </data>"));

            AssertSingleStringResource(resxWithSingleString, "StringResource", "StringValue");
        }

        [Fact]
        public void ParsesSingleStringWithPartialTypeName()
        {
            var resxWithSingleString = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
                    @"<data name=""StringResource"" type=""System.String"">
    <value>StringValue</value>
  </data>"));

            AssertSingleStringResource(resxWithSingleString, "StringResource", "StringValue");
        }


        [Fact]
        public void LoadsMultipleStringsPreservingOrder()
        {
            var resxWithTwoStrings = MSBuildResXReader.GetResourcesFromString(
    ResXHelper.SurroundWithBoilerplate(
        @"<data name=""StringResource"" xml:space=""preserve"">
    <value>StringValue</value>
    <comment>Comment</comment>
  </data>
  <data name=""2StringResource2"" xml:space=""preserve"">
    <value>2StringValue2</value>
  </data>"));

            resxWithTwoStrings.Count.ShouldBe(2);

            resxWithTwoStrings[0].Name.ShouldBe("StringResource");
            resxWithTwoStrings[0].ShouldBeOfType<StringResource>()
                .Value.ShouldBe("StringValue");

            resxWithTwoStrings[1].Name.ShouldBe("2StringResource2");
            resxWithTwoStrings[1].ShouldBeOfType<StringResource>()
                .Value.ShouldBe("2StringValue2");
        }

        [Fact]
        public void ResXNullRefProducesNullLiveObject()
        {
            var resxWithNullRef = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
@"  <assembly alias=""System.Windows.Forms"" name=""System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" />
  <data name=""$this.AccessibleDescription"" type=""System.Resources.ResXNullRef, System.Windows.Forms, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"">
    <value />
  </data>"));

            resxWithNullRef.ShouldHaveSingleItem();

            resxWithNullRef[0].Name.ShouldBe("$this.AccessibleDescription");

            resxWithNullRef[0].ShouldBeOfType<LiveObjectResource>()
                .Value.ShouldBeNull();
        }

        [Theory]
        [InlineData("System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        [InlineData("System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        public void LoadsStringFromFileRefAsString(string stringType)
        {
            File.Exists(Path.Combine("ResourceHandling", "TextFile1.txt")).ShouldBeTrue("Test deployment is missing None files");

            var resxWithLinkedString = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
$@"  <assembly alias=""System.Windows.Forms"" name=""System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" />
  <data name=""TextFile1"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>ResourceHandling\TextFile1.txt;{stringType};utf-8</value>
  </data>"));

            AssertSingleStringResource(resxWithLinkedString, "TextFile1", "Contents of TextFile1");
        }

        [Fact]
        public void LoadsStringFromFileRefAsStringWithShiftJISEncoding()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                const string JapaneseString = "ハローワールド！";

                var baseDir = env.CreateFolder(createFolder: true);
                var resourceHandlingFolder = baseDir.CreateDirectory("ResourceHandling");

                var linkedTextFile = resourceHandlingFolder.CreateFile("TextFileInShiftJIS.txt");

#if RUNTIME_TYPE_NETCORE
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

                File.WriteAllText(linkedTextFile.Path,
                                  JapaneseString,
                                  Encoding.GetEncoding("shift_jis"));

                var resxWithLinkedString = MSBuildResXReader.GetResourcesFromString(
                    ResXHelper.SurroundWithBoilerplate(
    @"  <assembly alias=""System.Windows.Forms"" name=""System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" />
  <data name=""TextFile1"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>ResourceHandling\TextFileInShiftJIS.txt;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;shift_jis</value>
  </data>"),
                    Path.Combine(baseDir.Path, nameof(LoadsStringFromFileRefAsStringWithShiftJISEncoding) + ".resx"),
                    useRelativePath: true);

                AssertSingleStringResource(resxWithLinkedString, "TextFile1", JapaneseString);
            }
        }

        private static void AssertSingleStringResource(IReadOnlyList<IResource> resources, string name, string value)
        {
            resources.ShouldHaveSingleItem();

            resources[0].Name.ShouldBe(name);

            resources[0].ShouldBeOfType<StringResource>()
                .Value.ShouldBe(value);
        }

        [Fact]
        public void PassesThroughBitmapInResx()
        {
            var resxWithEmbeddedBitmap = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
@"  <assembly alias=""System.Drawing"" name=""System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" />
  <data name=""pictureBox1.Image"" type=""System.Drawing.Bitmap, System.Drawing"" mimetype=""application/x-microsoft.net.object.bytearray.base64"">
    <value>
        iVBORw0KGgoAAAANSUhEUgAAACgAAAAeCAIAAADRv8uKAAAABGdBTUEAALGPC/xhBQAAAAlwSFlzAAAO
        wwAADsMBx2+oZAAAABh0RVh0U29mdHdhcmUAcGFpbnQubmV0IDQuMS41ZEdYUgAAAZNJREFUSEvtk6GL
        wmAYxlfMFoNpQWe4Nqt/gIjBJAgiVoMIhomCiElMphUZGtRm1jTErSlcEAwaxei6eCDbPe4dY8g4dnc4
        y37p+R5e9tu37xtjvIlA7BuB2DcCsW8E4lqt1ul0ut3ubDbTdZ3KarXabDYpHw6HSqVCGUyn01arhabX
        61mVN57FDMMUi8V2u83zPMuyp9OJSnA+n5E3mw2yOWuUy2VkQRD6/X6pVFoul9R7wUWMR1POZrPYOpUc
        xzUaDeTtdkvi8XgciUQul8tj9Pf8JC4UCvV6ncrhcBgKha7X6263I3E6nabX+hsuYlmWj8ejKIrI6/Wa
        yv1+n8lkBoOBLY7FYpPJBCGXy32YrFarxyO84SIGON1UKmWfGRqIF4tFPB63xbgEo9EI4dMEpaIo5rgn
        XMT2p7ZBCR9CMpnEJSJxPp93Xu9XibEnBPxjyABZVVUESZLu9zvNvFAMotEoicF8Pk8kEliGw2Fc+3+J
        b7eblRw4yy8Ta2GCpaZp1sIzz2LfCMS+EYh9401iw/gG1gYfvzjQIXcAAAAASUVORK5CYII=
</value>
  </data>
"));
            resxWithEmbeddedBitmap.ShouldHaveSingleItem();
            resxWithEmbeddedBitmap[0].ShouldBeOfType(typeof(TypeConverterByteArrayResource));

            var resource = (TypeConverterByteArrayResource)resxWithEmbeddedBitmap[0];
            resource.Name.ShouldBe("pictureBox1.Image");
            resource.TypeAssemblyQualifiedName.ShouldBe("System.Drawing.Bitmap, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        }

        [Fact]
        public void TypeConverterStringWellFormatted()
        {
            var resxWithEmbeddedBitmap = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
@"  <assembly alias=""System.Drawing"" name=""System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" />
    <data name=""color"" type=""System.Drawing.Color, System.Drawing"">
      <value>Blue</value>
    </data>
"));
            resxWithEmbeddedBitmap.ShouldHaveSingleItem();
            resxWithEmbeddedBitmap[0].ShouldBeOfType(typeof(TypeConverterStringResource));

            var resource = (TypeConverterStringResource)resxWithEmbeddedBitmap[0];
            resource.Name.ShouldBe("color");
            resource.TypeAssemblyQualifiedName.ShouldBe("System.Drawing.Color, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            resource.StringRepresentation.ShouldBe("Blue");
        }

        /// <summary>
        /// Test a string-based TypeConverter resource shaped like the one in the old
        /// ResXResourceWriter block comment without a "data" element.
        /// </summary>
        /// <remarks>
        /// https://github.com/dotnet/winforms/blob/195f89af79d550c2da1711c45c379efd63519ac1/src/System.Windows.Forms/src/System/Resources/ResXResourceWriter.cs#L141
        /// </remarks>
        [Fact]
        public void TypeConverterStringDirectValue()
        {
            var resxWithEmbeddedBitmap = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
@"  <assembly alias=""System.Drawing"" name=""System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" />
    <data name=""Color1"" type=""System.Drawing.Color, System.Drawing"">Blue</data>
"));
            resxWithEmbeddedBitmap.ShouldHaveSingleItem();
            resxWithEmbeddedBitmap[0].ShouldBeOfType(typeof(TypeConverterStringResource));

            var resource = (TypeConverterStringResource)resxWithEmbeddedBitmap[0];
            resource.Name.ShouldBe("Color1");
            resource.TypeAssemblyQualifiedName.ShouldBe("System.Drawing.Color, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            resource.StringRepresentation.ShouldBe("Blue");
        }

        [Fact]
        public void ResXFileRefToBitmap()
        {
            string bitmapPath = Build.UnitTests.GenerateResource_Tests.Utilities.CreateWorldsSmallestBitmap();

            var resxWithLinkedBitmap = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
$@"  <data name='Image1' type='System.Resources.ResXFileRef, System.Windows.Forms'>
    <value>{bitmapPath};System.Drawing.Bitmap, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
"));
            resxWithLinkedBitmap.ShouldHaveSingleItem();
            resxWithLinkedBitmap[0].ShouldBeOfType(typeof(FileStreamResource));

            var resource = (FileStreamResource)resxWithLinkedBitmap[0];
            resource.Name.ShouldBe("Image1");
            resource.TypeAssemblyQualifiedName.ShouldBe("System.Drawing.Bitmap, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        }

        [Theory]
        [InlineData("System.IO.MemoryStream, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        [InlineData("System.IO.MemoryStream, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        public void ResXFileRefToMemoryStream(string typeNameInResx)
        {
            using var env = TestEnvironment.Create(_output);

            var baseDir = env.CreateFolder(createFolder: true);
            var resourceHandlingFolder = baseDir.CreateDirectory("ResourceHandling");

            var linkedTextFile = resourceHandlingFolder.CreateFile("FileToBeIncluded.txt");

            File.WriteAllText(linkedTextFile.Path,
                  "Test data");

            var resources = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
$@"  <data name='Image1' type='System.Resources.ResXFileRef, System.Windows.Forms'>
    <value>{linkedTextFile.Path};{typeNameInResx}</value>
  </data>
"));

            var resource = resources.ShouldHaveSingleItem()
                .ShouldBeOfType<LiveObjectResource>();
            resource.Name.ShouldBe("Image1");

            byte[] bytes = new byte[4];
            resource.Value
                .ShouldBeOfType<MemoryStream>()
                .Read(bytes, 0, 4);
            bytes.ShouldBe(new byte[] { 84, 101, 115, 116 }, "Expected the bytes of 'Test' to start the stream");
        }

        [Fact]
        public void AssemblyElementWithNoAliasInfersSimpleName()
        {
            var resxWithEmbeddedBitmap = MSBuildResXReader.GetResourcesFromString(
                ResXHelper.SurroundWithBoilerplate(
@"  <assembly name=""System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" />
    <data name=""Color1"" type=""System.Drawing.Color, System.Drawing""><value>Blue</value></data>
"));
            resxWithEmbeddedBitmap.ShouldHaveSingleItem();
            resxWithEmbeddedBitmap[0].ShouldBeOfType(typeof(TypeConverterStringResource));

            var resource = (TypeConverterStringResource)resxWithEmbeddedBitmap[0];
            resource.Name.ShouldBe("Color1");
            resource.TypeAssemblyQualifiedName.ShouldBe("System.Drawing.Color, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            resource.StringRepresentation.ShouldBe("Blue");
        }

        // TODO: invalid resx xml

        // TODO: valid xml, but invalid resx-specific data

        // TODO: aliased entry but no defined alias (failure)
        // TODO: alias but with reversed order (ResXResourceReader fails)

        // TODO: not-well-qualified types: ResXFileRef with no alias
        // TODO: not-well-qualified types: System.String with no assembly qualification
    }
}
