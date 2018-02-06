// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolPackageObtainTransaction : IEnlistmentNotification
    {
        private readonly Func<List<DirectoryPath>, ToolConfigurationAndExecutablePath> _obtainAndReturnExecutablePath;
        private readonly Action<List<DirectoryPath>> _rollback;
        private List<DirectoryPath> _locationOfPackageDuringTransaction = new List<DirectoryPath>();

        public ToolPackageObtainTransaction(
            Func<List<DirectoryPath>, ToolConfigurationAndExecutablePath> obtainAndReturnExecutablePath,
            Action<List<DirectoryPath>> rollback)
        {
            _obtainAndReturnExecutablePath = obtainAndReturnExecutablePath ?? throw new ArgumentNullException(nameof(obtainAndReturnExecutablePath));
            _rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        }

        public ToolConfigurationAndExecutablePath ObtainAndReturnExecutablePath()
        {
            return _obtainAndReturnExecutablePath(_locationOfPackageDuringTransaction);
        }

        public void Commit(Enlistment enlistment)
        {
            enlistment.Done();
        }

        public void InDoubt(Enlistment enlistment)
        {
            Rollback(enlistment);
        }

        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            preparingEnlistment.Done();
        }

        public void Rollback(Enlistment enlistment)
        {
            _rollback(_locationOfPackageDuringTransaction);

            enlistment.Done();
        }
    }
}
