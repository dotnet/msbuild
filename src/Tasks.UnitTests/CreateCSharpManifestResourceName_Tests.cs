// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class CreateCSharpManifestResourceName_Tests
    {
        /// <summary>
        /// Test the basic functionality.
        /// </summary>
        [Fact]
        public void Basic()
        {
            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,
                    true,
                    null,    // Root namespace
                    null,
                    null,
                    StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    null
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
                    @"irrelevant",
                    null,
                    true,
                    null,    // Root namespace
                    null,
                    null,
                    sourcesStream,
                    null
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
                    @"irrelevant",
                    null,
                    true,
                    null,    // Root namespace
                    null,
                    null,
                    sourcesStream,
                    null
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
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,
                    true,
                    null,    // Root namespace
                    null,
                    null,
                    StreamHelpers.StringToStream("namespace Namespace { class Class {} }"),
                    null
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
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,
                    true,
                    "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    null,
                    null,
                    StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    null
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
                    @"f:\myproject\SubFolder\MyForm.en-GB.resx",
                    null,
                    true,
                    "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    null,
                    null,
                    StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    null
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
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,
                    true,
                    "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    null,
                    "en-GB",
                    StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    null
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
                    @"f:\myproject\SubFolder\MyForm.fr-fr.resx",
                    null,
                    true,
                    "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    null,
                    null,
                    StreamHelpers.StringToStream("namespace MyStuff.Namespace { class Class {} }"),
                    null
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
                    @"SubFolder\MyForm.en-GB.ResX",
                    null,
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
                );

            Assert.Equal("RootNamespace.SubFolder.MyForm.en-GB", result);
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
                    @"..\..\XmlEditor\Setup\XmlEditor.rgs",
                    @"XmlEditor.rgs",
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
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
                    @"SubFolder\SplashScreen.bmp",
                    null,
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
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
                    @"SubFolder\SplashScreen.fr.bmp",
                    null,
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
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
                    @"SubFolder\SplashScreen.fr.bmp",
                    null,    // Link file name
                    false,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
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

            Assert.Equal(1, resourceNames.Length);
            Assert.Equal(@"CustomToolTest.SR1", resourceNames[0].ItemSpec);
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
                    "MyForm.en-GB.resx",
                    null,
                    true,
                    "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    "MyForm.en-GB.cs",
                    null,
                    StreamHelpers.StringToStream("namespace ClassLibrary1 { class MyForm {} }"),
                    null
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
                    "MyForm.en-GB.resx",
                    null,
                    true,
                    "RootNamespace",
                    "MyForm.en-GB.cs",
                    null,
                    StreamHelpers.StringToStream(""),
                    null
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

            string result =
            CreateCSharpManifestResourceName.CreateManifestNameImpl
                (
                    "MyForm.resx",
                    null,
                    true,
                    "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    "MyForm.cs",
                    null,
                    StreamHelpers.StringToStream(
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
                    c.Log
                );

            Assert.True(
                m.Log.Contains
                (
                    String.Format(AssemblyResources.GetString("CreateManifestResourceName.DefinitionFoundWithinConditionalDirective"), "MyForm.cs", "MyForm.resx")
                )
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
            if (String.Compare(path, "SR1.strings", StringComparison.OrdinalIgnoreCase) == 0)
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

            Assert.Equal(1, resourceFiles.Length);
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

            Assert.Equal(1, resourceFiles.Length);
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

            Assert.Equal(1, resourceFiles.Length);
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

            Assert.Equal(1, resourceFiles.Length);
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
                    @"SubFolder\MyResource.fr.resources",
                    null,    // Link file name
                    false,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
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
                    @"MyResource.fr.resources",
                    null,    // Link file name
                    false,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
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
                    @"MyResource.resources",
                    null,    // Link file name
                    false,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null
                );

            Assert.Equal(@"RootNamespace.MyResource.resources", result);
        }
    }
}
