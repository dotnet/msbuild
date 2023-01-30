// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
