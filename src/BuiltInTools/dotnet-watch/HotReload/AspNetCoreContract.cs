// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal readonly struct UpdatePayload
    {
        public string ChangedFile { get; init; }
        public IEnumerable<UpdateDelta> Deltas { get; init; }
    }

    internal readonly struct UpdateDelta
    {
        public Guid ModuleId { get; init; }
        public byte[] MetadataDelta { get; init; }
        public byte[] ILDelta { get; init; }
        public int[] UpdatedMethods { get; init; }
    }

    internal enum ApplyResult
    {
        Failed = -1,
        Success = 0,
        Success_RefreshBrowser = 1,
    }
}
