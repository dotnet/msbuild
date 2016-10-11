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
#if FEATURE_RUN_EXE_IN_TESTS
        private const string MSBuildExeName = "MSBuild.exe";
#else
        private const string MSBuildExeName = "MSBuild.dll";
#endif
        [Fact]
        public void GetExecutablePath()
        {
            // This test will fail when CurrentDirectory is changed in another test. We will change it here
            // to the path to Microsoft.Build.dll (debug build output folder). This is what it *should* be
            // anyway.
            var msbuildPath = Path.GetDirectoryName(AssemblyUtilities.GetAssemblyLocation(typeof(Project).GetTypeInfo().Assembly));
            Directory.SetCurrentDirectory(msbuildPath);

            string path = Path.Combine(Directory.GetCurrentDirectory(), MSBuildExeName).ToLowerInvariant();
            string configPath = BuildEnvironmentHelper.Instance.CurrentMSBuildConfigurationFile.ToLowerInvariant();
            string directoryName = BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory.ToLowerInvariant();
            string executablePath = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath.ToLowerInvariant();

            Assert.Equal(configPath, executablePath + ".config");
            Assert.Equal(path, executablePath);
            Assert.Equal(directoryName, Path.GetDirectoryName(path));
        }

        [Fact]
        public void FindBuildEnvironmentByEnvironmentVariable()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                var path = env.BuildDirectory;
                var msBuildPath = Path.Combine(path, "msbuild.exe");
                var msBuildConfig = Path.Combine(path, "msbuild.exe.config");

                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", env.MSBuildExePath);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull, ReturnNull);

                Assert.Equal(path, BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);
                Assert.Equal(msBuildPath, BuildEnvironmentHelper.Instance.CurrentMSBuildExePath);
                Assert.Equal(msBuildConfig, BuildEnvironmentHelper.Instance.CurrentMSBuildConfigurationFile);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
            }
        }

        [Fact]
        public void FindBuildEnvironmentFromCommandLine()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                // All we know about is path to msbuild.exe as the command-line arg[0]
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.MSBuildExePath, ReturnNull, ReturnNull, ReturnNull, ReturnNull);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
            }
        }

        [Fact]
        public void FindBuildEnvironmentFromRunningProcess()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                // All we know about is path to msbuild.exe as the current process
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnNull, ReturnNull);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.False(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
            }
        }

        [Fact]
        public void FindBuildEnvironmentFromVisualStudioRoot()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                // All we know about is path to DevEnv.exe
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.DevEnvPath, ReturnNull, ReturnNull, ReturnNull, ReturnNull);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
                Assert.True(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.False(BuildEnvironmentHelper.Instance.RunningTests);
            }
        }

        [Fact]
        public void BuildEnvironmentDetectsVisualStudioByEnvironment()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                Environment.SetEnvironmentVariable("VSINSTALLDIR", env.TempFolderRoot);
                Environment.SetEnvironmentVariable("VisualStudioVersion", "15.0");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
            }
        }

        [Fact]
        public void BuildEnvironmentDetectsVisualStudioByMSBuildProcess()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                // We only know we're in msbuild.exe, we should still be able to attempt to find Visual Studio
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.MSBuildExePath, ReturnNull, ReturnNull, ReturnNull, ReturnNull);

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
            }
        }

        [Fact]
        public void BuildEnvironmentDetectsVisualStudioByMSBuildProcessAmd64()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                // We only know we're in amd64\msbuild.exe, we should still be able to attempt to find Visual Studio
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.MSBuildExePath64, ReturnNull, ReturnNull, ReturnNull, ReturnNull);

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
            }
        }

        [Fact]
        public void BuildEnvironmentDetectsVisualStudioFromSetupInstance()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                // This test has no context to find MSBuild other than Visual Studio root.
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull, ReturnNull,
                    () =>
                        new List<VisualStudioInstance>
                        {
                            new VisualStudioInstance("Invalid path", @"c:\_doesnotexist", new Version("15.0")),
                            new VisualStudioInstance("VS", env.TempFolderRoot, new Version("15.0")),
                        });

                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
            }
        }

        [Fact]
        public void BuildEnvironmentVisualStudioNotFoundWhenVersionMismatch()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                // This test has no context to find MSBuild other than Visual Studio root.
                Assert.Throws<InvalidOperationException>(() =>
                {
                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull, ReturnNull,
                        () =>
                            new List<VisualStudioInstance>
                            {
                                new VisualStudioInstance("Invalid path", @"c:\_doesnotexist", new Version("15.0")),
                                new VisualStudioInstance("VS", env.TempFolderRoot, new Version("14.0")),
                            });
                });
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
        public void BuildEnvironmentDetectsVisualStudioByProcessName()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.DevEnvPath, ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnNull);

                Assert.True(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
            }
        }

        [Fact]
        public void BuildEnvironmentDetectsVisualStudioByBlendProcess()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(() => env.BlendPath, ReturnNull, () => env.MSBuildExePath, ReturnNull, ReturnNull);

                Assert.True(BuildEnvironmentHelper.Instance.RunningInVisualStudio);
                Assert.Equal(env.TempFolderRoot, BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory);
            }
        }

        [Fact]
        public void BuildEnvironmentFindsAmd64()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", env.MSBuildExePath);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
            }
        }

        [Fact]
        public void BuildEnvironmentFindsAmd64RunningInAmd64()
        {
            using (var env = new EmptyBuildEnviroment())
            {
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", env.MSBuildExePath64);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull, ReturnNull);

                Assert.Equal(env.BuildDirectory, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32);
                Assert.Equal(env.BuildDirectory64, BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64);
            }
        }

        [Fact]
        public void FindBuildEnvironmentThrowsWhenNotAvailable()
        {
            using (new EmptyBuildEnviroment())
            {
                Assert.Throws<InvalidOperationException>(() => BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(ReturnNull, ReturnNull, ReturnNull, ReturnNull, ReturnNull));
            }
        }

        private static string ReturnNull()
        {
            return null;
        }

        private class EmptyBuildEnviroment : IDisposable
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

            public EmptyBuildEnviroment()
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
    }
}
