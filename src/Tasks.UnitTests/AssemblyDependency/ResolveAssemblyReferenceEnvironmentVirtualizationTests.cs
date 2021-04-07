using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using Xunit;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;
using Xunit.Abstractions;
using Shouldly;
using System.Text;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    public sealed class ResolveAssemblyReferenceEnvironmentVirtualization : ResolveAssemblyReferenceTestFixture
    {
        public ResolveAssemblyReferenceEnvironmentVirtualization(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// If a relative file name is passed in through the Assemblies parameter and the search paths contains {RawFileName}
        /// then try to resolve directly to that file name and make it a full path with current directory virtualization.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void RawFileNameRelativeWithActiveDirectoryVirtualization()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");

            Directory.CreateDirectory(testPath);
            try
            {
                t.Assemblies = new ITaskItem[] { new TaskItem(@"..\RawFileNameRelative\System.Xml.dll") };
                t.SearchPaths = new string[] { "{RawFileName}" };

                TaskExecutionContext taskExecutionContext = new TaskExecutionContext(testPath, null, null, null);
                (t as IConcurrentTask).ConfigureForConcurrentExecution(taskExecutionContext);

                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }


        /// <summary>
        /// If a relative searchPath is passed in through the search path parameter
        /// then try to resolve the file but make sure it is a full name with current directory virtualization.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void RelativeDirectoryResolverWithActiveDirectoryVirtualization()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            try
            {
                t.Assemblies = new ITaskItem[] { new TaskItem(@"System.Xml.dll") };
                t.SearchPaths = new string[] { "..\\RawFileNameRelative" };

                TaskExecutionContext taskExecutionContext = new TaskExecutionContext(testPath, null, null, null);
                (t as IConcurrentTask).ConfigureForConcurrentExecution(taskExecutionContext);

                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }


        /// <summary>
        /// If a relative file name is passed in through the HintPath then try to resolve directly to that file name and make it a full path with current directory virtualization.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void HintPathRelativeWithActiveDirectoryVirtualization()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            try
            {
                TaskItem taskItem = new TaskItem(AssemblyRef.SystemXml);
                taskItem.SetMetadata("HintPath", @"..\RawFileNameRelative\System.Xml.dll");

                t.Assemblies = new ITaskItem[] { taskItem };
                t.SearchPaths = new string[] { "{HintPathFromItem}" };

                TaskExecutionContext taskExecutionContext = new TaskExecutionContext(testPath, null, null, null);
                (t as IConcurrentTask).ConfigureForConcurrentExecution(taskExecutionContext);

                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }


        /// <summary>
        /// If a relative assemblyFile is passed in resolve it as a full path, checking current directory virtualization.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void RelativeAssemblyFilesWithActiveDirectoryVirtualization()
        {
            string testPath = Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles");

            Directory.CreateDirectory(testPath);

            try
            {
                // Create the engine.
                MockEngine engine = new MockEngine(_output);

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"..\RelativeAssemblyFiles\System.Xml.dll")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;

                TaskExecutionContext taskExecutionContext = new TaskExecutionContext(testPath, null, null, null);
                (t as IConcurrentTask).ConfigureForConcurrentExecution(taskExecutionContext);

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }

    }
}
