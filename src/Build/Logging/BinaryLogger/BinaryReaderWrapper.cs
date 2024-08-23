// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.Build.Logging
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Build.Framework.Logging;
    using Microsoft.Build.Shared;

    /// <summary>
    /// Implements <see cref="IBinaryReader"/> on a <see cref="System.IO.BinaryReader "/>.
    /// </summary>
    internal class BinaryReaderWrapper : IBinaryReader
    {
        private readonly BinaryReader _binaryReader;

        // This is used to verify that events deserialization is not overreading expected size.
        private readonly TransparentReadStream _readStream;

        public BinaryReaderWrapper(BinaryReader binaryReader)
        {
            this._readStream = TransparentReadStream.EnsureTransparentReadStream(binaryReader.BaseStream);

            this._binaryReader = binaryReader.BaseStream == _readStream
                ? binaryReader
                : new BinaryReader(_readStream);
        }

        int IBinaryReader.BytesCountAllowedToReadRemaining => _readStream.BytesCountAllowedToReadRemaining;

        int? IBinaryReader.BytesCountAllowedToRead { set => _readStream.BytesCountAllowedToRead = value; }

        long IBinaryReader.Position => _readStream.Position;

        public const int MaxBulkRead7BitLength = 10;
        private int[] resultInt = new int[MaxBulkRead7BitLength];

        int[] IBinaryReader.BulkRead7BitEncodedInt(int numIntegers)
        {
            if (numIntegers > MaxBulkRead7BitLength)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < numIntegers; i++)
            {
                resultInt[i] = _binaryReader.Read7BitEncodedInt();
            }

            return resultInt;
        }

        void IBinaryReader.Seek(int count, SeekOrigin current) => _binaryReader.BaseStream.Seek(count, current);

        Stream IBinaryReader.Slice(int numBytes) => _binaryReader.BaseStream.Slice(numBytes);

        int IBinaryReader.Read7BitEncodedInt() => _binaryReader.Read7BitEncodedInt();

        byte IBinaryReader.ReadByte() => _binaryReader.ReadByte();

        byte[] IBinaryReader.ReadBytes(int count) => _binaryReader.ReadBytes(count);

        byte[] IBinaryReader.ReadGuid() => _binaryReader.ReadBytes(16 /*sizeof(Guid) - to avoid unsafe context, Guid will never change in size */);

        bool IBinaryReader.ReadBoolean() => _binaryReader.ReadBoolean();

        long IBinaryReader.ReadInt64() => _binaryReader.ReadInt64();

        string IBinaryReader.ReadString() => _binaryReader.ReadString();

        int IBinaryReader.ReadInt32() => _binaryReader.ReadInt32();

        void IDisposable.Dispose() => _binaryReader.Dispose();
    }
}
