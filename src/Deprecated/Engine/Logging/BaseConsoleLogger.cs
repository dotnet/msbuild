// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.Collections;
using System.Globalization;
using System.IO;

namespace Microsoft.Build.BuildEngine
{
    #region Delegates
    internal delegate void WriteLinePrettyFromResourceDelegate(int indentLevel, string resourceString, params object[] args);
    #endregion

    abstract internal class BaseConsoleLogger : INodeLogger
    {
        #region Properties
        /// <summary>
        /// Gets or sets the level of detail to show in the event log.
        /// </summary>
        /// <value>Verbosity level.</value>
        public LoggerVerbosity Verbosity
        {
            get
            {
                return verbosity;
            }

            set
            {
                verbosity = value;
            }
        }

        /// <summary>
        /// The console logger takes a single parameter to suppress the output of the errors
        /// and warnings summary at the end of a build.
        /// </summary>
        /// <value>null</value>
        public string Parameters
        {
            get
            {
                return loggerParameters;
            }

            set
            {
                loggerParameters = value;
            }
        }

        /// <summary>
        /// Suppresses the display of project headers. Project headers are
        /// displayed by default unless this property is set.
        /// </summary>
        /// <remarks>This is only needed by the IDE logger.</remarks>
        internal bool SkipProjectStartedText
        {
            get
            {
                return skipProjectStartedText;
            }

            set
            {
                skipProjectStartedText = value;
            }
        }

        /// <summary>
        /// Suppresses the display of error and warnings summary.
        /// </summary>
        internal bool ShowSummary
        {
            get
            {
                return showSummary ?? false;
            }

            set
            {
                showSummary = value;
            }
        }

        /// <summary>
        /// Provide access to the write hander delegate so that it can be redirected
        /// if necessary (e.g. to a file)
        /// </summary>
        protected WriteHandler WriteHandler
        {
            get
            {
                return write;
            }

            set
            {
                write = value;
            }
        }
        #endregion

        /// <summary>
        /// Parses out the logger parameters from the Parameters string.
        /// </summary>
        public void ParseParameters()
        {
            if (loggerParameters != null)
            {
                string[] parameterComponents = loggerParameters.Split(parameterDelimiters);
                for (int param = 0; param < parameterComponents.Length; param++)
                {
                    if (parameterComponents[param].Length > 0)
                    {
                        string[] parameterAndValue = parameterComponents[param].Split(parameterValueSplitCharacter);

                        if (parameterAndValue.Length > 1)
                        {
                            ApplyParameter(parameterAndValue[0], parameterAndValue[1]);
                        }
                        else
                        {
                            ApplyParameter(parameterAndValue[0], null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// An implementation of IComparer useful for comparing the keys 
        /// on DictionaryEntry's
        /// </summary>
        /// <remarks>Uses CurrentCulture for display purposes</remarks>
        internal class DictionaryEntryKeyComparer : IComparer
        {
            public int Compare(Object a, Object b)
            {
                return String.Compare(
                    (string)(((DictionaryEntry)a).Key),
                    (string)(((DictionaryEntry)b).Key),
                    true /*case insensitive*/, CultureInfo.CurrentCulture);
            }
        }

        /// <summary>
        /// An implementation of IComparer useful for comparing the ItemSpecs 
        /// on ITaskItem's
        /// </summary>
        /// <remarks>Uses CurrentCulture for display purposes</remarks>
        internal class ITaskItemItemSpecComparer : IComparer
        {
            public int Compare(Object a, Object b)
            {
                return String.Compare(
                    (string)(((ITaskItem)a).ItemSpec),
                    (string)(((ITaskItem)b).ItemSpec),
                    true /*case insensitive*/, CultureInfo.CurrentCulture);
            }
        }

        /// <summary>
        /// Indents the given string by the specified number of spaces.
        /// </summary>
        /// <param name="s">String to indent.</param>
        /// <param name="indent">Depth to indent.</param>
        internal string IndentString(string s, int indent)
        {
            // It's possible the event has a null message
            if (s == null)
            {
                s = String.Empty;
            }

            // This will never return an empty array.  The returned array will always
            // have at least one non-null element, even if "s" is totally empty.
            String[] subStrings = SplitStringOnNewLines(s);

            StringBuilder result = new StringBuilder(
                (subStrings.Length * indent) +
                (subStrings.Length * Environment.NewLine.Length) +
                s.Length);

            for (int i = 0; i < subStrings.Length; i++)
            {
                result.Append(' ', indent).Append(subStrings[i]);
                result.AppendLine();
            }

            return result.ToString();
        }

        /// <summary>
        /// Splits strings on 'newLines' with tollerance for Everett and Dogfood builds.
        /// </summary>
        /// <param name="s">String to split.</param>
        internal static string[] SplitStringOnNewLines(string s)
        {
            string[] subStrings = s.Split(newLines, StringSplitOptions.None);
            return subStrings;
        }

        /// <summary>
        /// Writes a newline to the log.
        /// </summary>
        internal void WriteNewLine()
        {
            write(Environment.NewLine);
        }

        /// <summary>
        /// Writes a line from a resource string to the log, using the default indentation.
        /// </summary>
        /// <param name="resourceString"></param>
        /// <param name="args"></param>
        internal  void WriteLinePrettyFromResource(string resourceString, params object[] args)
        {
            int indentLevel = IsVerbosityAtLeast(LoggerVerbosity.Normal) ? this.currentIndentLevel : 0;
            WriteLinePrettyFromResource(indentLevel, resourceString, args);
        }

        /// <summary>
        /// Writes a line from a resource string to the log, using the specified indentation.
        /// </summary>
        internal  void WriteLinePrettyFromResource(int indentLevel, string resourceString, params object[] args)
        {
            string formattedString = ResourceUtilities.FormatResourceString(resourceString, args);
            WriteLinePretty(indentLevel, formattedString);
        }

        /// <summary>
        /// Writes to the log, using the default indentation. Does not 
        /// terminate with a newline.
        /// </summary>
        internal  void WritePretty(string formattedString)
        {
            int indentLevel = IsVerbosityAtLeast(LoggerVerbosity.Normal) ? this.currentIndentLevel : 0;
            WritePretty(indentLevel, formattedString);
        }

        /// <summary>
        /// If requested, display a performance summary at the end of the build.  This
        /// shows how much time (and # hits) were spent inside of each project, target,
        /// and task.
        /// </summary>
        internal void ShowPerfSummary()
        {
            // Show project performance summary.
            if (projectPerformanceCounters != null)
            {
                setColor(ConsoleColor.Green);
                WriteNewLine();
                WriteLinePrettyFromResource("ProjectPerformanceSummary");

                setColor(ConsoleColor.Gray);
                DisplayCounters(projectPerformanceCounters);
            }

            // Show target performance summary.
            if (targetPerformanceCounters != null)
            {
                setColor(ConsoleColor.Green);
                WriteNewLine();
                WriteLinePrettyFromResource("TargetPerformanceSummary");

                setColor(ConsoleColor.Gray);
                DisplayCounters(targetPerformanceCounters);
            }

            // Show task performance summary.
            if (taskPerformanceCounters != null)
            {
                setColor(ConsoleColor.Green);
                WriteNewLine();
                WriteLinePrettyFromResource("TaskPerformanceSummary");

                setColor(ConsoleColor.Gray);
                DisplayCounters(taskPerformanceCounters);
            }

            resetColor();
        }

        /// <summary>
        /// Writes to the log, using the specified indentation. Does not 
        /// terminate with a newline.
        /// </summary>
        internal void WritePretty(int indentLevel, string formattedString)
        {
            StringBuilder result = new StringBuilder();
            result.Append(' ', indentLevel * tabWidth).Append(formattedString);
            write(result.ToString());
        }

        /// <summary>
        /// Writes a line to the log, using the default indentation.
        /// </summary>
        /// <param name="formattedString"></param>
        internal void WriteLinePretty(string formattedString)
        {
            int indentLevel = IsVerbosityAtLeast(LoggerVerbosity.Normal) ? this.currentIndentLevel : 0;
            WriteLinePretty(indentLevel, formattedString);
        }

        /// <summary>
        /// Writes a line to the log, using the specified indentation.
        /// </summary>
        internal void WriteLinePretty(int indentLevel, string formattedString)
        {
            indentLevel = indentLevel > 0 ? indentLevel : 0;
            write(IndentString(formattedString, indentLevel * tabWidth));
        }

        /// <summary>
        /// Check to see what kind of device we are outputting the log to, is it a character device, a file, or something else
        /// this can be used by loggers to modify their outputs based on the device they are writing to
        /// </summary>
        internal void IsRunningWithCharacterFileType()
        {
            // Get the std out handle
            IntPtr stdHandle = Microsoft.Build.BuildEngine.Shared.NativeMethods.GetStdHandle(Microsoft.Build.BuildEngine.Shared.NativeMethods.STD_OUTPUT_HANDLE);

            if (stdHandle != Microsoft.Build.BuildEngine.NativeMethods.InvalidHandle)
            {
                uint fileType = Microsoft.Build.BuildEngine.Shared.NativeMethods.GetFileType(stdHandle);

                // The std out is a char type(LPT or Console)
                runningWithCharacterFileType = fileType == Microsoft.Build.BuildEngine.Shared.NativeMethods.FILE_TYPE_CHAR;
            }
            else
            {
                runningWithCharacterFileType = false;
            }
        }

        /// <summary>
        /// Determines whether the current verbosity setting is at least the value
        /// passed in.
        /// </summary>
        internal bool IsVerbosityAtLeast(LoggerVerbosity checkVerbosity)
        {
            return this.verbosity >= checkVerbosity;
        }

        /// <summary>
        /// Sets foreground color to color specified
        /// </summary>
        internal static void SetColor(ConsoleColor c)
        {
            Console.ForegroundColor =
                        TransformColor(c, Console.BackgroundColor);
        }

        /// <summary>
        /// Changes the foreground color to black if the foreground is the
        /// same as the background. Changes the foreground to white if the
        /// background is black.
        /// </summary>
        /// <param name="foreground">foreground color for black</param>
        /// <param name="background">current background</param>
        internal static ConsoleColor TransformColor(ConsoleColor foreground,
                                                   ConsoleColor background)
        {
            ConsoleColor result = foreground; //typically do nothing ...

            if (foreground == background)
            {
                if (background != ConsoleColor.Black)
                {
                    result = ConsoleColor.Black;
                }
                else
                {
                    result = ConsoleColor.Gray;
                }
            }

            return result;
        }

        /// <summary>
        /// Does nothing, meets the ColorSetter delegate type
        /// </summary>
        /// <param name="c">foreground color (is ignored)</param>
        internal static void DontSetColor(ConsoleColor c)
        {
            // do nothing...
        }

        /// <summary>
        /// Does nothing, meets the ColorResetter delegate type
        /// </summary>
        internal static void DontResetColor()
        {
            // do nothing...
        }

        internal void InitializeConsoleMethods(LoggerVerbosity logverbosity, WriteHandler logwriter, ColorSetter colorSet, ColorResetter colorReset)
        {
            this.verbosity = logverbosity;
            this.write = logwriter;
            IsRunningWithCharacterFileType();
            // This is a workaround, because the Console class provides no way to check that a color
            // can actually be set or not. Color cannot be set if the console has been redirected
            // in certain ways (e.g. how BUILD.EXE does it)
            bool canSetColor = true;

            try
            {
                ConsoleColor c = Console.BackgroundColor;
            }
            catch (IOException)
            {
                // If the attempt to set a color fails with an IO exception then it is
                // likely that the console has been redirected in a way that cannot
                // cope with color (e.g. BUILD.EXE) so don't try to do color again.
                canSetColor = false;
            }

            if ((colorSet != null) && canSetColor)
            {
                this.setColor = colorSet;
            }
            else
            {
                this.setColor = new ColorSetter(DontSetColor);
            }

            if ((colorReset != null) && canSetColor)
            {
                this.resetColor = colorReset;
            }
            else
            {
                this.resetColor = new ColorResetter(DontResetColor);
            }
        }

        /// <summary>
        /// Writes out the list of property names and their values.
        /// This could be done at any time during the build to show the latest
        /// property values, using the cached reference to the list from the 
        /// appropriate ProjectStarted event.
        /// </summary>
        /// <param name="properties">List of properties</param>
        internal void WriteProperties(ArrayList properties)
        {
            if (Verbosity == LoggerVerbosity.Diagnostic && showItemAndPropertyList)
            {
                if (properties.Count == 0)
                {
                    return;
                }

                OutputProperties(properties);
                // Add a blank line
                WriteNewLine();
            }
        }

        /// <summary>
        /// Generate an arraylist which contains the properties referenced
        /// by the properties enumerable object
        /// </summary>
        internal ArrayList ExtractPropertyList(IEnumerable properties)
        {
            // Gather a sorted list of all the properties.
            ArrayList list = new ArrayList();
            foreach (DictionaryEntry prop in properties)
            {
                list.Add(prop);
            }
            list.Sort(new DictionaryEntryKeyComparer());
            return list;
        }

        internal virtual void OutputProperties(ArrayList list)
        {
            // Write the banner
            setColor(ConsoleColor.Green);
            WriteLinePretty(currentIndentLevel, ResourceUtilities.FormatResourceString("PropertyListHeader"));
            // Write each property name and its value, one per line
            foreach (DictionaryEntry prop in list)
            {
                setColor(ConsoleColor.Gray);
                WritePretty(String.Format(CultureInfo.CurrentCulture, "{0,-30} = ", prop.Key));
                setColor(ConsoleColor.DarkGray);
                WriteLinePretty((string)(prop.Value));
            }
            resetColor();
        }

        /// <summary>
        /// Writes out the list of item specs and their metadata.
        /// This could be done at any time during the build to show the latest
        /// items, using the cached reference to the list from the 
        /// appropriate ProjectStarted event.
        /// </summary>
        /// <param name="items">List of items</param>
        internal void WriteItems(SortedList itemTypes)
        {
            if (Verbosity == LoggerVerbosity.Diagnostic && showItemAndPropertyList)
            {
                if (itemTypes.Count == 0)
                {
                    return;
                }

                // Write the banner
                setColor(ConsoleColor.Green);
                WriteLinePretty(currentIndentLevel, ResourceUtilities.FormatResourceString("ItemListHeader"));

                // Write each item type and its itemspec, one per line
                foreach (DictionaryEntry entry in itemTypes)
                {
                    string itemType = (string)entry.Key;
                    ArrayList itemTypeList = (ArrayList)entry.Value;

                    if (itemTypeList.Count == 0)
                    {
                        continue;
                    }

                    // Sort the list by itemSpec
                    itemTypeList.Sort(new ITaskItemItemSpecComparer());
                    OutputItems(itemType, itemTypeList);
                }

                // Add a blank line
                WriteNewLine();
            }
        }

        /// <summary>
        /// Extract the Items from the enumerable object and return a sorted list containing these items
        /// </summary>
        internal SortedList ExtractItemList(IEnumerable items)
        {
            // The "items" list is a flat list of itemtype-ITaskItem pairs.
            // We would like to organize the ITaskItems into groups by itemtype.

            // Use a SortedList instead of an ArrayList (because we need to lookup fast)
            // and instead of a Hashtable (because we need to sort it)
            SortedList itemTypes = new SortedList(CaseInsensitiveComparer.Default);
            foreach (DictionaryEntry item in items)
            {
                // Create a new list for this itemtype, if we haven't already
                if (itemTypes[(string)item.Key] == null)
                {
                    itemTypes[(string)item.Key] = new ArrayList();
                }

                // Add the item to the list for its itemtype
                ArrayList itemsOfAType = (ArrayList)itemTypes[(string)item.Key];
                itemsOfAType.Add(item.Value);
            }

            return itemTypes;
        }

        internal virtual void OutputItems(string itemType, ArrayList itemTypeList)
        {
            // Write each item, one per line
            bool haveWrittenItemType = false;
            setColor(ConsoleColor.DarkGray);
            foreach (ITaskItem item in itemTypeList)
            {
                if (!haveWrittenItemType)
                {
                    setColor(ConsoleColor.Gray);
                    WriteLinePretty(itemType);
                    haveWrittenItemType = true;
                    setColor(ConsoleColor.DarkGray);
                }
                WriteLinePretty("    "  /* indent slightly*/ + item.ItemSpec);

                // We could log the metadata for the item here. We choose not to do that, because
                // at present there is no way to get only the "custom" meta-data, so the output
                // would be cluttered with the "built-in" meta-data.
            }
            resetColor();
        }

        /// <summary>
        /// Returns a performance counter for a given scope (either task name or target name)
        /// from the given table.
        /// </summary>
        /// <param name="scopeName">Task name or target name.</param>
        /// <param name="table">Table that has tasks or targets.</param>
        /// <returns></returns>
        internal static PerformanceCounter GetPerformanceCounter(string scopeName, ref Hashtable table)
        {
            // Lazily construct the performance counter table.
            if (table == null)
            {
                table = new Hashtable(StringComparer.OrdinalIgnoreCase);
            }

            PerformanceCounter counter = (PerformanceCounter)table[scopeName];

            // And lazily construct the performance counter itself.
            if (counter == null)
            {
                counter = new PerformanceCounter(scopeName);
                table[scopeName] = counter;
            }

            return counter;
        }

        /// <summary>
        /// Display the timings for each counter in the hashtable
        /// </summary>
        /// <param name="counters"></param>
        internal void DisplayCounters(Hashtable counters)
        {
            ArrayList perfCounters = new ArrayList(counters.Values.Count);
            perfCounters.AddRange(counters.Values);

            perfCounters.Sort(PerformanceCounter.DescendingByElapsedTimeComparer);

            bool reentrantCounterExists = false;

            WriteLinePrettyFromResourceDelegate lineWriter = new WriteLinePrettyFromResourceDelegate(WriteLinePrettyFromResource);

            foreach (PerformanceCounter counter in perfCounters)
            {
                if (counter.ReenteredScope)
                {
                    reentrantCounterExists = true;
                }
 
                counter.PrintCounterMessage(lineWriter, setColor, resetColor);
            }

            if (reentrantCounterExists)
            {
                // display an explanation of why there was no value displayed
                WriteLinePrettyFromResource(4, "PerformanceReentrancyNote");
            }
        }

        /// <summary>
        /// Records performance information consumed by a task or target.
        /// </summary>
        internal class PerformanceCounter
        {
            protected string scopeName = String.Empty;
            protected int calls = 0;
            protected TimeSpan elapsedTime = new TimeSpan(0);
            protected bool inScope = false;
            protected DateTime scopeStartTime;
            protected bool reenteredScope = false;

            /// <summary>
            /// Construct.
            /// </summary>
            /// <param name="scopeName"></param>
            internal PerformanceCounter(string scopeName)
            {
                this.scopeName = scopeName;
            }

            /// <summary>
            /// Name of the scope.
            /// </summary>
            internal string ScopeName
            {
                get { return scopeName; }
            }

            /// <summary>
            /// Total number of calls so far.
            /// </summary>
            internal int Calls
            {
                get { return calls; }
            }

            /// <summary>
            /// Total accumalated time so far.
            /// </summary>
            internal TimeSpan ElapsedTime
            {
                get { return elapsedTime; }
            }

            /// <summary>
            /// Whether or not this scope was reentered. Timing information is not recorded in these cases.
            /// </summary>
            internal bool ReenteredScope
            {
                get { return reenteredScope; }
            }

            /// <summary>
            /// Whether or not this task or target is executing right now.
            /// </summary>
            internal  bool InScope 
            {
                get { return inScope; }
                set
                {
                    if (!reenteredScope)
                    {
                        if (InScope && !value)
                        {
                            // Edge meaning scope is finishing.
                            inScope = false;

                            elapsedTime += (System.DateTime.Now - scopeStartTime);
                        }
                        else if (!InScope && value)
                        {
                            // Edge meaning scope is starting.
                            inScope = true;

                            ++calls;
                            scopeStartTime = System.DateTime.Now;
                        }
                        else
                        {
                            // Should only happen when a scope is reentrant.
                            // We don't track these numbers.
                            reenteredScope = true;
                        }
                    }
                }
            }

            internal virtual void PrintCounterMessage(WriteLinePrettyFromResourceDelegate WriteLinePrettyFromResource, ColorSetter setColor, ColorResetter resetColor)
            {
                    string time;
                    if (!reenteredScope)
                    {
                        // round: submillisecond values are not meaningful
                        time = String.Format(CultureInfo.CurrentCulture,
                            "{0,5}", Math.Round(elapsedTime.TotalMilliseconds, 0));
                    }
                    else
                    {
                        // no value available; instead display an asterisk
                        time = "    *";
                    }

                    WriteLinePrettyFromResource
                        (
                            2,
                            "PerformanceLine",
                            time,
                            String.Format(CultureInfo.CurrentCulture,
                                    "{0,-40}" /* pad to 40 align left */, scopeName),
                            String.Format(CultureInfo.CurrentCulture,
                                    "{0,3}", calls)
                        );
            }

            /// <summary>
            /// Returns an IComparer that will put erformance counters 
            /// in descending order by elapsed time.
            /// </summary>
            static internal IComparer DescendingByElapsedTimeComparer
            {
                get { return new DescendingByElapsedTime(); }
            }

            /// <summary>
            /// Private IComparer class for sorting performance counters 
            /// in descending order by elapsed time.
            /// </summary>
            internal class DescendingByElapsedTime : IComparer
            {
                /// <summary>
                /// Compare two PerformanceCounters.
                /// </summary>
                /// <param name="o1"></param>
                /// <param name="o2"></param>
                /// <returns></returns>
                public int Compare(object o1, object o2)
                {
                    PerformanceCounter p1 = (PerformanceCounter)o1;
                    PerformanceCounter p2 = (PerformanceCounter)o2;

                    // don't compare reentrant counters, time is incorrect
                    // and we want to sort them first
                    if (!p1.reenteredScope && !p2.reenteredScope)
                    {
                        return TimeSpan.Compare(p1.ElapsedTime, p2.ElapsedTime);
                    }
                    else if (p1.Equals(p2))
                    {
                        return 0;
                    }
                    else if (p1.reenteredScope)
                    {
                        // p1 was reentrant; sort first
                        return -1;
                    }
                    else
                    {
                        // p2 was reentrant; sort first
                        return 1;
                    }
                }
            }
        }

        #region eventHandlers

        public virtual void Shutdown()
        {
            // do nothing
        }

        internal abstract void ResetConsoleLoggerState();

        public virtual void Initialize(IEventSource eventSource, int nodeCount)
        {
            numberOfProcessors = nodeCount;
            Initialize(eventSource);
        }

        /// <summary>
        /// Signs up the console logger for all build events.
        /// </summary>
        /// <param name="eventSource">Available events.</param>
        public virtual void Initialize(IEventSource eventSource)
        {
            ParseParameters();

            // Always show perf summary for diagnostic verbosity.
            if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic))
            {
                this.showPerfSummary = true;
            }

            // If not specifically instructed otherwise, show a summary in normal
            // and higher verbosities.
            if (showSummary == null && IsVerbosityAtLeast(LoggerVerbosity.Normal))
            {
                this.showSummary = true;
            }

            if (showOnlyWarnings || showOnlyErrors)
            {
                this.showSummary = false;
                this.showPerfSummary = false;
            }

            // Put this after reading the parameters, since it may want to initialize something
            // specially based on some parameter value. For example, choose whether to have a summary, based
            // on the verbosity.
            ResetConsoleLoggerState();

            // Event source is allowed to be null; this allows the logger to be wrapped by a class that wishes
            // to call its event handlers directly. The VS HostLogger does this.
            if (eventSource != null)
            {
                eventSource.BuildStarted +=
                         BuildStartedHandler;
                eventSource.BuildFinished +=
                         BuildFinishedHandler;
                eventSource.ProjectStarted +=
                         ProjectStartedHandler;
                eventSource.ProjectFinished +=
                         ProjectFinishedHandler;
                eventSource.TargetStarted +=
                         TargetStartedHandler;
                eventSource.TargetFinished +=
                         TargetFinishedHandler;
                eventSource.TaskStarted +=
                         TaskStartedHandler;
                eventSource.TaskFinished +=
                         TaskFinishedHandler;

                eventSource.ErrorRaised +=
                         ErrorHandler;
                eventSource.WarningRaised +=
                         WarningHandler;
                eventSource.MessageRaised +=
                         MessageHandler;

                eventSource.CustomEventRaised +=
                         CustomEventHandler;
            }
        }

        /// <summary>
        /// Apply a logger parameter.
        /// parameterValue may be null, if there is no parameter value.
        /// </summary>
        internal virtual bool ApplyParameter(string parameterName, string parameterValue)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parameterName, nameof(parameterName));

            switch (parameterName.ToUpperInvariant())
            {
                case "PERFORMANCESUMMARY":
                    showPerfSummary = true;
                    return true;
                case "NOSUMMARY":
                    showSummary = false;
                    return true;
                case "SUMMARY":
                    showSummary = true;
                    return true;
                case "NOITEMANDPROPERTYLIST":
                    showItemAndPropertyList = false;
                    return true;
                case "WARNINGSONLY":
                    showOnlyWarnings = true;
                    return true;
                case "ERRORSONLY":
                    showOnlyErrors = true;
                    return true;
                case "V":
                case "VERBOSITY":
                    {
                        switch (parameterValue.ToUpperInvariant())
                        {
                            case "Q":
                            case "QUIET":
                                verbosity = LoggerVerbosity.Quiet;
                                return true;
                            case "M":
                            case "MINIMAL":
                                verbosity = LoggerVerbosity.Minimal;
                                return true;
                            case "N":
                            case "NORMAL":
                                verbosity = LoggerVerbosity.Normal;
                                return true;
                            case "D":
                            case "DETAILED":
                                verbosity = LoggerVerbosity.Detailed;
                                return true;
                            case "DIAG":
                            case "DIAGNOSTIC":
                                verbosity = LoggerVerbosity.Diagnostic;
                                return true;
                            default:
                                string errorCode;
                                string helpKeyword;
                                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidVerbosity", parameterValue);
                                throw new LoggerException(message, null, errorCode, helpKeyword);
                        }
                    }
            }

            return false;
        }

        public abstract void BuildStartedHandler(object sender, BuildStartedEventArgs e);

        public abstract  void BuildFinishedHandler(object sender, BuildFinishedEventArgs e);

        public abstract void ProjectStartedHandler(object sender, ProjectStartedEventArgs e);

        public abstract void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs e);

        public abstract void TargetStartedHandler(object sender, TargetStartedEventArgs e);

        public abstract void TargetFinishedHandler(object sender, TargetFinishedEventArgs e);

        public abstract void TaskStartedHandler(object sender, TaskStartedEventArgs e);

        public abstract void TaskFinishedHandler(object sender, TaskFinishedEventArgs e);

        public abstract void ErrorHandler(object sender, BuildErrorEventArgs e);

        public abstract void WarningHandler(object sender, BuildWarningEventArgs e);

        public abstract void MessageHandler(object sender, BuildMessageEventArgs e);

        public abstract void CustomEventHandler(object sender, CustomBuildEventArgs e);

        #endregion

        #region Internal member data

        /// <summary>
        /// Controls the amount of text displayed by the logger
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal LoggerVerbosity verbosity = LoggerVerbosity.Normal;

        /// <summary>
        /// Time the build started
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal DateTime buildStarted;

        /// <summary>
        /// Delegate used to write text
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal WriteHandler write = null;

        /// <summary>
        /// Delegate used to change text color.
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal  ColorSetter setColor = null;

        /// <summary>
        /// Delegate used to reset text color
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal  ColorResetter resetColor = null;

        /// <summary>
        /// Indicates if project header should not be displayed.
        /// </summary>
        internal bool skipProjectStartedText = false;

        /// <summary>
        /// Number of spaces that each level of indentation is worth
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal const int tabWidth = 2;

        /// <summary>
        /// Keeps track of the current indentation level.
        /// </summary>
        /// <owner>RGoel</owner>
        internal int currentIndentLevel = 0;

        /// <summary>
        /// The kinds of newline breaks we expect.
        /// </summary>
        /// <remarks>Currently we're not supporting "\r".</remarks>
        internal static readonly string[] newLines = { "\r\n", "\n" };

        /// <summary>
        /// Visual separator for projects. Line length was picked arbitrarily.
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal const string projectSeparatorLine =
                 "__________________________________________________";

        /// <summary>
        /// Console logger parameters.
        /// </summary>
        internal string loggerParameters = null;

        /// <summary>
        /// Console logger parameters delimiters.
        /// </summary>
        internal static readonly char[] parameterDelimiters = { ';' };

        /// <summary>
        /// Console logger parameter value split character.
        /// </summary>
        private static readonly char[] parameterValueSplitCharacter = { '=' };

        /// <summary>
        /// Console logger should show error and warning summary at the end of build?
        /// If null, user has made no indication.
        /// </summary>
        private bool? showSummary;

        /// <summary>
        /// When true, accumulate performance numbers.
        /// </summary>
        internal bool showPerfSummary = false;

        /// <summary>
        /// When true, show the list of item and property values at the start of each project
        /// </summary>
        internal bool showItemAndPropertyList = true;

        /// <summary>
        /// When true, suppresses all messages except for warnings. (And possibly errors, if showOnlyErrors is true.)
        /// </summary>
        protected bool showOnlyWarnings;

        /// <summary>
        /// When true, suppresses all messages except for errors. (And possibly warnings, if showOnlyWarnings is true.)
        /// </summary>
        protected bool showOnlyErrors;

        internal bool ignoreLoggerErrors = true;

        internal bool runningWithCharacterFileType = false;

        #region Per-build Members
        internal int numberOfProcessors = 1;
        /// <summary>
        /// Number of errors encountered in this build
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal int errorCount = 0;

        /// <summary>
        /// Number of warnings encountered in this build
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        internal int warningCount = 0;

        /// <summary>
        /// A list of the errors that have occurred during this build.
        /// </summary>
        internal ArrayList errorList;

        /// <summary>
        /// A list of the warnings that have occurred during this build.
        /// </summary>
        internal ArrayList warningList;

        /// <summary>
        /// Accumulated project performance information.
        /// </summary>
        internal Hashtable projectPerformanceCounters;

        /// <summary>
        /// Accumulated target performance information.
        /// </summary>
        internal Hashtable targetPerformanceCounters;

        /// <summary>
        /// Accumulated task performance information.
        /// </summary>
        internal Hashtable taskPerformanceCounters;

        #endregion

        #endregion
    }
}
