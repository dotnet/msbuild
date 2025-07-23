// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework.BuildException;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Represents an exception that occurs when an SdkResolverService throws an unhandled exception.
    /// </summary>
    public class SdkResolverServiceException : BuildExceptionBase
    {
        public SdkResolverServiceException(string resourceName, params string[] args)
            : base(string.Format(ResourceUtilities.GetResourceString(resourceName), args))
        {
        }

        // Do not remove - used by BuildExceptionSerializationHelper
        internal SdkResolverServiceException(string message, Exception inner)
            : base(message, inner)
        { }
    }
}
