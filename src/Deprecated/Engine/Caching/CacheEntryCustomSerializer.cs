// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.IO;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    internal static class CacheEntryCustomSerializer
    {
        private enum CacheEntryTypes
        {
            BuildItem = 1,
            BuildResult = 2,
            Property = 3
        }

        internal static void WriteToStream(CacheEntry entry, BinaryWriter writer)
        {
            Type entryType = entry.GetType();
            if (typeof(BuildItemCacheEntry) == entryType)
            {
                writer.Write((byte)CacheEntryTypes.BuildItem);
                entry.WriteToStream(writer);
            }
            else if (typeof(BuildResultCacheEntry) == entryType)
            {
                writer.Write((byte)CacheEntryTypes.BuildResult);
                entry.WriteToStream(writer);
            }
            else if (typeof(PropertyCacheEntry) == entryType)
            {
                writer.Write((byte)CacheEntryTypes.Property);
                entry.WriteToStream(writer);
            }
        }

        internal static CacheEntry CreateFromStream(BinaryReader reader)
        {
            CacheEntryTypes entryType = (CacheEntryTypes)reader.ReadByte();
            CacheEntry entry = null;

            switch (entryType)
            {
                case CacheEntryTypes.BuildItem:
                    entry = new BuildItemCacheEntry();
                    break;
                case CacheEntryTypes.BuildResult:
                    entry = new BuildResultCacheEntry();
                    break;
                case CacheEntryTypes.Property:
                    entry = new PropertyCacheEntry();
                    break;
                default:
                    ErrorUtilities.VerifyThrow(false, "Should not get to the default of CacheEntryCustomSerializer CreateFromStream");
                    break;
            }

            entry.CreateFromStream(reader);
            return entry;
        }
    }
}
