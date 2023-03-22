// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.Installer.Windows.Security;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    [SupportedOSPlatform("windows")]
    public class WindowsInstallerTests
    {
        private static string s_testDataPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestData");

        private void LogTask(string pipeName)
        {
            using NamedPipeServerStream serverPipe = CreateServerPipe(pipeName);
            PipeStreamSetupLogger logger = new PipeStreamSetupLogger(serverPipe, pipeName);

            logger.Connect();

            for (int i = 0; i < 10; i++)
            {
                logger.LogMessage($"Hello from {pipeName} ({i}).");
            }
        }

        [WindowsOnlyFact]
        public void MultipleProcessesCanWriteToTheLog()
        {
            var logFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            TimestampedFileLogger logger = new(logFile);

            logger.AddNamedPipe("np1");
            logger.AddNamedPipe("np2");
            logger.AddNamedPipe("np3");

            var t1 = Task.Run(() => { LogTask("np1"); });
            var t2 = Task.Run(() => { LogTask("np2"); });
            var t3 = Task.Run(() => { LogTask("np3"); });

            Task.WaitAll(t1, t2, t3);
            logger.Dispose();

            string logContent = File.ReadAllText(logFile);

            Assert.Contains("Hello from np1", logContent);
            Assert.Contains("Hello from np2", logContent);
            Assert.Contains("Hello from np3", logContent);
            Assert.Contains("=== Logging ended ===", logContent);
        }

        [WindowsOnlyFact]
        public void InstallMessageDispatcherProcessesMessages()
        {
            string pipeName = Guid.NewGuid().ToString();
            NamedPipeServerStream serverPipe = CreateServerPipe(pipeName);
            NamedPipeClientStream clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

            InstallMessageDispatcher sd = new(serverPipe);
            InstallMessageDispatcher cd = new(clientPipe);

            Task.Run(() =>
            {
                ServerDispatcher server = new ServerDispatcher(sd);
                server.Run();
            });

            cd.Connect();

            InstallResponseMessage r1 = cd.SendMsiRequest(InstallRequestType.UninstallMsi, "");
            InstallResponseMessage r2 = cd.SendShutdownRequest();

            Assert.Equal("Received request: UninstallMsi", r1.Message);
            Assert.Equal("Shutting down!", r2.Message);
        }

        [WindowsOnlyTheory]
        [InlineData("1033,1041,1049", UpgradeAttributes.MigrateFeatures, 1041, false)]
        [InlineData(null, UpgradeAttributes.LanguagesExclusive, 3082, false)]
        [InlineData("1033,1041,1049", UpgradeAttributes.LanguagesExclusive, 1033, true)]
        public void RelatedProductExcludesLanguages(string language, UpgradeAttributes attributes, int lcid,
            bool expectedResult)
        {
            RelatedProduct rp = new()
            {
                Attributes = attributes,
                Language = language
            };

            Assert.Equal(expectedResult, rp.ExcludesLanguage(lcid));
        }

        [WindowsOnlyTheory]
        [InlineData("72.13.638", UpgradeAttributes.MigrateFeatures, "72.13.639", true)]
        [InlineData("72.13.638", UpgradeAttributes.VersionMaxInclusive, "72.13.638", false)]
        public void RelatedProductExcludesMaxVersion(string maxVersion, UpgradeAttributes attributes, string installedVersionValue,
            bool expectedResult)
        {
            Version installedVersion = new Version(installedVersionValue);

            RelatedProduct rp = new()
            {
                Attributes = attributes,
                VersionMax = maxVersion == null ? null : new Version(maxVersion),
                VersionMin = null
            };

            Assert.Equal(expectedResult, rp.ExcludesMaxVersion(installedVersion));
        }

        [WindowsOnlyTheory]
        [InlineData("72.13.638", UpgradeAttributes.MigrateFeatures, "72.13.638", true)]
        [InlineData("72.13.638", UpgradeAttributes.VersionMinInclusive, "72.13.638", false)]
        public void RelatedProductExcludesMinVersion(string minVersion, UpgradeAttributes attributes, string installedVersionValue,
            bool expectedResult)
        {
            Version installedVersion = new Version(installedVersionValue);

            RelatedProduct rp = new()
            {
                Attributes = attributes,
                VersionMin = minVersion == null ? null : new Version(minVersion),
                VersionMax = null
            };

            Assert.Equal(expectedResult, rp.ExcludesMinVersion(installedVersion));
        }

        [WindowsOnlyTheory]
        [InlineData("tampered.msi", false, "The digital signature of the object did not verify.")]
        [InlineData("dual_signed.dll", true, "")]
        [InlineData("dotnet_realsigned.exe", true, "")]
        [InlineData("dotnet_fakesigned.exe", false, "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.")]
        public void AuthentiCodeSignaturesCanBeVerified(string file, bool shouldBeSigned, string expectedError)
        {
            bool isSigned = AuthentiCode.IsSigned(Path.Combine(s_testDataPath, file));

            Assert.Equal(shouldBeSigned, isSigned);

            if (!shouldBeSigned)
            {
                Assert.Equal(expectedError, new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
        }

        [WindowsOnlyTheory]
        [InlineData("dotnet_realsigned.exe")]
        [InlineData("dotnet_fakesigned.exe")]
        public void IsSignedByTrustedOrganizationOnlyVerifiesTheSubjectOrganization(string file)
        {
            Assert.True(AuthentiCode.IsSignedByTrustedOrganization(Path.Combine(s_testDataPath, file), AuthentiCode.TrustedOrganizations));
        }

        [WindowsOnlyFact]
        public void IsSignedBytTrustedOrganizationVerifiesNestedSignatures()
        {
            Assert.True(AuthentiCode.IsSignedByTrustedOrganization(Path.Combine(s_testDataPath, "dual_signed.dll"),
                "Foo", "Bar", "Microsoft Corporation")); ;
            Assert.True(AuthentiCode.IsSignedByTrustedOrganization(Path.Combine(s_testDataPath, "dual_signed.dll"),
                "Foo", "Bar", "WiX Toolset (.NET Foundation)"));
        }

        [WindowsOnlyFact]
        public void GetCertificatesRetrievesNestedSignatures()
        {
            var certificates = AuthentiCode.GetCertificates(Path.Combine(s_testDataPath, "triple_signed.dll")).ToArray();

            Assert.Equal("CN=Microsoft Corporation, OU=MOPR, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", certificates[0].Subject);
            Assert.Equal("sha1RSA", certificates[0].SignatureAlgorithm.FriendlyName);
            Assert.Equal("CN=Microsoft 3rd Party Application Component, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", certificates[1].Subject);
            Assert.Equal("sha256RSA", certificates[1].SignatureAlgorithm.FriendlyName);
            Assert.Equal("CN=Microsoft Corporation, OU=MOPR, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", certificates[2].Subject);
            Assert.Equal("sha256RSA", certificates[2].SignatureAlgorithm.FriendlyName);
        }

        [WindowsOnlyFact]
        public void GetCertificatesRetrievesNothingForUnsignedFiles()
        {
            var certificates = AuthentiCode.GetCertificates(Assembly.GetExecutingAssembly().Location);

            Assert.Empty(certificates);
        }

        private NamedPipeServerStream CreateServerPipe(string name)
        {
            return new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Message);
        }
    }

    [SupportedOSPlatform("windows")]
    internal class ServerDispatcher
    {
        InstallMessageDispatcher _dispatcher;

        public ServerDispatcher(InstallMessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Run()
        {
            _dispatcher.Connect();
            bool done = false;

            while (!done)
            {
                if (_dispatcher == null || !_dispatcher.IsConnected)
                {
                    throw new IOException("Server dispatcher disconnected or not initialized.");
                }

                var request = _dispatcher.ReceiveRequest();

                if (request.RequestType == InstallRequestType.Shutdown)
                {
                    done = true;
                    _dispatcher.ReplySuccess("Shutting down!");
                }
                else
                {
                    _dispatcher.ReplySuccess($"Received request: {request.RequestType}");
                }
            }
        }
    }
}
