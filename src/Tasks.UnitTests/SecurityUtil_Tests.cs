﻿// Licensed to the .NET Foundation under one or more agreements.
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

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows")]
        public void SignFile_Success()
        {
            Uri timestampUrl = new("http://timestamp.comodoca.com/rfc3161");
            string clickOnceManifest = Path.Combine(TestAssembliesPaths, "ClickOnceProfile.pubxml");
            string targetFrameworkVersion = Constants.TargetFrameworkVersion40;
            string targetFrameworkIdentifier = Constants.DotNetFrameworkIdentifier;
            bool disallowMansignTimestampFallback = false;

            // the certificate was generated locally and does not contain any sensitive information
            string pathToCertificate = Path.Combine(TestAssembliesPaths, "mycert.pfx");
            X509Certificate2 cerfiticate = TestCertHelper.MockCertificate(pathToCertificate);

            void SignAction() => SecurityUtilities.SignFile(
                cerfiticate?.Thumbprint,
                timestampUrl,
                clickOnceManifest,
                targetFrameworkVersion,
                targetFrameworkIdentifier,
                disallowMansignTimestampFallback);

            Should.NotThrow(SignAction);

            TestCertHelper.RemoveCertificate(cerfiticate);
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
