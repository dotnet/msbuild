// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class MSBuildForwardingAppWithoutLogging
    {
        private static readonly bool AlwaysExecuteMSBuildOutOfProc = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC");
        private static readonly bool UseMSBUILDNOINPROCNODE = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_USE_MSBUILDNOINPROCNODE");

        private const string MSBuildExeName = "MSBuild.dll";

        private const string SdksDirectoryName = "Sdks";

        // Null if we're running MSBuild in-proc.
        private ForwardingAppImplementation _forwardingApp;

        // Command line arguments we're asked to forward to MSBuild.
        private readonly IEnumerable<string> _argsToForward;

        internal static string MSBuildExtensionsPathTestHook = null;

        // Path to the MSBuild binary to use.
        public string MSBuildPath { get; }

        // True if, given current state of the class, MSBuild would be executed in its own process.
        public bool ExecuteMSBuildOutOfProc => _forwardingApp != null;

        private readonly Dictionary<string, string> _msbuildRequiredEnvironmentVariables =
            new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", MSBuildExtensionsPathTestHook ?? AppContext.BaseDirectory },
                { "MSBuildSDKsPath", GetMSBuildSDKsPath() },
                { "DOTNET_HOST_PATH", GetDotnetPath() },
            };

        private readonly IEnumerable<string> _msbuildRequiredParameters =
            new List<string> { "-maxcpucount", "-verbosity:m" };

        public MSBuildForwardingAppWithoutLogging(IEnumerable<string> argsToForward, string msbuildPath = null)
        {
            string defaultMSBuildPath = GetMSBuildExePath();

            _argsToForward = argsToForward;
            MSBuildPath = msbuildPath ?? defaultMSBuildPath;

            if (UseMSBUILDNOINPROCNODE)
            {
                // Force MSBuild to use external working node long living process for building projects
                // We also refers to this as MSBuild Server V1 as entry process forwards most of the work to it.
                EnvironmentVariable("MSBUILDNOINPROCNODE", "1");
            }

            // If DOTNET_CLI_RUN_MSBUILD_OUTOFPROC is set or we're asked to execute a non-default binary, call MSBuild out-of-proc.
            if (AlwaysExecuteMSBuildOutOfProc || !string.Equals(MSBuildPath, defaultMSBuildPath, StringComparison.OrdinalIgnoreCase))
            {
                InitializeForOutOfProcForwarding();
            }
        }

        private void InitializeForOutOfProcForwarding()
        {
            _forwardingApp = new ForwardingAppImplementation(
                MSBuildPath,
                _msbuildRequiredParameters.Concat(_argsToForward.Select(Escape)),
                environmentVariables: _msbuildRequiredEnvironmentVariables);
        }

        public ProcessStartInfo GetProcessStartInfo()
        {
            Debug.Assert(_forwardingApp != null, "Can't get ProcessStartInfo when not executing out-of-proc");
            return _forwardingApp.GetProcessStartInfo();
        }

        public string[] GetAllArguments()
        {
            return _msbuildRequiredParameters.Concat(_argsToForward.Select(Escape)).ToArray();
        }

        public void EnvironmentVariable(string name, string value)
        {
            if (_forwardingApp != null)
            {
                _forwardingApp.WithEnvironmentVariable(name, value);
            }
            else
            {
                _msbuildRequiredEnvironmentVariables.Add(name, value);
            }

            if (value == string.Empty || value == "\0")
            {
                // Do not allow MSBuild NOIPROCNODE as null env vars are not properly transferred to build nodes
                _msbuildRequiredEnvironmentVariables["MSBUILDNOINPROCNODE"] = "0";
                // Unlike ProcessStartInfo.EnvironmentVariables, Environment.SetEnvironmentVariable can't set a variable
                // to an empty value, so we just fall back to calling MSBuild out-of-proc if we encounter this case.
                // https://github.com/dotnet/runtime/issues/50554
                InitializeForOutOfProcForwarding();
            }
        }

        public int Execute()
        {
            if (_forwardingApp != null)
            {
                return GetProcessStartInfo().Execute();
            }
            else
            {
                return ExecuteInProc(GetAllArguments());
            }
        }

        public int ExecuteInProc(string[] arguments)
        {
            // Save current environment variables before overwriting them.
            Dictionary<string, string> savedEnvironmentVariables = new Dictionary<string, string>();
            try
            {
                foreach (KeyValuePair<string, string> kvp in _msbuildRequiredEnvironmentVariables)
                {
                    savedEnvironmentVariables[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }

                try
                {
                    // Execute MSBuild in the current process by calling its Main method.
                    return Microsoft.Build.CommandLine.MSBuildApp.Main(arguments);
                }
                catch (Exception exception)
                {
                    // MSBuild, like all well-behaved CLI tools, handles all exceptions. In the unlikely case
                    // that something still escapes, we print the exception and fail the call. Non-localized
                    // string is OK here.
                    Console.Error.Write("Unhandled exception: ");
                    Console.Error.WriteLine(exception.ToString());

                    return unchecked((int)0xe0434352); // EXCEPTION_COMPLUS
                }
            }
            finally
            {
                // Restore saved environment variables.
                foreach (KeyValuePair<string, string> kvp in savedEnvironmentVariables)
                {
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }
            }
        }

        private static string Escape(string arg) =>
             // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
             IsRestoreSources(arg) ?
                arg.Replace(";", "%3B")
                   .Replace("://", ":%2F%2F") :
                arg;

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                MSBuildExeName);
        }

        private static string GetMSBuildSDKsPath()
        {
            var envMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");

            if (envMSBuildSDKsPath != null)
            {
                return envMSBuildSDKsPath;
            }

            return Path.Combine(
                AppContext.BaseDirectory,
                SdksDirectoryName);
        }

        private static string GetDotnetPath()
        {
            return new Muxer().MuxerPath;
        }

        private static bool IsRestoreSources(string arg)
        {
            return arg.StartsWith("/p:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/property:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-p:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-property:RestoreSources=", StringComparison.OrdinalIgnoreCase);
        }
    }
}

#endif
