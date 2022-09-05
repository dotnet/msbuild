// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a localizable mechanism for logging an error from the SDK targets.
    /// </summary>
    public class
#if EXTENSIONS
        // This task source is shared with multiple task Dlls.  Since both tasks
        // may be loaded into the same project and each task accesses only resources
        // in its own assembly they must have a unique name so-as not to clash.
        NETBuildExtensionsError
#else
        NETSdkError
#endif
     : MessageBase
    {
        protected override void LogMessage(string message)
        {
            Log.LogError(message);
        }
    }
}
