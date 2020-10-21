// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Exception thrown when a circular dependency is detected in the Scheduler.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Justification = "No point in adding the serialization constructors since BuildRequest is not serializable")]
    [SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable", Justification = "No point in marking as ISerializable since BuildRequest is not. ")]
    internal class SchedulerCircularDependencyException : Exception
    {
        /// <summary>
        /// The ancestors which led to this circular dependency.
        /// </summary>
        private IList<SchedulableRequest> _ancestors;

        /// <summary>
        /// The request which caused the circular dependency.
        /// </summary>
        private BuildRequest _request;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SchedulerCircularDependencyException(BuildRequest request, IList<SchedulableRequest> ancestors)
        {
            _request = request;
            _ancestors = ancestors;
        }

        /// <summary>
        /// Gets an enumeration of the ancestors which led to this circular dependency.
        /// </summary>
        public IEnumerable<SchedulableRequest> Ancestors
        {
            get { return _ancestors; }
        }

        /// <summary>
        /// Gets the request which caused the circular dependency.
        /// </summary>
        public BuildRequest Request
        {
            get { return _request; }
        }
    }
}
