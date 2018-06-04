// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Xml;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// XmlNameTable that is thread safe for concurrent users.
    /// </summary>
    /// <remarks>
    /// Fortunately the standard implementation has only four accessible members
    /// and all of them are virtual so we can easily add locks.
    /// </remarks>
    internal class XmlNameTableThreadSafe : NameTable
    {
        /// <summary>
        /// Synchronization object.
        /// </summary>
        private object _locker = new Object();

        /// <summary>
        /// Add a string to the table.
        /// </summary>
        public override string Add(string key)
        {
            lock (_locker)
            {
                return base.Add(key);
            }
        }

        /// <summary>
        /// Add a string to the table, passed in as
        /// an extent in a char array.
        /// </summary>
        public override string Add(char[] key, int start, int len)
        {
            lock (_locker)
            {
                return base.Add(key, start, len);
            }
        }

        /// <summary>
        /// Get a string from the table.
        /// </summary>
        public override string Get(string value)
        {
            lock (_locker)
            {
                return base.Get(value);
            }
        }

        /// <summary>
        /// Get a string from the table, passed in as
        /// an extent in a char array.
        /// </summary>
        public override string Get(char[] key, int start, int len)
        {
            lock (_locker)
            {
                return base.Get(key, start, len);
            }
        }
    }
}
