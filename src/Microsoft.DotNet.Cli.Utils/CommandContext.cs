using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class CommandContext
    {
        internal static class Variables
        {
            private static readonly string Prefix = "DOTNET_CLI_CONTEXT_";
            internal static readonly string Verbose = Prefix + "VERBOSE";
            internal static readonly string AnsiPassThru = Prefix + "ANSI_PASS_THRU";
        }

        private static Lazy<bool> _verbose = new Lazy<bool>(() => GetBool(Variables.Verbose));
        private static Lazy<bool> _ansiPassThru = new Lazy<bool>(() => GetBool(Variables.AnsiPassThru));

        public static bool IsVerbose()
        {
            return _verbose.Value;
        }

        public static bool ShouldPassAnsiCodesThrough()
        {
            return _ansiPassThru.Value;
        }
        
        private static bool GetBool(string name, bool defaultValue = false)
        {
            var str = Environment.GetEnvironmentVariable(name);
            bool value;
            if(string.IsNullOrEmpty(str) || !bool.TryParse(str, out value))
            {
                return defaultValue;
            }
            return value;
        }
    }
}
