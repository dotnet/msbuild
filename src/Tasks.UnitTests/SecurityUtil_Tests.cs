// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class SecurityUtil_Tests
    {
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
