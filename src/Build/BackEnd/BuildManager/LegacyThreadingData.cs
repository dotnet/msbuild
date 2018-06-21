// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// This class represents the data which is used for legacy threading semantics for the build
    /// </summary>
    internal class LegacyThreadingData
    {
        #region Fields
        /// <summary>
        /// Store the pair of start/end events used by a particular submission to track their ownership 
        /// of the legacy thread. 
        /// Item1: Start event, tracks when the submission has permission to start building. 
        /// Item2: End event, signalled when that submission is no longer using the legacy thread. 
        /// </summary>
        private readonly IDictionary<int, Tuple<AutoResetEvent, ManualResetEvent>> _legacyThreadingEventsById = new Dictionary<int, Tuple<AutoResetEvent, ManualResetEvent>>();

        /// <summary>
        /// The current submission id building on the main thread, if any.
        /// </summary>
        private int _mainThreadSubmissionId = -1;

        /// <summary>
        /// The instance to be used when the new request builder is started on the main thread.
        /// </summary>
        private RequestBuilder _instanceForMainThread;

        /// <summary>
        /// Lock object for startNewRequestBuilderMainThreadEventsById, since it's possible for multiple submissions to be 
        /// submitted at the same time. 
        /// </summary>
        private readonly Object _legacyThreadingEventsLock = new Object();
        #endregion

        #region Properties

        /// <summary>
        /// The instance to be used when the new request builder is started on the main thread.
        /// </summary>
        internal RequestBuilder InstanceForMainThread
        {
            get => _instanceForMainThread;

            set
            {
                ErrorUtilities.VerifyThrow(_instanceForMainThread == null || (_instanceForMainThread != null && value == null) || (_instanceForMainThread == value), "Should not assign to instanceForMainThread twice without cleaning it");
                _instanceForMainThread = value;
            }
        }

        /// <summary>
        /// The current submission id building on the main thread, if any.
        /// </summary>
        internal int MainThreadSubmissionId
        {
            get => _mainThreadSubmissionId;

            set
            {
                if (value == -1)
                {
                    _instanceForMainThread = null;
                }

                _mainThreadSubmissionId = value;
            }
        }
        #endregion

        /// <summary>
        /// Given a submission ID, assign it "start" and "finish" events to track its use of 
        /// the legacy thread.
        /// </summary>
        internal void RegisterSubmissionForLegacyThread(int submissionId)
        {
            lock (_legacyThreadingEventsLock)
            {
                ErrorUtilities.VerifyThrow(!_legacyThreadingEventsById.ContainsKey(submissionId), "Submission {0} should not already be registered with LegacyThreadingData", submissionId);

                _legacyThreadingEventsById[submissionId] = new Tuple<AutoResetEvent, ManualResetEvent>
                            (
                                new AutoResetEvent(false),
                                new ManualResetEvent(false)
                            );
            }
        }

        /// <summary>
        /// This submission is completely done with the legacy thread, so unregister it 
        /// from the dictionary so that we don't leave random events lying around. 
        /// </summary>
        internal void UnregisterSubmissionForLegacyThread(int submissionId)
        {
            lock (_legacyThreadingEventsLock)
            {
                ErrorUtilities.VerifyThrow(_legacyThreadingEventsById.ContainsKey(submissionId), "Submission {0} should have been previously registered with LegacyThreadingData", submissionId);

                if (_legacyThreadingEventsById.ContainsKey(submissionId))
                {
                    _legacyThreadingEventsById.Remove(submissionId);
                }
            }
        }

        /// <summary>
        /// Given a submission ID, return the event being used to track when that submission is ready 
        /// to be executed on the legacy thread. 
        /// </summary>
        internal WaitHandle GetStartRequestBuilderMainThreadEventForSubmission(int submissionId)
        {
            Tuple<AutoResetEvent, ManualResetEvent> legacyThreadingEvents;

            lock (_legacyThreadingEventsLock)
            {
                _legacyThreadingEventsById.TryGetValue(submissionId, out legacyThreadingEvents);
            }

            ErrorUtilities.VerifyThrow(legacyThreadingEvents != null, "We're trying to wait on the legacy thread for submission {0}, but that submission has not been registered.", submissionId);

            return legacyThreadingEvents.Item1;
        }

        /// <summary>
        /// Given a submission ID, return the event being used to track when that submission is ready 
        /// to be executed on the legacy thread. 
        /// </summary>
        internal Task GetLegacyThreadInactiveTask(int submissionId)
        {
            Tuple<AutoResetEvent, ManualResetEvent> legacyThreadingEvents;

            lock (_legacyThreadingEventsLock)
            {
                _legacyThreadingEventsById.TryGetValue(submissionId, out legacyThreadingEvents);
            }

            ErrorUtilities.VerifyThrow(legacyThreadingEvents != null, "We're trying to track when the legacy thread for submission {0} goes inactive, but that submission has not been registered.", submissionId);

            return legacyThreadingEvents.Item2.ToTask();
        }

        /// <summary>
        /// Signal that the legacy thread is starting work.
        /// </summary>
        internal void SignalLegacyThreadStart(RequestBuilder instance)
        {
            ErrorUtilities.VerifyThrow
                (
                    instance?.RequestEntry?.Request != null,
                    "Cannot signal legacy thread start for a RequestBuilder without a request"
                );

            int submissionId = instance.RequestEntry.Request.SubmissionId;
            InstanceForMainThread = instance;

            Tuple<AutoResetEvent, ManualResetEvent> legacyThreadingEvents;
            lock (_legacyThreadingEventsLock)
            {
                _legacyThreadingEventsById.TryGetValue(submissionId, out legacyThreadingEvents);
            }

            ErrorUtilities.VerifyThrow(legacyThreadingEvents != null, "We're trying to signal that the legacy thread is ready for submission {0} to execute, but that submission has not been registered", submissionId);

            // signal that this submission is currently controlling the legacy thread
            legacyThreadingEvents.Item1.Set();

            // signal that the legacy thread is not currently idle
            legacyThreadingEvents.Item2.Reset();
        }

        /// <summary>
        /// Signal that the legacy thread has finished its work.
        /// </summary>
        internal void SignalLegacyThreadEnd(int submissionId)
        {
            MainThreadSubmissionId = -1;

            Tuple<AutoResetEvent, ManualResetEvent> legacyThreadingEvents;
            lock (_legacyThreadingEventsLock)
            {
                _legacyThreadingEventsById.TryGetValue(submissionId, out legacyThreadingEvents);
            }

            ErrorUtilities.VerifyThrow(legacyThreadingEvents != null, "We're trying to signal that submission {0} is done with the legacy thread, but that submission has not been registered", submissionId);

            // The legacy thread is now idle
            legacyThreadingEvents.Item2.Set();
        }
    }
}
