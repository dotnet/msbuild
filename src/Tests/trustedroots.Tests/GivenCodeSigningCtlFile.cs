// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class GivenCodeSigningCtlFile
    {
        private readonly X509Certificate2Collection _certificates;
        private readonly string _filePath;

        public GivenCodeSigningCtlFile()
        {
            _filePath = Path.Combine(
                TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest,
                "trustedroots",
                "codesignctl.pem");

            _certificates = new();

            _certificates.ImportFromPemFile(_filePath);
        }

        [Fact]
        public void File_contains_certificates_very_commonly_used_in_NuGet_org_package_signatures()
        {
            // CN=DigiCert Assured ID Root CA, OU=www.digicert.com, O=DigiCert Inc, C=US
            VerifyCertificateExists(thumbprint: "0563b8630d62d75abbc8ab1e4bdfb5a899b24d43");

            // CN=VeriSign Universal Root Certification Authority, OU="(c) 2008 VeriSign, Inc. - For authorized use only", OU=VeriSign Trust Network, O="VeriSign, Inc.", C=US
            VerifyCertificateExists(thumbprint: "3679ca35668772304d30a5fb873b0fa77bb70d54");
        }

        private void VerifyCertificateExists(string thumbprint)
        {
            _certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
                .Count
                .Should()
                .Be(1, $"a certificate with thumbprint '{thumbprint}' should be in '{_filePath}'");
        }
    }
}
