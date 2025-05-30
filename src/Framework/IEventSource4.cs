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
    public interface IEventSource4 : IEventSource3
    {
        /// <summary>
        /// Determines whether properties and items should be logged on <see cref="ProjectEvaluationFinishedEventArgs"/>
        /// instead of <see cref="ProjectStartedEventArgs"/>.
        /// </summary>
        void IncludeEvaluationPropertiesAndItems();
    }
}
