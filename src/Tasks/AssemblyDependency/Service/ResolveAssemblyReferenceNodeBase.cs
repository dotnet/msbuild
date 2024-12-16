// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class ResolveAssemblyReferenceNodeBase
    {
        protected const int DefaultBufferSizeInBytes = 81_920;

        protected const int ClientConnectTimeout = 5_000;

        protected const int MessageOffsetInBytes = 4;

        protected readonly RarNodeHandshake _handshake = new(HandshakeOptions.None);

        protected readonly string _pipeName;

        protected ResolveAssemblyReferenceNodeBase()
        {
            _pipeName = $"msbuild-rar-{_handshake.ComputeHash()}";
        }

        protected static void Serialize<T>(T message, MemoryStream memoryStream, bool setHash = false)
            where T : RarSerializableMessageBase, INodePacket, new()
        {
            memoryStream.SetLength(MessageOffsetInBytes);
            memoryStream.Position = MessageOffsetInBytes;

            ITranslator translator = BinaryTranslator.GetWriteTranslator(memoryStream);

            // Skip serialization if the result is cached. 
            if (message.ByteArray != null)
            {
                using BinaryWriter binaryWriter = new(memoryStream, Encoding.Default, leaveOpen: true);
                binaryWriter.Write(message.ByteArray);

                return;
            }

            translator.Translate(ref message);

            if (setHash)
            {
                message.SetByteString(memoryStream.GetBuffer(), MessageOffsetInBytes, (int)memoryStream.Length - MessageOffsetInBytes);
            }
        }

        protected static T Deserialize<T>(byte[] buffer, int messageLength, bool setHash = false)
            where T : RarSerializableMessageBase, INodePacket, new()
        {
            T message = new();
            using MemoryStream memoryStream = new(buffer, 0, messageLength, writable: true, publiclyVisible: true);
            memoryStream.Position = MessageOffsetInBytes;
            ITranslator translator = BinaryTranslator.GetReadTranslator(memoryStream, InterningBinaryReader.PoolingBuffer);
            translator.Translate(ref message);

            if (setHash)
            {
                message.SetByteString(buffer, MessageOffsetInBytes, messageLength - MessageOffsetInBytes);
            }

            return message;
        }

        protected static void WritePipe(PipeStream pipe, MemoryStream memoryStream)
        {
            memoryStream.Position = 0;
            memoryStream.CopyTo(pipe);
        }

        protected static int ReadPipe(PipeStream pipe, byte[] buffer, int offset, int minBytesToRead)
        {
            int bytesRead = offset;

            while (bytesRead < minBytesToRead)
            {
                int n = pipe.Read(buffer, bytesRead, buffer.Length - bytesRead);

                // If the connection is broken, read operations will not explicitly throw.
                // This matches the exception thrown by a broken write operation.
                if (n == 0)
                {
                    throw new IOException("Pipe is broken.");
                }

                bytesRead += n;
            }

            return bytesRead;
        }

        protected static void SetMessageLength(MemoryStream memoryStream)
        {
            int messageLength = (int)memoryStream.Length - MessageOffsetInBytes;

            memoryStream.Position = 0;
            using BinaryWriter binaryWriter = new(memoryStream, Encoding.Default, leaveOpen: true);
            binaryWriter.Write(messageLength);
        }

        protected static int ParseMessageLength(byte[] buffer)
        {
            using MemoryStream memoryStream = new(buffer, 0, MessageOffsetInBytes);
            using BinaryReader binaryReader = new(memoryStream);

            return MessageOffsetInBytes + binaryReader.ReadInt32();
        }

        protected static byte[] EnsureBufferSize(byte[] buffer, int requiredSize)
        {
            if (requiredSize <= buffer.Length)
            {
                return buffer;
            }

            byte[] newBuffer = new byte[requiredSize];
            buffer.CopyTo(newBuffer, 0);

            return newBuffer;
        }
    }
}
