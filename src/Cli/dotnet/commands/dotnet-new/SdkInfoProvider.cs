// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.New
{
    internal class SdkInfoProvider : ISdkInfoProvider
    {
        public Guid Id { get; } = Guid.Parse("{A846C4E2-1E85-4BF5-954D-17655D916928}");
        public string VersionString => Product.Version;
    }
}
