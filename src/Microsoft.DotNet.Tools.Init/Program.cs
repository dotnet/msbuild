using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Init
{
    public class Program
    {
        private static string GetFileNameFromResourceName(string s)
        {
            // A.B.C.D.filename.extension
            string[] parts = s.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            // filename.extension
            return parts[parts.Length - 2] + "." + parts[parts.Length - 1];
        }

        public int CreateEmptyProject()
        {
            var thisAssembly = typeof(Program).GetTypeInfo().Assembly;
            var resources = from resourceName in thisAssembly.GetManifestResourceNames()
                            where resourceName.ToLowerInvariant().EndsWith(".cs") || resourceName.ToLowerInvariant().EndsWith(".json")
                            select resourceName;

            Dictionary<string, string> resourceNameToFileName = new Dictionary<string, string>();
            bool hasFilesToOverride = false;
            foreach (string resourceName in resources)
            {
                string fileName = GetFileNameFromResourceName(resourceName);

                resourceNameToFileName.Add(resourceName, fileName);
                if (File.Exists(fileName))
                {
                    Console.WriteLine("Creating new project would override file {0}.", fileName);
                    hasFilesToOverride = true;
                }
            }

            if (hasFilesToOverride)
            {
                Console.WriteLine("Creating new project failed.");
                return 1;
            }

            foreach (var kv in resourceNameToFileName)
            {
                using (var fileStream = File.Create(kv.Value))
                {
                    using (var resource = thisAssembly.GetManifestResourceStream(kv.Key))
                    {
                        resource.CopyTo(fileStream);
                    }
                }
            }

            Console.WriteLine("Created new project in {0}.", Directory.GetCurrentDirectory());

            return 0;
        }

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet init";
            app.FullName = ".NET Initializer";
            app.Description = "Initializes empty project for .NET Platform";
            app.HelpOption("-h|--help");

            var dotnetInit = new Program();
            app.OnExecute((Func<int>)dotnetInit.CreateEmptyProject);

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }
    }
}
