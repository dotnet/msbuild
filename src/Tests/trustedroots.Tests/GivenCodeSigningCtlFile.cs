// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    public class GivenCodeSigningCtlFile : CtlFileTests
    {
        private static IReadOnlySet<string> s_fingerprints = null;
        private static object s_lockObject = new();

        public GivenCodeSigningCtlFile()
            : base("codesignctl.pem")
        {
            LazyInitializer.EnsureInitialized(ref s_fingerprints, ref s_lockObject, Initialize);
        }

        [Theory]
        [InlineData("063e4afac491dfd332f3089b8542e94617d893d7fe944e10a7937ee29d9693c0")]  // CN=Chambers of Commerce Root - 2008, O=AC Camerfirma S.A., SERIALNUMBER=A82743287, L=Madrid (see current address at www.camerfirma.com/address), C=EU
        [InlineData("2399561127a57125de8cefea610ddf2fa078b5c8067f4e828290bfb860e84b3c")]  // CN=VeriSign Universal Root Certification Authority, OU="(c) 2008 VeriSign, Inc. - For authorized use only", OU=VeriSign Trust Network, O="VeriSign, Inc.", C=US
        [InlineData("2e7bf16cc22485a7bbe2aa8696750761b0ae39be3b2fe9d0cc6d4ef73491425c")]  // CN=SSL.com EV Root Certification Authority RSA R2, O=SSL Corporation, L=Houston, S=Texas, C=US
        [InlineData("3e9099b5015e8f486c00bcea9d111ee721faba355a89bcf1df69561e3dc6325c")]  // CN=DigiCert Assured ID Root CA, OU=www.digicert.com, O=DigiCert Inc, C=US
        [InlineData("43df5774b03e7fef5fe40d931a7bedf1bb2e6b42738c4e6d3841103d3aa7f339")]  // CN=Entrust Root Certification Authority - G2, OU="(c) 2009 Entrust, Inc. - for authorized use only", OU=See www.entrust.net/legal-terms, O="Entrust, Inc.", C=US
        [InlineData("45140b3247eb9cc8c5b4f0d7b53091f73292089e6e5a63e2749dd3aca9198eda")]  // CN=Go Daddy Root Certificate Authority - G2, O="GoDaddy.com, Inc.", L=Scottsdale, S=Arizona, C=US
        [InlineData("4b03f45807ad70f21bfc2cae71c9fde4604c064cf5ffb686bae5dbaad7fdd34c")]  // CN=thawte Primary Root CA - G3, OU="(c) 2008 thawte, Inc. - For authorized use only", OU=Certification Services Division, O="thawte, Inc.", C=US
        [InlineData("52f0e1c4e58ec629291b60317f074671b85d7ea80d5b07273463534b32b40234")]  // CN=COMODO RSA Certification Authority, O=COMODO CA Limited, L=Salford, S=Greater Manchester, C=GB
        [InlineData("552f7bdcf1a7af9e6ce672017f4f12abf77240c78e761ac203d1d9d20ac89988")]  // CN=DigiCert Trusted Root G4, OU=www.digicert.com, O=DigiCert Inc, C=US
        [InlineData("5c58468d55f58e497e743982d2b50010b6d165374acf83a7d4a32db768c4408e")]  // CN=Certum Trusted Network CA, OU=Certum Certification Authority, O=Unizeto Technologies S.A., C=PL
        [InlineData("7353b6d6c2d6da4247773f3f07d075decb5134212bead0928ef1f46115260941")]  // CN=DigiCert CS RSA4096 Root G5, O="DigiCert, Inc.", C=US
        [InlineData("7431e5f4c3c1ce4690774f0b61e05440883ba9a01ed00ba6abd7806ed3b118cf")]  // CN=DigiCert High Assurance EV Root CA, OU=www.digicert.com, O=DigiCert Inc, C=US
        [InlineData("7b9d553e1c92cb6e8803e137f4f287d4363757f5d44b37d52f9fca22fb97df86")]  // CN=GlobalSign Code Signing Root R45, O=GlobalSign nv-sa, C=BE
        [InlineData("85666a562ee0be5ce925c1d8890a6f76a87ec16d4d7d5f29ea7419cf20123b69")]  // CN=SSL.com Root Certification Authority RSA, O=SSL Corporation, L=Houston, S=Texas, C=US
        [InlineData("85a0dd7dd720adb7ff05f83d542b209dc7ff4528f7d677b18389fea5e5c49e86")]  // CN=QuoVadis Root CA 2, O=QuoVadis Limited, C=BM
        [InlineData("8d722f81a9c113c0791df136a2966db26c950a971db46b4199f4ea54b78bfb9f")]  // CN=thawte Primary Root CA, OU="(c) 2006 thawte, Inc. - For authorized use only", OU=Certification Services Division, O="thawte, Inc.", C=US
        [InlineData("9acfab7e43c8d880d06b262a94deeee4b4659989c3d0caf19baf6405e41ab7df")]  // CN=VeriSign Class 3 Public Primary Certification Authority - G5, OU="(c) 2006 VeriSign, Inc. - For authorized use only", OU=VeriSign Trust Network, O="VeriSign, Inc.", C=US
        [InlineData("b676f2eddae8775cd36cb0f63cd1d4603961f49e6265ba013a2f0307b6d0b804")]  // CN=Certum Trusted Network CA 2, OU=Certum Certification Authority, O=Unizeto Technologies S.A., C=PL
        [InlineData("bfff8fd04433487d6a8aa60c1a29767a9fc2bbb05e420f713a13b992891d3893")]  // CN=GDCA TrustAUTH R5 ROOT, O="GUANG DONG CERTIFICATE AUTHORITY CO.,LTD.", C=CN
        [InlineData("c3846bf24b9e93ca64274c0ec67c1ecc5e024ffcacd2d74019350e81fe546ae4")]  // OU=Go Daddy Class 2 Certification Authority, O="The Go Daddy Group, Inc.", C=US
        [InlineData("c766a9bef2d4071c863a31aa4920e813b2d198608cb7b7cfe21143b836df09ea")]  // CN=StartCom Certification Authority, OU=Secure Digital Certificate Signing, O=StartCom Ltd., C=IL
        [InlineData("cbb522d7b7f127ad6a0113865bdf1cd4102e7d0759af635a7cf4720dc963c53b")]  // CN=GlobalSign, O=GlobalSign, OU=GlobalSign Root CA - R3
        [InlineData("d7a7a0fb5d7e2731d771e9484ebcdef71d5f0c3e0a2948782bc83ee0ea699ef4")]  // CN=AAA Certificate Services, O=Comodo CA Limited, L=Salford, S=Greater Manchester, C=GB
        [InlineData("e793c9b02fd8aa13e21c31228accb08119643b749c898964b1746d46c3d4cbd2")]  // CN=USERTrust RSA Certification Authority, O=The USERTRUST Network, L=Jersey City, S=New Jersey, C=US
        [InlineData("ebd41040e4bb3ec742c9e381d31ef2a41a48b6685c96e7cef3c1df6cd4331c99")]  // CN=GlobalSign Root CA, OU=Root CA, O=GlobalSign nv-sa, C=BE
        public void File_contains_certificates_used_in_NuGet_org_package_signatures(string expectedFingerprint)
        {
            VerifyCertificateExists(s_fingerprints, expectedFingerprint);
        }
    }
}
