using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class SetEnvVar : Task
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Value { get; set; }

        public override bool Execute()
        {
            Environment.SetEnvironmentVariable(Name, Value);

            return true;
        }
    }
}
