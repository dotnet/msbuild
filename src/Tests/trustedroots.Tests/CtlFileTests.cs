// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
