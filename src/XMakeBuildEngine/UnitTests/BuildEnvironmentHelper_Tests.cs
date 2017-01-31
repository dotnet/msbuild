using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests
{
    public class BuildEnvironmentHelper_Tests
    {
#if USE_MSBUILD_DLL_EXTN
        private const string MSBuildExeName = "MSBuild.dll";
#else
        private const string MSBuildExeName = "MSBuild.exe";
#endif
        [Fact]
        public void GetExecutablePath()
        {
            var msbuildPath = Path.GetDirectoryName(FileUtilities.ExecutingAssemblyPath);
            string expectedMSBuildPath = Path.Combine(msbuildPath, MSBuildExeName).ToLowerInvariant();

            string configFilePath = BuildEnvironmentHelper.Instance.CurrentMSBuildConfigurationFile.ToLowerInvariant();
            string toolsDirectoryPath = BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory.ToLowerInvariant();
            string actualMSBuildPath = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath.ToLowerInvariant();

            Assert.Equal(configFilePath, actualMSBuildPath + ".config");
            Assert.Equal(expectedMSBuildPath, actualMSBuildPath);
            Assert.Equal(toolsDirectoryPath, Path.GetDirectoryName(expectedMSBuildPath));
            Assert.Equal(BuildEnvironmentMode.Standalone, BuildEnvironmentHelper.Instance.Mode);
        }

        [Fact]
        public void FindBuildEnvironmentByEnvironmentVariable()
        {
            using (var env = new EmptyStandaloneEnviroment(MSBuildExeName))
            {
                var path = env.BuildDirectory;
                var msBuildPath = Path.Combine(path, MSBuildExeName);
                var msBuildConfig = Path.Combine(path, $"{MSBuildExeName}.config");

                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", env.MSBuildExePath);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(path, BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);
                Assert.Equal(msBuildPath, BuildEnvironmentHelper.Instance.CurrentMSBuildExePath);
                Assert.Equal(msBuildConfig, BuildEnvironmentHelper.Instance.CurrentMSBuildConfigurationFile);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.Null(BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.Standalone, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void FindBuildEnvironmentFromCommandLineVisualStudio()
        {
            using (var env = new EmptyVSEnviroment())
            {
                // All we know about is path to msbuild.exe as the command-line arg[0]
                // FIXME: we have added a func for command line arg now.. does this need to be changed?
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.MSBuildExePath, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        public void FindBuildEnvironmentFromCommandLineStandalone()
        {
            // Path will not be under a Visual Studio install like path.
            using (var env = new EmptyStandaloneEnviroment(MSBuildExeName))
            {
                // All we know about is path to msbuild.exe as the command-line arg[0]
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.MSBuildExePath, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.Standalone, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void FindBuildEnvironmentFromRunningProcessVisualStudio()
        {
            using (var env = new EmptyVSEnviroment())
            {
                // All we know about is path to msbuild.exe as the current process
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        public void FindBuildEnvironmentFromRunningProcessStandalone()
        {
            // Path will not be under a Visual Studio install like path.
            using (var env = new EmptyStandaloneEnviroment(MSBuildExeName))
            {
                // All we know about is path to msbuild.exe as the current process
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.Standalone, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        public void FindBuildEnvironmentFromExecutingAssemblyAsDll()
        {
            // Ensure the correct file is found (.dll not .exe)
            using (var env = new EmptyStandaloneEnviroment("MSBuild.dll"))
            {
                // All we know about is path to msbuild.exe as the current process
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.Standalone, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        public void FindBuildEnvironmentFromAppContextDirectory()
        {
            using (var env = new EmptyStandaloneEnviroment(MSBuildExeName))
            {
                // Only the app base directory will be available
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, () => env.BuildDirectory, ReturnEmptyInstances);

                // Make sure we get the right MSBuild entry point. On .NET Core this will be MSBuild.dll, otherwise MSBuild.exe
                Assert.Equal(MSBuildExeName, Path.GetFileName(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath));

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.Standalone, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void FindBuildEnvironmentFromVisualStudioRoot()
        {
            using (var env = new EmptyVSEnviroment())
            {
                // All we know about is path to DevEnv.exe
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.DevEnvPath, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.True(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentDetectsVisualStudioByEnvironment()
        {
            using (var env = new EmptyVSEnviroment())
            {
                Environment.SetEnvironmentVariable("VSINSTALLDIR", env.TempFolderRoot);
                Environment.SetEnvironmentVariable("VisualStudioVersion", "15.0");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentDetectsVisualStudioByMSBuildProcess()
        {
            using (var env = new EmptyVSEnviroment())
            {
                // We only know we're in msbuild.exe, we should still be able to attempt to find Visual Studio
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.MSBuildExePath, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentDetectsVisualStudioByMSBuildProcessAmd64()
        {
            using (var env = new EmptyVSEnviroment())
            {
                // We only know we're in amd64\msbuild.exe, we should still be able to attempt to find Visual Studio
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.MSBuildExePath64, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentDetectsVisualStudioFromSetupInstance()
        {
            using (var env = new EmptyVSEnviroment())
            {
                // This test has no context to find MSBuild other than Visual Studio root.
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull,
                    () =>
                        new List<VisualStudioInstance>
                        {
                            new VisualStudioInstance("Invalid path", @"c:\_doesnotexist", new Version("15.0")),
                            new VisualStudioInstance("VS", env.TempFolderRoot, new Version("15.0")),
                        });

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        public void BuildEnvironmentVisualStudioNotFoundWhenVersionMismatch()
        {
            using (var env = new EmptyVSEnviroment())
            {
                // This test has no context to find MSBuild other than Visual Studio root.
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull,
                    () =>
                        new List<VisualStudioInstance>
                        {
                            new VisualStudioInstance("Invalid path", @"c:\_doesnotexist", new Version("15.0")),
                            new VisualStudioInstance("VS", env.TempFolderRoot, new Version("14.0")),
                        });

                Assert.Null(BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.None, BuildEnvironmentHelper.Instance.Mode);
            }
        }

#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/669")]
#else
        [Fact]
#endif
        public void BuildEnvironmentDetectsRunningTests()
        {
            Assert.True(BuildEnvironmentHelper.Instance.RunningTests);
            Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentDetectsVisualStudioByProcessName()
        {
            using (var env = new EmptyVSEnviroment())
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.DevEnvPath, ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnEmptyInstances);

                Assert.True(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentDetectsVisualStudioByBlendProcess()
        {
            using (var env = new EmptyVSEnviroment())
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.BlendPath, ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnEmptyInstances);

                Assert.True(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentFindsAmd64()
        {
            using (var env = new EmptyVSEnviroment())
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.DevEnvPath, ReturnNull, ReturnNull,
                    ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        [Trait("Category", "nonlinuxtests")]
        [Trait("Category", "nonosxtests")]
        public void BuildEnvironmentFindsAmd64RunningInAmd64()
        {
            using (var env = new EmptyVSEnviroment())
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(()=>env.MSBuildExePath64, ReturnNull, ReturnNull, ReturnNull, ReturnEmptyInstances);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.VisualStudio, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        [Fact]
        public void BuildEnvironmentNoneWhenNotAvailable()
        {
            using (new EmptyStandaloneEnviroment(MSBuildExeName))
            {
                var entryProcess = Path.Combine(Path.GetTempPath(), "foo.exe");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => entryProcess, ReturnNull, ReturnNull, ReturnNull,
                    ReturnEmptyInstances);

                Assert.Equal(entryProcess, BuildEnvironmentHelper.Instance.CurrentMSBuildExePath);
                Assert.Equal(Path.GetDirectoryName(entryProcess), BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);
                Assert.Null(BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.Equal(BuildEnvironmentMode.None, BuildEnvironmentHelper.Instance.Mode);
            }
        }

        private static string ReturnNull()
        {
            return null;
        }

        private static IEnumerable<VisualStudioInstance> ReturnEmptyInstances()
        {
            return new List<VisualStudioInstance>();
        }

        private class EmptyVSEnviroment : IDisposable
        {
            public string TempFolderRoot { get; }

            public string DevEnvPath { get; }

            public string BlendPath { get; }

            public string BuildDirectory { get; }

            public string BuildDirectory64 { get; }

            public string MSBuildExePath => Path.Combine(BuildDirectory, "msbuild.exe");

            public string MSBuildExePath64 => Path.Combine(BuildDirectory64, "msbuild.exe");

            private readonly Dictionary<string, string> _originalEnvironment = new Dictionary<string, string>
            {
                ["MSBUILD_EXE_PATH"] = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH"),
                ["VSINSTALLDIR"] = Environment.GetEnvironmentVariable("VSINSTALLDIR"),
                ["VisualStudioVersion"] = Environment.GetEnvironmentVariable("VisualStudioVersion"),
            };

            public EmptyVSEnviroment()
            {
                try
                {
                    var files = new[] { "msbuild.exe", "msbuild.exe.config" };
                    TempFolderRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                    BuildDirectory = Path.Combine(TempFolderRoot, "MSBuild", "15.0", "Bin");
                    BuildDirectory64 = Path.Combine(BuildDirectory, "amd64");
                    DevEnvPath = Path.Combine(TempFolderRoot, "Common7", "IDE", "devenv.exe");
                    BlendPath = Path.Combine(TempFolderRoot, "Common7", "IDE", "blend.exe");

                    Directory.CreateDirectory(BuildDirectory);
                    foreach (var file in files)
                    {
                        File.WriteAllText(Path.Combine(BuildDirectory, file), string.Empty);
                    }

                    Directory.CreateDirectory(BuildDirectory64);
                    foreach (var file in files)
                    {
                        File.WriteAllText(Path.Combine(BuildDirectory64, file), string.Empty);
                    }

                    Directory.CreateDirectory(Path.Combine(TempFolderRoot, "Common7", "IDE"));
                    File.WriteAllText(DevEnvPath, string.Empty);
                }
                catch (Exception)
                {
                    FileUtilities.DeleteDirectoryNoThrow(BuildDirectory, true);
                    throw;
                }
            }

            public void Dispose()
            {
                FileUtilities.DeleteDirectoryNoThrow(TempFolderRoot, true);

                foreach (var env in _originalEnvironment)
                    Environment.SetEnvironmentVariable(env.Key, env.Value);

                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }
        }

        private class EmptyStandaloneEnviroment : IDisposable
        {
            public string TempFolderRoot { get; }

            public string BuildDirectory { get; }

            public string MSBuildExeName { get; }

            public string MSBuildExePath => Path.Combine(BuildDirectory, MSBuildExeName);

            private readonly Dictionary<string, string> _originalEnvironment = new Dictionary<string, string>
            {
                ["MSBUILD_EXE_PATH"] = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH"),
                ["VSINSTALLDIR"] = Environment.GetEnvironmentVariable("VSINSTALLDIR"),
                ["VisualStudioVersion"] = Environment.GetEnvironmentVariable("VisualStudioVersion"),
            };

            public EmptyStandaloneEnviroment(string msBuildExeName, bool writeFakeFiles = true)
            {
                try
                {
                    MSBuildExeName = msBuildExeName;
                    TempFolderRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                    BuildDirectory = Path.Combine(TempFolderRoot, "MSBuild");

                    Directory.CreateDirectory(BuildDirectory);
                    if (writeFakeFiles)
                    {
                        File.WriteAllText(MSBuildExePath, string.Empty);
                        File.WriteAllText($"{MSBuildExePath}.config", string.Empty);
                    }
                }
                catch (Exception)
                {
                    FileUtilities.DeleteDirectoryNoThrow(BuildDirectory, true);
                    throw;
                }
            }

            public void Dispose()
            {
                FileUtilities.DeleteDirectoryNoThrow(TempFolderRoot, true);

                foreach (var env in _originalEnvironment)
                    Environment.SetEnvironmentVariable(env.Key, env.Value);

                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }
        }
    }
}
