using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.SharedUtilities
{

    internal static class RunnerUtilities
    {
        public static string PathToMsBuildExe => FileUtilities.CurrentExecutablePath;

        /// <summary>
        /// Invoke msbuild.exe with the given parameters and return the stdout, stderr, and process exit status.
        /// This method may invoke msbuild via other runtimes.
        /// </summary>
        public static string ExecMSBuild(string msbuildParameters, out bool successfulExit)
        {
            return ExecMSBuild(PathToMsBuildExe, msbuildParameters, out successfulExit);
        }

        /// <summary>
        /// Invoke msbuild.exe with the given parameters and return the stdout, stderr, and process exit status.
        /// This method may invoke msbuild via other runtimes.
        /// </summary>
        public static string ExecMSBuild(string pathToMsBuildExe, string msbuildParameters, out bool successfulExit)
        {
#if FEATURE_RUN_EXE_IN_TESTS
            var pathToExecutable = pathToMsBuildExe;
#else
            var pathToExecutable = ResolveRuntimeExecutableName();
            msbuildParameters = "\"" + pathToMsBuildExe + "\"" + " " + msbuildParameters;
#endif

            return RunProcessAndGetOutput(pathToExecutable, msbuildParameters, out successfulExit);
        }

#if !FEATURE_RUN_EXE_IN_TESTS
        /// <summary>
        /// Resolve the platform specific path to the runtime executable that msbuild.exe needs to be run in (unix-mono, {unix, windows}-corerun).
        /// </summary>
        private static string ResolveRuntimeExecutableName()
        {
            return NativeMethodsShared.IsMono ? "mono" : Path.Combine(FileUtilities.CurrentExecutableDirectory, "CoreRun");
        }
#endif

        /// <summary>
        /// Run the process and get stdout and stderr
        /// </summary>
        private static string RunProcessAndGetOutput(string process, string parameters, out bool successfulExit)
        {
            ProcessStartInfo psi = new ProcessStartInfo(process);
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.Arguments = parameters;
            string output = String.Empty;

            using (Process p = new Process { EnableRaisingEvents = true, StartInfo = psi })
            {
                p.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args)
                {
                    if (args != null)
                    {
                        output += args.Data + "\r\n";
                    }
                };

                p.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs args)
                {
                    if (args != null)
                    {
                        output += args.Data + "\r\n";
                    }
                };

                Console.WriteLine("Executing [{0} {1}]", process, parameters);

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.StandardInput.Dispose();
                p.WaitForExit();

                successfulExit = p.ExitCode == 0;
            }

            Console.WriteLine("==== OUTPUT ====");
            Console.WriteLine(output);
            Console.WriteLine("==============");

            return output;
        }
    }
}
