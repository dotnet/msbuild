// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Security;
using System.Text;

namespace Microsoft.DotNet.Cli
{
    /// <summary>
    /// A program can change the encoding of the console which would affect other programs.
    /// We would prefer to have a pattern where the program does not affect encoding of other programs.
    /// Create this class in a function akin to Main and let it manage the console encoding resources to return it to the state before execution upon destruction.
    /// </summary>
    internal class AutomaticEncodingRestorer : IDisposable
    {
        Encoding _originalOutputEncoding = null;
        Encoding _originalInputEncoding = null;

        bool outputEncodingAccessible = false;
        bool inputEncodingAccessible = false;

        public AutomaticEncodingRestorer()
        {
            try
            {
                if (!OperatingSystem.IsIOS() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsTvOS()) // Output + Input Encoding are unavailable on these platforms per docs.
                {
                    _originalOutputEncoding = Console.OutputEncoding;
                    outputEncodingAccessible = true;
                    if (!OperatingSystem.IsBrowser()) // Input Encoding is also unavailable in this platform.
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
                if (outputEncodingAccessible)
                {
                    Console.OutputEncoding = _originalOutputEncoding;
                }
                if (inputEncodingAccessible)
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
