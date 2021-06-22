// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class FirstPartyNuGetPackageSigningVerifier : IFirstPartyNuGetPackageSigningVerifier
    {
        private const string FirstPartyCertificateThumbprint = "F404000FB11E61F446529981C7059A76C061631E";
        private const string UpperFirstPartyCertificateThumbprint = "92C1588E85AF2201CE7915E8538B492F605B80C6";
        private const string FirstPartyCertificateSubject =
            "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

        private DirectoryPath _tempDirectory;
        private ILogger _logger;

        public FirstPartyNuGetPackageSigningVerifier(DirectoryPath? tempDirectory = null, ILogger logger = null)
        {
            _tempDirectory = tempDirectory ?? new DirectoryPath(Path.GetTempPath());
            _logger = new NullLogger();
        }

        public bool Verify(FilePath nupkgToVerify, out string commandOutput)
        {
            return NuGetVerify(nupkgToVerify, out commandOutput) && IsFirstParty(nupkgToVerify);
        }

        private bool IsFirstParty(FilePath nupkgToVerify)
        {
            var packageReader = new PackageArchiveReader(nupkgToVerify.Value);
            Directory.CreateDirectory(_tempDirectory.Value);
            FilePath targetFilePath = _tempDirectory.WithFile(Path.GetRandomFileName());
            try
            {
                packageReader.ExtractFile(".signature.p7s", targetFilePath.Value, _logger);
                using var fs = new FileStream(targetFilePath.Value, FileMode.Open);
                PrimarySignature primarySignature = PrimarySignature.Load(fs);
                IX509CertificateChain certificateChain = SignatureUtility.GetCertificateChain(primarySignature);

                if (certificateChain.Count < 2)
                {
                    return false;
                }

                X509Certificate2 firstCert = certificateChain.First();
                if (firstCert.Thumbprint.Equals(FirstPartyCertificateThumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (firstCert.Subject.Equals(FirstPartyCertificateSubject, StringComparison.OrdinalIgnoreCase)
                    && certificateChain[1].Thumbprint.Equals(UpperFirstPartyCertificateThumbprint,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
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
        
        public bool IsExecutableIsFirstPartySignedWithoutValidation(FilePath executable)
        {
            try
            {
                X509Certificate signedFile = X509Certificate2.CreateFromSignedFile(executable.Value);
                return signedFile.Subject.Contains("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
