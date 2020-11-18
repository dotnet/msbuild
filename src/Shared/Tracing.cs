// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// A debug only helper class for tracing
    /// </summary>
    internal static class Tracing
    {
        // Disabling warning about unused fields -- this is effectively a 
        // debug-only class, so these fields cause a build break in RET
#pragma warning disable 649
        /// <summary>
        /// A dictionary of named counters
        /// </summary>
        private static Dictionary<string, int> s_counts;

        /// <summary>
        /// Last time logging happened
        /// </summary>
        private static DateTime s_last = DateTime.MinValue;

        /// <summary>
        /// How often to log
        /// </summary>
        private static TimeSpan s_interval;

        /// <summary>
        /// A place callers can put something worth logging later
        /// </summary>
        private static string s_slot = String.Empty;

        /// <summary>
        /// Short name of the current assembly - to distinguish statics when this type is shared into different assemblies
        /// </summary> 
        private static string s_currentAssemblyName;
#pragma warning restore 649

#if DEBUG
        /// <summary>
        /// Setup
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Clearly I can't inline this. Plus, it's debug only.")]
        static Tracing()
        {
            s_counts = new Dictionary<string, int>();

            string val = Environment.GetEnvironmentVariable("MSBUILDTRACEINTERVAL");
            double seconds;
            if (!String.IsNullOrEmpty(val) && System.Double.TryParse(val, out seconds))
            {
                s_interval = TimeSpan.FromSeconds(seconds);
            }
            else
            {
                s_interval = TimeSpan.FromSeconds(1);
            }

            s_currentAssemblyName = typeof(Tracing).GetTypeInfo().Assembly.GetName().Name;

            // Trace.WriteLine(new string('/', 100));
            // Trace.WriteLine("interval: " + interval.Seconds);
        }
#endif

        /// <summary>
        /// Put something in the slot
        /// </summary>
        [Conditional("DEBUG")]
        internal static void Slot(string tag, string value)
        {
            lock (s_counts)
            {
                s_slot = tag + ": " + value;
            }
        }

        /// <summary>
        /// Put something in the slot
        /// </summary>
        /// <typeparam name="K">The key type.</typeparam>
        /// <typeparam name="V">The value type.</typeparam>
        [Conditional("DEBUG")]
        internal static void Slot<K, V>(string tag, KeyValuePair<K, V> value)
        {
            Slot(tag, value.Key.ToString() + "=" + value.Key.ToString());
        }

        /// <summary>
        /// Increment the named counter, and dump if it's time to do so
        /// </summary>
        [Conditional("DEBUG")]
        internal static void Record(string counter)
        {
            lock (s_counts)
            {
                int existing;
                s_counts.TryGetValue(counter, out existing);
                int incremented = ++existing;
                s_counts[counter] = incremented;
                DateTime now = DateTime.Now;

                if (now > s_last + s_interval)
                {
                    Trace.WriteLine("================================================");
                    Trace.WriteLine(s_slot);
                    s_slot = String.Empty;
                    Dump();
                    Trace.WriteLine(System.Environment.StackTrace);
                    s_last = now;
                }
            }
        }

        /// <summary>
        /// Log the provided items
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        [Conditional("DEBUG")]
        internal static void List<T>(IEnumerable<T> items)
        {
            foreach (T item in items)
            {
                Trace.WriteLine(item.ToString());
            }
        }

        /// <summary>
        /// Dump all the named counters, if any
        /// </summary>
        [Conditional("DEBUG")]
        [SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies", Justification = "Debug only")]
        internal static void Dump()
        {
            if (s_counts.Count > 0)
            {
                Trace.WriteLine(s_currentAssemblyName);
                foreach (KeyValuePair<string, int> count in s_counts)
                {
                    Trace.WriteLine("# " + count.Key + "=" + count.Value);
                }
            }
        }
    }
}
