// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class AssignCulture_Tests
    {
        /// <summary>
        /// Tests the basic functionality.
        /// </summary>
        [MSBuildTestMethod]
        public void Basic()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Any pre-existing Culture attribute on the item is to be ignored
        /// </summary>
        [MSBuildTestMethod]
        public void CultureAttributePrecedence()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "en-GB");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// This is really a corner case.
        /// If the incoming item has a 'Culture' attribute already, but that culture is invalid,
        /// we still overwrite that culture.
        /// </summary>
        [MSBuildTestMethod]
        public void CultureAttributePrecedenceWithBogusCulture()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "invalid");   // Bogus culture.
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Make sure that attributes set on input items are forwarded to output items.
        /// This applies to every attribute except for the one pointed to by CultureAttribute.
        /// </summary>
        [MSBuildTestMethod]
        public void AttributeForwarding()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("MyAttribute", "My Random String");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("My Random String", t.AssignedFiles[0].GetMetadata("MyAttribute"));
            Assert.AreEqual("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }


        /// <summary>
        /// Test the case where an item has no embedded culture. For example:
        /// "MyResource.resx"
        /// </summary>
        [MSBuildTestMethod]
        public void NoCulture()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Test the case where an item has no extension. For example "MyResource".
        /// </summary>
        [MSBuildTestMethod]
        public void NoExtension()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        ///  Test the case where an item has two dots embedded, but otherwise looks
        /// like a well-formed item.For example "MyResource..resx".
        /// </summary>
        [MSBuildTestMethod]
        public void DoubleDot()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource..resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource..resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource..resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If an item has a "DependentUpon" who's base name matches exactly, then just assume this
        /// is a resource and form that happen to have an embedded culture. That is, don't assign a
        /// culture to these.
        /// </summary>
        [MSBuildTestMethod]
        public void Regress283991()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("DependentUpon", "MyResourcE.fr.vb");
            t.Files = new ITaskItem[] { i };

            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.IsEmpty(t.AssignedFilesWithCulture);
            Assert.ContainsSingle(t.AssignedFilesWithNoCulture);
        }

        /// <summary>
        /// Test the usage of Windows Pseudo-Locales
        /// https://docs.microsoft.com/en-gb/windows/desktop/Intl/pseudo-locales
        /// </summary>
        /// <param name="culture"></param>
        [MSBuildTestMethod]
        [DataRow("qps-ploc")]
        [DataRow("qps-plocm")]
        [DataRow("qps-ploca")]
        [DataRow("qps-Latn-x-sh")] // Windows 10+
        public void PseudoLocalization(string culture)
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem($"MyResource.{culture}.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual(culture, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual($"MyResource.{culture}.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Testing that certain aliases are considered valid cultures. Regression test for https://github.com/dotnet/msbuild/issues/3897.
        /// </summary>
        /// <param name="culture"></param>
        [MSBuildTestMethod]
        [DataRow("zh-TW")]
        [DataRow("zh-MO")]
        public void SupportAliasedCultures(string culture)
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem($"MyResource.{culture}.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual(culture, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual($"MyResource.{culture}.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        [DotNetOnlyTheory(additionalMessage: "These cultures are not returned via Culture api on net472.")]
        [DataRow("sh-BA")]
        [DataRow("shi-MA")]
        public void AliasedCultures_SupportedOnNetCore(string culture)
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem($"MyResource.{culture}.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual(culture, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual($"MyResource.{culture}.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        [DotNetOnlyFact(additionalMessage: "Pseudoloc is special-cased in .NET relative to Framework.")]
        public void Pseudolocales_CaseInsensitive()
        {
            string culture = "qps-Ploc";
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem($"MyResource.{culture}.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual("true", t.AssignedFiles[0].GetMetadata("WithCulture"));
            Assert.AreEqual(culture, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual($"MyResource.{culture}.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Any pre-existing Culture attribute on the item is to be respected
        /// </summary>
        [MSBuildTestMethod]
        public void CultureMetaDataShouldBeRespected()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "en-GB");
            t.Files = new ITaskItem[] { i };
            t.RespectAlreadyAssignedItemCulture = true;
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual("en-GB", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.fr.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Any pre-existing Culture attribute on the item is not to be respected, because culture is not set
        /// </summary>
        [MSBuildTestMethod]
        public void CultureMetaDataShouldNotBeRespected()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "");
            t.Files = new ITaskItem[] { i };
            t.RespectAlreadyAssignedItemCulture = true;
            t.Execute();

            Assert.ContainsSingle(t.AssignedFiles);
            Assert.ContainsSingle(t.CultureNeutralAssignedFiles);
            Assert.AreEqual("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.AreEqual("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.AreEqual("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }
    }
}
