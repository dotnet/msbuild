// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface defines the events raised by the build engine.
    /// Loggers use this interface to subscribe to the events they
    /// are interested in receiving.
    /// </summary>
    public interface IEventSource3 : IEventSource2
    {
        /// <summary>
        /// Should evaluation events include generated metaprojects?
        /// </summary>
        void IncludeEvaluationMetaprojects();

        /// <summary>
        /// Should evaluation events include profiling information?
        /// </summary>
        void IncludeEvaluationProfiles();

        /// <summary>
        /// Should task events include task inputs?
        /// </summary>
        void IncludeTaskInputs();
    }
}
