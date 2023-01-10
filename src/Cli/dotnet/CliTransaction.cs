// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli
{
    internal interface ITransactionContext
    {
        public void AddRollbackAction(Action action);

        //  Add an action which will be run at the end of the transaction, whether it succeeds or not (after any rollback actions, if applicable)
        public void AddCleanupAction(Action action);
    }

    internal class CliTransaction
    {
        //  Delegate called when rollback starts, mainly in order to print a log message
        public Action RollbackStarted { get; set; }

        //  Delegate called when rollback fails.  If set, exception will be passed to the delegate, but not rethrown
        public Action<Exception> RollbackFailed { get; set; }

        public static void RunNew(Action<ITransactionContext> action)
        {
            new CliTransaction().Run(action);
        }

        public void Run(Action<ITransactionContext> action, Action rollback)
        {
            Run(context =>
            {
                context.AddRollbackAction(rollback);

                action(context);
            });
        }

        public void Run(Action<ITransactionContext> action)
        {
            TransactionContext transactionContext = new();
            try
            {
                action(transactionContext);
            }
            catch (Exception)
            {
                //  Roll back transaction
                RollbackStarted?.Invoke();

                transactionContext.RollbackActions.Reverse();
                foreach (var rollbackAction in transactionContext.RollbackActions)
                {
                    try
                    {
                        rollbackAction();
                    }
                    catch (Exception ex)
                    {
                        if (RollbackFailed != null)
                        {
                            RollbackFailed(ex);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                throw;
            }
            finally
            {
                foreach (var cleanupAction in transactionContext.CleanupActions)
                {
                    cleanupAction();
                }
            }
        }

        class TransactionContext : ITransactionContext
        {
            public List<Action> RollbackActions { get; set; } = new List<Action>();

            //  Actions which will be run either when the transaction completes successfully, or after rollback actions have been run
            public List<Action> CleanupActions { get; set; } = new List<Action>();

            public void AddRollbackAction(Action action)
            {
                RollbackActions.Add(action);
            }

            public void AddCleanupAction(Action action)
            {
                CleanupActions.Add(action);
            }
        }
    }

    static class CliTransactionExtensions
    {
        public static void Run(this ITransactionContext context, Action action, Action rollback = null, Action cleanup = null)
        {
            if (rollback != null)
            {
                context.AddRollbackAction(rollback);
            }
            if (cleanup != null)
            {
                context.AddCleanupAction(cleanup);
            }
            action();
        }
    }
}
