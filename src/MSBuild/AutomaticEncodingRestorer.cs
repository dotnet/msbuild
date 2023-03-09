// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Ported from https://github.com/dotnet/sdk/blob/bcea1face15458814b8e53e8785b52ba464f6538/src/Cli/dotnet/AutomaticEncodingRestorer.cs.
    /// A program can change the encoding of the console which would affect other programs.
    /// We would prefer to have a pattern where the program does not affect encoding of other programs.
    /// Create this class in a function akin to Main and let it manage the console encoding resources to return it to the state before execution upon destruction.
    /// </summary>
    public class AutomaticEncodingRestorer : IDisposable
    {
        private Encoding? _originalOutputEncoding = null;
        private Encoding? _originalInputEncoding = null;

        private bool outputEncodingAccessible = false;
        private bool inputEncodingAccessible = false;

        public AutomaticEncodingRestorer()
        {
            try
            {
                if (
#if NET7_0_OR_GREATER
                        !OperatingSystem.IsIOS() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsTvOS()) // Output + Input Encoding are unavailable on these platforms per docs.
#else
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // Windows is the only platform where we need to change the encoding as other platforms are UTF 8 by default.
#endif
                {
                    _originalOutputEncoding = Console.OutputEncoding;
                    outputEncodingAccessible = true;
                    if (
#if NET7_0_OR_GREATER
                        !OperatingSystem.IsBrowser()) // Input Encoding is also unavailable in this platform.
#else
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
                    {
                        _originalInputEncoding = Console.InputEncoding;
                        inputEncodingAccessible = true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SecurityException)
            {
                // The encoding is unavailable. Do nothing.
            }
        }

        public void Dispose()
        {
            try
            {
                if (outputEncodingAccessible && _originalOutputEncoding != null)
                {
                    Console.OutputEncoding = _originalOutputEncoding;
                }
                if (inputEncodingAccessible && _originalInputEncoding != null)
                {
                    Console.InputEncoding = _originalInputEncoding;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SecurityException)
            {
                // The encoding is unavailable. Do nothing.
            }
        }
    }
}
