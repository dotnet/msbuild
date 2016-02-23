// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Newtonsoft.Json;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class LineDelimitedJsonStream
    {
        private readonly StreamWriter _stream;

        public LineDelimitedJsonStream(Stream stream)
        {
            _stream = new StreamWriter(stream);
        }

        public void Send(object @object)
        {
            _stream.WriteLine(JsonConvert.SerializeObject(@object));

            _stream.Flush();
        }
    }
}