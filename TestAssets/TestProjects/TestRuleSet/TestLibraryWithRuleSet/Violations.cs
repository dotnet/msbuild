// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace TestLibrary
{
    public class AttributeWithoutUsage : Attribute
    {
    }

    public class ClassWithUndisposedStream
    {
        private Stream _nonDisposedStream = new MemoryStream();

        public ClassWithUndisposedStream()
        {
        }

        public Stream GetStream()
        {
            return _nonDisposedStream;
        }
    }
}
