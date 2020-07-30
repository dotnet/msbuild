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
            char c = ' ';
            List<string> allInstancesOfGACUtil = new List<string>();
            StreamReader output;
            StreamWriter input;

            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "C:\\Windows\\System32\\cmd.exe",
                    Arguments = "/k",
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
            input.WriteLine("where /F /R \"C:\\Program Files (x86)\\Microsoft SDKs\\Windows\" gacutil.exe");

            //throw away the first line - it's the command we just used
            output.ReadLine();
            string s = output.ReadLine();

            // store all instances of gacutil.exe
            // doing this for now but not acting on it to acknowledge that some weird scenario may prevent us from using the
            // first found gacutil.
            while (!String.IsNullOrEmpty(s))
            {
                allInstancesOfGACUtil.Add(s);
                s = output.ReadLine();
            }

            //couldn't find gacutil, quietly fail.
            if (allInstancesOfGACUtil.Count <= 0)
            {
                proc.Close();
                Console.WriteLine("Could not find instances of gacutil, exiting...");
                return;
            }

            // we've found gacutil, now let's use it.
            string gacUtilExe = allInstancesOfGACUtil[0];

            if (proc.StandardInput.BaseStream.CanWrite)
            {
                input.WriteLine($"{gacUtilExe} /nologo /u \"MSBuild, Version=15.1.0.0\"");
                input.WriteLine($"{gacUtilExe} /nologo /u \"Microsoft.Build.Conversion.Core, Version=15.1.0.0\"");
                input.WriteLine($"{gacUtilExe} /nologo /u \"Microsoft.Build, Version=15.1.0.0\"");
                input.WriteLine($"{gacUtilExe} /nologo /u \"Microsoft.Build.Engine, Version=15.1.0.0\"");
                input.WriteLine($"{gacUtilExe} /nologo /u \"Microsoft.Build.Tasks.Core, Version=15.1.0.0\"");
                input.WriteLine($"{gacUtilExe} /nologo /u \"Microsoft.Build.Utilities.Core, Version=15.1.0.0\"");
                input.WriteLine($"{gacUtilExe} /nologo /u \"Microsoft.Build.Framework, Version=15.1.0.0\"");

            }

            if(output.BaseStream.CanSeek && output.Peek() == -1)
            {

            }
            //hacky temporary workaround
            int i = 0;

            // Peek is a strange beast. It returns a -1 if no character can be read or if the stream does not support seeking.
            // Yet there are cases where you can peek a -1, yet read() returns a valid character.
            // I've tested breakpoints where output.Peek() == -1 && output.BaseStream.Caneek == true, but it never gets hit.
            
            while (!(i > 2000 && output.Peek() == -1))
            {
                // We're reading because if you happen to attempt a ReadLine() at the end of the stream, it will lock up.
                // With reading we can at least preemptively catch it before it locks (hence the hacky temporary workaround).
                c = (char)output.Read();

                Console.Write(c);
                i++;

                if (output.BaseStream.CanSeek && output.Peek() == -1)
                {

                }
            }

            proc.Close();
            proc.Dispose();
            Console.ReadKey();
        }
    }
}
