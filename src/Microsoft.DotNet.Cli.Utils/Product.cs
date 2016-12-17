using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Product
    {
        public static readonly string LongName = LocalizableStrings.DotNetCommandLineTools;
        public static readonly string Version = GetProductVersion();

        private static string GetProductVersion()
        {
            var attr = typeof(Product)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr?.InformationalVersion;
        }
    }
}
