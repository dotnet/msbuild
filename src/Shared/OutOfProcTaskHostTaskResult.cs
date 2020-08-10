// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// A result of executing a target or task.
    /// </summary>
    internal class OutOfProcTaskHostTaskResult
    {
        /// <summary>
        /// Constructor 
        /// </summary>
        internal OutOfProcTaskHostTaskResult(TaskCompleteType result)
            : this(result, null /* no final parameters */, null /* no exception */, null /* no exception message */, null /* and no args to go with it */)
        {
            // do nothing else
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal OutOfProcTaskHostTaskResult(TaskCompleteType result, IDictionary<string, Object> finalParams)
            : this(result, finalParams, null /* no exception */, null /* no exception message */, null /* and no args to go with it */)
        {
            // do nothing else
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal OutOfProcTaskHostTaskResult(TaskCompleteType result, Exception taskException)
            : this(result, taskException, null /* no exception message */, null /* and no args to go with it */)
        {
            // do nothing else
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal OutOfProcTaskHostTaskResult(TaskCompleteType result, Exception taskException, string exceptionMessage, string[] exceptionMessageArgs)
            : this(result, null /* no final parameters */, taskException, exceptionMessage, exceptionMessageArgs)
        {
            // do nothing else
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal OutOfProcTaskHostTaskResult(TaskCompleteType result, IDictionary<string, Object> finalParams, Exception taskException, string exceptionMessage, string[] exceptionMessageArgs)
        {
            // If we're returning a crashing result, we should always also be returning the exception that caused the crash, although 
            // we may not always be returning an accompanying message. 
            if (result == TaskCompleteType.CrashedDuringInitialization ||
                result == TaskCompleteType.CrashedDuringExecution ||
                result == TaskCompleteType.CrashedAfterExecution)
            {
                ErrorUtilities.VerifyThrowInternalNull(taskException, nameof(taskException));
            }

            if (exceptionMessage != null)
            {
                ErrorUtilities.VerifyThrow
                    (
                        result == TaskCompleteType.CrashedDuringInitialization ||
                        result == TaskCompleteType.CrashedDuringExecution ||
                        result == TaskCompleteType.CrashedAfterExecution,
                        "If we have an exception message, the result type should be 'crashed' of some variety."
                    );
            }

            if (exceptionMessageArgs?.Length > 0)
            {
                ErrorUtilities.VerifyThrow(exceptionMessage != null, "If we have message args, we need a message.");
            }

            Result = result;
            FinalParameterValues = finalParams;
            TaskException = taskException;
            ExceptionMessage = exceptionMessage;
            ExceptionMessageArgs = exceptionMessageArgs;
        }

        /// <summary>
        /// The overall result of the task execution. 
        /// </summary>
        public TaskCompleteType Result
        {
            get;
            private set;
        }

        /// <summary>
        /// Dictionary of the final values of the task parameters
        /// </summary>
        public IDictionary<string, Object> FinalParameterValues
        {
            get;
            private set;
        }

        /// <summary>
        /// The exception thrown by the task during initialization or execution, 
        /// if any. 
        /// </summary>
        public Exception TaskException
        {
            get;
            private set;
        }

        /// <summary>
        /// The name of the resource representing the message to be logged along with the 
        /// above exception. 
        /// </summary>
        public string ExceptionMessage
        {
            get;
            private set;
        }

        /// <summary>
        /// The arguments to be used when formatting ExceptionMessage
        /// </summary>
        public string[] ExceptionMessageArgs
        {
            get;
            private set;
        }
    }
}
