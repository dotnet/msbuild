// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework.Logging
{
    internal interface IBinaryReader : IDisposable
    {
        public int BytesCountAllowedToReadRemaining { get; }

        public int? BytesCountAllowedToRead { set; }

        public long Position { get; }

        public byte ReadByte();

        public byte[] ReadBytes(int count);

        public byte[] ReadGuid();

        public bool ReadBoolean();

        public long ReadInt64();

        public string ReadString();

        public int ReadInt32();

        public void Seek(int count, SeekOrigin current);

        public Stream Slice(int numBytes);

        public int Read7BitEncodedInt();

        public int[] BulkRead7BitEncodedInt(int numIntegers);
    }
}
