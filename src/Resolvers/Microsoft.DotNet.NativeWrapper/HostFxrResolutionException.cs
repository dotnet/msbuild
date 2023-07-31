// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    /// <summary>
    /// An instance of this exception is thrown when hostfxr fails to be loaded
    /// by the native bundler due to problems finding its path.
    /// </summary>
    public class HostFxrResolutionException : Exception
    {
        internal HostFxrResolutionException()
            : base()
        {
        }

        internal HostFxrResolutionException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// The path specified by HOSTFXR_PATH points to a file which could not be loaded.
    /// </summary>
    public sealed class HostFxrNotFoundException : HostFxrResolutionException
    {
        public HostFxrNotFoundException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Runtime property HOSTFXR_PATH was not set or empty. This property should have been
    /// set by the muxer. 
    /// </summary>
    public sealed class HostFxrRuntimePropertyNotSetException : HostFxrResolutionException
    {
        public HostFxrRuntimePropertyNotSetException()
            : base()
        {
        }
    }
}