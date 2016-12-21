using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("dotnet-new3.UnitTests")]

namespace dotnet_new3
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return New3Command.Run(args);
        }
    }
}
