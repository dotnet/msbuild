// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Captures the data needed to continue chunked upload
/// </summary>
internal record NextChunkUploadInformation(Uri UploadUri);
