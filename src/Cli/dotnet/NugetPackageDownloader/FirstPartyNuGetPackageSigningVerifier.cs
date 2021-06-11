// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class FirstPartyNuGetPackageSigningVerifier : IFirstPartyNuGetPackageSigningVerifier
    {
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
                var x509Certificate = X509Certificate2.CreateFromCertFile(targetFilePath.Value);
                return x509Certificate.Subject.Contains("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase);
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
