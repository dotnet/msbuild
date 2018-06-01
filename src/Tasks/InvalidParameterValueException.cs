// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A parameter passed to the task was invalid.
    /// Currently used by the RAR task.
    /// ArgumentException was not used because it does not have a property for ActualValue.
    /// ArgumentOutOfRangeException does, but it appends its own message to yours.
    /// </summary>
    [Serializable]
    internal sealed class InvalidParameterValueException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        internal InvalidParameterValueException(string paramName, string actualValue, string message)
            : this(paramName, actualValue, message, null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal InvalidParameterValueException(string paramName, string actualValue, string message, Exception innerException)
            : base(message, innerException)
        {
            ParamName = paramName;
            ActualValue = actualValue;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        private InvalidParameterValueException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// The name of the parameter.
        /// </summary>
        public string ParamName { get; set; }

        /// <summary>
        /// The value supplied, that was bad.
        /// </summary>
        public string ActualValue { get; set; }
    }
}
