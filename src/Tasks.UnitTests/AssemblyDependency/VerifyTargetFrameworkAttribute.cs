// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit test the cases where we need to determine if the target framework is greater than the current target framework through the use of the target framework attribute
    /// </summary>
    [TestClass]
    public sealed class VerifyTargetFrameworkAttribute : ResolveAssemblyReferenceTestFixture
    {
        public VerifyTargetFrameworkAttribute(TestContext output) : base(output)
        {
        }

        /// <summary>
        /// Verify there are no warnings if the target framework identifier passed to rar and the target framework identifier in the dll do not match.
        /// </summary>
        [MSBuildTestMethod]
        public void FrameworksDoNotMatch()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "BAR, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "BAR";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "DependsOnFoo4Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework but we are a lower version. With a primary reference in the project.
        /// </summary>
        [MSBuildTestMethod]
        public void LowerVersionSameFrameworkDirect()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo35Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=v4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "DependsOnFoo35Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and the same version and a direct reference
        /// </summary>
        [MSBuildTestMethod]
        public void SameVersionSameFrameworkDirect()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "DependsOnFoo4Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if the reference was built for a higher framework but specific version is true
        /// </summary>
        [MSBuildTestMethod]
        public void HigherVersionButSpecificVersionDirect()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral");
            item.SetMetadata("SpecificVersion", "true");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework but we are a lower version.
        /// </summary>
        [MSBuildTestMethod]
        public void LowerVersionSameFrameworkInDirect()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("IndirectDependsOnFoo35Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=v4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.ContainsSingle(t.ResolvedDependencyFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "IndirectDependsOnFoo35Framework.dll"))); // "Expected to find assembly, but didn't."
            Assert.IsTrue(ContainsItem(t.ResolvedDependencyFiles, Path.Combine(s_frameworksPath, "DependsOnFoo35Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and the same version.
        /// </summary>
        [MSBuildTestMethod]
        public void SameVersionSameFrameworkInDirect()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("IndirectDependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.ContainsSingle(t.ResolvedDependencyFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "IndirectDependsOnFoo4Framework.dll"))); // "Expected to find assembly, but didn't."
            Assert.IsTrue(ContainsItem(t.ResolvedDependencyFiles, Path.Combine(s_frameworksPath, "DependsOnFoo4Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and a higher version but specific version is true.
        /// </summary>
        [MSBuildTestMethod]
        public void HigherVersionButSpecificVersionInDirect()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");
            item.SetMetadata("SpecificVersion", "true");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.ContainsSingle(t.ResolvedDependencyFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "IndirectDependsOnFoo45Framework.dll"))); // "Expected to find assembly, but didn't."
            Assert.IsTrue(ContainsItem(t.ResolvedDependencyFiles, Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are warnings if there is an indirect reference to a dll that is higher that what the current target framework is.
        /// </summary>
        [MSBuildTestMethod]
        public void HigherVersionInDirect()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t, false);

            Assert.AreEqual(1, e.Warnings); // "One warning expected in this scenario."
            e.AssertLogContains("MSB3275");
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.IsEmpty(t.ResolvedFiles);
            Assert.IsEmpty(t.ResolvedDependencyFiles);
        }

        /// <summary>
        /// Verify there are no warnings if there is an indirect reference to a dll that is higher that what the current target framework is but IgnoreFrameworkAttributeVersionMismatch is true.
        /// </summary>
        [MSBuildTestMethod]
        public void HigherVersionInDirectIgnoreMismatch()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            t.IgnoreTargetFrameworkAttributeVersionMismatch = true;
            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.ContainsSingle(t.ResolvedDependencyFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "IndirectDependsOnFoo45Framework.dll"))); // "Expected to find assembly, but didn't."
            Assert.IsTrue(ContainsItem(t.ResolvedDependencyFiles, Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if there is a direct reference to a dll that is higher that what the current target framework is but the property IgnoreFrameworkAttributeVersionMismatch is true.
        /// </summary>
        [MSBuildTestMethod]
        public void HigherVersionDirectIgnoreMismatch()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            t.IgnoreTargetFrameworkAttributeVersionMismatch = true;

            Execute(t);

            Assert.AreEqual(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll"))); // "Expected to find assembly, but didn't."
        }


        /// <summary>
        /// Verify there are warnings if there is a direct reference to a dll that is higher that what the current target framework is.
        /// </summary>
        [MSBuildTestMethod]
        public void HigherVersionDirect()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { s_frameworksPath + Path.DirectorySeparatorChar };
            Execute(t, false);

            Assert.AreEqual(1, e.Warnings); // "One warning expected in this scenario."
            e.AssertLogContains("MSB3274");
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.IsEmpty(t.ResolvedFiles);
            Assert.IsEmpty(t.ResolvedDependencyFiles);
        }

        /// <summary>
        /// Verify there are no warnings if there is a direct reference to a dll that is higher that what the current target framework is but
        /// find dependencies is false. This is because we do not want to add an extra read for this attribute during the project load phase.
        /// which has dependencies set to false.  A regular build or design time build has this set to true so we do the correct check.
        /// </summary>
        [MSBuildTestMethod]
        public void HigherVersionDirectDependenciesFalse()
        {
            MockEngine e = new MockEngine(_output);

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.FindDependencies = false;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Assert.IsTrue(
                t.Execute(
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader));


            Assert.AreEqual(0, e.Warnings); // "No warning expected in this scenario."
            Assert.AreEqual(0, e.Errors); // "No errors expected in this scenario."
            Assert.ContainsSingle(t.ResolvedFiles);
            Assert.IsEmpty(t.ResolvedDependencyFiles);
            Assert.IsTrue(ContainsItem(t.ResolvedFiles, Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll"))); // "Expected to find assembly, but didn't."
        }
    }
}
