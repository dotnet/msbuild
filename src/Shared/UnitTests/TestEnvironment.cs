// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Debugging;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

using TempPaths = System.Collections.Generic.Dictionary<string, string>;
using CommonWriterType = System.Action<string, string, System.Collections.Generic.IEnumerable<string>>;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    public partial class TestEnvironment : IDisposable
    {
        /// <summary>
        ///     List of test invariants to assert value does not change.
        /// </summary>
        private readonly List<TestInvariant> _invariants = new List<TestInvariant>();

        /// <summary>
        ///     List of test variants which need to be reverted when the test completes.
        /// </summary>
        private readonly List<TransientTestState> _variants = new List<TransientTestState>();

        public ITestOutputHelper Output { get; }

        private readonly Lazy<TransientTestFolder> _defaultTestDirectory;

        private bool _disposed;

        public TransientTestFolder DefaultTestDirectory => _defaultTestDirectory.Value;

        public static TestEnvironment Create(ITestOutputHelper output = null, bool ignoreBuildErrorFiles = false)
        {
            var env = new TestEnvironment(output ?? new DefaultOutput());

            // In most cases, if MSBuild wrote an MSBuild_*.txt to the temp path something went wrong.
            if (!ignoreBuildErrorFiles)
            {
                env.WithInvariant(new BuildFailureLogInvariant());
            }

            return env;
        }

        private TestEnvironment(ITestOutputHelper output)
        {
            Output = output;
            _defaultTestDirectory = new Lazy<TransientTestFolder>(() => CreateFolder());
            SetDefaultInvariant();
        }

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }

        ~TestEnvironment()
        {
            Cleanup();
        }

        /// <summary>
        ///     Revert / cleanup variants and then assert invariants.
        /// </summary>
        private void Cleanup()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Reset test variants
                foreach (var variant in _variants)
                    variant.Revert();

                // Assert invariants
                foreach (var item in _invariants)
                    item.AssertInvariant(Output);

                // Reset change waves
                SetChangeWave(string.Empty);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }
        }

        public void SetChangeWave(string wave)
        {
            ChangeWaves.ResetStateForTests();
            SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", wave);
        }

        /// <summary>
        ///     Evaluate the test with the given invariant.
        /// </summary>
        /// <param name="invariant">Test invariant to assert unchanged on completion.</param>
        public T WithInvariant<T>(T invariant) where T : TestInvariant
        {
            _invariants.Add(invariant);
            return invariant;
        }

        /// <summary>
        ///     Evaluate the test with the given transient test state.
        /// </summary>
        /// <returns>Test state to revert on completion.</returns>
        public T WithTransientTestState<T>(T transientState) where T : TransientTestState
        {
            _variants.Add(transientState);
            return transientState;
        }

        /// <summary>
        ///     Clears all test invariants. This should only be used if there is a known
        ///     issue with a test!
        /// </summary>
        public void ClearTestInvariants()
        {
            _invariants.Clear();
        }

        #region Common test variants

        private void SetDefaultInvariant()
        {
            // Temp folder should not change before and after a test
            WithInvariant(new StringInvariant("Path.GetTempPath()", Path.GetTempPath));

            // Temp folder should not change before and after a test
            WithInvariant(new StringInvariant("Directory.GetCurrentDirectory", Directory.GetCurrentDirectory));

            WithEnvironmentInvariant();
        }

        /// <summary>
        ///     Creates a test invariant that asserts an environment variable does not change during the test.
        /// </summary>
        /// <param name="environmentVariableName">Name of the environment variable.</param>
        public TestInvariant WithEnvironmentVariableInvariant(string environmentVariableName)
        {
            return WithInvariant(new StringInvariant(environmentVariableName,
                () => Environment.GetEnvironmentVariable(environmentVariableName)));
        }
        /// <summary>
        /// Creates a test invariant which asserts that the environment variables do not change
        /// </summary>
        public TestInvariant WithEnvironmentInvariant()
        {
            return WithInvariant(new EnvironmentInvariant());
        }

        /// <summary>
        ///     Creates a string invariant that will assert the value is the same before and after the test.
        /// </summary>
        /// <param name="name">Name of the item to keep track of.</param>
        /// <param name="value">Delegate to get the value for the invariant.</param>
        public TestInvariant WithStringInvariant(string name, Func<string> value)
        {
            return WithInvariant(new StringInvariant(name, value));
        }

        /// <summary>
        /// Creates a new temp path
        /// </summary>
        public TransientTempPath CreateNewTempPath()
        {
            var folder = CreateFolder();
            return SetTempPath(folder.Path, true);
        }

        /// <summary>
        /// Creates a new temp path
        /// Sets all OS temp environment variables to the new path
        ///
        /// Cleanup:
        /// - restores OS temp environment variables
        /// </summary>
        public TransientTempPath SetTempPath(string tempPath, bool deleteTempDirectory = false)
        {
            var transientTempPath = new TransientTempPath(tempPath, deleteTempDirectory);
            _variants.Add(transientTempPath);

            return transientTempPath;
        }

        /// <summary>
        ///     Creates a test variant that corresponds to a temporary file which will be deleted when the test completes.
        /// </summary>
        /// <param name="extension">Extensions of the file (defaults to '.tmp')</param>
        public TransientTestFile CreateFile(string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(extension, createFile:true, expectedAsOutput:false));
        }

        public TransientTestFile CreateFile(string fileName, string contents = "")
        {
            return CreateFile(DefaultTestDirectory, fileName, contents);
        }

        public TransientTestFile CreateFile(TransientTestFolder transientTestFolder, string fileName, string contents = "")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.Path, fileName, contents));
        }

        /// <summary>
        ///     Creates a test variant that corresponds to a temporary file under a specific temporary folder. File will
        ///     be cleaned up when the test completes.
        /// </summary>
        /// <param name="transientTestFolder"></param>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        public TransientTestFile CreateFile(TransientTestFolder transientTestFolder, string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.Path, extension,
                createFile: true, expectedAsOutput: false));
        }


        /// <summary>
        ///     Gets a transient test file associated with a unique file name but does not create the file.
        /// </summary>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile GetTempFile(string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(extension, createFile: false, expectedAsOutput: false));
        }

        /// <summary>
        ///     Gets a transient test file under a specified folder associated with a unique file name but does not create the file.
        /// </summary>
        /// <param name="transientTestFolder">Temp folder</param>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile GetTempFile(TransientTestFolder transientTestFolder, string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.Path, extension,
                createFile: false, expectedAsOutput: false));
        }

        /// <summary>
        ///     Create a temp file name that is expected to exist when the test completes.
        /// </summary>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile ExpectFile(string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(extension, createFile: false, expectedAsOutput: true));
        }

        /// <summary>
        /// Create a temp file name under a specific temporary folder. The file is expected to exist when the test completes.
        /// </summary>
        /// <param name="transientTestFolder">Temp folder</param>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        /// <returns></returns>
        public TransientTestFile ExpectFile(TransientTestFolder transientTestFolder, string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.Path, extension, createFile: false, expectedAsOutput: true));
        }

        /// <summary>
        ///     Creates a test variant used to add a unique temporary folder during a test. Will be deleted when the test
        ///     completes.
        /// </summary>
        public TransientTestFolder CreateFolder(string folderPath = null, bool createFolder = true)
        {
            var folder = WithTransientTestState(new TransientTestFolder(folderPath, createFolder));

            Assert.True(!(createFolder ^ FileSystems.Default.DirectoryExists(folder.Path)));

            return folder;
        }

        /// <summary>
        ///     Creates a test variant used to add a unique temporary folder during a test. Will be deleted when the test
        ///     completes.
        /// </summary>
        public TransientTestFolder CreateFolder(bool createFolder)
        {
            return CreateFolder(null, createFolder);
        }

        /// <summary>
        /// Creates a debugger which can be used to write to from anywhere in the msbuild code base
        /// It also enables logging in the out of proc nodes, but the given writer object would not be available in the nodes, set one in OutOfProcNode
        /// </summary>
        public TransientPrintLineDebugger CreatePrintLineDebugger(CommonWriterType writer)
        {
            return WithTransientTestState(new TransientPrintLineDebugger(this, writer));
        }

        /// <summary>
        /// Creates a debugger which can be used to write to from (hopefully) anywhere in the msbuild code base using the ITestOutputWriter in this TestEnvironmentHelper
        /// Will not work for out of proc nodes since the output writer does not reach into those
        public TransientPrintLineDebugger CreatePrintLineDebuggerWithTestOutputHelper()
        {
            ErrorUtilities.VerifyThrowInternalNull(Output, nameof(Output));
            return WithTransientTestState(new TransientPrintLineDebugger(this, OutPutHelperWriter(Output)));

            CommonWriterType OutPutHelperWriter(ITestOutputHelper output)
            {
                return (id, callsite, args) => output.WriteLine(PrintLineDebuggerWriters.SimpleFormat(id, callsite, args));
            }
        }

        /// <summary>
        ///     Create an test variant used to change the value of an environment variable during a test. Original value
        ///     will be restored when complete.
        /// </summary>
        public TransientTestState SetEnvironmentVariable(string environmentVariableName, string newValue)
        {
            return WithTransientTestState(new TransientTestEnvironmentVariable(environmentVariableName, newValue));
        }

        public TransientTestState SetCurrentDirectory(string newWorkingDirectory)
        {
            return WithTransientTestState(new TransientWorkingDirectory(newWorkingDirectory));
        }

        #endregion

        private class DefaultOutput : ITestOutputHelper
        {
            public void WriteLine(string message)
            {
                Console.WriteLine(message);
            }

            public void WriteLine(string format, params object[] args)
            {
                Console.WriteLine(format, args);
            }
        }

        /// <summary>
        /// MSBuild launches the debugger on ErrorUtilities exceptions when in DEBUG. Disable this in tests that assert these exceptions.
        /// </summary>
        public void DoNotLaunchDebugger()
        {
            SetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER", "1");
        }
    }

    /// <summary>
    ///     Things that are expected not to change and should be asserted before and after running.
    /// </summary>
    public abstract class TestInvariant
    {
        public abstract void AssertInvariant(ITestOutputHelper output);
    }

    /// <summary>
    ///     Things that are expected to change and should be reverted after running.
    /// </summary>
    public abstract class TransientTestState
    {
        public abstract void Revert();
    }

    public class StringInvariant : TestInvariant
    {
        private readonly Func<string> _accessorFunc;
        private readonly string _name;
        private readonly string _originalValue;

        public StringInvariant(string name, Func<string> accessorFunc)
        {
            _name = name;
            _accessorFunc = accessorFunc;
            _originalValue = accessorFunc();
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            var currentValue = _accessorFunc();

            //  Something like the following might be preferrable, but the assertion method truncates the values leaving us without
            //  useful information.  So use Assert.True instead
            //  Assert.Equal($"{_name}: {_originalValue}", $"{_name}: {_accessorFunc()}");

            Assert.True(currentValue == _originalValue, $"Expected {_name} to be '{_originalValue}', but it was '{currentValue}'");
        }
    }

    public class EnvironmentInvariant : TestInvariant
    {
        private readonly IDictionary _initialEnvironment;

        public EnvironmentInvariant()
        {
            _initialEnvironment = Environment.GetEnvironmentVariables();
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            var environment = Environment.GetEnvironmentVariables();

            AssertDictionaryInclusion(_initialEnvironment, environment, "added");
            AssertDictionaryInclusion(environment, _initialEnvironment, "removed");

            void AssertDictionaryInclusion(IDictionary superset, IDictionary subset, string operation)
            {
                foreach (var key in subset.Keys)
                {
                    // workaround for https://github.com/Microsoft/msbuild/pull/3866
                    // if the initial environment had empty keys, then MSBuild will accidentally remove them via Environment.SetEnvironmentVariable
                    if (operation != "removed" || !string.IsNullOrEmpty((string) subset[key]))
                    {
                        superset.Contains(key).ShouldBe(true, $"environment variable {operation}: {key}");
                        superset[key].ShouldBe(subset[key]);
                    }
                }
            }
        }
    }

    public class BuildFailureLogInvariant : TestInvariant
    {
        private readonly string[] _originalFiles;

        public BuildFailureLogInvariant()
        {
            _originalFiles = Directory.GetFiles(Path.GetTempPath(), "MSBuild_*.txt");
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            var newFiles = Directory.GetFiles(Path.GetTempPath(), "MSBuild_*.txt");

            int newFilesCount = newFiles.Length;
            if (newFilesCount > _originalFiles.Length)
            {
                foreach (FileInfo file in newFiles.Except(_originalFiles).Select(f => new FileInfo(f)))
                {
                    string contents = File.ReadAllText(file.FullName);

                    // Delete the file so we don't pollute the build machine
                    FileUtilities.DeleteNoThrow(file.FullName);

                    // Ignore clean shutdown trace logs.
                    if (Regex.IsMatch(file.Name, @"MSBuild_NodeShutdown_\d+\.txt") &&
                        Regex.IsMatch(contents, @"Node shutting down with reason BuildComplete and exception:\s*"))
                    {
                        newFilesCount--;
                        continue;
                    }

                    // Com trace file. This is probably fine, but output it as it was likely turned on
                    // for a reason.
                    if (Regex.IsMatch(file.Name, @"MSBuild_CommTrace_PID_\d+\.txt"))
                    {
                        output.WriteLine($"{file.Name}: {contents}");
                        newFilesCount--;
                        continue;
                    }

                    output.WriteLine($"Build Error File {file.Name}: {contents}");
                }
            }

            // Assert file count is equal minus any files that were OK
            Assert.Equal(_originalFiles.Length, newFilesCount);
        }
    }

    public class TransientTempPath : TransientTestState
    {
        private const string TMP = "TMP";
        private const string TMPDIR = "TMPDIR";
        private const string TEMP = "TEMP";

        private readonly bool _deleteTempDirectory;

        private readonly TempPaths _oldtempPaths;

        public string TempPath { get; }

        public TransientTempPath(string tempPath, bool deleteTempDirectory)
        {
            TempPath = tempPath;
            _deleteTempDirectory = deleteTempDirectory;

            _oldtempPaths = SetTempPath(tempPath);
        }

        private static TempPaths SetTempPath(string tempPath)
        {
            var oldTempPaths = GetTempPaths();

            foreach (var key in oldTempPaths.Keys)
            {
                Environment.SetEnvironmentVariable(key, tempPath);
            }

            return oldTempPaths;
        }

        private static TempPaths SetTempPaths(TempPaths tempPaths)
        {
            var oldTempPaths = GetTempPaths();

            foreach (var key in oldTempPaths.Keys)
            {
                Environment.SetEnvironmentVariable(key, tempPaths[key]);
            }

            return oldTempPaths;
        }

        private static TempPaths GetTempPaths()
        {
            var tempPaths = new TempPaths
            {
                [TMP] = Environment.GetEnvironmentVariable(TMP),
                [TEMP] = Environment.GetEnvironmentVariable(TEMP)
            };

            if (NativeMethodsShared.IsUnixLike)
            {
                tempPaths[TMPDIR] = Environment.GetEnvironmentVariable(TMPDIR);
            }

            return tempPaths;
        }

        public override void Revert()
        {
            SetTempPaths(_oldtempPaths);

            if (_deleteTempDirectory)
            {
                FileUtilities.DeleteDirectoryNoThrow(TempPath, recursive: true);
            }
        }
    }


    public class TransientTestFile : TransientTestState
    {
        private readonly bool _createFile;
        private readonly bool _expectedAsOutput;

        public TransientTestFile(string extension, bool createFile, bool expectedAsOutput)
        {
            _createFile = createFile;
            _expectedAsOutput = expectedAsOutput;
            Path = FileUtilities.GetTemporaryFile(null, extension, createFile);
        }

        public TransientTestFile(string rootPath, string extension, bool createFile, bool expectedAsOutput)
        {
            _createFile = createFile;
            _expectedAsOutput = expectedAsOutput;
            Path = FileUtilities.GetTemporaryFile(rootPath, extension, createFile);
        }

        public TransientTestFile(string rootPath, string fileName, string contents = null)
        {
            Path = System.IO.Path.Combine(rootPath, fileName);

            File.WriteAllText(Path, contents ?? string.Empty);
        }

        public string Path { get; }

        public override void Revert()
        {
            try
            {
                if (_expectedAsOutput)
                {
                    Assert.True(FileSystems.Default.FileExists(Path), $"A file expected as an output does not exist: {Path}");
                }
            }
            finally
            {
                FileUtilities.DeleteNoThrow(Path);
            }
        }

        public void Delete()
        {
            File.Delete(Path);
        }
    }

    public class TransientTestFolder : TransientTestState
    {
        public TransientTestFolder(string folderPath = null, bool createFolder = true)
        {
            Path = folderPath ?? FileUtilities.GetTemporaryDirectory(createFolder);

            if (createFolder)
            {
                Directory.CreateDirectory(Path);
            }
        }

        public TransientTestFolder CreateDirectory(string directoryName)
        {
            return new TransientTestFolder(System.IO.Path.Combine(Path, directoryName));
        }

        public TransientTestFile CreateFile(string fileName, string contents = null)
        {
            return new TransientTestFile(Path, fileName, contents);
        }

        public string Path { get; }

        public override void Revert()
        {
            // Basic checks to make sure we're not deleting something very obviously wrong (e.g.
            // the entire temp drive).
            Path.ShouldNotBeNullOrEmpty();
            Path.ShouldNotBe(@"\");
            Path.ShouldNotBe(@"/");
            System.IO.Path.GetFullPath(Path).ShouldNotBe(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
            System.IO.Path.IsPathRooted(Path).ShouldBeTrue(() => $"{Path} is not rooted");

            FileUtilities.DeleteDirectoryNoThrow(Path, true);
        }
    }

    public class TransientTestEnvironmentVariable : TransientTestState
    {
        private readonly string _environmentVariableName;
        private readonly string _originalValue;

        public TransientTestEnvironmentVariable(string environmentVariableName, string newValue)
        {
            _environmentVariableName = environmentVariableName;
            _originalValue = Environment.GetEnvironmentVariable(environmentVariableName);

            Environment.SetEnvironmentVariable(environmentVariableName, newValue);
        }

        public override void Revert()
        {
            Environment.SetEnvironmentVariable(_environmentVariableName, _originalValue);
        }
    }

    public class TransientWorkingDirectory : TransientTestState
    {
        private readonly string _originalValue;

        public TransientWorkingDirectory(string newWorkingDirectory)
        {
            _originalValue = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(newWorkingDirectory);
        }

        public override void Revert()
        {
            Directory.SetCurrentDirectory(_originalValue);
        }
    }

    public class TransientZipArchive : TransientTestState
    {
        private TransientZipArchive()
        {
        }

        public string Path { get; set; }

        public static TransientZipArchive Create(TransientTestFolder source, TransientTestFolder destination, string filename = "test.zip")
        {
            Directory.CreateDirectory(destination.Path);

            string path = System.IO.Path.Combine(destination.Path, filename);

            ZipFile.CreateFromDirectory(source.Path, path);

            return new TransientZipArchive
            {
                Path = path
            };
        }

        public override void Revert()
        {
            FileUtilities.DeleteNoThrow(Path);
        }
    }

    public class TransientPrintLineDebugger : TransientTestState
    {
        private readonly PrintLineDebugger _printLineDebugger;

        public TransientPrintLineDebugger(TestEnvironment environment, CommonWriterType writer)
        {
            _printLineDebugger = PrintLineDebugger.Create(writer);
        }

        public override void Revert()
        {
            _printLineDebugger.Dispose();
        }
    }
}
