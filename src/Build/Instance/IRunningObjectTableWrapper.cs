// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Execution
{
    internal interface IRunningObjectTableWrapper : IDisposable
    {
        object GetObject(string itemName);
    }
}
