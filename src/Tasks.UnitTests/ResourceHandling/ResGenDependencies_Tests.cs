// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.ResourceHandling;
using Microsoft.Build.Tasks.UnitTests.ResourceHandling;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class ResGenDependencies_Tests : IDisposable
    {
        private readonly TestEnvironment _env;
        private readonly ITestOutputHelper _output;

        public ResGenDependencies_Tests(ITestOutputHelper output)
        {
            _env = TestEnvironment.Create(output);
            _output = output;
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        [Theory]
        [MemberData(nameof(GenerateResource_Tests.Utilities.UsePreserializedResourceStates), MemberType = typeof(GenerateResource_Tests.Utilities))]

        public void DirtyCleanScenario(bool useMSBuildResXReader)
        {
            ResGenDependencies cache = new();
            string resx = CreateSampleResx();
            string stateFile = FileUtilities.GetTemporaryFile();

            try
            {
                // A newly created cache is not dirty.
                cache.IsDirty.ShouldBeFalse();

                ResGenDependencies.PortableLibraryFile libFile = new("otherFileName");
                libFile.outputFiles = new string[] { "first", "second" };
                libFile.assemblySimpleName = "simpleName";
                libFile.lastModified = DateTime.Now.Subtract(TimeSpan.FromSeconds(10));
                cache.portableLibraries.Add("fileName", libFile);

                // Writing the file to disk should make the cache clean.
                cache.SerializeCache(stateFile, /* Log */ null);
                cache.IsDirty.ShouldBeFalse();

                // Getting a file that wasn't in the cache is a write operation.
                cache.GetResXFileInfo(resx, useMSBuildResXReader, null, false);
                cache.IsDirty.ShouldBeTrue();

                // Add linkedFiles to further test serialization and deserialization.
                cache.resXFiles.TryGetValue(resx, out ResGenDependencies.ResXFile file).ShouldBeTrue();
                file.linkedFiles = new string[] { "third", "fourth" };

                // Writing the file to disk should make the cache clean again.
                cache.SerializeCache(stateFile, /* Log */ null);
                cache.IsDirty.ShouldBeFalse();

                // Deserialize from disk. Result should not be dirty.
                ResGenDependencies cache2 = ResGenDependencies.DeserializeCache(stateFile, true, /* Log */ null);
                cache2.IsDirty.ShouldBeFalse();

                // Validate that serialization worked
                cache.portableLibraries.TryGetValue("fileName", out ResGenDependencies.PortableLibraryFile portableLibrary);
                cache2.portableLibraries.TryGetValue("fileName", out ResGenDependencies.PortableLibraryFile portableLibrary2);
                portableLibrary2.filename.ShouldBe(portableLibrary.filename);
                portableLibrary2.exists.ShouldBe(portableLibrary.exists);
                portableLibrary2.assemblySimpleName.ShouldBe(portableLibrary.assemblySimpleName);
                portableLibrary2.lastModified.ShouldBe(portableLibrary.lastModified);
                portableLibrary2.outputFiles.Length.ShouldBe(portableLibrary.outputFiles.Length);
                portableLibrary2.outputFiles[1].ShouldBe(portableLibrary.outputFiles[1]);
                cache.resXFiles.TryGetValue(resx, out ResGenDependencies.ResXFile resX);
                cache2.resXFiles.TryGetValue(resx, out ResGenDependencies.ResXFile resX2);
                resX2.filename.ShouldBe(resX.filename);
                resX2.lastModified.ShouldBe(resX.lastModified);
                resX2.linkedFiles.Length.ShouldBe(resX.linkedFiles.Length);
                resX2.linkedFiles[1].ShouldBe(resX.linkedFiles[1]);

                // Asking for a file that's in the cache should not dirty the cache.
                cache2.GetResXFileInfo(resx, useMSBuildResXReader, null, false);
                cache2.IsDirty.ShouldBeFalse();

                // Changing UseSourcePath to false should dirty the cache.
                cache2.UseSourcePath = false;
                cache2.IsDirty.ShouldBeTrue();
            }
            finally
            {
                File.Delete(resx);
                File.Delete(stateFile);
            }
        }

        /// <summary>
        /// When useMSBuildResXReader is true, GetResXFileInfo should track linked files
        /// for all resource types that produce an ILinkedFileResource: System.String,
        /// System.Byte[], System.IO.MemoryStream, and FileStreamResource types
        /// (e.g. System.Drawing.Bitmap).
        /// </summary>
        [Fact]
        public void LinkedFilesTrackedForAllResourceTypes()
        {
            var folder = _env.CreateFolder(createFolder: true);

            // Create four linked files representing each code path in AddLinkedResource.
            var textFile = folder.CreateFile("linked.txt", "hello");
            var byteFile = folder.CreateFile("linked.bin", "bytes");
            var memStreamFile = folder.CreateFile("linked.dat", "stream");

            // Create a minimal bitmap file inside the managed folder.
            string bitmapPath = Path.Combine(folder.Path, "linked.bmp");
            byte[] bmp = new byte[66];
            bmp[0x00] = 0x42; bmp[0x01] = 0x4D; bmp[0x02] = 0x42;
            bmp[0x0a] = 0x3E; bmp[0x0e] = 0x28; bmp[0x12] = 0x01; bmp[0x16] = 0x01;
            bmp[0x1a] = 0x01; bmp[0x1c] = 0x01; bmp[0x22] = 0x04;
            bmp[0x3a] = 0xFF; bmp[0x3b] = 0xFF; bmp[0x3c] = 0xFF;
            bmp[0x3e] = 0x80;
            File.WriteAllBytes(bitmapPath, bmp);

            string resxPath = Path.Combine(folder.Path, "test.resx");
            File.WriteAllText(resxPath, ResXHelper.SurroundWithBoilerplate(
        $@"  <assembly alias=""System.Windows.Forms"" name=""System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" />
        <data name=""TextResource"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
            <value>{textFile.Path};System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;utf-8</value>
        </data>
        <data name=""ByteArrayResource"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
            <value>{byteFile.Path};System.Byte[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
        </data>
        <data name=""MemoryStreamResource"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
            <value>{memStreamFile.Path};System.IO.MemoryStream, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
        </data>
        <data name=""BitmapResource"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
            <value>{bitmapPath};System.Drawing.Bitmap, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
        </data>
        "));

            var cache = new ResGenDependencies();
            ResGenDependencies.ResXFile resxFile = cache.GetResXFileInfo(resxPath, useMSBuildResXReader: true, log: null, logWarningForBinaryFormatter: false);

            resxFile.LinkedFiles.ShouldNotBeNull();
            resxFile.LinkedFiles.Length.ShouldBe(4);
            resxFile.LinkedFiles.ShouldContain(textFile.Path);
            resxFile.LinkedFiles.ShouldContain(byteFile.Path);
            resxFile.LinkedFiles.ShouldContain(memStreamFile.Path);
            resxFile.LinkedFiles.ShouldContain(bitmapPath);
        }

        /// <summary>
        /// Create a sample resx file on disk. Caller is responsible for deleting.
        /// </summary>
        /// <returns></returns>
        private string CreateSampleResx()
        {
            string resx = FileUtilities.GetTemporaryFileName();
            Stream fileToSend = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Build.Tasks.UnitTests.SampleResx");
            using (FileStream f = new FileStream(resx, FileMode.CreateNew))
            {
                byte[] buffer = new byte[2048];
                int bytes;
                while ((bytes = fileToSend.Read(buffer, 0, 2048)) > 0)
                {
                    f.Write(buffer, 0, bytes);
                }
                fileToSend.Close();
            }
            return resx;
        }
    }
}
