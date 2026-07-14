// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable disable

#pragma warning disable MSTEST0057 // Custom test-method attributes intentionally preserve existing xUnit-style one-attribute tests.

namespace Microsoft.Build.UnitTests
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class UseInvariantCultureAttribute : Attribute
    {
    }

    public class MSBuildTestMethodAttribute : TestMethodAttribute
    {
        // The BuildEnvironmentHelper.Instance is a process-wide singleton. A number of tests reset it
        // via ResetInstance_ForUnitTestsOnly() (which re-detects the environment from the current process)
        // and never restore it, leaving the singleton polluted with values derived from the test host
        // working directory. Under xUnit this was harmless because of the test ordering; under MSTest the
        // different ordering exposes it and destabilizes environment-sensitive tests (e.g. MSBuildExtensionsPath
        // resolution on .NET Framework). To keep every test isolated regardless of ordering, capture the clean
        // environment established by MSBuildTestAssemblyHooks.AssemblyInitialize (before any test mutates it)
        // and restore it after each test runs.
        private static BuildEnvironment s_cleanBuildEnvironment;
        private static bool s_cleanBuildEnvironmentCaptured;

        // The MSBuildExtensionsPath[32|64] environment variables are NOT reserved: when set, they override the
        // computed default in Utilities.GetEnvironmentProperties. On .NET Framework, MSBuild persists a build's
        // environment block into the native process environment (via CommunicationsUtilities.SetEnvironmentVariable,
        // a raw kernel32 P/Invoke) and does not always restore it. That can leak an MSBuildExtensionsPath that points
        // at the test host's own output directory into later tests. Because this is written on the native side, a
        // managed Environment.SetEnvironmentVariable(name, null) issued by an unrelated test does not reliably clear
        // what MSBuild reads back through GetEnvironmentStringsW. Under xUnit the test ordering happened to hide this;
        // under MSTest the different ordering exposes it (e.g. Evaluator_Tests.MSBuildExtensionsPath* and
        // VerifyPropertyTrackingLogging observing the leaked value on net472). To keep tests isolated regardless of
        // ordering, unset these variables (via both the managed and the native APIs on Windows) before and after every
        // test. The clean state on .NET Framework is *unset*: when the variables are absent MSBuild computes the
        // default from Program Files, which is what these tests expect. We deliberately do NOT capture-and-restore a
        // baseline value, because the very first capture can already observe the leaked value and would then re-apply
        // it on every scrub.
        private static readonly string[] s_extensionPathVariableNames = { "MSBuildExtensionsPath", "MSBuildExtensionsPath32", "MSBuildExtensionsPath64" };

#if NETFRAMEWORK
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEnvironmentVariable(string lpName, string lpValue);
#endif

        public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            CaptureCleanBuildEnvironment();

            string skipReason = GetSkipReason() ?? GetCategoryFilterSkipReason(testMethod);
            if (skipReason != null)
            {
                return Skipped(testMethod, skipReason);
            }

            // Scrub the extension-path environment variables to their clean values BEFORE the test runs as well.
            // The `dotnet` muxer injects MSBuildExtensionsPath into the native environment block when it launches
            // the test host, and an earlier test's in-proc build can leak these via the native environment block.
            // AssemblyInitialize only clears them through the managed API, which on .NET Framework does not
            // neutralize a value written to the native block. Restoring here guarantees a clean slate for the very
            // first environment-sensitive test too, not just for tests that follow a polluting one.
            ScrubExtensionPathEnvironmentVariables();

            CultureInfo originalCulture = null;
            CultureInfo originalUICulture = null;
            if (UsesInvariantCulture(testMethod))
            {
                originalCulture = CultureInfo.CurrentCulture;
                originalUICulture = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            }

            using var _ = TestEnvironment.Create();
            string initialHandshakeSalt = Framework.Traits.MSBuildNodeHandshakeSalt;
            bool initialLogAllEnvVariables = Framework.Traits.LogAllEnvironmentVariables;
            try
            {
                Assert.IsTrue(BuildEnvironmentState.s_runningTests);
                return await base.ExecuteAsync(testMethod);
            }
            finally
            {
                RestoreCleanBuildEnvironment();

                Assert.IsTrue(BuildEnvironmentState.s_runningTests);
                Assert.AreEqual(initialHandshakeSalt, Framework.Traits.MSBuildNodeHandshakeSalt);
                Assert.AreEqual(initialLogAllEnvVariables, Framework.Traits.LogAllEnvironmentVariables);

                if (originalCulture != null)
                {
                    CultureInfo.CurrentCulture = originalCulture;
                    CultureInfo.CurrentUICulture = originalUICulture;
                }
            }
        }

        private static void CaptureCleanBuildEnvironment()
        {
            // Runs on the first test invocation, immediately after AssemblyInitialize has established the
            // clean environment and before any test body has had a chance to mutate the singleton. Tests run
            // serially (MSTestParallelizeScope=None), so no synchronization is required.
            if (!s_cleanBuildEnvironmentCaptured)
            {
                s_cleanBuildEnvironment = BuildEnvironmentHelper.Instance;
                s_cleanBuildEnvironmentCaptured = true;
            }
        }

        private static void RestoreCleanBuildEnvironment()
        {
            if (s_cleanBuildEnvironment != null)
            {
                // Cheap: just swaps the singleton back to the captured clean instance so that any pollution
                // left behind by the test just executed does not leak into subsequent tests.
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(s_cleanBuildEnvironment);
            }

            ScrubExtensionPathEnvironmentVariables();
        }

        private static void ScrubExtensionPathEnvironmentVariables()
        {
            foreach (string name in s_extensionPathVariableNames)
            {
                // Unset via the managed API...
                Environment.SetEnvironmentVariable(name, null);
#if NETFRAMEWORK
                // ...and unconditionally via the native API, because MSBuild reads its environment through
                // kernel32 (GetEnvironmentStringsW) and, on .NET Framework, the managed and native environment
                // blocks can diverge: a value leaked into the native block by a prior in-proc build (or injected
                // by the `dotnet` muxer at host launch) is often invisible to Environment.GetEnvironmentVariable.
                // We therefore must NOT short-circuit on the managed read - always write through to the native
                // block. Passing null clears the variable so MSBuild falls back to its computed default.
                SetEnvironmentVariable(name, null);
#endif
            }

            // The native environment block we just modified is cached by CommunicationsUtilities across calls.
            // Invalidate that cache so the next evaluation re-reads the freshly-scrubbed block rather than serving
            // a stale snapshot that still contains the leaked MSBuildExtensionsPath value.
            CommunicationsUtilities.ResetEnvironmentStateForUnitTestsOnly();
        }

        protected virtual string GetSkipReason() => null;

        protected static TestResult[] Skipped(ITestMethod testMethod, string reason)
            => new[]
            {
                new TestResult
                {
                    DisplayName = testMethod.TestMethodName,
                    Outcome = UnitTestOutcome.Ignored,
                    TestFailureException = new AssertInconclusiveException(reason),
                },
            };

        private static bool UsesInvariantCulture(ITestMethod testMethod)
            => testMethod.GetAttributes<UseInvariantCultureAttribute>().Any()
                || testMethod.MethodInfo.DeclaringType?.GetCustomAttributes(typeof(UseInvariantCultureAttribute), inherit: true).Any() == true;

        private static string GetCategoryFilterSkipReason(ITestMethod testMethod)
        {
            foreach (string category in GetTestCategories(testMethod))
            {
                if (IsExcludedCategory(category))
                {
                    return $"Test category '{category}' is excluded on this platform/framework.";
                }
            }

            return null;
        }

        private static IEnumerable<string> GetTestCategories(ITestMethod testMethod)
        {
            foreach (TestCategoryBaseAttribute attribute in testMethod.GetAttributes<TestCategoryBaseAttribute>())
            {
                foreach (string category in attribute.TestCategories)
                {
                    yield return category;
                }
            }

            Type declaringType = testMethod.MethodInfo.DeclaringType;
            if (declaringType != null)
            {
                foreach (TestCategoryBaseAttribute attribute in declaringType.GetCustomAttributes(typeof(TestCategoryBaseAttribute), inherit: true))
                {
                    foreach (string category in attribute.TestCategories)
                    {
                        yield return category;
                    }
                }
            }

            foreach (TestCategoryBaseAttribute attribute in testMethod.MethodInfo.Module.Assembly.GetCustomAttributes(typeof(TestCategoryBaseAttribute)))
            {
                foreach (string category in attribute.TestCategories)
                {
                    yield return category;
                }
            }
        }

        private static bool IsExcludedCategory(string category)
            => category switch
            {
                "failing" => true,
                "nonwindowstests" => RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                "nonlinuxtests" => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                "nonosxtests" => RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
                "nonfreebsdtests" => RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")),
                "nonnetcoreapptests" => !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase),
                "nonnetfxtests" => RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase),
                "netcore-linux-failing" => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase),
                "netcore-osx-failing" => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
    }

    public abstract class ConditionalMSBuildTestMethodAttribute : MSBuildTestMethodAttribute
    {
        private readonly Func<bool> _canRun;
        private readonly string _skipReason;

        protected ConditionalMSBuildTestMethodAttribute(Func<bool> canRun, string skipReason, string additionalMessage = null)
        {
            _canRun = canRun;
            _skipReason = skipReason.AppendAdditionalMessage(additionalMessage);
        }

        protected override string GetSkipReason() => _canRun() ? null : _skipReason;
    }

    public class WindowsOnlyFactAttribute : ConditionalMSBuildTestMethodAttribute
    {
        public WindowsOnlyFactAttribute(string additionalMessage = null)
            : base(() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test requires Windows to run.", additionalMessage)
        {
        }
    }

    public sealed class WindowsOnlyTheoryAttribute : WindowsOnlyFactAttribute
    {
        public WindowsOnlyTheoryAttribute(string additionalMessage = null)
            : base(additionalMessage)
        {
        }
    }

    public class UnixOnlyFactAttribute : ConditionalMSBuildTestMethodAttribute
    {
        public UnixOnlyFactAttribute(string additionalMessage = null)
            : base(() => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test requires Unix to run.", additionalMessage)
        {
        }
    }

    public sealed class UnixOnlyTheoryAttribute : UnixOnlyFactAttribute
    {
        public UnixOnlyTheoryAttribute(string additionalMessage = null)
            : base(additionalMessage)
        {
        }
    }

    public sealed class LinuxOnlyFactAttribute : ConditionalMSBuildTestMethodAttribute
    {
        public LinuxOnlyFactAttribute(string additionalMessage = null)
            : base(() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "This test requires Linux to run.", additionalMessage)
        {
        }
    }

    public class DotNetOnlyFactAttribute : ConditionalMSBuildTestMethodAttribute
    {
        public DotNetOnlyFactAttribute(string additionalMessage = null)
            : base(() => !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase), "This test only runs on .NET.", additionalMessage)
        {
        }
    }

    public sealed class DotNetOnlyTheoryAttribute : DotNetOnlyFactAttribute
    {
        public DotNetOnlyTheoryAttribute(string additionalMessage = null)
            : base(additionalMessage)
        {
        }
    }

    public class WindowsFullFrameworkOnlyFactAttribute : ConditionalMSBuildTestMethodAttribute
    {
        public WindowsFullFrameworkOnlyFactAttribute(string additionalMessage = null)
            : base(() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase), "This test only runs on Windows on .NET Framework.", additionalMessage)
        {
        }
    }

    public sealed class WindowsFullFrameworkOnlyTheoryAttribute : WindowsFullFrameworkOnlyFactAttribute
    {
        public WindowsFullFrameworkOnlyTheoryAttribute(string additionalMessage = null)
            : base(additionalMessage)
        {
        }
    }

    public sealed class LongPathSupportDisabledTestMethodAttribute : MSBuildTestMethodAttribute
    {
        private readonly string _additionalMessage;
        private readonly bool _fullFrameworkOnly;

        public LongPathSupportDisabledTestMethodAttribute(string additionalMessage = null, bool fullFrameworkOnly = false)
        {
            _additionalMessage = additionalMessage;
            _fullFrameworkOnly = fullFrameworkOnly;
        }

        protected override string GetSkipReason()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "This test only runs on Windows and when long path support is disabled.".AppendAdditionalMessage(_additionalMessage);
            }

            if (_fullFrameworkOnly && !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                return "This test only runs on full .NET Framework and when long path support is disabled.".AppendAdditionalMessage(_additionalMessage);
            }

            return NativeMethodsShared.IsMaxPathLegacyWindows()
                ? null
                : "This test only runs when long path support is disabled.".AppendAdditionalMessage(_additionalMessage);
        }
    }

    public sealed class RequiresSymbolicLinksTestMethodAttribute : MSBuildTestMethodAttribute
    {
        protected override string GetSkipReason()
        {
            if ((bool.TryParse(Environment.GetEnvironmentVariable("TF_BUILD"), out bool value) && value) || !NativeMethodsShared.IsWindows)
            {
                return null;
            }

            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.Create(sourceFile).Dispose();

                string errorMessage = null;
                return NativeMethodsShared.MakeSymbolicLink(destinationFile, sourceFile, ref errorMessage)
                    ? null
                    : "Requires permission to create symbolic links. Need to be run elevated or under development mode (https://learn.microsoft.com/en-us/windows/apps/get-started/enable-your-device-for-development).";
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    File.Delete(sourceFile);
                }

                if (File.Exists(destinationFile))
                {
                    File.Delete(destinationFile);
                }
            }
        }
    }

    public sealed class WindowsNet35OnlyTestMethodAttribute : MSBuildTestMethodAttribute
    {
        private const string Message = "This test only runs on Windows under .NET Framework when .NET Framework 3.5 is installed.";
        private readonly string _additionalMessage;

        public WindowsNet35OnlyTestMethodAttribute(string additionalMessage = null)
            => _additionalMessage = additionalMessage;

        protected override string GetSkipReason()
            => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase)
                && FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Current) != null
                && BootstrapHasNetFxMicrosoftNetBuildExtensions()
                    ? null
                    : Message.AppendAdditionalMessage(_additionalMessage);

        private static bool BootstrapHasNetFxMicrosoftNetBuildExtensions()
        {
            var binDir = new DirectoryInfo(RunnerUtilities.BootstrapMsBuildBinaryLocation);
            if (!"Bin".Equals(binDir.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var currentDir = binDir.Parent;
            if (currentDir == null || !"Current".Equals(currentDir.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var msbuildDir = currentDir.Parent;
            if (msbuildDir == null || !"MSBuild".Equals(msbuildDir.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var tfmDir = msbuildDir.Parent;
            string tfm = tfmDir?.Name;
            if (tfm == null || !tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var directories = msbuildDir.GetDirectories(@"Microsoft\Microsoft.NET.Build.Extensions\tools\net4*");
            return Array.Exists(directories, x => x.Name == tfm);
        }
    }

    public enum TestPlatforms
    {
        Windows,
        Linux,
        OSX,
        FreeBSD,
        AnyUnix,
    }

    public enum TargetFrameworkMonikers
    {
        Any,
        NetFramework,
        NetCoreApp,
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class ActiveIssueAttribute : TestCategoryBaseAttribute
    {
        private readonly bool _active;

        public ActiveIssueAttribute(string issue)
            => _active = true;

        public ActiveIssueAttribute(string issue, TestPlatforms platform)
            => _active = AppliesTo(platform);

        public ActiveIssueAttribute(string issue, TargetFrameworkMonikers targetFramework)
            => _active = AppliesTo(targetFramework);

        public override IList<string> TestCategories => _active ? new[] { "failing" } : Array.Empty<string>();

        private static bool AppliesTo(TestPlatforms platform)
            => platform switch
            {
                TestPlatforms.Windows => NativeMethodsShared.IsWindows,
                TestPlatforms.Linux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                TestPlatforms.OSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
                TestPlatforms.FreeBSD => RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")),
                TestPlatforms.AnyUnix => !NativeMethodsShared.IsWindows,
                _ => false,
            };

        private static bool AppliesTo(TargetFrameworkMonikers targetFramework)
            => targetFramework switch
            {
                TargetFrameworkMonikers.Any => true,
                TargetFrameworkMonikers.NetFramework => RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase),
                TargetFrameworkMonikers.NetCoreApp => !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class SkipOnPlatformAttribute : ConditionalMSBuildTestMethodAttribute
    {
        public SkipOnPlatformAttribute(TestPlatforms platform, string reason)
            : base(() => !ActiveIssueAttributeAppliesTo(platform), reason)
        {
        }

        private static bool ActiveIssueAttributeAppliesTo(TestPlatforms platform)
            => platform switch
            {
                TestPlatforms.Windows => NativeMethodsShared.IsWindows,
                TestPlatforms.Linux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                TestPlatforms.OSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
                TestPlatforms.FreeBSD => RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")),
                TestPlatforms.AnyUnix => !NativeMethodsShared.IsWindows,
                _ => false,
            };
    }

#pragma warning restore MSTEST0057

    internal static class TestAttributeUtilities
    {
        public static string AppendAdditionalMessage(this string message, string additionalMessage)
            => !string.IsNullOrWhiteSpace(additionalMessage) ? $"{message} {additionalMessage}" : message;
    }
}
