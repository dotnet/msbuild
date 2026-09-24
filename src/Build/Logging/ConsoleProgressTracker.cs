// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Turns the stream of task progress events into a single line of text for each operation.
    /// </summary>
    /// <remarks>
    /// A console cannot animate a progress row the way Terminal Logger does, so intermediate updates
    /// have nothing to draw to. This tracker keeps only the data that the final event does not carry -
    /// the title and the unit, which arrive on the started event - and renders one message when the
    /// operation ends.
    /// </remarks>
    internal sealed class ConsoleProgressTracker
    {
        /// <summary>
        /// The largest number of operations tracked at one time. A task that starts operations without
        /// ending them cannot grow this dictionary without bound; further operations render without a
        /// title rather than being remembered.
        /// </summary>
        private const int MaxTrackedOperations = 1024;

        private readonly Dictionary<long, (string? Title, TaskProgressUnit Unit)> _operations = [];

        /// <summary>
        /// Records the data needed to describe <paramref name="started"/> when it ends.
        /// </summary>
        internal void Start(TaskProgressStartedEventArgs started)
        {
            if (_operations.ContainsKey(started.OperationId) || _operations.Count < MaxTrackedOperations)
            {
                _operations[started.OperationId] = (started.Title, started.Unit);
            }
        }

        /// <summary>
        /// Stops tracking <paramref name="finished"/> and describes how it ended.
        /// </summary>
        internal string Finish(TaskProgressFinishedEventArgs finished)
        {
            if (!_operations.TryGetValue(finished.OperationId, out (string? Title, TaskProgressUnit Unit) operation))
            {
                // The started event never arrived, so the title and unit are unknown. This happens when
                // the operation limit was reached, and it must not lose the outcome.
                operation = (null, TaskProgressUnit.Unspecified);
            }
            else
            {
                _operations.Remove(finished.OperationId);
            }

            string title = string.IsNullOrWhiteSpace(operation.Title) ? finished.SenderName ?? string.Empty : operation.Title!;
            string amount = TaskProgressFormatter.FormatAmount(finished.Completed, finished.Total, operation.Unit);

            string resource = finished.Outcome switch
            {
                TaskProgressOutcome.Completed => "TaskProgressCompleted",
                TaskProgressOutcome.Canceled => "TaskProgressCanceled",
                TaskProgressOutcome.Failed => "TaskProgressFailed",
                _ => "TaskProgressAbandoned",
            };

            return ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(resource, title, amount);
        }
    }
}
