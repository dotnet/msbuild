// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities.Mock
{
    internal interface ITemporaryDirectoryMock : ITemporaryDirectory
    {
        bool DisposedTemporaryDirectory { get; }
    }
}
