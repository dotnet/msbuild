// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Build
{
    internal class CompilerIO
    {
        public readonly IEnumerable<string> Inputs;
        public readonly IEnumerable<string> Outputs;

        public CompilerIO(IEnumerable<string> inputs, IEnumerable<string> outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
        }

        public DiffResult DiffInputs(CompilerIO other)
        {
            var myInputSet = new HashSet<string>(Inputs);
            var otherInputSet = new HashSet<string>(other.Inputs);

            var additions = myInputSet.Except(otherInputSet);
            var deletions = otherInputSet.Except(myInputSet);

            return new DiffResult(additions, deletions);
        }

        internal class DiffResult
        {
            public IEnumerable<string> Additions { get; private set; }
            public IEnumerable<string> Deletions { get; private set; }

            public DiffResult(IEnumerable<string> additions, IEnumerable<string> deletions)
            {
                Additions = additions;
                Deletions = deletions;
            }
        }

    }
}