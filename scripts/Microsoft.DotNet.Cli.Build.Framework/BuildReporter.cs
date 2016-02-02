using System;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    public static class BuildReporter
    {
        public static void BeginSection(string type, string name)
        {
            Reporter.Output.WriteLine($"[{type.PadRight(10)} >]".Green() + $" [....] [{DateTime.Now:O}]".Blue() + $" {name}");
        }

        public static void EndSection(string type, string name, bool success)
        {
            var header = $"[{type.PadRight(10)} <]";
            if(success)
            {
                header = header.Green();
            }
            else
            {
                header = header.Red();
            }
            var successString = success ? " OK " : "FAIL";
            Reporter.Output.WriteLine(header + $" [{successString}] [{DateTime.Now:O}]".Blue() + $" {name}");
        }
    }
}
