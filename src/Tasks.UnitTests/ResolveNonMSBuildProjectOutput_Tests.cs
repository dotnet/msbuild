// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Task = System.Threading.Tasks.Task;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class ResolveNonMSBuildProjectOutput_Tests
    {
        private const string attributeProject = "Project";

        private readonly ITestOutputHelper _output;

        public ResolveNonMSBuildProjectOutput_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        internal static ITaskItem CreateReferenceItem(string itemSpec, string projectGuid, string package, string name)
        {
            TaskItem reference = new TaskItem(itemSpec);

            if (projectGuid != null)
            {
                reference.SetMetadata(attributeProject, projectGuid);
            }

            if (package != null)
            {
                reference.SetMetadata("Package", package);
            }

            if (name != null)
            {
                reference.SetMetadata("Name", name);
            }

            return reference;
        }

        private void TestVerifyReferenceAttributesHelper(string itemSpec, string projectGuid, string package, string name,
            bool expectedResult, string expectedMissingAttribute)
        {
            ITaskItem reference = CreateReferenceItem(itemSpec, projectGuid, package, name);

            ResolveNonMSBuildProjectOutput rvpo = new ResolveNonMSBuildProjectOutput();
            string missingAttr;
            bool result = rvpo.VerifyReferenceAttributes(reference, out missingAttr);

            string message = string.Format("Reference \"{0}\" [project \"{1}\", package \"{2}\", name \"{3}\"], " +
                "expected result \"{4}\", actual result \"{5}\", expected missing attr \"{6}\", actual missing attr \"{7}\".",
                itemSpec, projectGuid, package, name, expectedResult, result,
                expectedMissingAttribute, missingAttr);

            Assert.Equal(result, expectedResult);
            if (!result)
            {
                Assert.Equal(missingAttr, expectedMissingAttribute);
            }
            else
            {
                Assert.Null(missingAttr);
            }
        }

        [Fact]
        public void TestVerifyReferenceAttributes()
        {
            // a correct reference
            TestVerifyReferenceAttributesHelper("proj1.csproj", "{CFF438C3-51D1-4E61-BECD-D7D3A6193DF7}", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", "CSDep",
                true, null);

            // incorrect project guid - should not work
            TestVerifyReferenceAttributesHelper("proj1.csproj", "{invalid guid}", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", "CSDep",
                false, "Project");
        }

        internal static string CreatePregeneratedPathDoc(IDictionary projectOutputs)
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

            Assert.Equal(result, expectedResult);
            if (result)
            {
                Assert.Equal(resolvedPath.ItemSpec, expectedPath);
            }
            else
            {
                Assert.Null(resolvedPath);
            }
        }

        [Fact]
        public void TestResolve()
        {
            // empty pre-generated string
            Hashtable projectOutputs = new Hashtable();
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null);

            // non matching project in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null);

            // matching project in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", Path.Combine("obj", "correct.dll"));
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, Path.Combine("obj", "correct.dll"));

            // multiple non matching projects in string
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, false, null);

            // multiple non matching projects in string, one matching
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", Path.Combine("obj", "correct.dll"));
            TestResolveHelper("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1",
                projectOutputs, true, Path.Combine("obj", "correct.dll"));
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

            MockEngine engine = new MockEngine(_output);
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

        [Fact]
        public void TestManagedCheck()
        {
            Hashtable unresolvedOutputs = null;
            Hashtable resolvedOutputs = null;
            Hashtable projectOutputs = null;
            ArrayList projectRefs = new ArrayList();
            projectRefs.Add(CreateReferenceItem("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-000000000000}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1"));
            projectRefs.Add(CreateReferenceItem("MCDep2.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep2"));

            // 1. multiple project refs, none resolvable
            projectOutputs = new Hashtable();
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", Path.Combine("obj", "managed.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", Path.Combine("obj", "unmanaged.dll"));

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, path => path == Path.GetFullPath(Path.Combine("obj", "managed.dll")), out unresolvedOutputs, out resolvedOutputs);

            Assert.NotNull(resolvedOutputs);
            Assert.True(resolvedOutputs.Contains(Path.Combine("obj", "managed.dll")));
            Assert.True(resolvedOutputs.Contains(Path.Combine("obj", "unmanaged.dll")));
            Assert.Equal("true", ((ITaskItem)resolvedOutputs[Path.Combine("obj", "managed.dll")]).GetMetadata("ManagedAssembly"));
            Assert.NotEqual("true", ((ITaskItem)resolvedOutputs[Path.Combine("obj", "unmanaged.dll")]).GetMetadata("ManagedAssembly"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ConcurrentRelativeOutputsUseEachProjectDirectory(bool resolveByGuid)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFolder managedProject = env.CreateFolder();
            TransientTestFolder nativeProject = env.CreateFolder();
            string relativeOutputPath = Path.Combine("obj", "reference.dll");
            string relativeProjectPath = Path.Combine("obj", "..", "reference.vcxproj");

            Directory.CreateDirectory(Path.Combine(managedProject.Path, "obj"));
            Directory.CreateDirectory(Path.Combine(nativeProject.Path, "obj"));
            File.Copy(typeof(ResolveNonMSBuildProjectOutput).Assembly.Location, Path.Combine(managedProject.Path, relativeOutputPath));
            File.WriteAllText(Path.Combine(nativeProject.Path, relativeOutputPath), "Not a managed assembly.");

            ResolveNonMSBuildProjectOutput CreateTask(TransientTestFolder projectDirectory, string projectGuid, string name)
            {
                return new ResolveNonMSBuildProjectOutput
                {
                    BuildEngine = new MockEngine(_output),
                    TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory.Path),
                    ProjectReferences =
                    [
                        CreateReferenceItem(
                            Path.Combine(projectDirectory.Path, "reference.vcxproj"),
                            resolveByGuid ? projectGuid : null,
                            null,
                            name),
                        CreateReferenceItem(
                            Path.Combine(projectDirectory.Path, "unresolved.csproj"),
                            null,
                            null,
                            name + "-unresolved")
                    ],
                    PreresolvedProjectOutputs = $"""
                        <VSIDEResolvedNonMSBuildProjectOutputs>
                          <ProjectRef Project="{projectGuid}" AbsolutePath="{relativeProjectPath}">{relativeOutputPath}</ProjectRef>
                        </VSIDEResolvedNonMSBuildProjectOutputs>
                        """
                };
            }

            ResolveNonMSBuildProjectOutput managedTask = CreateTask(managedProject, "{11111111-1111-1111-1111-111111111111}", "managed");
            ResolveNonMSBuildProjectOutput nativeTask = CreateTask(nativeProject, "{22222222-2222-2222-2222-222222222222}", "native");

            bool[] results = await Task.WhenAll(Task.Run(managedTask.Execute), Task.Run(nativeTask.Execute));
            results.ShouldBe([true, true]);

            ITaskItem managedOutput = managedTask.ResolvedOutputPaths.ShouldHaveSingleItem();
            managedOutput.ItemSpec.ShouldBe(relativeOutputPath);
            managedOutput.GetMetadata("Name").ShouldBe("managed");
            managedOutput.GetMetadata("ManagedAssembly").ShouldBe("true");
            managedTask.UnresolvedProjectReferences.ShouldHaveSingleItem().ShouldBeSameAs(managedTask.ProjectReferences[1]);

            ITaskItem nativeOutput = nativeTask.ResolvedOutputPaths.ShouldHaveSingleItem();
            nativeOutput.ItemSpec.ShouldBe(relativeOutputPath);
            nativeOutput.GetMetadata("Name").ShouldBe("native");
            nativeOutput.GetMetadata("ManagedAssembly").ShouldBeEmpty();
            nativeTask.UnresolvedProjectReferences.ShouldHaveSingleItem().ShouldBeSameAs(nativeTask.ProjectReferences[1]);

            ((MockEngine)managedTask.BuildEngine).Errors.ShouldBe(0);
            ((MockEngine)managedTask.BuildEngine).Warnings.ShouldBe(0);
            ((MockEngine)nativeTask.BuildEngine).Errors.ShouldBe(0);
            ((MockEngine)nativeTask.BuildEngine).Warnings.ShouldBe(0);
        }

        [WindowsOnlyFact]
        public void FallbackEnvironmentPreservesDriveRelativePaths()
        {
            string drive = Path.GetPathRoot(Environment.CurrentDirectory).Equals(@"C:\", StringComparison.OrdinalIgnoreCase) ? "D:" : "C:";
            string projectPath = drive + "reference.vcxproj";
            string outputPath = drive + "reference.dll";
            string expectedAssemblyPath = Path.GetFullPath(outputPath);
            string probedAssemblyPath = null;

            var task = new ResolveNonMSBuildProjectOutput
            {
                BuildEngine = new MockEngine(_output),
                ProjectReferences = [new TaskItem(Path.GetFullPath(projectPath))],
                PreresolvedProjectOutputs = $"""
                    <VSIDEResolvedNonMSBuildProjectOutputs>
                      <ProjectRef Project="33333333-3333-3333-3333-333333333333" AbsolutePath="{projectPath}">{outputPath}</ProjectRef>
                    </VSIDEResolvedNonMSBuildProjectOutputs>
                    """,
                GetAssemblyName = path =>
                {
                    probedAssemblyPath = path;
                    return null;
                }
            };

            task.Execute().ShouldBeTrue();

            probedAssemblyPath.ShouldBe(expectedAssemblyPath);
            ITaskItem output = task.ResolvedOutputPaths.ShouldHaveSingleItem();
            output.ItemSpec.ShouldBe(outputPath);
            output.GetMetadata("ManagedAssembly").ShouldBe("true");
            task.UnresolvedProjectReferences.ShouldBeEmpty();
        }

        /// <summary>
        /// Verifies that the UnresolvedProjectReferences output parameter is populated correctly.
        /// </summary>
        [Fact]
        public void TestUnresolvedReferences()
        {
            ArrayList projectRefs = new ArrayList();
            projectRefs.Add(CreateReferenceItem("MCDep1.vcproj", "{2F6BBCC3-7111-4116-A68B-000000000000}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep1"));
            projectRefs.Add(CreateReferenceItem("MCDep2.vcproj", "{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", "MCDep2"));

            // 1. multiple project refs, none resolvable
            Hashtable projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));

            Hashtable unresolvedOutputs;
            Hashtable resolvedOutputs;
            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.Empty(resolvedOutputs); // "No resolved refs expected for case 1"
            Assert.Equal(2, unresolvedOutputs.Count); // "Two unresolved refs expected for case 1"
            Assert.Equal(unresolvedOutputs["MCDep1.vcproj"], projectRefs[0]);
            Assert.Equal(unresolvedOutputs["MCDep2.vcproj"], projectRefs[1]);

            // 2. multiple project refs, one resolvable
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", Path.Combine("obj", "correct.dll"));

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.Single(resolvedOutputs); // "One resolved ref expected for case 2"
            Assert.True(resolvedOutputs.ContainsKey(Path.Combine("obj", "correct.dll")));
            Assert.Single(unresolvedOutputs); // "One unresolved ref expected for case 2"
            Assert.Equal(unresolvedOutputs["MCDep1.vcproj"], projectRefs[0]);

            // 3. multiple project refs, all resolvable
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", Path.Combine("obj", "correct.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", Path.Combine("obj", "correct2.dll"));

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.Equal(2, resolvedOutputs.Count); // "Two resolved refs expected for case 3"
            Assert.True(resolvedOutputs.ContainsKey(Path.Combine("obj", "correct.dll")));
            Assert.True(resolvedOutputs.ContainsKey(Path.Combine("obj", "correct2.dll")));
            Assert.Empty(unresolvedOutputs); // "No unresolved refs expected for case 3"

            // 4. multiple project refs, all failed to resolve
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", @"");
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.Empty(resolvedOutputs); // "No resolved refs expected for case 4"
            Assert.Empty(unresolvedOutputs); // "No unresolved refs expected for case 4"

            // 5. multiple project refs, one resolvable, one failed to resolve
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-34CFC76F37C5}", Path.Combine("obj", "correct.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.Single(resolvedOutputs); // "One resolved ref expected for case 5"
            Assert.True(resolvedOutputs.ContainsKey(Path.Combine("obj", "correct.dll")));
            Assert.Empty(unresolvedOutputs); // "No unresolved refs expected for case 5"

            // 6. multiple project refs, one unresolvable, one failed to resolve
            projectOutputs = new Hashtable();
            projectOutputs.Add("{11111111-1111-1111-1111-111111111111}", Path.Combine("obj", "wrong.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111112}", Path.Combine("obj", "wrong2.dll"));
            projectOutputs.Add("{11111111-1111-1111-1111-111111111113}", Path.Combine("obj", "wrong3.dll"));
            projectOutputs.Add("{2F6BBCC3-7111-4116-A68B-000000000000}", @"");

            TestUnresolvedReferencesHelper(projectRefs, projectOutputs, out unresolvedOutputs, out resolvedOutputs);

            Assert.Empty(resolvedOutputs); // "No resolved refs expected for case 6"
            Assert.Single(unresolvedOutputs); // "One unresolved ref expected for case 6"
            Assert.Equal(unresolvedOutputs["MCDep2.vcproj"], projectRefs[1]);
        }

        [Fact]
        public void TestVerifyProjectReferenceItem()
        {
            ResolveNonMSBuildProjectOutput rvpo = new ResolveNonMSBuildProjectOutput();

            ITaskItem[] taskItems = new ITaskItem[1];
            // bad GUID - this reference is invalid
            taskItems[0] = new TaskItem("projectReference");
            taskItems[0].SetMetadata(attributeProject, "{invalid guid}");

            MockEngine engine = new MockEngine(_output);
            rvpo.BuildEngine = engine;
            Assert.True(rvpo.VerifyProjectReferenceItems(taskItems, false /* treat problems as warnings */));
            Assert.Equal(1, engine.Warnings);
            Assert.Equal(0, engine.Errors);
            engine.AssertLogContains("MSB3107");

            engine = new MockEngine(_output);
            rvpo.BuildEngine = engine;
            Assert.False(rvpo.VerifyProjectReferenceItems(taskItems, true /* treat problems as errors */));
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(1, engine.Errors);
            engine.AssertLogContains("MSB3107");
        }
    }
}
