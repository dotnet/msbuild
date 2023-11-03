// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    internal sealed class StreamHelpers
    {
        /*
         * Method:  StringToStream (overload)
         *
         * Take a string and convert it into a Stream.
         * Use the default encoding which means this machine's ANSI codepage.
         */
        internal static Stream StringToStream(string value)
        {
#if FEATURE_ENCODING_DEFAULT
            return StringToStream(value, System.Text.Encoding.Default); // We want this to be Default which is ANSI
#else
            return StringToStream(value, System.Text.Encoding.UTF8); // We want this to be Default which is ANSI
#endif
        }

        /*
         * Method:  StringToStream (overload)
         *
         * Take a string and convert it into a Stream.
         * Takes an alternate encoding type
         */
        internal static Stream StringToStream(string value, System.Text.Encoding encoding)
        {
            MemoryStream m = new MemoryStream();
            TextWriter w = new StreamWriter(m, encoding); // HIGHCHAR: StringToStream helper accepts encoding from caller.

            w.Write(value);
            w.Flush();
            m.Seek(0, SeekOrigin.Begin);
            return m;
        }
    }
}
