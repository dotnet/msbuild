// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    sealed public class CreateCSharpManifestResourceName_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public CreateCSharpManifestResourceName_Tests(ITestOutputHelper output)
        {
            _testOutput = output;
        }
        /// <summary>
        /// Test the basic functionality.
        /// </summary>
        [Fact]
        public void Basic()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: null,    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    log: null
                );

            Assert.Equal("MyStuff.Namespace.Class", result);
        }

        /// <summary>
        /// Test for a namespace that has ANSI but non-ascii characters.
        ///
        /// NOTE: namespace dÃa {} get's compiled into different IL depending on the language of the OS
        /// that its running on. This is because 'Ã' is a high ANSI character which is interpreted differently
        /// for different codepages.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact (Skip = "https://github.com/Microsoft/msbuild/issues/295")]
#else
        [Fact]
#endif
        [Trait("Category", "mono-osx-failing")]
        public void Regress172107()
        {
            // Can't embed the 'Ã' directly because the string is Unicode already and the Unicode<-->ANSI transform
            // isn't bidirectional.
            MemoryStream sourcesStream = (MemoryStream)StreamHelpers.StringToStream("namespace d?a { class Class {} }");

            // Instead, directly write the ANSI character into the memory buffer.
            sourcesStream.Seek(11, SeekOrigin.Begin);
            sourcesStream.WriteByte(0xc3);    // Plug the 'Ã' in
            sourcesStream.Seek(0, SeekOrigin.Begin);

            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"irrelevant",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: null,    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: sourcesStream,
                    log: null
                );

            MemoryStream m = new MemoryStream();
            m.Write(new byte[] { 0x64, 0xc3, 0x61, 0x2e, 0x43, 0x6c, 0x61, 0x73, 0x73 }, 0, 9); // dÃa.Class in ANSI
            m.Flush();
            m.Seek(0, SeekOrigin.Begin);
#if FEATURE_ENCODING_DEFAULT
            StreamReader r = new StreamReader(m, System.Text.Encoding.Default, true); // HIGHCHAR: Test reads ANSI because that's the scenario.
#else
            StreamReader r = new StreamReader(m, System.Text.Encoding.ASCII, true); // HIGHCHAR: Test reads ANSI because that's the scenario.
#endif
            string className = r.ReadToEnd();

            Assert.Equal(className, result);
        }

        /// <summary>
        /// Test for a namespace that has UTF8 characters but there's no BOM at the start.
        ///
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact (Skip = "https://github.com/Microsoft/msbuild/issues/295")]
#else
        [Fact]
#endif
        [Trait("Category", "mono-osx-failing")]
        public void Regress249540()
        {
            // Special character is 'Ä' in UTF8: 0xC3 84
            MemoryStream sourcesStream = (MemoryStream)StreamHelpers.StringToStream("namespace d??a { class Class {} }");

            // Instead, directly write the ANSI character into the memory buffer.
            sourcesStream.Seek(11, SeekOrigin.Begin);
            sourcesStream.WriteByte(0xc3);    // Plug the first byte of 'Ä' in.
            sourcesStream.WriteByte(0x84);    // Plug the second byte of 'Ä' in.
            sourcesStream.Seek(0, SeekOrigin.Begin);

            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"irrelevant",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: null,    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: sourcesStream,
                    log: null
                );

            Assert.Equal("d\u00C4a.Class", result);
        }

        /// <summary>
        /// Test a dependent with a relative path
        /// </summary>
        [Fact]
        public void RelativeDependentUpon()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: null,    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream("namespace Namespace { class Class {} }"),
                    log: null
                );

            Assert.Equal("Namespace.Class", result);
        }

        /// <summary>
        /// Test a dependent with a relative path
        /// </summary>
        [Fact]
        public void AbsoluteDependentUpon()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    log: null
                );

            Assert.Equal("MyStuff.Namespace.Class", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture.
        /// </summary>
        [Fact]
        public void DependentWithCulture()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"f:\myproject\SubFolder\MyForm.en-GB.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    log: null
                );

            Assert.Equal("MyStuff.Namespace.Class.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture that was expressed in the metadata of the
        /// item rather than the filename.
        /// </summary>
        [Fact]
        public void DependentWithCultureMetadata()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    dependentUponFileName: null,
                    culture: "en-GB",
                    binaryStream: StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    log: null
                );

            Assert.Equal("MyStuff.Namespace.Class.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture embedded in the .RESX filename.
        /// </summary>
        [Fact]
        public void DependentWithEmbeddedCulture()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"f:\myproject\SubFolder\MyForm.fr-fr.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    log: null
                );

            Assert.Equal("MyStuff.Namespace.Class.fr-fr", result);
        }

        /// <summary>
        /// No dependent class, but there is a root namespace place.  Also, the .resx
        /// extension contains some upper-case characters.
        /// </summary>
        [Fact]
        public void RootnamespaceWithCulture()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"SubFolder\MyForm.en-GB.ResX",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal("RootNamespace.SubFolder.MyForm.en-GB", result);
        }

        /// <summary>
        /// Explicitly retain culture
        /// </summary>
        [Fact]
        public void RootnamespaceWithCulture_RetainCultureInFileName()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"Subfolder\File.cs.cshtml",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null,
                    treatAsCultureNeutral: true // retain culture in name
                );

            result.ShouldBe("RootNamespace.Subfolder.File.cs.cshtml");
        }

        /// <summary>
        /// If there is a link file name then it is preferred over the main file name.
        /// </summary>
        [Fact]
        public void Regress222308()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"..\..\XmlEditor\Setup\XmlEditor.rgs",
                    linkFileName: @"XmlEditor.rgs",
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal("RootNamespace.XmlEditor.rgs", result);
        }

        /// <summary>
        /// A non-resx file in a subfolder, with a root namespace.
        /// </summary>
        [Fact]
        public void BitmapWithRootNamespace()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"SubFolder\SplashScreen.bmp",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal("RootNamespace.SubFolder.SplashScreen.bmp", result);
        }

        /// <summary>
        /// A culture-specific non-resx file in a subfolder, with a root namespace.
        /// </summary>
        [Fact]
        public void CulturedBitmapWithRootNamespace()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"SubFolder\SplashScreen.fr.bmp",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal(FileUtilities.FixFilePath(@"fr\RootNamespace.SubFolder.SplashScreen.bmp"), result);
        }

        /// <summary>
        /// A culture-specific non-resx file in a subfolder, with a root namespace, but no culture directory prefix
        /// </summary>
        [Fact]
        public void CulturedBitmapWithRootNamespaceNoDirectoryPrefix()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"SubFolder\SplashScreen.fr.bmp",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal(@"RootNamespace.SubFolder.SplashScreen.bmp", result);
        }

        /// <summary>
        /// If the filename passed in as the "DependentUpon" file doesn't end in .cs then
        /// we want to fall back to the RootNamespace+FileName logic.
        /// </summary>
        [Fact]
        public void Regress188319()
        {
            CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("SR1.resx");
            i.SetMetadata("BuildAction", "EmbeddedResource");
            i.SetMetadata("DependentUpon", "SR1.strings");        // Normally, this would be a C# file.
            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "CustomToolTest";
            bool success = t.Execute
            (
                new Microsoft.Build.Tasks.CreateFileStream(CreateFileStream)
            );

            Assert.True(success); // "Expected the task to succeed."

            ITaskItem[] resourceNames = t.ManifestResourceNames;

            Assert.Single(resourceNames);
            Assert.Equal(@"CustomToolTest.SR1", resourceNames[0].ItemSpec);
        }

        /// <summary>
        /// Opt into DependentUpon convention and load the expected file properly.
        /// </summary>
        [Fact]
        public void DependentUponConvention_FindsMatch()
        {
            using (var env = TestEnvironment.Create(_testOutput))
            {
                var csFile = env.CreateFile("SR1.cs", "namespace MyStuff.Namespace { class Class { } }");
                var resXFile = env.CreateFile("SR1.resx", "");

                ITaskItem i = new TaskItem(resXFile.Path);
                i.SetMetadata("BuildAction", "EmbeddedResource");
                // Don't set DependentUpon so it goes by convention

                CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName
                {
                    BuildEngine = new MockEngine(_testOutput),
                    UseDependentUponConvention = true,
                    ResourceFiles = new ITaskItem[] { i }
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed.");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                t.ManifestResourceNames[0].ItemSpec.ShouldBe("MyStuff.Namespace.Class", "Expecting to find the namespace & class name from SR1.cs");
            }
        }

        /// <summary>
        /// Opt into DependentUpon convention but don't expect it to be used for this file.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DependentUponConvention_DoesNotApplyToNonResx(bool explicitlySpecifyType)
        {
            using (var env = TestEnvironment.Create())
            {
                var csFile = env.CreateFile("SR1.cs", "namespace MyStuff.Namespace { class Class { } }");
                const string ResourceFileName = "SR1.txt";
                var resourceFile = env.CreateFile(ResourceFileName, "");

                // Default resource naming is based on the item include, so use a relative
                // path here instead of a full path.
                env.SetCurrentDirectory(Path.GetDirectoryName(resourceFile.Path));
                ITaskItem i = new TaskItem(ResourceFileName);
                i.SetMetadata("BuildAction", "EmbeddedResource");
                if (explicitlySpecifyType)
                {
                    i.SetMetadata("Type", "Non-Resx");
                }
                // Don't set DependentUpon so it goes by convention

                CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName
                {
                    BuildEngine = new MockEngine(_testOutput),
                    UseDependentUponConvention = true,
                    ResourceFiles = new ITaskItem[] { i }
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed.");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                t.ManifestResourceNames[0].ItemSpec.ShouldBe(ResourceFileName, "Expecting to find the namespace & class name from SR1.cs");
            }
        }

        /// <summary>
        /// Opt into DependentUpon convention and load the expected file properly when the file is in a subfolder.
        /// </summary>
        [Fact]
        public void DependentUponConvention_FindsMatchInSubfolder()
        {
            using (var env = TestEnvironment.Create())
            {
                var subfolder = env.DefaultTestDirectory.CreateDirectory("SR1");
                var csFile = subfolder.CreateFile("SR1.cs", "namespace MyStuff.Namespace { class Class { } }");
                var resXFile = subfolder.CreateFile("SR1.resx", "");

                env.SetCurrentDirectory(env.DefaultTestDirectory.Path);

                ITaskItem i = new TaskItem(@"SR1\SR1.resx");
                i.SetMetadata("BuildAction", "EmbeddedResource");
                // Don't set DependentUpon so it goes by convention

                CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName
                {
                    BuildEngine = new MockEngine(_testOutput),
                    UseDependentUponConvention = true,
                    ResourceFiles = new ITaskItem[] { i }
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed.");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                t.ManifestResourceNames[0].ItemSpec.ShouldBe("MyStuff.Namespace.Class", "Expecting to find the namespace & class name from SR1.cs");
            }
        }

        /// <summary>
        /// Opt into DependentUpon convention without creating the equivalent .cs file for our resource file.
        /// </summary>
        [Fact]
        public void DependentUpon_UseConventionFileDoesNotExist()
        {
            using (var env = TestEnvironment.Create())
            {
                // cs file doesn't exist for this case.
                var resXFile = env.CreateFile("SR1.resx", "");

                ITaskItem i = new TaskItem(Path.GetFileName(resXFile.Path));
                i.SetMetadata("BuildAction", "EmbeddedResource");
                // Don't set DependentUpon so it goes by convention

                // Use relative paths to ensure short manifest name based on the path to the resx.
                // See CreateManifestNameImpl
                env.SetCurrentDirectory(Path.GetDirectoryName(resXFile.Path));

                CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName
                {
                    BuildEngine = new MockEngine(_testOutput),
                    UseDependentUponConvention = true,
                    ResourceFiles = new ITaskItem[] { i }
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed.");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                t.ManifestResourceNames[0].ItemSpec.ShouldBe("SR1", "Expected only the file name.");
            }
        }

        /// <summary>
        /// Opt into DependentUponConvention, but include DependentUpon metadata with different name.
        /// </summary>
        [Fact]
        public void DependentUpon_SpecifyNewFile()
        {
            using (var env = TestEnvironment.Create())
            {
                var conventionCSFile = env.CreateFile("SR1.cs", "namespace MyStuff.Namespace { class Class { } }");
                var nonConventionCSFile = env.CreateFile("SR2.cs", "namespace MyStuff2.Namespace { class Class2 { } }");
                var resXFile = env.CreateFile("SR1.resx", "");

                ITaskItem i = new TaskItem(resXFile.Path);
                i.SetMetadata("BuildAction", "EmbeddedResource");
                i.SetMetadata("DependentUpon", "SR2.cs");

                CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName
                {
                    BuildEngine = new MockEngine(_testOutput),
                    UseDependentUponConvention = true,
                    ResourceFiles = new ITaskItem[] { i }
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed.");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                t.ManifestResourceNames[0].ItemSpec.ShouldBe("MyStuff2.Namespace.Class2", "Expected the namespace & class of SR2.");
            }
        }

        /// <summary>
        /// When disabling UseDependentUponConvention it will find no .cs file and default to filename.
        /// </summary>
        [Fact]
        public void DependentUponConvention_ConventionDisabledDoesNotReadConventionFile()
        {
            using (var env = TestEnvironment.Create())
            {
                var csFile = env.CreateFile("SR1.cs", "namespace MyStuff.Namespace { class Class { } }");
                var resXFile = env.CreateFile("SR1.resx", "");

                ITaskItem i = new TaskItem(Path.GetFileName(resXFile.Path));
                i.SetMetadata("BuildAction", "EmbeddedResource");
                // No need to set DependentUpon

                // Use relative paths to ensure short manifest name based on the path to the resx.
                // See CreateManifestNameImpl
                env.SetCurrentDirectory(Path.GetDirectoryName(resXFile.Path));

                CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName
                {
                    BuildEngine = new MockEngine(_testOutput),
                    UseDependentUponConvention = false,
                    ResourceFiles = new ITaskItem[] { i }
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed.");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                t.ManifestResourceNames[0].ItemSpec.ShouldBe("SR1", "Expected only the file name.");
            }
        }

        /// <summary>
        /// If we have a resource file that has a culture within it's name (resourceFile.de.cs), find it by convention.
        /// </summary>
        [Fact]
        public void CulturedResourceFileFindByConvention()
        {
            using (var env = TestEnvironment.Create(_testOutput))
            {
                var csFile = env.CreateFile("SR1.cs", "namespace MyStuff.Namespace { class Class { } }");
                var resXFile = env.CreateFile("SR1.de.resx", "");

                ITaskItem i = new TaskItem(resXFile.Path);

                i.SetMetadata("BuildAction", "EmbeddedResource");

                // this data is set automatically through the AssignCulture task, so we manually set it here
                i.SetMetadata("WithCulture", "true");
                i.SetMetadata("Culture", "de");

                env.SetCurrentDirectory(Path.GetDirectoryName(resXFile.Path));

                CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName
                {
                    BuildEngine = new MockEngine(),
                    UseDependentUponConvention = true,
                    ResourceFiles = new ITaskItem[] { i },
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                // CreateManifestNameImpl appends culture to the end of the convention
                t.ManifestResourceNames[0].ItemSpec.ShouldBe("MyStuff.Namespace.Class.de", "Expected Namespace.Class.Culture");
            }
        }

        /// <summary>
        /// helper method for verifying manifest resource names
        /// </summary>
        private void VerifyExpectedManifestResourceName(string resourcePath, string expectedName)
        {
            string result = CreateCSharpManifestResourceName.CreateManifestNameImpl(resourcePath, null, true, "Root", null, null, null, null);
            string expected = "Root." + expectedName;

            Assert.Equal(result, expected);
        }

        /// <summary>
        /// Need to convert any spaces in the directory name of embedded resource files to underscores.
        /// Leave spaces in the file name itself alone. That's how Everett did it.
        /// </summary>
        [Fact]
        public void Regress309027()
        {
            VerifyExpectedManifestResourceName(
                @"SubFolder With Spaces\Splash Screen.bmp", "SubFolder_With_Spaces.Splash Screen.bmp");
        }

        /// <summary>
        /// The folder part of embedded resource names (not the file name though) needs to be a proper identifier,
        /// since that's how Everett used to do this
        /// </summary>
        [Fact]
        public void Regress311473()
        {
            // First char must be a letter or a connector (underscore), others must be a letter/digit/connector or a combining mark
            // If the first character is not a valid first character but valid subsequent character, the name is prepended
            // with an underscore. Invalid subsequent characters are replaced with an underscore.
            VerifyExpectedManifestResourceName(@"1abc()\pic.bmp", "_1abc__.pic.bmp");

            // if the first character is not a valid id character at all, it's replaced with an underscore instead of
            // prepending an underscore to it
            VerifyExpectedManifestResourceName(@"@abc\pic.bmp", "_abc.pic.bmp");

            // Each folder name is processed independently
            VerifyExpectedManifestResourceName(@"1234\1abc\pic.bmp", "_1234._1abc.pic.bmp");

            // Each part of folder name separated by dots is processed independently as well
            VerifyExpectedManifestResourceName(@"1abc.@abc@._1234()\pic.bmp", "_1abc._abc_._1234__.pic.bmp");
            VerifyExpectedManifestResourceName(@"1abc\@abc@\_1234()\pic.bmp", "_1abc._abc_._1234__.pic.bmp");

            // Combination of dots and folders
            VerifyExpectedManifestResourceName(@"@Ab2.=gh\1hl.l=a1\pic.bmp", "_Ab2._gh._1hl.l_a1.pic.bmp");

            // A single underscore folder name is expanded to two underscores
            VerifyExpectedManifestResourceName(@"_\pic.bmp", "__.pic.bmp");

            // A more complex example of the last rule
            VerifyExpectedManifestResourceName(@"_\__\_.__\_\pic.bmp", "__.__._.__.__.pic.bmp");
        }

        /// <summary>
        /// If the dependent upon filename and the resource filename both contain what looks like
        /// a culture, do not treat it as a culture identifier.  E.g.:
        ///
        ///     Form1.ro.resx == DependentUpon ==> Form1.ro.vb
        ///
        /// In this case, we don't include "ro" as the culture because it's in both filenames.  In
        /// the case of:
        ///
        ///     Form1.ro.resx == DependentUpon ==> Form1.vb
        ///
        /// we continue to treat "ro" as the culture.
        /// </summary>
        [Fact]
        public void Regress419591()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: "MyForm.en-GB.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    dependentUponFileName: "MyForm.en-GB.cs",
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream("namespace ClassLibrary1 { class MyForm {} }"),
                    log: null
                );

            Assert.Equal("ClassLibrary1.MyForm", result);
        }

        /// <summary>
        /// If the dependent upon filename and the resource filename both contain what looks like
        /// a culture, do not treat it as a culture identifier.  E.g.:
        ///
        ///     Form1.ro.resx == DependentUpon ==> Form1.ro.vb
        ///
        /// In this case, we don't include "ro" as the culture because it's in both filenames.  If
        /// The parent source file doesn't have a class name in it, we just use the culture neutral
        /// filename of the resource file.
        /// </summary>
        [Fact]
        public void Regress419591_EmptySource()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: "MyForm.en-GB.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",
                    dependentUponFileName: "MyForm.en-GB.cs",
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(""),
                    log: null
                );

            Assert.Equal("RootNamespace.MyForm.en-GB", result);
        }

        /// <summary>
        /// If we encounter a class or namespace name within a conditional compilation directive,
        /// we need to warn because we do not try to resolve the correct manifest name depending
        /// on conditional compilation of code.
        /// </summary>
        [Fact]
        public void Regress459265()
        {
            MockEngine m = new MockEngine();
            CreateCSharpManifestResourceName c = new CreateCSharpManifestResourceName();
            c.BuildEngine = m;

            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: "MyForm.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    dependentUponFileName: "MyForm.cs",
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"using System;
#if false
namespace ClassLibrary1
#endif
#if Debug
namespace ClassLibrary2
#else
namespace ClassLibrary3
#endif
{
    class MyForm 
    {
    }
}"
                    ),
                    log: c.Log
                );

            Assert.Contains(
                String.Format(AssemblyResources.GetString("CreateManifestResourceName.DefinitionFoundWithinConditionalDirective"), "MyForm.cs", "MyForm.resx"),
                m.Log
            );
        }

        /// <summary>
        /// Given a file path, return a stream on top of that path.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="mode">File mode</param>
        /// <param name="access">Access type</param>
        /// <returns>The Stream</returns>
        private Stream CreateFileStream(string path, FileMode mode, FileAccess access)
        {
            if (String.Equals(path, "SR1.strings", StringComparison.OrdinalIgnoreCase))
            {
                return StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }");
            }
            Assert.True(false, String.Format("Encountered a new path {0}, needs unittesting support", path));
            return null;
        }

        /// <summary>
        /// Tests to ensure that the ResourceFilesWithManifestResourceNames contains everything that
        /// the ResourceFiles property on the task contains, but with additional metadata called ManifestResourceName
        /// </summary>
        [Fact]
        public void ResourceFilesWithManifestResourceNamesContainsAdditionalMetadata()
        {
            CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("strings.resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.True(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.Single(resourceFiles);
            Assert.Equal(@"strings.resx", resourceFiles[0].ItemSpec);
            Assert.Equal(@"ResourceRoot.strings", resourceFiles[0].GetMetadata("ManifestResourceName"));
        }

        /// <summary>
        /// Ensure that if no LogicalName is specified, that the same ManifestResourceName metadata
        /// gets applied as LogicalName
        /// </summary>
        [Fact]
        public void AddLogicalNameForNonResx()
        {
            CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("pic.bmp");
            i.SetMetadata("Type", "Non-Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.True(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.Single(resourceFiles);
            Assert.Equal(@"pic.bmp", resourceFiles[0].ItemSpec);
            Assert.Equal(@"ResourceRoot.pic.bmp", resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// Ensure that a LogicalName that is already present is preserved during manifest name generation
        /// </summary>
        [Fact]
        public void PreserveLogicalNameForNonResx()
        {
            CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("pic.bmp");
            i.SetMetadata("LogicalName", "foo");
            i.SetMetadata("Type", "Non-Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.True(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.Single(resourceFiles);
            Assert.Equal(@"pic.bmp", resourceFiles[0].ItemSpec);
            Assert.Equal(@"foo", resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// Resx resources should not get ManifestResourceName metadata copied to the LogicalName value
        /// </summary>
        [Fact]
        public void NoLogicalNameAddedForResx()
        {
            CreateCSharpManifestResourceName t = new CreateCSharpManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("strings.resx");
            i.SetMetadata("Type", "Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.True(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.Single(resourceFiles);
            Assert.Equal(@"strings.resx", resourceFiles[0].ItemSpec);
            Assert.Equal(String.Empty, resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// A culture-specific resources file in a subfolder, with a root namespace
        /// </summary>
        [Fact]
        public void CulturedResourcesFileWithRootNamespaceWithinSubfolder()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"SubFolder\MyResource.fr.resources",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal(@"RootNamespace.SubFolder.MyResource.fr.resources", result);
        }

        /// <summary>
        /// A culture-specific resources file with a root namespace
        /// </summary>
        [Fact]
        public void CulturedResourcesFileWithRootNamespace()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"MyResource.fr.resources",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal(@"RootNamespace.MyResource.fr.resources", result);
        }

        /// <summary>
        /// A non-culture-specific resources file with a root namespace
        /// </summary>
        [Fact]
        public void ResourcesFileWithRootNamespace()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    fileName: @"MyResource.resources",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null
                );

            Assert.Equal(@"RootNamespace.MyResource.resources", result);
        }
    }
}
