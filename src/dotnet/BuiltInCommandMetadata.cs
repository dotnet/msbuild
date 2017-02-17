using System;

namespace Microsoft.DotNet.Cli
{
    public class BuiltInCommandMetadata
    {
        public Func<string[], int> Command { get; set; }
        public Uri DocLink { get; set; }
    }
}