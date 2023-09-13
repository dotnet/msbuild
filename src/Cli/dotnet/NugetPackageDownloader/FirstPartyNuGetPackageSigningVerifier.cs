// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using HashAlgorithmName = System.Security.Cryptography.HashAlgorithmName;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class FirstPartyNuGetPackageSigningVerifier : IFirstPartyNuGetPackageSigningVerifier
    {
        internal readonly HashSet<string> _firstPartyCertificateThumbprints =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE",
                "AA12DA22A49BCE7D5C1AE64CC1F3D892F150DA76140F210ABD2CBFFCA2C18A27",
                "566A31882BE208BE4422F7CFD66ED09F5D4524A5994F50CCC8B05EC0528C1353"
            };

        private readonly HashSet<string> _upperFirstPartyCertificateThumbprints =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "51044706BD237B91B89B781337E6D62656C69F0FCFFBE8E43741367948127862",
                "46011EDE1C147EB2BC731A539B7C047B7EE93E48B9D3C3BA710CE132BBDFAC6B"
            };

        private const string FirstPartyCertificateSubject =
            "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

        public FirstPartyNuGetPackageSigningVerifier()
        {
        }

        public bool Verify(FilePath nupkgToVerify, out string commandOutput)
        {
            return NuGetVerify(nupkgToVerify, out commandOutput) && IsFirstParty(nupkgToVerify);
        }

        internal bool IsFirstParty(FilePath nupkgToVerify)
        {
            try
            {
                using (var packageReader = new PackageArchiveReader(nupkgToVerify.Value))
                {
                    PrimarySignature primarySignature = packageReader.GetPrimarySignatureAsync(CancellationToken.None).GetAwaiter().GetResult();
                    using (IX509CertificateChain certificateChain = SignatureUtility.GetCertificateChain(primarySignature))
                    {
                        if (certificateChain.Count < 2)
                        {
                            return false;
                        }

                        X509Certificate2 firstCert = certificateChain.First();
                        if (_firstPartyCertificateThumbprints.Contains(firstCert.GetCertHashString(HashAlgorithmName.SHA256)))
                        {
                            return true;
                        }

                        if (firstCert.Subject.Equals(FirstPartyCertificateSubject, StringComparison.OrdinalIgnoreCase)
                            && _upperFirstPartyCertificateThumbprints.Contains(
                                certificateChain[1].GetCertHashString(HashAlgorithmName.SHA256)))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        private static bool NuGetVerify(FilePath nupkgToVerify, out string commandOutput)
        {
            var args = new[] {"verify", "--all", nupkgToVerify.Value};
            var command = new DotNetCommandFactory(alwaysRunOutOfProc: true)
                .Create("nuget", args);

            var commandResult = command.CaptureStdOut().Execute();
            commandOutput = commandResult.StdOut + Environment.NewLine + commandResult.StdErr;
            return commandResult.ExitCode == 0;
        }
    }
}
