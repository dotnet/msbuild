// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Transactions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public sealed class TransactionalAction
    {
        static TransactionalAction()
        {
            DisableTransactionTimeoutUpperLimit();
        }

        private class EnlistmentNotification : IEnlistmentNotification
        {
            private Action _commit;
            private Action _rollback;

            public EnlistmentNotification(Action commit, Action rollback)
            {
                _commit = commit;
                _rollback = rollback;
            }

            public void Commit(Enlistment enlistment)
            {
                if (_commit != null)
                {
                    _commit();
                    _commit = null;
                }

                enlistment.Done();
            }

            public void InDoubt(Enlistment enlistment)
            {
                Rollback(enlistment);
            }

            public void Prepare(PreparingEnlistment enlistment)
            {
                enlistment.Prepared();
            }

            public void Rollback(Enlistment enlistment)
            {
                if (_rollback != null)
                {
                    _rollback();
                    _rollback = null;
                }

                enlistment.Done();
            }
        }

        public static T Run<T>(
            Func<T> action,
            Action commit = null,
            Action rollback = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // This automatically inherits any ambient transaction
            // If a transaction is inherited, completing this scope will be a no-op
            T result = default(T);
            try
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    Transaction.Current.EnlistVolatile(
                        new EnlistmentNotification(commit, rollback),
                        EnlistmentOptions.None);

                    result = action();

                    scope.Complete();
                }

                return result;
            }
            catch (TransactionAbortedException ex)
            {
                Reporter.Verbose.WriteLine(string.Format("TransactionAbortedException Message: {0}", ex.Message));
                Reporter.Verbose.WriteLine(
                    $"Inner Exception Message: {ex?.InnerException?.Message + "---" + ex?.InnerException}");
                throw;
            }
        }

        private static void SetTransactionManagerField(string fieldName, object value)
        {
            typeof(TransactionManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
        }

        // https://github.com/dotnet/sdk/issues/21101
        // we should use the proper API once it is available
        public static void DisableTransactionTimeoutUpperLimit()
        {
            SetTransactionManagerField("s_cachedMaxTimeout", true);
            SetTransactionManagerField("s_maximumTimeout", TimeSpan.Zero);
        }

        public static void Run(
            Action action,
            Action commit = null,
            Action rollback = null)
        {
            Run<object>(
                action: () =>
                {
                    action();
                    return null;
                },
                commit: commit,
                rollback: rollback);
        }
    }
}
