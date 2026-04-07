// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework.BuildException;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Represents an exception that occurs when an SdkResolverService throws an unhandled exception.
    /// </summary>
    public class SdkResolverServiceException : BuildExceptionBase
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0022:Constructor make noninheritable base class inheritable", Justification = "Shipped in 18.0")]
        public SdkResolverServiceException(string message, params string[] args)
            : base(string.Format(message, args))
        {
        }

        // Do not remove - used by BuildExceptionSerializationHelper
        internal SdkResolverServiceException(string message, Exception inner)
            : base(message, inner)
        { }
    }
}
