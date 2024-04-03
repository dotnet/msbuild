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
