// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class SecurityUtil_Tests
    {
        private readonly ITestOutputHelper _output;

        public SecurityUtil_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string TestAssembliesPaths { get; } = Path.Combine(AppContext.BaseDirectory, "TestResources");

        [WindowsOnlyTheory]
        [InlineData("v4.5", Constants.DotNetFrameworkIdentifier)]
        [InlineData("v4.5", Constants.DotNetCoreAppIdentifier)]
        [SupportedOSPlatform("windows")]
        public void SignFile_Success(string tfVersion, string tfIdentifier)
        {
            Uri timestampUrl = new("http://timestamp.comodoca.com/rfc3161");
            string clickOnceManifest = Path.Combine(TestAssembliesPaths, "ClickOnceProfile.pubxml");
            string targetFrameworkVersion = tfVersion;
            string targetFrameworkIdentifier = tfIdentifier;
            bool disallowMansignTimestampFallback = false;

            // the certificate was generated locally and does not contain any sensitive information
            string pathToCertificate = Path.Combine(TestAssembliesPaths, "mycert.pfx");
            using X509Certificate2 certificate = TestCertHelper.MockCertificate(pathToCertificate);

            void SignAction() => SecurityUtilities.SignFile(
                certificate?.Thumbprint,
                timestampUrl,
                clickOnceManifest,
                targetFrameworkVersion,
                targetFrameworkIdentifier,
                disallowMansignTimestampFallback);

            Should.NotThrow(SignAction);

            TestCertHelper.RemoveCertificate(certificate);
        }

        /// <summary>
        /// Regression test: GetPathToTool with a fallback directory must never
        /// return a path under Directory.GetCurrentDirectory(). Whether the SDK
        /// signtool is found or not, the result must NOT be the CWD-based path.
        /// </summary>
        [WindowsOnlyFact]
        [SupportedOSPlatform("windows")]
        public void GetPathToTool_WithFallbackDirectory_NeverUsesCwd()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Create a fake signtool in a "project" directory
            var projectDir = env.CreateFolder();
            string projectSignTool = Path.Combine(projectDir.Path, "signtool.exe");
            File.WriteAllText(projectSignTool, "fake-project");

            // Create a different fake signtool in a "CWD" directory
            var cwdDir = env.CreateFolder();
            string cwdSignTool = Path.Combine(cwdDir.Path, "signtool.exe");
            File.WriteAllText(cwdSignTool, "fake-cwd");

            env.SetCurrentDirectory(cwdDir.Path);

            var resources = new System.Resources.ResourceManager(
                "Microsoft.Build.Tasks.Core.Strings.ManifestUtilities",
                typeof(SecurityUtilities).Assembly);

            string toolPath = SecurityUtilities.GetPathToTool(resources, projectDir.Path);
            _output.WriteLine($"GetPathToTool returned: {toolPath}");

            // Regardless of whether SDK signtool was found, the result must
            // never be the CWD-based path — that would indicate process-global state leaking.
            toolPath.Equals(cwdSignTool, StringComparison.OrdinalIgnoreCase)
                .ShouldBeFalse("GetPathToTool must not fall back to Directory.GetCurrentDirectory().");
        }

        /// <summary>
        /// Regression test: When TaskEnvironment is provided with a specific ProjectDirectory,
        /// ProcessStartInfo obtained from it must have WorkingDirectory set to that directory,
        /// not the process CWD. This is the mechanism that makes SignPEFileInternal thread-safe.
        /// </summary>
        [WindowsOnlyFact]
        [SupportedOSPlatform("windows")]
        public void SignFile_WithTaskEnvironment_UsesTaskEnvironmentProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var projectDir = env.CreateFolder();

            var taskEnvironment = TaskEnvironmentHelper.CreateMultithreadedForTest(projectDir.Path);

            ProcessStartInfo psi = taskEnvironment.GetProcessStartInfo();
            psi.WorkingDirectory.ShouldBe(projectDir.Path,
                "TaskEnvironment.GetProcessStartInfo() must set WorkingDirectory to ProjectDirectory.");

            taskEnvironment.ProjectDirectory.Value.ShouldBe(projectDir.Path);
        }

        internal static class TestCertHelper
        {
            private static readonly X509Store s_personalStore = new(StoreName.My, StoreLocation.CurrentUser);

            internal static X509Certificate2 MockCertificate(string pathToCertificate)
            {
                var certificate = new X509Certificate2(pathToCertificate);
                UpdateCertificateState(certificate, s_personalStore.Add);

                return certificate;
            }

            internal static void RemoveCertificate(X509Certificate2 certificate) => UpdateCertificateState(certificate, s_personalStore.Remove);

            private static void UpdateCertificateState(X509Certificate2 certificate, Action<X509Certificate2> updateAction)
            {
                try
                {
                    s_personalStore.Open(OpenFlags.ReadWrite);
                    updateAction(certificate);
                }
                finally
                {
                    s_personalStore.Close();
                }
            }
        }
    }
}
