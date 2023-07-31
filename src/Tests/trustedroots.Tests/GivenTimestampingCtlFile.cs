// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    public class GivenTimestampingCtlFile : CtlFileTests
    {
        private static IReadOnlySet<string> s_fingerprints = null;
        private static object s_lockObject = new();

        public GivenTimestampingCtlFile()
            : base("timestampctl.pem")
        {
            LazyInitializer.EnsureInitialized(ref s_fingerprints, ref s_lockObject, Initialize);
        }

        [Theory]
        [InlineData("2399561127a57125de8cefea610ddf2fa078b5c8067f4e828290bfb860e84b3c")]  // CN=VeriSign Universal Root Certification Authority, OU="(c) 2008 VeriSign, Inc. - For authorized use only", OU=VeriSign Trust Network, O="VeriSign, Inc.", C=US
        [InlineData("2cabeafe37d06ca22aba7391c0033d25982952c453647349763a3ab5ad6ccf69")]  // CN=GlobalSign, O=GlobalSign, OU=GlobalSign Root CA - R6
        [InlineData("2ce1cb0bf9d2f9e102993fbe215152c3b2dd0cabde1c68e5319b839154dbb7f5")]  // CN=Starfield Root Certificate Authority - G2, O="Starfield Technologies, Inc.", L=Scottsdale, S=Arizona, C=US
        [InlineData("3b222e566711e992300dc0b15ab9473dafdef8c84d0cef7d3317b4c1821d1436")]  // CN=SwissSign Platinum CA - G2, O=SwissSign AG, C=CH
        [InlineData("3e9099b5015e8f486c00bcea9d111ee721faba355a89bcf1df69561e3dc6325c")]  // CN=DigiCert Assured ID Root CA, OU=www.digicert.com, O=DigiCert Inc, C=US
        [InlineData("43df5774b03e7fef5fe40d931a7bedf1bb2e6b42738c4e6d3841103d3aa7f339")]  // CN=Entrust Root Certification Authority - G2, OU="(c) 2009 Entrust, Inc. - for authorized use only", OU=See www.entrust.net/legal-terms, O="Entrust, Inc.", C=US
        [InlineData("5367f20c7ade0e2bca790915056d086b720c33c1fa2a2661acf787e3292e1270")]  // CN=Microsoft Identity Verification Root Certificate Authority 2020, O=Microsoft Corporation, C=US
        [InlineData("5c58468d55f58e497e743982d2b50010b6d165374acf83a7d4a32db768c4408e")]  // CN=Certum Trusted Network CA, OU=Certum Certification Authority, O=Unizeto Technologies S.A., C=PL
        [InlineData("6dc47172e01cbcb0bf62580d895fe2b8ac9ad4f873801e0c10b9c837d21eb177")]  // CN=Entrust.net Certification Authority (2048), OU=(c) 1999 Entrust.net Limited, OU=www.entrust.net/CPS_2048 incorp. by ref. (limits liab.), O=Entrust.net
        [InlineData("6fff78e400a70c11011cd85977c459fb5af96a3df0540820d0f4b8607875e58f")]  // CN=UTN-USERFirst-Object, OU=http://www.usertrust.com, O=The USERTRUST Network, L=Salt Lake City, S=UT, C=US
        [InlineData("8a866fd1b276b57e578e921c65828a2bed58e9f2f288054134b7f1f4bfc9cc74")]  // CN=QuoVadis Root CA 1 G3, O=QuoVadis Limited, C=BM
        [InlineData("a45ede3bbbf09c8ae15c72efc07268d693a21c996fd51e67ca079460fd6d8873")]  // CN=QuoVadis Root Certification Authority, OU=Root Certification Authority, O=QuoVadis Limited, C=BM
        [InlineData("cbb522d7b7f127ad6a0113865bdf1cd4102e7d0759af635a7cf4720dc963c53b")]  // CN=GlobalSign, O=GlobalSign, OU=GlobalSign Root CA - R3
        [InlineData("d7a7a0fb5d7e2731d771e9484ebcdef71d5f0c3e0a2948782bc83ee0ea699ef4")]  // CN=AAA Certificate Services, O=Comodo CA Limited, L=Salford, S=Greater Manchester, C=GB
        [InlineData("e793c9b02fd8aa13e21c31228accb08119643b749c898964b1746d46c3d4cbd2")]  // CN=USERTrust RSA Certification Authority, O=The USERTRUST Network, L=Jersey City, S=New Jersey, C=US
        public void File_contains_certificates_used_in_NuGet_org_package_signatures(string expectedFingerprint)
        {
            VerifyCertificateExists(s_fingerprints, expectedFingerprint);
        }
    }
}
