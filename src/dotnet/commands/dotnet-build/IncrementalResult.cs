// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Build
{
    internal class IncrementalResult
    {
        public static readonly IncrementalResult DoesNotNeedRebuild = new IncrementalResult(false, "", Enumerable.Empty<string>());

        public bool NeedsRebuilding { get; }
        public string Reason { get; }
        public IEnumerable<string> Items { get; }

        private IncrementalResult(bool needsRebuilding, string reason, IEnumerable<string> items)
        {
            NeedsRebuilding = needsRebuilding;
            Reason = reason;
            Items = items;
        }

        public IncrementalResult(string reason)
            : this(true, reason, Enumerable.Empty<string>())
        {
        }

        public IncrementalResult(string reason, IEnumerable<string> items)
            : this(true, reason, items)
        {
        }
    }
}