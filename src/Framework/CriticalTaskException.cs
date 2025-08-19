// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework.BuildException;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// A wrapper exception for exceptions thrown by MsBuild Tasks (in TaskBuilder) that are critical to the task run and overall to the build process.
    /// However such exception desn't indicate problem within the MsBuild engine, but rather in the Task itself - for this reason we wrap the exception,
    ///  so that we can properly log it up the stack (and not assume it is a bug within the build engine)
    /// </summary>
    internal sealed class CriticalTaskException : BuildExceptionBase
    {
        public CriticalTaskException(
            Exception innerException)
            : base(string.Empty, innerException)
        { }

        // Do not remove - used by BuildExceptionSerializationHelper
        internal CriticalTaskException(string message, Exception? inner)
            : base(message, inner)
        { }
    }
}
