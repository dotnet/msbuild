// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    internal sealed class StreamHelpers
    {
        /// <summary>
        /// Take a string and convert it to a StreamReader.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static StreamReader StringToStreamReader(string value)
        {
            MemoryStream m = new MemoryStream();
#if FEATURE_ENCODING_DEFAULT
            TextWriter w = new StreamWriter(m, System.Text.Encoding.Default);
#else
            TextWriter w = new StreamWriter(m, System.Text.Encoding.UTF8);
#endif

            w.Write(value);
            w.Flush();
            m.Seek(0, SeekOrigin.Begin);

            return new StreamReader(m);
        }
    }
}
