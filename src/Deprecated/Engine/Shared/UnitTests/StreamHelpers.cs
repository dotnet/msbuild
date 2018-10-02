// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;


namespace Microsoft.Build.UnitTests
{
    sealed internal class StreamHelpers
    {
        /// <summary>
        /// Take a string and convert it to a StreamReader.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static internal StreamReader StringToStreamReader(string value)
        {
            MemoryStream m = new MemoryStream();
            TextWriter w = new StreamWriter(m, System.Text.Encoding.Default);

            w.Write(value);
            w.Flush();
            m.Seek(0, SeekOrigin.Begin);

            return new StreamReader(m);
        }
    }
}
