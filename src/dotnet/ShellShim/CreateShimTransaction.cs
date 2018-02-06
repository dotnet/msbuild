// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal class CreateShimTransaction : IEnlistmentNotification
    {
        private readonly Action<List<FilePath>> _createShim;
        private readonly Action<List<FilePath>> _rollback;
        private List<FilePath> _locationOfShimDuringTransaction = new List<FilePath>();

        public CreateShimTransaction(
            Action<List<FilePath>> createShim,
            Action<List<FilePath>> rollback)
        {
            _createShim = createShim ?? throw new ArgumentNullException(nameof(createShim));
            _rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        }

        public void CreateShim()
        {
            _createShim(_locationOfShimDuringTransaction);
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
            _rollback(_locationOfShimDuringTransaction);

            enlistment.Done();
        }
    }
}
