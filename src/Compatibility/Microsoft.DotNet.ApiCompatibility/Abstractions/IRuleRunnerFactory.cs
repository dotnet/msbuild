// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// The factory to create the driver that the differ should use based on the <see cref="ComparingSettings"/>.
    /// </summary>
    public interface IRuleRunnerFactory
    {
        IRuleRunner GetRuleRunner();
    }
}
