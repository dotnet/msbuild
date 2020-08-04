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
                        Arguments = "/k", // keep cmd open until we're done
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };
                proc.Start();

                // hook into standard input/output of the process
                output = proc.StandardOutput;
                input = proc.StandardInput;

                // use where.exe to find gacutil.exe
                if (input.BaseStream.CanWrite)
                {
                    input.WriteLine("where /F /R \"C:\\Program Files (x86)\\Microsoft SDKs\\Windows\" gacutil.exe");
                }

                //throw away the first line - it's the command we just used
                output.ReadLine();
                string s = output.ReadLine();

                // store all instances of gacutil.exe
                // doing this for now "just in case"
                while (!String.IsNullOrEmpty(s))
                {
                    allInstancesOfGACUtil.Add(s);
                    s = output.ReadLine();
                }

                if (allInstancesOfGACUtil.Count <= 0)
                {
                    proc.Close();
                    Console.WriteLine("Could not find instances of gacutil, exiting...");
                    return;
                }

                // we've found gacutil, now let's use it.
                string gacUtilExe = allInstancesOfGACUtil[0];

                if (input.BaseStream.CanWrite)
                {
                    input.WriteLine($"{gacUtilExe} /nologo /u \"MSBuild, Version=15.1.0.0\"\n" +
                                    $"{gacUtilExe} /nologo /u \"Microsoft.Build.Conversion.Core, Version=15.1.0.0\"\n" +
                                    $"{gacUtilExe} /nologo /u \"Microsoft.Build, Version=15.1.0.0\"\n" +
                                    $"{gacUtilExe} /nologo /u \"Microsoft.Build.Engine, Version=15.1.0.0\"\n" +
                                    $"{gacUtilExe} /nologo /u \"Microsoft.Build.Tasks.Core, Version=15.1.0.0\"\n" +
                                    $"{gacUtilExe} /nologo /u \"Microsoft.Build.Utilities.Core, Version=15.1.0.0\"\n" +
                                    $"{gacUtilExe} /nologo /u \"Microsoft.Build.Framework, Version=15.1.0.0\"\n");
                }

                //hacky temporary workaround
                int i = 0;

                // Peek is a strange beast. It returns a -1 if "no character can be read or if the stream does not support seeking."
                // Yet there are cases where you can peek a -1 and read() returns a valid character.
                // I've tested breakpoints where output.Peek() == -1 && output.BaseStream.CanSeek == true, but it never gets hit.
                while (!(i > 2000 && output.Peek() == -1))
                {
                    // We're read()ing because if you attempt a ReadLine() at the end of the stream, it will lock up.
                    // With reading we can at least preemptively catch it before it locks (hence the hacky temporary workaround).
                    Console.Write((char)output.Read());
                    i++;
                }

                proc.Close();
                proc.Dispose();
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Caught an exception! We don't want to throw because we want MSBuild to continue to install." +
                                    $"Message: {e.Message}" +
                                    $"Inner Exception: {e.InnerException}" +
                                    $"Stack Trace: {e.StackTrace}");
            }
        }
    }
}
