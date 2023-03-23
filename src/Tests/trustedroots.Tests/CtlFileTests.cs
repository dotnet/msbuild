// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tests
{
    public abstract class CtlFileTests
    {
        private readonly string _filePath;

        public CtlFileTests(string fileName)
        {
            _filePath = Path.Combine(
                TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest,
                "trustedroots",
                fileName);
        }

        protected IReadOnlySet<string> Initialize()
        {
            X509Certificate2Collection certificates = new();

            certificates.ImportFromPemFile(_filePath);

            HashSet<string> fingerprints = new(StringComparer.OrdinalIgnoreCase);

            foreach (X509Certificate2 certificate in certificates)
            {
                string fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);

                fingerprints.Add(fingerprint);
            }

            return fingerprints;
        }

        protected void VerifyCertificateExists(IReadOnlySet<string> fingerprints, string expectedFingerprint)
        {
            fingerprints.Contains(expectedFingerprint)
                .Should()
                .Be(true, $"The certificate bundle at {_filePath} did not contain a certificate with fingerprint '{expectedFingerprint}'.");
        }
    }
}
