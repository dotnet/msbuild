using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task's concurrency.
    /// Do not use mock operations with paths from (ResolveAssemblyReferenceTestFixture). 
    /// </summary>
    public sealed class ResolveAssemblyReferenceEnvironmentVirtualizationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _temp_directory;
        private readonly string _test_assembly_name;
        private readonly string _test_assembly_path;

        public ResolveAssemblyReferenceEnvironmentVirtualizationTests(ITestOutputHelper output)
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", "1");
            // TODO: Use temp files utilities in shared!
            _temp_directory = Path.GetTempPath();
            _test_assembly_name = Guid.NewGuid() + ".UnitTest.TestAssemply.dll";
            _test_assembly_path = Path.Combine(_temp_directory, _test_assembly_name);
            File.Create(_test_assembly_path);
            _output = output;
        }
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", null);
            if (File.Exists(_test_assembly_path))
            {
                FileUtilities.DeleteNoThrow(_test_assembly_path);
            }
        }

        /// <summary>
        /// Finding assembly in the (temporary) current directory, without virtualization of task execution context.
        /// </summary>
        [Fact]
        public void FindAssemblyInCurrentDirectory()
        {
            ResolveAssemblyReference rarTask = new ResolveAssemblyReference();

            rarTask.BuildEngine = new MockEngine(_output);
            rarTask.Assemblies = new ITaskItem[] {
                new TaskItem(_test_assembly_name)
            };
            rarTask.SearchPaths = new string[] {
                "{AssemblyFolders}",
                "{HintPathFromItem}",
                "{RawFileName}",
                "./"
            };

            string currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_temp_directory);
                rarTask.Execute();
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
            
            Assert.Single(rarTask.ResolvedFiles);
            Assert.Equal(0, String.Compare(_test_assembly_path, rarTask.ResolvedFiles[0].ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finding assembly in the current directory, with virtualization of task execution context.
        /// </summary>
        [Fact]
        public void FindAssemblyInCurrentDirectoryWithActiveDirectoryVirtualization()
        {
            ResolveAssemblyReference rarTask = new ResolveAssemblyReference();

            rarTask.BuildEngine = new MockEngine(_output);
            rarTask.Assemblies = new ITaskItem[] {
                new TaskItem(_test_assembly_name)
            };
            rarTask.SearchPaths = new string[] {
                "{AssemblyFolders}",
                "{HintPathFromItem}",
                "{RawFileName}",
                "./"
            };

            TaskExecutionContext taskExecutionContext = new TaskExecutionContext(_temp_directory, null, null, null);
            IConcurrentTask concurrentTask = rarTask;
            concurrentTask.ConfigureForConcurrentExecution(taskExecutionContext);

            rarTask.Execute();

            Assert.Single(rarTask.ResolvedFiles);
            Assert.Equal(0, String.Compare(_test_assembly_path, rarTask.ResolvedFiles[0].ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finding assembly in a specified absolute path.
        /// </summary>
        [Fact]
        public void FindAssemblyInSpecifiedAbsoluteDirectory()
        {
            ResolveAssemblyReference rarTask = new ResolveAssemblyReference();

            rarTask.BuildEngine = new MockEngine(_output);
            rarTask.Assemblies = new ITaskItem[] {
                new TaskItem(_test_assembly_name)
            };
            rarTask.SearchPaths = new string[] {
                "{AssemblyFolders}",
                "{HintPathFromItem}",
                "{RawFileName}",
                _temp_directory
            };
            rarTask.Execute();
            Assert.Single(rarTask.ResolvedFiles);
            Assert.Equal(0, String.Compare(_test_assembly_path, rarTask.ResolvedFiles[0].ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Make sure that nonexistent path in SearchPaths are eliminated.
        /// </summary>
        [Fact]
        public void RunWithNonExistentPath()
        {
            ResolveAssemblyReference rarTask = new ResolveAssemblyReference();

            string cur_path = Directory.GetCurrentDirectory();
            rarTask.BuildEngine = new MockEngine(_output);
            rarTask.Assemblies = new ITaskItem[] {
                new TaskItem("System.Xml"), new TaskItem("System.Nonexistent")
            };
            rarTask.SearchPaths = new string[] {
                Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName),
                "{AssemblyFolders}",
                "{HintPathFromItem}",
                "{RawFileName}",
                "C:\\NonExistentPath"
            };
            rarTask.Execute();
            Assert.Single(rarTask.ResolvedFiles);
            Assert.Equal(0, String.Compare(ToolLocationHelper.GetPathToDotNetFrameworkFile("System.Xml.dll", TargetDotNetFrameworkVersion.Version45), rarTask.ResolvedFiles[0].ItemSpec, StringComparison.OrdinalIgnoreCase));
        }
    }
}
