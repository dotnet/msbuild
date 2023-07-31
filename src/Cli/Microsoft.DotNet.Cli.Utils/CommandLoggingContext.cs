// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// Defines settings for logging.
    /// </summary>
    public static class CommandLoggingContext
    {
        public static class Variables
        {
            private const string Prefix = "DOTNET_CLI_CONTEXT_";
            public static readonly string Verbose = Prefix + "VERBOSE";
            internal static readonly string Output = Prefix + "OUTPUT";
            internal static readonly string Error = Prefix + "ERROR";
            internal static readonly string AnsiPassThru = Prefix + "ANSI_PASS_THRU";
        }

        private static Lazy<bool> s_verbose = new(() => Env.GetEnvironmentVariableAsBool(Variables.Verbose));
        private static Lazy<bool> s_output = new(() => Env.GetEnvironmentVariableAsBool(Variables.Output, true));
        private static Lazy<bool> s_error = new(() => Env.GetEnvironmentVariableAsBool(Variables.Error, true));
        private static readonly Lazy<bool> s_ansiPassThru = new(() => Env.GetEnvironmentVariableAsBool(Variables.AnsiPassThru));

        /// <summary>
        /// True if the verbose output is enabled.
        /// </summary>
        public static bool IsVerbose => s_verbose.Value;

        public static bool ShouldPassAnsiCodesThrough => s_ansiPassThru.Value;

        /// <summary>
        /// Sets or resets the verbose output.
        /// </summary>
        /// <remarks>
        /// After calling, consider calling <see cref="Reporter.Reset()"/> to apply change to reporter.
        /// </remarks>
        public static void SetVerbose(bool value)
        {
            s_verbose = new Lazy<bool>(() => value);
        }

        /// <summary>
        /// Sets or resets the normal output.
        /// </summary>
        /// <remarks>
        /// After calling, consider calling <see cref="Reporter.Reset()"/> to apply change to reporter.
        /// </remarks>
        public static void SetOutput(bool value)
        {
            s_output = new Lazy<bool>(() => value);
        }

        /// <summary>
        /// Sets or resets the error output.
        /// </summary>
        /// <remarks>
        /// After calling, consider calling <see cref="Reporter.Reset()"/> to apply change to reporter.
        /// </remarks>
        public static void SetError(bool value)
        {
            s_error = new Lazy<bool>(() => value);
        }

        /// <summary>
        /// True if normal output is enabled.
        /// </summary>
        internal static bool OutputEnabled => s_output.Value;

        /// <summary>
        /// True if error output is enabled.
        /// </summary>
        internal static bool ErrorEnabled => s_error.Value;
    }
}
