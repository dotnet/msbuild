using System;


namespace DeniedAssemblyTest
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            System.StringNormalizationExtensions.IsNormalized("hello");

            var os = System.Runtime.InteropServices.OSPlatform.OSX;

            var client = new System.Net.Http.HttpClient();

            // system.threading.overlapped
            var over = new System.Threading.PreAllocatedOverlapped(null, null, null);

            // codepages
            var provider = System.Text.CodePagesEncodingProvider.Instance;

            // compression
            var mode = System.IO.Compression.ZipArchiveMode.Create;
            Console.WriteLine(mode);
        }
    }

    abstract class DeriveFromDispatchProxy : System.Reflection.DispatchProxy
    {}
}
