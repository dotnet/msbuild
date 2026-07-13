// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class CreateVisualBasicManifestResourceName_Tests
    {
        private readonly TestContext _testOutput;

        public CreateVisualBasicManifestResourceName_Tests(TestContext output)
        {
            _testOutput = output;
        }

        /// <summary>
        /// Test the basic functionality.
        /// </summary>
        [MSBuildTestMethod]
        public void Basic()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: null,    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace
    Class TestClass
    End Class
End Namespace
"),
                    log: null);

            Assert.AreEqual("Nested.TestNamespace.TestClass", result);
        }

        /// <summary>
        /// Test a dependent with a relative path
        /// </summary>
        [MSBuildTestMethod]
        public void RelativeDependentUpon()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: null,    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"
Namespace TestNamespace
    Class TestClass
    End Class
End Namespace
"),
                    log: null);

            Assert.AreEqual("TestNamespace.TestClass", result);
        }

        /// <summary>
        /// Test a dependent with a relative path
        /// </summary>
        [MSBuildTestMethod]
        public void AbsoluteDependentUpon()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: null,    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace
    Class TestClass
    End Class
End Namespace
"),
                    log: null);

            Assert.AreEqual("Nested.TestNamespace.TestClass", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture.
        /// </summary>
        [MSBuildTestMethod]
        public void DependentWithCulture()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"f:\myproject\SubFolder\MyForm.en-GB.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: null,        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace
    Class TestClass
    End Class
End Namespace
"),
                    log: null);

            Assert.AreEqual("Nested.TestNamespace.TestClass.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture that was expressed in the metadata of the
        /// item rather than the filename.
        /// </summary>
        [MSBuildTestMethod]
        public void DependentWithCultureMetadata()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"f:\myproject\SubFolder\MyForm.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: null,        // Root namespace
                    dependentUponFileName: null,
                    culture: "en-GB",
                    binaryStream: StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace
    Class TestClass
    End Class
End Namespace
"),
                    log: null);

            Assert.AreEqual("Nested.TestNamespace.TestClass.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture and a root namespace.
        /// </summary>
        [MSBuildTestMethod]
        public void DependentWithCultureAndRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"f:\myproject\SubFolder\MyForm.en-GB.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace
    Class TestClass
    End Class
End Namespace
"),
                    log: null);

            Assert.AreEqual("RootNamespace.Nested.TestNamespace.TestClass.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture embedded in the .RESX filename.
        /// </summary>
        [MSBuildTestMethod]
        public void DependentWithEmbeddedCulture()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"f:\myproject\SubFolder\MyForm.fr-fr.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace
    Class TestClass
    End Class
End Namespace
"),
                    log: null);

            Assert.AreEqual("RootNamespace.Nested.TestNamespace.TestClass.fr-fr", result);
        }

        /// <summary>
        /// No dependent class, but there is a root namespace place.  Also, the .resx
        /// extension contains some upper-case characters.
        /// </summary>
        [MSBuildTestMethod]
        public void RootnamespaceWithCulture()
        {
            string result =
                CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: FileUtilities.FixFilePath(@"SubFolder\MyForm.en-GB.ResX"),
                    linkFileName: null,
                    // Link file name
                    prependCultureAsDirectory:
                    // Link file name
                    true,
                    rootNamespace: "RootNamespace",
                    // Root namespace
                    dependentUponFileName:
                    // Root namespace
                    null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual("RootNamespace.MyForm.en-GB", result);
        }

        [MSBuildTestMethod]
        public void RootnamespaceWithCulture_RetainCultureInFileName()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"File.cs.vb",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null,
                    treatAsCultureNeutral: true); // retain culture in name

            // Visual basic does not carry subfolder.
            // See https://github.com/dotnet/msbuild/pull/5477
            result.ShouldBe("RootNamespace.File.cs.vb");
        }

        /// <summary>
        /// If there is a link file name then it is preferred over the main file name.
        /// </summary>
        [MSBuildTestMethod]
        public void Regress222308()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"..\..\XmlEditor\Setup\XmlEditor.rgs",
                    linkFileName: @"MyXmlEditor.rgs",
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual("RootNamespace.MyXmlEditor.rgs", result);
        }

        /// <summary>
        /// A non-resx file in a subfolder, with a root namespace.
        /// </summary>
        [MSBuildTestMethod]
        public void BitmapWithRootNamespace()
        {
            string result =
                CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: FileUtilities.FixFilePath(@"SubFolder\SplashScreen.bmp"),
                    linkFileName: null,             // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace", // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual("RootNamespace.SplashScreen.bmp", result);
        }

        /// <summary>
        /// A culture-specific non-resx file in a subfolder, with a root namespace.
        /// </summary>
        [MSBuildTestMethod]
        public void CulturedBitmapWithRootNamespace()
        {
            string result =
                CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: FileUtilities.FixFilePath(@"SubFolder\SplashScreen.fr.bmp"),
                    linkFileName: null,             // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",  // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual(FileUtilities.FixFilePath(@"fr\RootNamespace.SplashScreen.bmp"), result);
        }

        /// <summary>
        /// A culture-specific non-resx file in a subfolder, with a root namespace, but no culture directory prefix
        /// </summary>
        [MSBuildTestMethod]
        public void CulturedBitmapWithRootNamespaceNoDirectoryPrefix()
        {
            string result =
                CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: FileUtilities.FixFilePath(@"SubFolder\SplashScreen.fr.bmp"),
                    linkFileName: null,             // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",  // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual(@"RootNamespace.SplashScreen.bmp", result);
        }

        /// <summary>
        /// If the filename passed in as the "DependentUpon" file doesn't end in .cs then
        /// we want to fall back to the RootNamespace+FileName logic.
        /// </summary>
        [MSBuildTestMethod]
        public void Regress188319()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();

            ITaskItem i = new TaskItem("SR1.resx");

            i.SetMetadata("BuildAction", "EmbeddedResource");
            i.SetMetadata("DependentUpon", "SR1.strings");        // Normally, this would be a C# file.
            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "CustomToolTest";

            bool success = t.Execute(new Microsoft.Build.Tasks.CreateFileStream(CreateFileStream));

            Assert.IsTrue(success); // "Expected the task to succeed."

            ITaskItem[] resourceNames = t.ManifestResourceNames;

            Assert.ContainsSingle(resourceNames);
            Assert.AreEqual(@"CustomToolTest.SR1", resourceNames[0].ItemSpec);
        }

        /// <summary>
        /// If we have a resource file that has a culture within it's name (resourceFile.de.cs), find it by convention.
        /// </summary>
        [MSBuildTestMethod]
        public void CulturedResourceFileFindByConvention()
        {
            using (var env = TestEnvironment.Create(_testOutput))
            {
                var csFile = env.CreateFile("SR1.vb", @"
Namespace MyStuff
    Class Class2
    End Class
End Namespace");
                var resXFile = env.CreateFile("SR1.de.resx", "");

                ITaskItem i = new TaskItem(resXFile.Path);

                i.SetMetadata("BuildAction", "EmbeddedResource");

                // this data is set automatically through the AssignCulture task, so we manually set it here
                i.SetMetadata("WithCulture", "true");
                i.SetMetadata("Culture", "de");

                env.SetCurrentDirectory(Path.GetDirectoryName(resXFile.Path));

                CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName
                {
                    BuildEngine = new MockEngine(_testOutput),
                    UseDependentUponConvention = true,
                    ResourceFiles = new ITaskItem[] { i },
                };

                t.Execute().ShouldBeTrue("Expected the task to succeed");

                t.ManifestResourceNames.ShouldHaveSingleItem();

                // CreateManifestNameImpl appends culture to the end of the convention
                t.ManifestResourceNames[0].ItemSpec.ShouldBe("MyStuff.Class2.de", "Expected Namespace.Class.Culture");
            }
        }

        /// <summary>
        /// Given a file path, return a stream on top of that path.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="mode">File mode</param>
        /// <param name="access">Access type</param>
        /// <returns>The Stream</returns>
        private Stream CreateFileStream(AbsolutePath path, FileMode mode, FileAccess access)
        {
            if (String.Equals(Path.GetFileName(path.Value), "SR1.strings", StringComparison.OrdinalIgnoreCase))
            {
                return StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace
    Class TestClass
    End Class
End Namespace
");
            }

            Assert.Fail(String.Format("Encountered a new path {0}, needs unittesting support", path));
            return null;
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
        [MSBuildTestMethod]
        public void Regress419591()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: "MyForm.ro.resx",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace
                    dependentUponFileName: "MyForm.ro.vb",
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"
    Class MyForm
    End Class
"),
                    log: null);

            Assert.AreEqual("RootNamespace.MyForm", result);
        }

        /// <summary>
        /// If we encounter a class or namespace name within a conditional compilation directive,
        /// we need to warn because we do not try to resolve the correct manifest name depending
        /// on conditional compilation of code.
        /// </summary>
        [MSBuildTestMethod]
        public void Regress459265()
        {
            MockEngine m = new MockEngine();
            CreateVisualBasicManifestResourceName c = new CreateVisualBasicManifestResourceName();
            c.BuildEngine = m;

            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: "MyForm.resx",
                    linkFileName: null,
                    prependCultureAsDirectory: true,
                    rootNamespace: "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    dependentUponFileName: "MyForm.vb",
                    culture: null,
                    binaryStream: StreamHelpers.StringToStream(
@"Imports System

#if false
Namespace ClassLibrary1
#end if
#if Debug
Namespace ClassLibrary2
#else
Namespace ClassLibrary3
#end if
    Class MyForm
    End Class
End Namespace
"),
                    log: c.Log);

            Assert.Contains(
                String.Format(AssemblyResources.GetString("CreateManifestResourceName.DefinitionFoundWithinConditionalDirective"), "MyForm.vb", "MyForm.resx"),
                m.Log);
        }

        /// <summary>
        /// Tests to ensure that the ResourceFilesWithManifestResourceNames contains everything that
        /// the ResourceFiles property on the task contains, but with additional metadata called ManifestResourceName
        /// </summary>
        [MSBuildTestMethod]
        public void ResourceFilesWithManifestResourceNamesContainsAdditionalMetadata()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("strings.resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.IsTrue(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.ContainsSingle(resourceFiles);
            Assert.AreEqual(@"strings.resx", resourceFiles[0].ItemSpec);
            Assert.AreEqual(@"ResourceRoot.strings", resourceFiles[0].GetMetadata("ManifestResourceName"));
        }

        /// <summary>
        /// Ensure that if no LogicalName is specified, that the same ManifestResourceName metadata
        /// gets applied as LogicalName
        /// </summary>
        [MSBuildTestMethod]
        public void AddLogicalNameForNonResx()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("pic.bmp");
            i.SetMetadata("Type", "Non-Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.IsTrue(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.ContainsSingle(resourceFiles);
            Assert.AreEqual(@"pic.bmp", resourceFiles[0].ItemSpec);
            Assert.AreEqual(@"ResourceRoot.pic.bmp", resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// Ensure that a LogicalName that is already present is preserved during manifest name generation
        /// </summary>
        [MSBuildTestMethod]
        public void PreserveLogicalNameForNonResx()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("pic.bmp");
            i.SetMetadata("LogicalName", "foo");
            i.SetMetadata("Type", "Non-Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.IsTrue(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.ContainsSingle(resourceFiles);
            Assert.AreEqual(@"pic.bmp", resourceFiles[0].ItemSpec);
            Assert.AreEqual(@"foo", resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// Resx resources should not get ManifestResourceName metadata copied to the LogicalName value
        /// </summary>
        [MSBuildTestMethod]
        public void NoLogicalNameAddedForResx()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("strings.resx");
            i.SetMetadata("Type", "Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.IsTrue(success); // "Expected the task to succeed."

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.ContainsSingle(resourceFiles);
            Assert.AreEqual(@"strings.resx", resourceFiles[0].ItemSpec);
            Assert.AreEqual(String.Empty, resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// A culture-specific resources file in a subfolder, with a root namespace
        /// </summary>
        [MSBuildTestMethod]
        public void CulturedResourcesFileWithRootNamespaceWithinSubfolder()
        {
            string result =
                CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: FileUtilities.FixFilePath(@"SubFolder\MyResource.fr.resources"),
                    linkFileName: null,             // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",  // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual(@"RootNamespace.MyResource.fr.resources", result);
        }

        /// <summary>
        /// A culture-specific resources file with a root namespace
        /// </summary>
        [MSBuildTestMethod]
        public void CulturedResourcesFileWithRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"MyResource.fr.resources",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual(@"RootNamespace.MyResource.fr.resources", result);
        }

        /// <summary>
        /// A non-culture-specific resources file with a root namespace
        /// </summary>
        [MSBuildTestMethod]
        public void ResourcesFileWithRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl(
                    fileName: @"MyResource.resources",
                    linkFileName: null,    // Link file name
                    prependCultureAsDirectory: false,
                    rootNamespace: "RootNamespace",        // Root namespace
                    dependentUponFileName: null,
                    culture: null,
                    binaryStream: null,
                    log: null);

            Assert.AreEqual(@"RootNamespace.MyResource.resources", result);
        }
    }
}
