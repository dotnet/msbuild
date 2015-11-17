using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public static class Helpers
    {
        internal static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        internal static void CleanOrCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to remove directory: " + path);
                    Console.WriteLine(e.Message);
                }
            }

            Directory.CreateDirectory(path);
        }

        internal static ArchitectureMode GetCurrentArchitecture()
        {
            // TODO Support Architectures other than x64
            return ArchitectureMode.x64;
        }

        internal static OSMode GetCurrentOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OSMode.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OSMode.Mac;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OSMode.Linux;
            }
            else
            {
                throw new Exception("Unrecognized OS. dotnet-compile-native is compatible with Windows, OSX, and Linux");
            }
        }
        internal static IEnumerable<string> SplitStringCommandLine(string commandLine)
        {
            bool inQuotes = false;

            return commandLine.Split(c =>
            {
                if (c == '\"')
                    inQuotes = !inQuotes;

                return !inQuotes && c == ' ';
            })
            .Select(arg => arg.Trim().TrimMatchingQuotes('\"'))
            .Where(arg => !string.IsNullOrEmpty(arg));
        }

        internal static string TrimMatchingQuotes(this string input, char quote)
        {
            if ((input.Length >= 2) &&
                (input[0] == quote) && (input[input.Length - 1] == quote))
                return input.Substring(1, input.Length - 2);

            return input;
        }

        public static IEnumerable<string> Split(this string str,
                                            Func<char, bool> controller)
        {
            int nextPiece = 0;

            for (int c = 0; c < str.Length; c++)
            {
                if (controller(str[c]))
                {
                    yield return str.Substring(nextPiece, c - nextPiece);
                    nextPiece = c + 1;
                }
            }

            yield return str.Substring(nextPiece);
        }
    }
    internal class ArgValues
    {
        public string LogPath { get; set; }
        public string InputManagedAssemblyPath { get; set; }
        public string OutputDirectory { get; set; }
        public string IntermediateDirectory { get; set; }
        public string BuildType { get; set; }
        public string Architecture { get; set; }
        public string NativeMode { get; set; }
        public List<string> ReferencePaths { get; set; }
        public string IlcArgs { get; set; }
        public List<string> LinkLibPaths { get; set; }
        public string AppDepSDKPath { get; set; }
        public string IlcPath { get; set; }
        public string RuntimeLibPath { get; set; }
    }


}
