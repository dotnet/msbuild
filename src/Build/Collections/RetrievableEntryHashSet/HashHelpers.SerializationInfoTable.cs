// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
// Used by Hashtable and Dictionary's SerializationInfo .ctor's to store the SerializationInfo
// object until OnDeserialization is called.
using System.Threading;

namespace System.Collections
{
    internal static partial class HashHelpers
    {
        private static ConditionalWeakTable<object, SerializationInfo>? s_serializationInfoTable;

        public static ConditionalWeakTable<object, SerializationInfo> SerializationInfoTable
        {
            get
            {
                if (s_serializationInfoTable == null)
                {
                    _ = Interlocked.CompareExchange(ref s_serializationInfoTable, new ConditionalWeakTable<object, SerializationInfo>(), null);
                }

                return s_serializationInfoTable;
            }
        }
    }
}
