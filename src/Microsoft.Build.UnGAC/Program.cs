using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.UnGAC
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                List<string> allInstancesOfGACUtil = new List<string>();
                StreamReader output;
                StreamWriter input;

                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "C:\\Windows\\System32\\cmd.exe",
                        Arguments = "/k", // Keep cmd open until we're done
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };
                proc.Start();

                // Hook into standard input/output of the process
                output = proc.StandardOutput;
                input = proc.StandardInput;

                if (!input.BaseStream.CanWrite)
                {
                    proc.Close();
                    proc.Dispose();
                    Console.WriteLine("Could not use where.exe to find gacutil.exe. Exiting...");
                    return;
                }

                // Use where.exe to find gacutil.exe
                input.WriteLine("where /F /R \"C:\\Program Files (x86)\\Microsoft SDKs\\Windows\" gacutil.exe");

                // Throw away the first line - it's the command we just used
                output.ReadLine();

                for(string gacInstance = output.ReadLine(); !string.IsNullOrEmpty(gacInstance); gacInstance = output.ReadLine())
                {
                    allInstancesOfGACUtil.Add(gacInstance);
                }

                if (allInstancesOfGACUtil.Count == 0)
                {
                    proc.Close();
                    proc.Dispose();
                    Console.WriteLine("Could not find instances of gacutil. Exiting...");
                    return;
                }

                // We've found gacutil, now let's use it.
                string gacUtilExe = allInstancesOfGACUtil[0];

                if (!input.BaseStream.CanWrite)
                {
                    proc.Close();
                    proc.Dispose();
                    Console.WriteLine("Could not write gacutil commands. Exiting...");
                    return;
                }

                input.WriteLine($"{gacUtilExe} /nologo /u \"MSBuild, Version=15.1.0.0\"\n" +
                                $"{gacUtilExe} /nologo /u \"Microsoft.Build, Version=15.1.0.0\"\n" +
                                $"{gacUtilExe} /nologo /u \"Microsoft.Build.Engine, Version=15.1.0.0\"\n" +
                                $"{gacUtilExe} /nologo /u \"Microsoft.Build.Framework, Version=15.1.0.0\"\n" +
                                $"{gacUtilExe} /nologo /u \"Microsoft.Build.Tasks.Core, Version=15.1.0.0\"\n" +
                                $"{gacUtilExe} /nologo /u \"Microsoft.Build.Utilities.Core, Version=15.1.0.0\"\n" +
                                $"{gacUtilExe} /nologo /u \"Microsoft.Build.Conversion.Core, Version=15.1.0.0\"\n");

                // This prevents output.ReadLine() from locking up below.
                input.Flush();
                input.Close();

                // Output everything gacutil returned.
                for (string s = output.ReadLine(); s != null; s = output.ReadLine())
                {
                    Console.WriteLine(s);
                }

                proc.Close();
                proc.Dispose();
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Caught an exception! We don't want to throw because we want MSBuild to install." +
                                    $"Message: {e.Message}" +
                                    $"Inner Exception: {e.InnerException}" +
                                    $"Stack Trace: {e.StackTrace}");
            }
        }
    }
}
