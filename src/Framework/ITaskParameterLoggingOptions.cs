// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A Task implementation can implement this interface to customize logging for its parameters
    /// when LogTaskInputs is set.
    /// </summary>
    /// <remarks>
    /// This can be internal as it for now only applies to our own tasks.
    /// </remarks>
    internal interface ITaskParameterLoggingOptions
    {
        ParameterLoggingOptions GetParameterLoggingOptions(string parameterName);
    }

    /// <summary>
    /// Determines whether to log a parameter, and if yes, and it's an item list, whether to log item metadata.
    /// This is effectively a 3-state, encoded by 2 bits. This is not an enum for potential future extensibility.
    /// </summary>
    internal struct ParameterLoggingOptions
    {
        public static ParameterLoggingOptions DoNotLog = new ParameterLoggingOptions(disableLogging: true, disableLoggingItemMetadata: false);
        public static ParameterLoggingOptions DoNotLogItemMetadata = new ParameterLoggingOptions(disableLogging: false, disableLoggingItemMetadata: true);

        public ParameterLoggingOptions(bool disableLogging, bool disableLoggingItemMetadata)
        {
            DisableLogging = disableLogging;
            DisableLoggingItemMetadata = disableLoggingItemMetadata;
        }

        public bool DisableLogging;
        public bool DisableLoggingItemMetadata;
    }
}