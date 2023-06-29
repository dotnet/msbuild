// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Data derived from the 'start upload' call that is used to determine how perform the upload.
/// </summary>
internal record StartUploadInformation(Uri UploadUri);
