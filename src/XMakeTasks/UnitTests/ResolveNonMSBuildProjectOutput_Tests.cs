// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using System.Linq;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class ResolveNonMSBuildProjectOutput_Tests
    {
        private const string attributeProject = "Project";

        static internal ITaskItem CreateReferenceItem(string itemSpec, string projectGuid, string package, string name)
        {
            TaskItem reference = new TaskItem(itemSpec);

            if (projectGuid != null)
                reference.SetMetadata(attributeProject, projectGuid);
            if (package != null)
                reference.SetMetadata("Package", package);
            if (name != null)
                reference.SetMetadata("Name", name);

            return reference;
        }

        private void TestVerifyReferenceAttributesHelper(string itemSpec, string projectGuid, string package, string name,
            bool expectedResult, string expectedMissingAttribute)
        {
            ITaskItem reference = CreateReferenceItem(itemSpec, projectGuid, package, name);

            ResolveNonMSBuildProjectOutput rvpo = new ResolveNonMSBuildProjectOutput();
            string missingAttr = null;
            bool result = rvpo.VerifyReferenceAttributes(reference, out missingAttr);

            string message = string.Format("Reference \"{0}\" [project \"{1}\", package \"{2}\", name \"{3}\"], " +
                "expected result \"{4}\", actual result \"{5}\", expected missing attr \"{6}\", actual missing attr \"{7}\".",
                itemSpec, projectGuid, package, name, expectedResult, result,
                expectedMissingAttribute, missingAttr);

            Assert.IsTrue(result == expectedResult, message);
            if (result == false)
            {
                Assert.IsTrue(missingAttr == expectedMissingAttribute, message);
            }
            else
            {
                Assert.IsNull(missingAttr, message);
            }
        }

        [TestMethod]
        public void TestVerifyReferenceAttributes()
        {
            // a correct reference
            TestVerifyReferenceAttributesHelper("proj1.csproj", "{CFF438C3-51D1-4E61-BECD-D7D3A6193DF7}", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", "CSDep",
                true, null);

            // incorrect project guid - should not work
            TestVerifyReferenceAttributesHelper("proj1.csproj", "{invalid guid}", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", "CSDep",
                false, "Project");
        }

        static internal string CreatePregeneratedPathDoc(IDictionary projectOutputs)
        {
            string xmlString = "<VSIDEResolvedNonMSBuildProjectOutputs>";

            foreach (DictionaryEntry entry in projectOutputs)
            {
                xmlString += string.Format("<ProjectRef Project=\"{0}\">{1}</ProjectRef>", entry.Key, entry.Value);
            }

            xmlString += "</VSIDEResolvedNonMSBuildProjectOutputs>";

            return xmlString;
        }

        private void TestResolveHelper(string itemSpec, string projectGuid, string package, string name,
            Hashtable pregenOutputs, bool expectedResult, string expectedPath)
        {
            ITaskItem reference = CreateReferenceItem(itemSpec, projectGuid, package, name);
            string xmlString = CreatePregeneratedPathDoc(pregenOutputs);
            ITaskItem resolvedPath;

            ResolveNonMSBuildProjectOutput rvpo = new ResolveNonMSBuildProjectOutput();
            rvpo.CacheProjectElementsFromXml(xmlString);
            bool result = rvpo.ResolveProject(reference, out resolvedPath);

            string message = string.Format("Reference \"{0}\" [project \"{1}\", package \"{2}\", name \"{3}\"] Pregen Xml string : \"{4}\"" +
                "expected result \"{5}\", actual result \"{6}\", expected path \"{7}\", actual path \"{8}\".",
                itemSpec, projectGuid, package, name, xmlString, expectedResult, result, expectedPath, resolvedPath);

            Assert.IsTrue(result == expectedResult, message);
            if (result == true)
            {
                Assert.IsTrue(resolvedPath.ItemSpec == expectedPath, message);
            }
            else
            {
                Assert.IsNull(resolvedPath, message);
            }
        }

        [TestMethod]
        public void TestResolve()
        {
            // empty pre-generated string
            Hashtable projectOutputs = new Hashtable();
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null);

            // non matching project in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null);

            // matching project in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"obj\correct.dll");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, @"obj\correct.dll");

            // multiple non matching projects in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null);

            // multiple non matching projects in string, one matching
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"obj\correct.dll");
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, @"obj\correct.dll");
        }

        private void TestUnresolvedReferencesHelper(ArrayList projectRefs, Hashtable pregenOutputs,
            out Hashtable unresolvedOutputs, out Hashtable resolvedOutputs)
        {
            TestUnresolvedReferencesHelper(projectRefs, pregenOutputs, null, out unresolvedOutputs, out resolvedOutputs);
        }

        private void TestUnresolvedReferencesHelper(ArrayList projectRefs, Hashtable pregenOutputs, Func<string, bool> isManaged,
            out Hashtable unresolvedOutputs, out Hashtable resolvedOutputs)
        {
            ResolveNonMSBuildProjectOutput.GetAssemblyNameDelegate pretendGetAssemblyName = path =>
            {
                if (isManaged != null && isManaged(path))
                {
                    return null; // just don't throw an exception
                }
                else
                {
                    throw new BadImageFormatException(); // the hint that the caller takes for an unmanaged binary.
                }
            };

            string xmlString = CreatePregeneratedPathDoc(pregenOutputs);

            MockEngine engine = new MockEngine();
            ResolveNonMSBuildProjectOutput rvpo = new ResolveNonMSBuildProjectOutput();
            rvpo.GetAssemblyName = pretendGetAssemblyName;
            rvpo.BuildEngine = engine;
            rvpo.PreresolvedProjectOutputs = xmlString;
            rvpo.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));

            bool result = rvpo.Execute();
            unresolvedOutputs = new Hashtable();

            for (int i = 0; i < rvpo.UnresolvedProjectReferences.Length; i++)
            {
                unresolvedOutputs[rvpo.UnresolvedProjectReferences[i].ItemSpec] = rvpo.UnresolvedProjectReferences[i];
            }

            resolvedOutputs = new Hashtable();
            for (int i = 0; i < rvpo.ResolvedOutputPaths.Length; i++)
            {
                resolvedOutputs[rvpo.ResolvedOutputPaths[i].ItemSpec] = rvpo.ResolvedOutputPaths[i];
            }
        }

        [TestMethod]
        public void TestManagedCheck()
        {
            Hashtable unresolvedOutputs = null;
            Hashtable resolvedOutputs = null;
            Hashtable projectOutputs = null;
            ArrayList projectRefs = null;

            projectRefs = new ArrayList();
            projectRefs.Add(CreateReferenceItem("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-000000000000}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1"));
            projectRefs.Add(CreateReferenceItem("MCDep2.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep2"));

            // 1. multiple project refs, none resolvable
            projectOutputs = new Hashtable();
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"obj\managed.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"obj\unmanaged.dll");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, path => (path == @"obj\managed.dll"), out unresolvedOutputs, out resolvedOutputs);

            Assert.IsNotNull(resolvedOutputs);
            Assert.IsTrue(resolvedOutputs.Contains(@"obj\managed.dll"));
            Assert.IsTrue(resolvedOutputs.Contains(@"obj\unmanaged.dll"));
            Assert.AreEqual(((ITaskItem)resolvedOutputs[@"obj\managed.dll"]).GetMetadata("ManagedAssembly"), "true");
            Assert.AreNotEqual(((ITaskItem)resolvedOutputs[@"obj\unmanaged.dll"]).GetMetadata("ManagedAssembly"), "true");
        }

        /// <summary>
        /// Verifies that the UnresolvedProjectReferences output parameter is populated correctly.
        /// </summary>
        [TestMethod]
        public void TestUnresolvedReferences()
        {
            Hashtable unresolvedOutputs = null;
            Hashtable resolvedOutputs = null;
            Hashtable projectOutputs = null;
            ArrayList projectRefs = null;

            projectRefs = new ArrayList();
            projectRefs.Add(CreateReferenceItem("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-000000000000}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1"));
            projectRefs.Add(CreateReferenceItem("MCDep2.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep2"));

            // 1. multiple project refs, none resolvable
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.IsTrue(resolvedOutputs.Count == 0, "No resolved refs expected for case 1");
            Assert.IsTrue(unresolvedOutputs.Count == 2, "Two unresolved refs expected for case 1");
            Assert.IsTrue(unresolvedOutputs["MCDep1.vcproj"] == projectRefs[0]);
            Assert.IsTrue(unresolvedOutputs["MCDep2.vcproj"] == projectRefs[1]);

            // 2. multiple project refs, one resolvable
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"obj\correct.dll");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.IsTrue(resolvedOutputs.Count == 1, "One resolved ref expected for case 2");
            Assert.IsTrue(resolvedOutputs.ContainsKey(@"obj\correct.dll"));
            Assert.IsTrue(unresolvedOutputs.Count == 1, "One unresolved ref expected for case 2");
            Assert.IsTrue(unresolvedOutputs["MCDep1.vcproj"] == projectRefs[0]);

            // 3. multiple project refs, all resolvable
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"obj\correct.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"obj\correct2.dll");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.IsTrue(resolvedOutputs.Count == 2, "Two resolved refs expected for case 3");
            Assert.IsTrue(resolvedOutputs.ContainsKey(@"obj\correct.dll"));
            Assert.IsTrue(resolvedOutputs.ContainsKey(@"obj\correct2.dll"));
            Assert.IsTrue(unresolvedOutputs.Count == 0, "No unresolved refs expected for case 3");

            // 4. multiple project refs, all failed to resolve
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.IsTrue(resolvedOutputs.Count == 0, "No resolved refs expected for case 4");
            Assert.IsTrue(unresolvedOutputs.Count == 0, "No unresolved refs expected for case 4");

            // 5. multiple project refs, one resolvable, one failed to resolve
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"obj\correct.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.IsTrue(resolvedOutputs.Count == 1, "One resolved ref expected for case 5");
            Assert.IsTrue(resolvedOutputs.ContainsKey(@"obj\correct.dll"));
            Assert.IsTrue(unresolvedOutputs.Count == 0, "No unresolved refs expected for case 5");

            // 6. multiple project refs, one unresolvable, one failed to resolve
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", @"obj\wrong.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", @"obj\wrong2.dll");
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", @"obj\wrong3.dll");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.IsTrue(resolvedOutputs.Count == 0, "No resolved refs expected for case 6");
            Assert.IsTrue(unresolvedOutputs.Count == 1, "One unresolved ref expected for case 6");
            Assert.IsTrue(unresolvedOutputs["MCDep2.vcproj"] == projectRefs[1]);
        }

        [TestMethod]
        public void TestVerifyProjectReferenceItem()
        {
            ResolveNonMSBuildProjectOutput rvpo = new ResolveNonMSBuildProjectOutput();

            ITaskItem[] taskItems = new ITaskItem[1];
            // bad GUID - this reference is invalid
            taskItems[0] = new TaskItem("projectReference");
            taskItems[0].SetMetadata(attributeProject, "{invalid guid}");

            MockEngine engine = new MockEngine();
            rvpo.BuildEngine = engine;
            Assert.AreEqual(true, rvpo.VerifyProjectReferenceItems(taskItems, false /* treat problems as warnings */));
            Assert.AreEqual(1, engine.Warnings);
            Assert.AreEqual(0, engine.Errors);
            engine.AssertLogContains("MSB3107");

            engine = new MockEngine();
            rvpo.BuildEngine = engine;
            Assert.AreEqual(false, rvpo.VerifyProjectReferenceItems(taskItems, true /* treat problems as errors */));
            Assert.AreEqual(0, engine.Warnings);
            Assert.AreEqual(1, engine.Errors);
            engine.AssertLogContains("MSB3107");
        }
    }
}
