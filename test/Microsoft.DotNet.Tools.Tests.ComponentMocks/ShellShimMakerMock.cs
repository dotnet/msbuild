// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ShellShimMakerMock : IShellShimMaker
    {
        private static IFileSystem _fileSystem;
        private readonly string _pathToPlaceShim;

        public ShellShimMakerMock(string pathToPlaceShim, IFileSystem fileSystem = null)
        {
            _pathToPlaceShim =
                pathToPlaceShim ??
                throw new ArgumentNullException(nameof(pathToPlaceShim));

            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public void EnsureCommandNameUniqueness(string shellCommandName)
        {
            if (_fileSystem.File.Exists(GetShimPath(shellCommandName).Value))
            {
                throw new GracefulException(
                    string.Format(CommonLocalizableStrings.FailInstallToolSameName,
                        shellCommandName));
            }
        }

        public void CreateShim(FilePath packageExecutable, string shellCommandName)
        {
            var createShimTransaction = new CreateShimTransaction(
                createShim: locationOfShimDuringTransaction =>
                {
                    EnsureCommandNameUniqueness(shellCommandName);
                    PlaceShim(packageExecutable, shellCommandName, locationOfShimDuringTransaction);
                },
                rollback: locationOfShimDuringTransaction =>
                {
                    foreach (FilePath f in locationOfShimDuringTransaction)
                    {
                        if (File.Exists(f.Value))
                        {
                            File.Delete(f.Value);
                        }
                    }
                });

            using (var transactionScope = new TransactionScope())
            {
                Transaction.Current.EnlistVolatile(createShimTransaction, EnlistmentOptions.None);
                createShimTransaction.CreateShim();

                transactionScope.Complete();
            }
        }

        public void Remove(string shellCommandName)
        {
            File.Delete(GetShimPath(shellCommandName).Value);
        }

        private void PlaceShim(FilePath packageExecutable, string shellCommandName, List<FilePath> locationOfShimDuringTransaction)
        {
            var fakeshim = new FakeShim
            {
                Runner = "dotnet",
                ExecutablePath = packageExecutable.Value
            };
            var script = JsonConvert.SerializeObject(fakeshim);

            FilePath scriptPath = GetShimPath(shellCommandName);
            _fileSystem.File.WriteAllText(scriptPath.Value, script);
            locationOfShimDuringTransaction.Add(scriptPath);
        }

        private FilePath GetShimPath(string shellCommandName)
        {
            var scriptPath = Path.Combine(_pathToPlaceShim, shellCommandName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath += ".exe";
            }

            return new FilePath(scriptPath);
        }

        public class FakeShim
        {
            public string Runner { get; set; }
            public string ExecutablePath { get; set; }
        }
    }
}
