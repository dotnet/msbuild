// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.IO.Hashing;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal abstract class RarSerializableMessageBase : INodePacket
    {
        internal ulong ByteHash { get; private set; }

        internal byte[]? ByteArray { get; private set; }

        public abstract NodePacketType Type { get; }

        public abstract void Translate(ITranslator translator);

        internal void SetByteString(byte[] buffer, int sourceIndex, int messageLength)
        {
            ByteArray = new byte[messageLength];
            Array.Copy(buffer, sourceIndex, ByteArray, 0, messageLength);

            // TODO: Properly implement IEquatable
            ByteHash = XxHash64.HashToUInt64(ByteArray);
        }

        protected void InternTaskItems(RarTaskItemBase[] taskItems, RarMetadataInternCache internCache)
        {
            foreach (RarTaskItemBase taskItem in taskItems)
            {
                taskItem.InternMetadata(internCache);
            }
        }

        protected void PopulateTaskItems(RarTaskItemBase[] taskItems, RarMetadataInternCache internCache)
        {
            foreach (RarTaskItemBase taskItem in taskItems)
            {
                taskItem.PopulateMetadata(internCache);
            }
        }
    }
}
