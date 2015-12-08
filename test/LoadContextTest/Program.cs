using System;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Loader;
using NuGet.Frameworks;

namespace LoadContextTest
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Press enter to start");
            Console.ReadLine();
            // Get the path to the project
            if(args.Length < 1)
            {
                Console.Error.WriteLine("Usage: LoadContextTest <path to project>");
                return 1;
            }
            var project = Path.GetFullPath(args[0]);

            // Load the project load context
            Console.WriteLine($"Creating load context for {project}");
            var context = ProjectContext.Create(project, FrameworkConstants.CommonFrameworks.DnxCore50, new[] { RuntimeIdentifier.Current });
            var loadContext = context.CreateLoadContext();

            // Load the project assembly
            var asm = loadContext.LoadFromAssemblyName(new AssemblyName(context.ProjectFile.Name));

            // Find the helper type and method
            var type = asm.GetType("TestLibrary.Helper");
            if(type == null)
            {
                Console.Error.WriteLine("Failed to find type");
                return 1;
            }
            var method = type.GetMethod("SayHi", BindingFlags.Public | BindingFlags.Static);
            if(method == null)
            {
                Console.Error.WriteLine("Failed to find method");
                return 1;
            }

            // Call it!
            method.Invoke(null, new object[0]);
            return 0;
        }
    }
}
