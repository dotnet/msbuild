// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class CreateVisualBasicManifestResourceName_Tests
    {
        /// <summary>
        /// Test the basic functionality.
        /// </summary>
        [TestMethod]
        public void Basic()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,    // Link file name
                    true,
                    null,    // Root namespace
                    null,
                    null,
                    StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace 
    Class TestClass 
    End Class
End Namespace
"),
                    null
                );

            Assert.AreEqual("Nested.TestNamespace.TestClass", result);
        }

        /// <summary>
        /// Test a dependent with a relative path
        /// </summary>
        [TestMethod]
        public void RelativeDependentUpon()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,    // Link file name
                    true,
                    null,    // Root namespace
                    null,
                    null,
                    StreamHelpers.StringToStream(
@"
Namespace TestNamespace 
    Class TestClass 
    End Class
End Namespace
"),
                    null

                );

            Assert.AreEqual("TestNamespace.TestClass", result);
        }

        /// <summary>
        /// Test a dependent with a relative path
        /// </summary>
        [TestMethod]
        public void AbsoluteDependentUpon()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,    // Link file name
                    true,
                    null,    // Root namespace
                    null,
                    null,
                    StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace 
    Class TestClass 
    End Class
End Namespace
"),
                    null

                );

            Assert.AreEqual("Nested.TestNamespace.TestClass", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture.
        /// </summary>
        [TestMethod]
        public void DependentWithCulture()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.en-GB.resx",
                    null,    // Link file name
                    true,
                    null,        // Root namespace
                    null,
                    null,
                    StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace 
    Class TestClass 
    End Class
End Namespace
"),
                    null

                );

            Assert.AreEqual("Nested.TestNamespace.TestClass.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture that was expressed in the metadata of the 
        /// item rather than the filename.
        /// </summary>
        [TestMethod]
        public void DependentWithCultureMetadata()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.resx",
                    null,    // Link file name
                    true,
                    null,        // Root namespace
                    null,
                    "en-GB",
                    StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace 
    Class TestClass 
    End Class
End Namespace
"),
                    null

                );

            Assert.AreEqual("Nested.TestNamespace.TestClass.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture and a root namespace.
        /// </summary>
        [TestMethod]
        public void DependentWithCultureAndRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.en-GB.resx",
                    null,    // Link file name
                    true,
                    "RootNamespace",
                    null,
                    null,
                    StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace 
    Class TestClass 
    End Class
End Namespace
"),
                    null

                );

            Assert.AreEqual("RootNamespace.Nested.TestNamespace.TestClass.en-GB", result);
        }

        /// <summary>
        /// A dependent class plus there is a culture embedded in the .RESX filename.
        /// </summary>
        [TestMethod]
        public void DependentWithEmbeddedCulture()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"f:\myproject\SubFolder\MyForm.fr-fr.resx",
                    null,    // Link file name
                    true,
                    "RootNamespace",    // Root namespace
                    null,
                    null,
                    StreamHelpers.StringToStream(
@"
Namespace Nested.TestNamespace 
    Class TestClass 
    End Class
End Namespace
"),
                    null

                );

            Assert.AreEqual("RootNamespace.Nested.TestNamespace.TestClass.fr-fr", result);
        }

        /// <summary>
        /// No dependent class, but there is a root namespace place.  Also, the .resx
        /// extension contains some upper-case characters.
        /// </summary>
        [TestMethod]
        public void RootnamespaceWithCulture()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"SubFolder\MyForm.en-GB.ResX",
                    null,    // Link file name
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null

                );

            Assert.AreEqual("RootNamespace.MyForm.en-GB", result);
        }

        /// <summary>
        /// If there is a link file name then it is preferred over the main file name.
        /// </summary>
        [TestMethod]
        public void Regress222308()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"..\..\XmlEditor\Setup\XmlEditor.rgs",
                    @"MyXmlEditor.rgs",
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null

                );

            Assert.AreEqual("RootNamespace.MyXmlEditor.rgs", result);
        }

        /// <summary>
        /// A non-resx file in a subfolder, with a root namespace.
        /// </summary>
        [TestMethod]
        public void BitmapWithRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"SubFolder\SplashScreen.bmp",
                    null,    // Link file name
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null

                );

            Assert.AreEqual("RootNamespace.SplashScreen.bmp", result);
        }

        /// <summary>
        /// A culture-specific non-resx file in a subfolder, with a root namespace.
        /// </summary>
        [TestMethod]
        public void CulturedBitmapWithRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    @"SubFolder\SplashScreen.fr.bmp",
                    null,    // Link file name
                    true,
                    "RootNamespace",        // Root namespace
                    null,
                    null,
                    null,
                    null

                );

            Assert.AreEqual(@"fr\RootNamespace.SplashScreen.bmp", result);
        }

        /// <summary>
        /// A culture-specific non-resx file in a subfolder, with a root namespace, but no culture directory prefix
        /// </summary>
        [TestMethod]
        public void CulturedBitmapWithRootNamespaceNoDirectoryPrefix()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
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

            Assert.AreEqual(@"RootNamespace.SplashScreen.bmp", result);
        }

        /// <summary>
        /// If the filename passed in as the "DependentUpon" file doesn't end in .cs then
        /// we want to fall back to the RootNamespace+FileName logic.
        /// </summary>
        [TestMethod]
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

            Assert.IsTrue(success, "Expected the task to succceed.");

            ITaskItem[] resourceNames = t.ManifestResourceNames;

            Assert.AreEqual(1, resourceNames.Length);
            Assert.AreEqual(@"CustomToolTest.SR1", resourceNames[0].ItemSpec);
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
        [TestMethod]
        public void Regress419591()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    "MyForm.ro.resx",
                    null,    // Link file name
                    true,
                    "RootNamespace",    // Root namespace
                    "MyForm.ro.vb",
                    null,
                    StreamHelpers.StringToStream(
@"
    Class MyForm 
    End Class
"),
                    null

                );

            Assert.AreEqual("RootNamespace.MyForm", result);
        }

        /// <summary>
        /// If we encounter a class or namespace name within a conditional compilation directive,
        /// we need to warn because we do not try to resolve the correct manifest name depending
        /// on conditional compilation of code.
        /// </summary>
        [TestMethod]
        public void Regress459265()
        {
            MockEngine m = new MockEngine();
            CreateVisualBasicManifestResourceName c = new CreateVisualBasicManifestResourceName();
            c.BuildEngine = m;

            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    "MyForm.resx",
                    null,
                    true,
                    "RootNamespace",    // Root namespace (will be ignored because it's dependent)
                    "MyForm.vb",
                    null,
                    StreamHelpers.StringToStream(
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
"
                    ),
                    c.Log
                );

            Assert.IsTrue
            (
                m.Log.Contains
                (
                    String.Format(AssemblyResources.GetString("CreateManifestResourceName.DefinitionFoundWithinConditionalDirective"), "MyForm.vb", "MyForm.resx")
                )
            );
        }

        /// <summary>
        /// Tests to ensure that the ResourceFilesWithManifestResourceNames contains everything that
        /// the ResourceFiles property on the task contains, but with additional metadata called ManifestResourceName
        /// </summary>
        [TestMethod]
        public void ResourceFilesWithManifestResourceNamesContainsAdditionalMetadata()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("strings.resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.IsTrue(success, "Expected the task to succceed.");

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.AreEqual(1, resourceFiles.Length);
            Assert.AreEqual(@"strings.resx", resourceFiles[0].ItemSpec);
            Assert.AreEqual(@"ResourceRoot.strings", resourceFiles[0].GetMetadata("ManifestResourceName"));
        }

        /// <summary>
        /// Ensure that if no LogicalName is specified, that the same ManifestResourceName metadata
        /// gets applied as LogicalName
        /// </summary>
        [TestMethod]
        public void AddLogicalNameForNonResx()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("pic.bmp");
            i.SetMetadata("Type", "Non-Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.IsTrue(success, "Expected the task to succceed.");

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.AreEqual(1, resourceFiles.Length);
            Assert.AreEqual(@"pic.bmp", resourceFiles[0].ItemSpec);
            Assert.AreEqual(@"ResourceRoot.pic.bmp", resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// Ensure that a LogicalName that is already present is preserved during manifest name generation
        /// </summary>
        [TestMethod]
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

            Assert.IsTrue(success, "Expected the task to succceed.");

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.AreEqual(1, resourceFiles.Length);
            Assert.AreEqual(@"pic.bmp", resourceFiles[0].ItemSpec);
            Assert.AreEqual(@"foo", resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// Resx resources should not get ManifestResourceName metadata copied to the LogicalName value
        /// </summary>
        [TestMethod]
        public void NoLogicalNameAddedForResx()
        {
            CreateVisualBasicManifestResourceName t = new CreateVisualBasicManifestResourceName();

            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("strings.resx");
            i.SetMetadata("Type", "Resx");

            t.ResourceFiles = new ITaskItem[] { i };
            t.RootNamespace = "ResourceRoot";
            bool success = t.Execute();

            Assert.IsTrue(success, "Expected the task to succceed.");

            ITaskItem[] resourceFiles = t.ResourceFilesWithManifestResourceNames;

            Assert.AreEqual(1, resourceFiles.Length);
            Assert.AreEqual(@"strings.resx", resourceFiles[0].ItemSpec);
            Assert.AreEqual(String.Empty, resourceFiles[0].GetMetadata("LogicalName"));
        }

        /// <summary>
        /// A culture-specific resources file in a subfolder, with a root namespace
        /// </summary>
        [TestMethod]
        public void CulturedResourcesFileWithRootNamespaceWithinSubfolder()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
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

            Assert.AreEqual(@"RootNamespace.MyResource.fr.resources", result);
        }

        /// <summary>
        /// A culture-specific resources file with a root namespace
        /// </summary>
        [TestMethod]
        public void CulturedResourcesFileWithRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
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

            Assert.AreEqual(@"RootNamespace.MyResource.fr.resources", result);
        }

        /// <summary>
        /// A non-culture-specific resources file with a root namespace
        /// </summary>
        [TestMethod]
        public void ResourcesFileWithRootNamespace()
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
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

            Assert.AreEqual(@"RootNamespace.MyResource.resources", result);
        }

        private void AssertSimpleCase(string code, string expected)
        {
            string result =
            CreateVisualBasicManifestResourceName.CreateManifestNameImpl
                (
                    "MyForm.resx",
                    null,    // Link file name
                    true,
                    "RootNamespace",    // Root namespace
                    "MyForm.vb",
                    null,
                    StreamHelpers.StringToStream(code),
                    null
                );

            Assert.AreEqual(expected, result);
        }
    }
}



