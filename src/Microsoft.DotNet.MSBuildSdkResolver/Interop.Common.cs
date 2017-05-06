// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal static partial class Interop
    {
        internal static string hostfxr_resolve_sdk(string exe_dir, string working_dir)
        {
            var buffer = new StringBuilder(capacity: 64);

            for (;;)
            {
                int size = hostfxr_resolve_sdk(exe_dir, working_dir, buffer, buffer.Capacity);
                if (size <= 0)
                {
                    Debug.Assert(size == 0);
                    return null;
                }

                if (size <= buffer.Capacity)
                {
                    break;
                }

                buffer.Capacity = size;
            }

            return buffer.ToString();
        }
    }
}