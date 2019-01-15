// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommonWriterType = System.Action<string, string, System.Collections.Generic.IEnumerable<string>>;

namespace Microsoft.Build.Shared.Debugging
{
    /// <summary>
    ///     A collection of useful writers
    /// </summary>
    internal static class PrintLineDebuggerWriters
    {
        public class IdBasedFilesWriter
        {
            private string LogFileRoot { get; }

            public CommonWriterType Writer => (id, callsite, args) =>
            {
                try
                {
                    var file = Path.Combine(LogFileRoot, string.IsNullOrEmpty(id) ? "NoId" : id) + ".csv";
                    File.AppendAllText(file, CsvFormat(string.Empty, callsite, args));
                }
                catch (Exception e)
                {
                    var errorFile = Path.Combine(LogFileRoot, $"LoggingException_{Guid.NewGuid()}");
                    File.AppendAllText(errorFile, $"{SimpleFormat(id, callsite, args)}\n{e.Message}\n{e.StackTrace}");
                }
            };

            public IdBasedFilesWriter(string logFileRoot)
            {
                this.LogFileRoot = logFileRoot;
                Directory.CreateDirectory(logFileRoot);
            }

            public static IdBasedFilesWriter FromArtifactLogDirectory()
            {
                return new IdBasedFilesWriter(ArtifactsLogDirectory);
            }
        }

        public class CompositeWriter
        {
            private IEnumerable<CommonWriterType> Writers { get; }

            public CommonWriterType Writer => (id, callsite, args) =>
            {
                var argsArray = args as string[] ?? args.ToArray();

                foreach (var writer in Writers)
                {
                    writer(id, callsite, argsArray);
                }
            };

            public CompositeWriter(IEnumerable<CommonWriterType> writers)
            {
                Writers = writers;
            }
        }

        public static CommonWriterType StdOutWriter = (id, callsite, args) => Console.WriteLine(SimpleFormat(id, callsite, args));

        private static Lazy<string> _artifactsLogs = new Lazy<string>(
            () =>
            {
                var executingAssembly = FileUtilities.ExecutingAssemblyPath;

                var binPart = $"bin";

                var logIndex = executingAssembly.IndexOf(binPart, StringComparison.Ordinal);

                var artifactsPart = executingAssembly.Substring(0, logIndex);
                return logIndex < 0
                    ? null
                    : Path.Combine(artifactsPart, "log", "Debug");
            });

        public static string ArtifactsLogDirectory => _artifactsLogs.Value;

        public static string SimpleFormat(string id, string callsite, IEnumerable<string> args)
        {
            return $"\n{(id == null ? string.Empty : id + ": ")}{callsite}:{string.Join(";", args)}";
        }

        public static string CsvFormat(string id, string callsite, IEnumerable<string> args)
        {
            var joinedArgs = $"{EscapeCommas(callsite)},{string.Join(",", args.Select(arg => EscapeCommas(arg)))}\n";

            return string.IsNullOrEmpty(id)
                ? joinedArgs
                : $"{EscapeCommas(id)},{joinedArgs}";

            string EscapeCommas(string s)
            {
                return s.Replace(",", ";");
            }
        }
    }
}
