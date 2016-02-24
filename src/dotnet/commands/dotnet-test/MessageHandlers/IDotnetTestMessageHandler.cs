// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public interface IDotnetTestMessageHandler
    {
        DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message);
    }
}
