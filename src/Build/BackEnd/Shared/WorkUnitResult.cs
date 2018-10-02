// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The result of executing the task or target.
    /// </summary>
    internal enum WorkUnitResultCode
    {
        /// <summary>
        /// The work unit was skipped.
        /// </summary>
        Skipped,

        /// <summary>
        /// The work unit succeeded.
        /// </summary>
        Success,

        /// <summary>
        /// The work unit failed.
        /// </summary>
        Failed,

        /// <summary>
        /// The work unit was cancelled.
        /// </summary>
        Canceled,
    }

    /// <summary>
    /// Indicates whether further work should be done.
    /// </summary>
    internal enum WorkUnitActionCode
    {
        /// <summary>
        /// Work should proceed with the next work unit.
        /// </summary>
        Continue,

        /// <summary>
        /// No further work units should be executed.
        /// </summary>
        Stop,
    }

    /// <summary>
    /// A result of executing a target or task.
    /// </summary>
    internal class WorkUnitResult : INodePacketTranslatable
    {
        /// <summary>
        /// The result.
        /// </summary>
        private WorkUnitResultCode _resultCode;

        /// <summary>
        /// The next action to take.
        /// </summary>
        private WorkUnitActionCode _actionCode;

        /// <summary>
        /// The exception from the failure, if any.
        /// </summary>
        private Exception _exception;

        /// <summary>
        /// Creates a new work result ready for aggregation during batches.
        /// </summary>
        internal WorkUnitResult()
        {
            _resultCode = WorkUnitResultCode.Skipped;
            _actionCode = WorkUnitActionCode.Continue;
            _exception = null;
        }

        /// <summary>
        /// Creates a work result with the specified result codes.
        /// </summary>
        internal WorkUnitResult(WorkUnitResultCode resultCode, WorkUnitActionCode actionCode, Exception e)
        {
            _resultCode = resultCode;
            _actionCode = actionCode;
            _exception = e;
        }

        /// <summary>
        /// Translator constructor
        /// </summary>
        private WorkUnitResult(INodePacketTranslator translator)
        {
            ((INodePacketTranslatable)this).Translate(translator);
        }

        /// <summary>
        /// Get the result code.
        /// </summary>
        internal WorkUnitResultCode ResultCode => _resultCode;

        /// <summary>
        /// Get the action code.
        /// </summary>
        internal WorkUnitActionCode ActionCode
        {
            get => _actionCode;
            set => _actionCode = value;
        }

        /// <summary>
        /// Get the exception
        /// </summary>
        internal Exception Exception => _exception;

        #region INodePacketTranslatable Members

        /// <summary>
        /// Translator.
        /// </summary>
        public void Translate(INodePacketTranslator translator)
        {
            translator.TranslateEnum(ref _resultCode, (int)_resultCode);
            translator.TranslateEnum(ref _actionCode, (int)_actionCode);
            translator.TranslateException(ref _exception);
        }

        #endregion

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static WorkUnitResult FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new WorkUnitResult(translator);
        }

        /// <summary>
        /// Aggregates the specified result with this result and returns the aggregation.
        /// </summary>
        /// <remarks>
        /// The rules are:
        /// 1. Errors take precedence over success.
        /// 2. Success takes precedence over skipped.
        /// 3. Stop takes precedence over continue.
        /// 4. The first exception in the result wins.
        /// </remarks>
        internal WorkUnitResult AggregateResult(WorkUnitResult result)
        {
            WorkUnitResultCode aggregateResult = _resultCode;
            WorkUnitActionCode aggregateAction = _actionCode;
            Exception aggregateException = _exception;

            if (result._resultCode == WorkUnitResultCode.Canceled || result.ResultCode == WorkUnitResultCode.Failed)
            {
                // Failed and canceled take priority
                aggregateResult = result._resultCode;
            }
            else if (result._resultCode == WorkUnitResultCode.Success && aggregateResult == WorkUnitResultCode.Skipped)
            {
                // Success only counts if we were previously in the skipped category.
                aggregateResult = result._resultCode;
            }

            if (result._actionCode == WorkUnitActionCode.Stop)
            {
                aggregateAction = result.ActionCode;
            }

            if (aggregateException == null)
            {
                aggregateException = result._exception;
            }

            return new WorkUnitResult(aggregateResult, aggregateAction, aggregateException);
        }
    }
}
