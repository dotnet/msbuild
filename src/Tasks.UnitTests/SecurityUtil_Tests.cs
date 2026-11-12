// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Constants = Microsoft.Build.Tasks.Deployment.ManifestUtilities.Constants;
#if FEATURE_WINDOWSINTEROP
using System.ComponentModel;
using System.Security.Cryptography.Xml;
using System.Xml;
using Windows.Win32;
#endif

namespace Microsoft.Build.Tasks.UnitTests
{
    [Collection(nameof(SignFileTestsCollection))]
    public class SecurityUtil_Tests
    {
        private readonly ITestOutputHelper _output;
        private static string TestAssembliesPaths { get; } = Path.Combine(AppContext.BaseDirectory, "TestResources");

        public SecurityUtil_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

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

#if FEATURE_WINDOWSINTEROP
        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        [SupportedOSPlatform("windows6.1")]
        public async System.Threading.Tasks.Task SignFile_ManifestsUseIndependentDirectoriesWithoutChangingDllDirectory(bool validManifest)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFolder firstProject = env.CreateFolder();
            TransientTestFolder secondProject = env.CreateFolder();
            TransientTestFolder decoy = env.CreateFolder();
            env.SetCurrentDirectory(decoy.Path);
            env.WithTransientTestState(new DllDirectoryScope(decoy.Path));
            StoredTestCertificate certificate = env.WithTransientTestState(new StoredTestCertificate());
            string manifestContents = validManifest
                ? File.ReadAllText(Path.Combine(TestAssembliesPaths, "ClickOnceProfile.pubxml"))
                : "<invalid";
            TransientTestFile firstManifest = env.CreateFile(firstProject, "application.manifest", manifestContents);
            TransientTestFile secondManifest = env.CreateFile(secondProject, "application.manifest", manifestContents);
            SignFile firstTask = CreateTask(certificate.Certificate, firstProject.Path, "application.manifest");
            SignFile secondTask = CreateTask(certificate.Certificate, secondProject.Path, "application.manifest");

            bool[] results = await System.Threading.Tasks.Task.WhenAll(
                System.Threading.Tasks.Task.Run(firstTask.Execute),
                System.Threading.Tasks.Task.Run(secondTask.Execute));

            results.ShouldAllBe(result => result == validManifest);
            GetDllDirectory().ShouldBe(decoy.Path);
            File.Exists(Path.Combine(decoy.Path, "application.manifest")).ShouldBeFalse();
            if (validManifest)
            {
                VerifyManifestSignature(firstManifest.Path, certificate.Certificate);
                VerifyManifestSignature(secondManifest.Path, certificate.Certificate);
            }
            else
            {
                ((MockEngine)firstTask.BuildEngine).Log.ShouldContain("MSB3482");
                ((MockEngine)secondTask.BuildEngine).Log.ShouldContain("MSB3482");
            }
        }
#endif

        [WindowsOnlyTheory]
        [InlineData(0, true, 0, 0, false)]
        [InlineData(1, false, 1, 0, false)]
        [InlineData(2, true, 0, 1, false)]
        [InlineData(7, false, 1, 0, false)]
        [InlineData(0, true, 0, 0, true)]
        [SupportedOSPlatform("windows")]
        public void SignFile_PEToolUsesTaskEnvironment(
            int exitCode, bool expectedSuccess, int expectedErrors, int expectedWarnings, bool retryTimestamp)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFolder project = env.CreateFolder();
            TransientTestFolder decoy = env.CreateFolder();
            string toolPath = CreateSignTool(env, project);
            env.CreateFile(project, "application.exe", "MZtest");
            env.SetCurrentDirectory(decoy.Path);
            env.WithTransientTestState(new SignToolFileSystemScope(toolPath));
            StoredTestCertificate certificate = env.WithTransientTestState(new StoredTestCertificate());
            SignFile task = CreateTask(certificate.Certificate, project.Path, "application.exe");
            string observationsPath = Path.Combine(project.Path, "tool-observations.txt");
            task.TaskEnvironment.SetEnvironmentVariable("SIGNFILE_TEST_OUTPUT", observationsPath);
            task.TaskEnvironment.SetEnvironmentVariable("SIGNFILE_TEST_VALUE", "project-specific");
            task.TaskEnvironment.SetEnvironmentVariable("SIGNFILE_TEST_EXIT_CODE", exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            task.TaskEnvironment.SetEnvironmentVariable("SIGNFILE_TEST_RETRY", retryTimestamp ? "1" : "0");
            task.TimestampUrl = retryTimestamp ? "https://timestamp.invalid" : null;

            task.Execute().ShouldBe(expectedSuccess);

            MockEngine engine = (MockEngine)task.BuildEngine;
            engine.Errors.ShouldBe(expectedErrors);
            engine.Warnings.ShouldBe(expectedWarnings);
            engine.Log.ShouldNotContain(Path.Combine(project.Path, "application.exe"));
            string[] observations = File.ReadAllLines(observationsPath);
            observations.Length.ShouldBe(retryTimestamp ? 8 : 4);
            for (int i = 0; i < observations.Length; i += 4)
            {
                observations[i].ShouldBe(project.Path);
                observations[i + 1].ShouldBe("project-specific");
                observations[i + 2].ShouldBe("application.exe");
            }

            if (retryTimestamp)
            {
                observations[3].ShouldContain("/tr https://timestamp.invalid/");
                observations[7].ShouldContain("/t https://timestamp.invalid/");
            }
        }

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows")]
        public void SignFile_MissingTargetPreservesOriginalPath()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFolder project = env.CreateFolder();
            StoredTestCertificate certificate = env.WithTransientTestState(new StoredTestCertificate());
            SignFile task = CreateTask(certificate.Certificate, project.Path, "missing.manifest");

            task.Execute().ShouldBeFalse();

            MockEngine engine = (MockEngine)task.BuildEngine;
            engine.Errors.ShouldBe(1);
            engine.Log.ShouldContain("MSB3484");
            engine.Log.ShouldContain("missing.manifest");
            engine.Log.ShouldNotContain(project.Path);
        }

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows")]
        public void SignFile_EmptyTargetPreservesArgumentNullException()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            StoredTestCertificate certificate = env.WithTransientTestState(new StoredTestCertificate());
            SignFile task = CreateTask(certificate.Certificate, env.DefaultTestDirectory.Path, string.Empty);

            Should.Throw<ArgumentNullException>(() => task.Execute()).ParamName.ShouldBe("path");
        }

        [SupportedOSPlatform("windows")]
        private SignFile CreateTask(X509Certificate2 certificate, string projectDirectory, string target)
            => new()
            {
                BuildEngine = new MockEngine(_output),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory),
                CertificateThumbprint = certificate.Thumbprint,
                SigningTarget = new TaskItem(target),
                TargetFrameworkVersion = "v4.5"
            };

        [SupportedOSPlatform("windows")]
        private string CreateSignTool(TestEnvironment env, TransientTestFolder project)
        {
            TransientTestFile source = env.CreateFile(project, "signtool.cs", """
                using System;
                using System.Globalization;
                using System.IO;

                public static class Program
                {
                    public static int Main(string[] args)
                    {
                        File.AppendAllLines(Environment.GetEnvironmentVariable("SIGNFILE_TEST_OUTPUT"), new[]
                        {
                            Directory.GetCurrentDirectory(),
                            Environment.GetEnvironmentVariable("SIGNFILE_TEST_VALUE"),
                            args[args.Length - 1],
                            string.Join(" ", args)
                        });
                        Console.Error.WriteLine("signtool diagnostic: " + args[args.Length - 1]);
                        if (Environment.GetEnvironmentVariable("SIGNFILE_TEST_RETRY") == "1"
                            && Array.IndexOf(args, "/tr") >= 0)
                        {
                            return 1;
                        }
                        return int.Parse(Environment.GetEnvironmentVariable("SIGNFILE_TEST_EXIT_CODE"), CultureInfo.InvariantCulture);
                    }
                }
                """);
            string toolPath = Path.Combine(project.Path, "signtool.exe");
            string compiler = Path.Combine(
                Path.GetDirectoryName(Environment.SystemDirectory)!,
                "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe");
            string compilerOutput = RunnerUtilities.RunProcessAndGetOutput(
                compiler, $"/nologo /target:exe /out:\"{toolPath}\" \"{source.Path}\"",
                out bool compiled, outputHelper: _output);
            compiled.ShouldBeTrue(compilerOutput);
            return toolPath;
        }

#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows")]
        private static void VerifyManifestSignature(string path, X509Certificate2 certificate)
        {
            var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            document.Load(reader);
            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            XmlElement signature = document.SelectSingleNode("/*/ds:Signature", namespaces).ShouldBeOfType<XmlElement>();
            var signedXml = new SignedXml(document);
            signedXml.LoadXml(signature);
            signedXml.CheckSignature(certificate, verifySignatureOnly: true).ShouldBeTrue();
        }

        [SupportedOSPlatform("windows6.1")]
        private static string GetDllDirectory()
        {
            using BufferScope<char> buffer = new(stackalloc char[260]);
            uint length = PInvoke.GetDllDirectory(buffer.AsSpan());
            if (length >= buffer.Length)
            {
                buffer.EnsureCapacity((int)length);
                length = PInvoke.GetDllDirectory(buffer.AsSpan());
            }
            return buffer.Slice(0, (int)length).ToString();
        }

        [SupportedOSPlatform("windows6.1")]
        private sealed class DllDirectoryScope : TransientTestState
        {
            private readonly string _originalDirectory = GetDllDirectory();

            public DllDirectoryScope(string directory)
            {
                if (!PInvoke.SetDllDirectory(directory))
                {
                    throw new Win32Exception();
                }
            }

            public override void Revert()
            {
                if (!PInvoke.SetDllDirectory(_originalDirectory.Length == 0 ? null : _originalDirectory))
                {
                    throw new Win32Exception();
                }
            }
        }
#endif

        [SupportedOSPlatform("windows")]
        private sealed class StoredTestCertificate : TransientTestState
        {
            public X509Certificate2 Certificate { get; } = TestCertHelper.MockCertificate(Path.Combine(TestAssembliesPaths, "mycert.pfx"));

            public override void Revert()
            {
                TestCertHelper.RemoveCertificate(Certificate);
                Certificate.Dispose();
            }
        }

        private sealed class SignToolFileSystemScope : TransientTestState, IFileSystem
        {
            private readonly IFileSystem _original = FileSystems.Default;
            private readonly string _toolPath;

            public SignToolFileSystemScope(string toolPath)
            {
                _toolPath = toolPath;
                FileSystems.Default = this;
            }

            public override void Revert() => FileSystems.Default = _original;
            public bool FileExists(string path) => _original.FileExists(path)
                && (!string.Equals(Path.GetFileName(path), "signtool.exe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, _toolPath, StringComparison.OrdinalIgnoreCase));
            public TextReader ReadFile(string path) => _original.ReadFile(path);
            public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share) => _original.GetFileStream(path, mode, access, share);
            public string ReadFileAllText(string path) => _original.ReadFileAllText(path);
            public byte[] ReadFileAllBytes(string path) => _original.ReadFileAllBytes(path);
            public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => _original.EnumerateFiles(path, searchPattern, searchOption);
            public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => _original.EnumerateDirectories(path, searchPattern, searchOption);
            public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => _original.EnumerateFileSystemEntries(path, searchPattern, searchOption);
            public FileAttributes GetAttributes(string path) => _original.GetAttributes(path);
            public DateTime GetLastWriteTimeUtc(string path) => _original.GetLastWriteTimeUtc(path);
            public bool DirectoryExists(string path) => _original.DirectoryExists(path);
            public bool FileOrDirectoryExists(string path) => _original.FileOrDirectoryExists(path);
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

    [CollectionDefinition(nameof(SignFileTestsCollection), DisableParallelization = true)]
    public sealed class SignFileTestsCollection
    {
    }
}
