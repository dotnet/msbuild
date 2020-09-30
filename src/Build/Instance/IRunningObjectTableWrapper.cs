// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Execution
{
    internal interface IRunningObjectTableWrapper
    {
        object GetObject(string itemName);
    }
}
