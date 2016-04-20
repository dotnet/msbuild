// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Build
{
    internal struct CompilerIO
    {
        public readonly List<string> Inputs;
        public readonly List<string> Outputs;

        public CompilerIO(List<string> inputs, List<string> outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
        }
    }
}