using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Build
{
    public struct CompilerIO
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