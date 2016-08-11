// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class implements the default logger that outputs event data
    /// to the console (stdout).
    /// </summary>
    /// <remarks>This class is not thread safe.</remarks>
    internal class ParallelConsoleLogger : BaseConsoleLogger
    {
        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ParallelConsoleLogger()
            : this(LoggerVerbosity.Normal)
        {
            // do nothing
        }

        /// <summary>
        /// Create a logger instance with a specific verbosity.  This logs to
        /// the default console.
        /// </summary>
        public ParallelConsoleLogger(LoggerVerbosity verbosity)
            :
            this
            (
                verbosity,
                new WriteHandler(Console.Out.Write),
                new ColorSetter(SetColor),
                new ColorResetter(Console.ResetColor)
            )
        {
            // do nothing
        }

        /// <summary>
        /// Initializes the logger, with alternate output handlers.
        /// </summary>
        public ParallelConsoleLogger
        (
            LoggerVerbosity verbosity,
            WriteHandler write,
            ColorSetter colorSet,
            ColorResetter colorReset
        )
        {
            InitializeConsoleMethods(verbosity, write, colorSet, colorReset);
            deferredMessages = new Dictionary<BuildEventContext, List<BuildMessageEventArgs>>(compareContextNodeId);
            buildEventManager = new BuildEventManager();
        }

        /// <summary>
        /// Check to see if the console is going to a char output such as a console,printer or com port, or if it going to a file
        /// </summary>
        private void CheckIfOutputSupportsAlignment()
        {
            alignMessages = false;
            bufferWidth = -1;

            // If forceNoAlign is set there is no point getting the console width as there will be no aligning of the text
            if (!forceNoAlign)
            {
                if (runningWithCharacterFileType)
                {
                    // Get the size of the console buffer so messages can be formatted to the console width
                    bufferWidth = Console.BufferWidth;
                    alignMessages = true;
                }
                else
                {
                    alignMessages = false;
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Allows the logger to take action based on a parameter passed on when initializing the logger
        /// </summary>
        internal override bool ApplyParameter(string parameterName, string parameterValue)
        {
            if (base.ApplyParameter(parameterName, parameterValue))
            {
                return true;
            }
            if (0 == String.Compare(parameterName, "SHOWCOMMANDLINE", StringComparison.OrdinalIgnoreCase))
            {
                showCommandline = true;
                return true;
            }
            else if (0 == String.Compare(parameterName, "SHOWTIMESTAMP", StringComparison.OrdinalIgnoreCase))
            {
                showTimeStamp = true;
                return true;
            }
            else if (0 == String.Compare(parameterName, "SHOWEVENTID", StringComparison.OrdinalIgnoreCase))
            {
                showEventId = true;
                return true;
            }
            else if (0 == String.Compare(parameterName, "FORCENOALIGN", StringComparison.OrdinalIgnoreCase))
            {
                forceNoAlign = true;
                alignMessages = false;
                return true;
            }
            return false;
        }

        public override void Initialize(IEventSource eventSource)
        {
            // If the logger is being used in singleproc do not show EventId after each message unless it is set as part of a console parameter
            if (numberOfProcessors == 1)
            {
                showEventId = false;
            }

            // Parameters are parsed in Initialize
            base.Initialize(eventSource);
            CheckIfOutputSupportsAlignment();
        }

        /// <summary>
        /// Keep track of the last event displayed so target names can be displayed at the correct time
        /// </summary>
        private void ShownBuildEventContext(BuildEventContext e)
        {
            lastDisplayedBuildEventContext = e;
        }

        /// <summary>
        /// Reset the states of per-build member variables
        /// VSW#516376 
        /// </summary>
        internal override void ResetConsoleLoggerState()
        {
            if (ShowSummary)
            {
                errorList = new ArrayList();
                warningList = new ArrayList();
            }
            else
            {
                errorList = null;
                warningList = null;
            }

            errorCount = 0;
            warningCount = 0;
            projectPerformanceCounters = null;
            targetPerformanceCounters = null;
            taskPerformanceCounters = null;
            hasBuildStarted = false;

            // Reset the two data structures created when the logger was created
            buildEventManager = new BuildEventManager();
            deferredMessages = new Dictionary<BuildEventContext, List<BuildMessageEventArgs>>(compareContextNodeId);
            prefixWidth = 0;
            lastDisplayedBuildEventContext = null;
        }

        /// <summary>
        /// Handler for build started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void BuildStartedHandler(object sender, BuildStartedEventArgs e)
        {
            buildStarted = e.Timestamp;
            hasBuildStarted = true;

            if (showOnlyErrors || showOnlyWarnings) return; 

            if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
            {
                WriteLinePrettyFromResource("BuildStartedWithTime", e.Timestamp);
            }
        }

        /// <summary>
        /// Handler for build finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void BuildFinishedHandler(object sender, BuildFinishedEventArgs e)
        {
            if (!showOnlyErrors && !showOnlyWarnings)
            {
                // If for some reason we have deferred messages at the end of the build they should be displayed
                // so that the reason why they are still buffered can be determined
                if (deferredMessages.Count > 0)
                {
                    if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
                    {
                        // Print out all of the deferred messages
                        WriteLinePrettyFromResource("DeferredMessages");
                        foreach (List<BuildMessageEventArgs> messageList in deferredMessages.Values)
                        {
                            foreach (BuildMessageEventArgs message in messageList)
                            {
                                PrintMessage(message, false);
                            }
                        }
                    }
                    else if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
                    {
                        // In normal vervosity we do not want to print out the deferred messages but we do want
                        // to let the users know that there were deferred messages to be seen
                        WriteLinePrettyFromResource("DeferredMessagesAvailiable");
                    }
                }

                // Show the performance summary iff the verbosity is diagnostic or the user specifically asked for it
                // with a logger parameter.
                if (this.showPerfSummary)
                {
                    ShowPerfSummary();
                }

                // if verbosity is normal, detailed or diagnostic
                if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
                {
                    if (e.Succeeded)
                    {
                        setColor(ConsoleColor.Green);
                    }

                    // Write the "Build Finished" event.
                    WriteNewLine();
                    WriteLinePretty(e.Message);
                    resetColor();
                }

                // The decision whether or not to show a summary at this verbosity
                // was made during initalization. We just do what we're told.
                if (ShowSummary)
                {
                    // We can't display a nice nested summary unless we're at Normal or above,
                    // since we need to have gotten TargetStarted events, which aren't forwarded otherwise.
                    if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
                    {
                        ShowNestedErrorWarningSummary();

                        // Emit text like:
                        //     1 Warning(s)
                        //     0 Error(s)
                        // Don't color the line if it's zero. (Per Whidbey behavior.)
                        if (warningCount > 0)
                        {
                            setColor(ConsoleColor.Yellow);
                        }
                        WriteLinePrettyFromResource(2, "WarningCount", warningCount);
                        resetColor();

                        if (errorCount > 0)
                        {
                            setColor(ConsoleColor.Red);
                        }
                        WriteLinePrettyFromResource(2, "ErrorCount", errorCount);
                        resetColor();
                    }
                    else
                    {
                        ShowFlatErrorWarningSummary();
                    }
                }

                // if verbosity is normal, detailed or diagnostic
                if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
                {
                    // The time elapsed is the difference between when the BuildStartedEventArg 
                    // was created and when the BuildFinishedEventArg was created
                    string timeElapsed = LogFormatter.FormatTimeSpan(e.Timestamp - buildStarted);

                    WriteNewLine();
                    WriteLinePrettyFromResource("TimeElapsed", timeElapsed);
                }
            }

           ResetConsoleLoggerState();
           CheckIfOutputSupportsAlignment();
        }

        /// <summary>
        /// At the end of the build, repeats the errors and warnings that occurred 
        /// during the build, and displays the error count and warning count.
        /// Does this in a "flat" style, without context.
        /// </summary>
        private void ShowFlatErrorWarningSummary()
        {
            if (warningList.Count == 0 && errorList.Count == 0) return;

            // If we're showing only warnings and/or errors, don't summarize.
            // This is the buildc.err case. There's no point summarizing since we'd just 
            // repeat the entire log content again.
            if (showOnlyErrors || showOnlyWarnings) return;

            // Make some effort to distinguish this summary from the output above, since otherwise
            // it's not clear in lower verbosities
            WriteNewLine();

            if (warningList.Count > 0)
            {
                setColor(ConsoleColor.Yellow);
                foreach (BuildWarningEventArgs warning in warningList)
                {
                    WriteMessageAligned(EventArgsFormatting.FormatEventMessage(warning, runningWithCharacterFileType), true);
                }
            }

            if (errorList.Count > 0)
            {
                setColor(ConsoleColor.Red);
                foreach (BuildErrorEventArgs error in errorList)
                {
                    WriteMessageAligned(EventArgsFormatting.FormatEventMessage(error, runningWithCharacterFileType), true);
                }
            }

            resetColor();
        }

        /// <summary>
        /// At the end of the build, repeats the errors and warnings that occurred 
        /// during the build, and displays the error count and warning count.
        /// Does this in a "nested" style.
        /// </summary>
        private void ShowNestedErrorWarningSummary()
        {
            if (warningList.Count == 0 && errorList.Count == 0) return;

            // If we're showing only warnings and/or errors, don't summarize.
            // This is the buildc.err case. There's no point summarizing since we'd just 
            // repeat the entire log content again.
            if (showOnlyErrors || showOnlyWarnings) return;

            if (warningCount > 0)
            {
                setColor(ConsoleColor.Yellow);
                ShowErrorWarningSummary<BuildWarningEventArgs>(warningList);
            }

            if (errorCount > 0)
            {
                setColor(ConsoleColor.Red);
                ShowErrorWarningSummary<BuildErrorEventArgs>(errorList);
            }

            resetColor();
        }

        private void ShowErrorWarningSummary<T>(ArrayList listToProcess) where T : BuildEventArgs
        {
            // Group the build warning event args based on the entry point and the target in which the warning occurred
            Dictionary<ErrorWarningSummaryDictionaryKey, List<T>> groupByProjectEntryPoint = new Dictionary<ErrorWarningSummaryDictionaryKey, List<T>>();

            // Loop through each of the warnings and put them into the correct buckets
            for (int listCount = 0; listCount < listToProcess.Count; listCount++)
            {

                T errorWarningEventArgs = (T)listToProcess[listCount];

                // Target event may be null for a couple of reasons:
                // 1) If the event was from a project load, or engine 
                // 2) If the flushing of the event queue for each request and result is turned off
                // as this could cause errors and warnings to be seen by the logger after the target finished event
                // which would cause the error or warning to have no matching target started event as they are removed
                // when a target finished event is logged.
                // 3) On NORMAL verbosity if the error or warning occurres in a project load then the error or warning and the target started event will be forwarded to 
                // different forwarding loggers which cannot communicate to each other, meaning there will be no matching target started event logged 
                // as the forwarding logger did not know to forward the target started event
                string targetName = null;
                TargetStartedEventMinimumFields targetEvent = buildEventManager.GetTargetStartedEvent(errorWarningEventArgs.BuildEventContext);

                if (targetEvent != null)
                {
                    targetName = targetEvent.TargetName;
                }

                // Create a new key from the error event context and the target where the error happened
                ErrorWarningSummaryDictionaryKey key = new ErrorWarningSummaryDictionaryKey(errorWarningEventArgs.BuildEventContext, targetName);

                // Check to see if there is a bucket for the warning
                if (!groupByProjectEntryPoint.ContainsKey(key))
                {
                    // If there is no bucket create a new one which contains a list of all the errors which
                    // happened for a given buildEventContext / target
                    List<T> errorWarningEventListByTarget = new List<T>();
                    groupByProjectEntryPoint.Add(key, errorWarningEventListByTarget);
                }

                // Add the error event to the correct bucket
                groupByProjectEntryPoint[key].Add(errorWarningEventArgs);
            }

            BuildEventContext previousEntryPoint = null;
            string previousTarget = null;
            // Loop through each of the bucket and print out the stack trace information for the errors
            foreach (KeyValuePair<ErrorWarningSummaryDictionaryKey, List<T>> valuePair in groupByProjectEntryPoint)
            {
                //If the project entrypoint where the error occurred is the same as the previous message do not print the
                // stack trace again
                if (previousEntryPoint != valuePair.Key.EntryPointContext)
                {
                    WriteNewLine();
                    foreach (string s in buildEventManager.ProjectCallStackFromProject(valuePair.Key.EntryPointContext))
                    {
                        WriteMessageAligned(s, false);
                    }
                    previousEntryPoint = valuePair.Key.EntryPointContext;
                }

                //If the target where the error occurred is the same as the previous message do not print the location
                // where the error occurred again
                if (String.Compare(previousTarget, valuePair.Key.TargetName, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    //If no targetName was specified then do not show the target where the error occurred
                    if (! string.IsNullOrEmpty(valuePair.Key.TargetName))
                    {
                        WriteMessageAligned(ResourceUtilities.FormatResourceString("ErrorWarningInTarget", valuePair.Key.TargetName), false);
                    }
                    previousTarget = valuePair.Key.TargetName;
                }

                // Print out all of the errors under the ProjectEntryPoint / target
                foreach (T errorWarningEvent in valuePair.Value)
                {
                    if (errorWarningEvent is BuildErrorEventArgs)
                    {
                        WriteMessageAligned("  " + EventArgsFormatting.FormatEventMessage(errorWarningEvent as BuildErrorEventArgs, runningWithCharacterFileType), false);
                    }
                    else if (errorWarningEvent is BuildWarningEventArgs)
                    {
                        WriteMessageAligned("  " + EventArgsFormatting.FormatEventMessage(errorWarningEvent as BuildWarningEventArgs, runningWithCharacterFileType), false);
                    }
                }
                WriteNewLine();
            }
        }

        /// <summary>
        /// Handler for project started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void ProjectStartedHandler(object sender, ProjectStartedEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            ErrorUtilities.VerifyThrowArgumentNull(e.ParentProjectBuildEventContext, "ParentProjectBuildEventContext");
           
            // Add the project to the BuildManager so we can use the start information later in the build process
            buildEventManager.AddProjectStartedEvent(e);

            if (this.showPerfSummary)
            {
                // Create a new project performance counter for this project
                MPPerformanceCounter counter = GetPerformanceCounter(e.ProjectFile, ref projectPerformanceCounters);
                counter.AddEventStarted(e.TargetNames, e.BuildEventContext, e.Timestamp, compareContextNodeId);
            }

            // If there were deferred messages then we should show them now, this will cause the project started event to be shown properly
            if (deferredMessages.ContainsKey(e.BuildEventContext))
            {
                if (!showOnlyErrors && !showOnlyWarnings)
                {
                    foreach (BuildMessageEventArgs message in deferredMessages[e.BuildEventContext])
                    {
                        // This will display the project started event before the messages is shown
                        this.MessageHandler(sender, message);
                    }
                }
                deferredMessages.Remove(e.BuildEventContext);
            }

            //If we are in diagnostic and are going to show items, show the project started event
            // along with the items. The project started event will only be shown if it has not been shown before
            if (Verbosity == LoggerVerbosity.Diagnostic && showItemAndPropertyList)
            {
                //Show the deferredProjectStartedEvent
                if (!showOnlyErrors && !showOnlyWarnings)
                {
                    DisplayDeferredProjectStartedEvent(e.BuildEventContext);
                }
                if (null != e.Properties)
                {
                    WriteProperties(e, e.Properties);          
                }

                if (null != e.Items)
                {
                    WriteItems(e, e.Items);
                }
            }
        }

        /// <summary>
        /// Handler for project finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            
            
            //Get the project started event so we can use its information to properly display a project finished event
            ProjectStartedEventMinimumFields startedEvent = buildEventManager.GetProjectStartedEvent(e.BuildEventContext);
            ErrorUtilities.VerifyThrow(startedEvent != null, "Started event should not be null in the finished event handler");

            if (this.showPerfSummary)
            {
                // Stop the performance counter which was created in the project started event handler
                MPPerformanceCounter counter = GetPerformanceCounter(e.ProjectFile, ref projectPerformanceCounters);
                counter.AddEventFinished(startedEvent.TargetNames, e.BuildEventContext, e.Timestamp);
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
            {
                // Only want to show the project finished event if a project started event has been shown
                if (startedEvent.ShowProjectFinishedEvent)
                {
                    lastProjectFullKey = GetFullProjectKey(e.BuildEventContext);

                    if (!showOnlyErrors && !showOnlyWarnings)
                    {
                        WriteLinePrefix(e.BuildEventContext, e.Timestamp, false);
                        setColor(ConsoleColor.Cyan);

                        // In the project finished message the targets which were built and the project which was built
                        // should be shown
                        string targets = startedEvent.TargetNames;

                        string projectName = string.Empty;

                        projectName = startedEvent.ProjectFile == null ? string.Empty : startedEvent.ProjectFile;
                        // Show which targets were built as part of this project
                        if (string.IsNullOrEmpty(targets))
                        {
                            if (e.Succeeded)
                            {
                                WriteMessageAligned(ResourceUtilities.FormatResourceString("ProjectFinishedPrefixWithDefaultTargetsMultiProc", projectName), true);
                            }
                            else
                            {
                                WriteMessageAligned(ResourceUtilities.FormatResourceString("ProjectFinishedPrefixWithDefaultTargetsMultiProcFailed", projectName), true);
                            }
                        }
                        else
                        {
                            if (e.Succeeded)
                            {
                                WriteMessageAligned(ResourceUtilities.FormatResourceString("ProjectFinishedPrefixWithTargetNamesMultiProc", projectName, targets), true);
                            }
                            else
                            {
                                WriteMessageAligned(ResourceUtilities.FormatResourceString("ProjectFinishedPrefixWithTargetNamesMultiProcFailed", projectName, targets), true);
                            }
                        }

                        // In single proc only make a space between the project done event and the next line, this 
                        // is to increase the readability on the single proc log when there are a number of done events
                        // or a mix of done events and project started events. Also only do this on the console and not any log file.
                        if (numberOfProcessors == 1 && runningWithCharacterFileType)
                        {
                            WriteNewLine();
                        }
                    }

                    ShownBuildEventContext(e.BuildEventContext);
                    resetColor();
                }
            }
            // We are done with the project started event if the project has finished building, remove its reference
            buildEventManager.RemoveProjectStartedEvent(e.BuildEventContext);
        }

        /// <summary>
        /// Writes out the list of property names and their values.
        /// This could be done at any time during the build to show the latest
        /// property values, using the cached reference to the list from the 
        /// appropriate ProjectStarted event.
        /// </summary>
        /// <param name="properties">List of properties</param>
        internal void WriteProperties(BuildEventArgs e, IEnumerable properties)
        {
            if (showOnlyErrors || showOnlyWarnings) return;
            ArrayList propertyList = ExtractPropertyList(properties);

            // if there are no properties to display return out of the method and dont print out anything related to displaying
            // the properties, this includes the multiproc prefix information or the Initial properties header
            if (propertyList.Count == 0)
            {
                return;
            }
          
            WriteLinePrefix(e.BuildEventContext, e.Timestamp, false);
            WriteProperties(propertyList);
            ShownBuildEventContext(e.BuildEventContext);
        }

        internal override void OutputProperties(ArrayList list)
        {
            // Write the banner
            setColor(ConsoleColor.Green);
            WriteMessageAligned(ResourceUtilities.FormatResourceString("PropertyListHeader"), true);
            // Write each property name and its value, one per line
            foreach (DictionaryEntry prop in list)
            {
                setColor(ConsoleColor.Gray);
                string propertyString = String.Format(CultureInfo.CurrentCulture, "{0} = {1}", prop.Key, (string)(prop.Value));
                WriteMessageAligned(propertyString, false);
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
        internal void WriteItems(BuildEventArgs e, IEnumerable items)
        {
            if (showOnlyErrors || showOnlyWarnings) return;
            SortedList itemList = ExtractItemList(items);
  
            // if there are no Items to display return out of the method and dont print out anything related to displaying
            // the items, this includes the multiproc prefix information or the Initial items header
            if (itemList.Count == 0)
            {
                return;
            }
            WriteLinePrefix(e.BuildEventContext, e.Timestamp, false);
            WriteItems(itemList);
            ShownBuildEventContext(e.BuildEventContext);
        }

        internal override void OutputItems(string itemType, ArrayList itemTypeList)
        {
            // Write each item, one per line
            bool haveWrittenItemType = false;
            foreach (ITaskItem item in itemTypeList)
            {
                string itemString = null;
                if (!haveWrittenItemType)
                {
                    itemString=itemType;
                    setColor(ConsoleColor.DarkGray);
                    WriteMessageAligned(itemType, false);
                    haveWrittenItemType = true;
                }
                setColor(ConsoleColor.Gray);

                // Indent the text by two tab lengths
                StringBuilder result = new StringBuilder();
                result.Append(' ', 2 * tabWidth).Append(item.ItemSpec);
                WriteMessageAligned(result.ToString() , false);
            }
            resetColor();
        }
        /// <summary>
        /// Handler for target started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void TargetStartedHandler(object sender, TargetStartedEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            
            // Add the target started information to the buildEventManager so its information can be used
            // later in the build
            buildEventManager.AddTargetStartedEvent(e);

            if (this.showPerfSummary)
            {
                // Create a new performance counter for this target
                MPPerformanceCounter counter = GetPerformanceCounter(e.TargetName, ref targetPerformanceCounters);
                counter.AddEventStarted(null, e.BuildEventContext, e.Timestamp, compareContextNodeIdTargetId);
            }
        }

        /// <summary>
        /// Handler for target finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void TargetFinishedHandler(object sender, TargetFinishedEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");

            if (this.showPerfSummary)
            {
                // Stop the performance counter started in the targetStartedEventHandler
                MPPerformanceCounter counter = GetPerformanceCounter(e.TargetName, ref targetPerformanceCounters);
                counter.AddEventFinished(null, e.BuildEventContext, e.Timestamp);
            }

            if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                // Get the target started event so we can determine whether or not to show the targetFinishedEvent
                // as we only want to show target finished events if a target started event has been shown
                TargetStartedEventMinimumFields startedEvent = buildEventManager.GetTargetStartedEvent(e.BuildEventContext);
                ErrorUtilities.VerifyThrow(startedEvent != null, "Started event should not be null in the finished event handler");
                if (startedEvent.ShowTargetFinishedEvent)
                {
                    if (!showOnlyErrors && !showOnlyWarnings)
                    {
                        lastProjectFullKey = GetFullProjectKey(e.BuildEventContext);
                        WriteLinePrefix(e.BuildEventContext, e.Timestamp, false);
                        setColor(ConsoleColor.Cyan);
                        if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic) || showEventId)
                        {
                            WriteMessageAligned(ResourceUtilities.FormatResourceString("TargetMessageWithId", e.Message, e.BuildEventContext.TargetId), true);
                        }
                        else
                        {
                            WriteMessageAligned(e.Message, true);
                        }
                        resetColor();
                    }
                    ShownBuildEventContext(e.BuildEventContext);
                }
            }

            //We no longer need this target started event, it can be removed
            buildEventManager.RemoveTargetStartedEvent(e.BuildEventContext);
        }

        /// <summary>
        /// Handler for task started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void TaskStartedHandler(object sender, TaskStartedEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");

            // if verbosity is detailed or diagnostic

            if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                DisplayDeferredStartedEvents(e.BuildEventContext);

                if (!showOnlyErrors && !showOnlyWarnings)
                {
                    bool prefixAlreadyWritten = WriteTargetMessagePrefix(e, e.BuildEventContext, e.Timestamp);
                    setColor(ConsoleColor.DarkCyan);
                    if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic) || showEventId)
                    {
                        WriteMessageAligned(ResourceUtilities.FormatResourceString("TaskMessageWithId", e.Message, e.BuildEventContext.TaskId), prefixAlreadyWritten);
                    }
                    else
                    {
                        WriteMessageAligned(e.Message, prefixAlreadyWritten);
                    }
                    resetColor();
                }

                ShownBuildEventContext(e.BuildEventContext);
            }

            if (this.showPerfSummary)
            {
                // Create a new performance counter for this task
                MPPerformanceCounter counter = GetPerformanceCounter(e.TaskName, ref taskPerformanceCounters);
                counter.AddEventStarted(null, e.BuildEventContext, e.Timestamp, null);
            }
        }

        /// <summary>
        /// Handler for task finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        public override void TaskFinishedHandler(object sender, TaskFinishedEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            if (this.showPerfSummary)
            {
                // Stop the task performance counter which was started in the task started event
                MPPerformanceCounter counter = GetPerformanceCounter(e.TaskName, ref taskPerformanceCounters);
                counter.AddEventFinished(null, e.BuildEventContext, e.Timestamp);
            }

            // if verbosity is detailed or diagnostic
            if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                if (!showOnlyErrors && !showOnlyWarnings)
                {
                    bool prefixAlreadyWritten = WriteTargetMessagePrefix(e, e.BuildEventContext, e.Timestamp);
                    setColor(ConsoleColor.DarkCyan);
                    if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic) || showEventId)
                    {
                        WriteMessageAligned(ResourceUtilities.FormatResourceString("TaskMessageWithId", e.Message, e.BuildEventContext.TaskId), prefixAlreadyWritten);
                    }
                    else
                    {
                        WriteMessageAligned(e.Message, prefixAlreadyWritten);
                    }
                    resetColor();
                }
                ShownBuildEventContext(e.BuildEventContext);
            }
        }

        /// <summary>
        /// Prints an error event
        /// </summary>
        public override void ErrorHandler(object sender, BuildErrorEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            // Keep track of the number of error events raisd 
            errorCount++;

            // If there is an error we need to walk up the call stack and make sure that 
            // the project started events back to the root project know an error has occurred
            // and are not removed when they finish
            buildEventManager.SetErrorWarningFlagOnCallStack(e.BuildEventContext);

            TargetStartedEventMinimumFields targetStartedEvent = buildEventManager.GetTargetStartedEvent(e.BuildEventContext);
            // Can be null if the error occurred outside of a target, or the error occurres before the targetStartedEvent
            if (targetStartedEvent != null)
            {
                targetStartedEvent.ErrorInTarget = true;
            }
          
            DisplayDeferredStartedEvents(e.BuildEventContext);

            // Display only if showOnlyWarnings is false;
            // unless showOnlyErrors is true, which trumps it.
            if (!showOnlyWarnings || showOnlyErrors)
            {
                if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
                {
                    WriteLinePrefix(e.BuildEventContext, e.Timestamp, false);
                }

                setColor(ConsoleColor.Red);
                WriteMessageAligned(EventArgsFormatting.FormatEventMessage(e, runningWithCharacterFileType), true);
                ShownBuildEventContext(e.BuildEventContext);
                if (ShowSummary)
                {
                    if (!errorList.Contains(e))
                    {
                        errorList.Add(e);
                    }
                }
                resetColor();
            }
        }

        /// <summary>
        /// Prints a warning event
        /// </summary>
        public override void WarningHandler(object sender, BuildWarningEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            // Keep track of the number of warning events raised during the build
            warningCount++;

            // If there is a warning we need to walk up the call stack and make sure that 
            // the project started events back to the root project know a warning has ocured
            // and are not removed when they finish
            buildEventManager.SetErrorWarningFlagOnCallStack(e.BuildEventContext);
            TargetStartedEventMinimumFields targetStartedEvent = buildEventManager.GetTargetStartedEvent(e.BuildEventContext);

            // Can be null if the error occurred outside of a target, or the error occurres before the targetStartedEvent
            if (targetStartedEvent != null)
            {
                targetStartedEvent.ErrorInTarget = true;
            }

            DisplayDeferredStartedEvents(e.BuildEventContext);

            // Display only if showOnlyErrors is false;
            // unless showOnlyWarnings is true, which trumps it.
            if (!showOnlyErrors || showOnlyWarnings)
            {
                if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
                {
                    WriteLinePrefix(e.BuildEventContext, e.Timestamp, false);
                }

                setColor(ConsoleColor.Yellow);
                WriteMessageAligned(EventArgsFormatting.FormatEventMessage(e, runningWithCharacterFileType), true);
            }

            ShownBuildEventContext(e.BuildEventContext);

            if (ShowSummary)
            {
                if (!warningList.Contains(e))
                {
                    warningList.Add(e);
                }
            }
            resetColor();
        }

        /// <summary>
        /// Prints a message event
        /// </summary>
        public override void MessageHandler(object sender, BuildMessageEventArgs e)
        {
            if (showOnlyErrors || showOnlyWarnings) return;

            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            bool print = false;
            bool lightenText = false;

            if (e is TaskCommandLineEventArgs)
            {
                if (!showCommandline && verbosity < LoggerVerbosity.Detailed)
                {
                    return;
                }
                print = true;
            }
            else
            {
                switch (e.Importance)
                {
                    case MessageImportance.High:
                        print = IsVerbosityAtLeast(LoggerVerbosity.Minimal);
                        break;
                    case MessageImportance.Normal:
                        print = IsVerbosityAtLeast(LoggerVerbosity.Normal);
                        lightenText = true;
                        break;
                    case MessageImportance.Low:
                        print = IsVerbosityAtLeast(LoggerVerbosity.Detailed);
                        lightenText = true;
                        break;
                    default:
                        ErrorUtilities.VerifyThrow(false, "Impossible");
                        break;
                }
            }

            if (print)
            {
                // If the event has a valid Project contextId but the project started event has not been fired, the message needs to be 
                // buffered until the project started event is fired
                if (
                       hasBuildStarted 
                       && e.BuildEventContext.ProjectContextId != BuildEventContext.InvalidProjectContextId 
                       && buildEventManager.GetProjectStartedEvent(e.BuildEventContext) == null
                       && IsVerbosityAtLeast(LoggerVerbosity.Normal)
                    )
                {
                    List<BuildMessageEventArgs> messageList = null;
                    if (deferredMessages.ContainsKey(e.BuildEventContext))
                    {
                        messageList = deferredMessages[e.BuildEventContext];
                    }
                    else
                    {
                        messageList = new List<BuildMessageEventArgs>();
                        deferredMessages.Add(e.BuildEventContext, messageList);
                    }
                    messageList.Add(e);
                    return;
                }

                DisplayDeferredStartedEvents(e.BuildEventContext);

                // Print the message event out to the console
                PrintMessage(e, lightenText);
                ShownBuildEventContext(e.BuildEventContext);
            }
        }

        private void DisplayDeferredStartedEvents(BuildEventContext e)
        {
            if (showOnlyErrors || showOnlyWarnings) return;

            // Display any project started events which were deferred until a visible 
            // message from their project is displayed
            if (IsVerbosityAtLeast(LoggerVerbosity.Normal))
            {
                DisplayDeferredProjectStartedEvent(e);
            }

            // Display any target started events which were deferred until a visible 
            // message from their target is displayed
            if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                DisplayDeferredTargetStartedEvent(e);
            }
        }

        /// <summary>
        /// Prints out a message event to the console
        /// </summary>
        private void PrintMessage(BuildMessageEventArgs e, bool lightenText)
        {
            string nonNullMessage = (e.Message == null) ? String.Empty : e.Message;
            int prefixAdjustment = 0;

            if (e.BuildEventContext.TaskId != BuildEventContext.InvalidTaskId)
            {
                prefixAdjustment = 2;
            }

            if (lightenText)
            {
                setColor(ConsoleColor.DarkGray);
            }
            
            PrintTargetNamePerMessage(e, lightenText);
          
            // On diagnostic or if showEventId is set the task message should also display the taskId to assist debugging
            if ((IsVerbosityAtLeast(LoggerVerbosity.Diagnostic) || showEventId) && e.BuildEventContext.TaskId != BuildEventContext.InvalidTaskId)
            {
                bool prefixAlreadyWritten = WriteTargetMessagePrefix(e, e.BuildEventContext, e.Timestamp);
                WriteMessageAligned(ResourceUtilities.FormatResourceString("TaskMessageWithId", nonNullMessage, e.BuildEventContext.TaskId), prefixAlreadyWritten, prefixAdjustment);
            }
            else
            {
                //A time stamp may be shown on verbosities lower than diagnostic
                if (showTimeStamp || IsVerbosityAtLeast(LoggerVerbosity.Detailed))
                {
                    bool prefixAlreadyWritten = WriteTargetMessagePrefix(e, e.BuildEventContext, e.Timestamp);
                    WriteMessageAligned(nonNullMessage, prefixAlreadyWritten, prefixAdjustment);
                }
                else
                {
                    WriteMessageAligned(nonNullMessage, false, prefixAdjustment);
                }
            }

            if (lightenText)
            {
                resetColor();
            }
        }

        private void PrintTargetNamePerMessage(BuildMessageEventArgs e, bool lightenText)
        {
                // Event Context of the current message
                BuildEventContext currentBuildEventContext = e.BuildEventContext;

                // Should the target name be written before the message
                bool writeTargetName = false;
                string targetName = string.Empty;

                // Does the context (Project, Node, Context, Target, NOT task) of the previous event match the current message
                bool contextAreEqual = compareContextNodeIdTargetId.Equals(currentBuildEventContext, lastDisplayedBuildEventContext == null ? null : lastDisplayedBuildEventContext);

                TargetStartedEventMinimumFields targetStartedEvent = null;
                // If the previous event does not have the same target context information, the target name needs to be printed to the console
                // to give the message some more contextual information
                if (!contextAreEqual)
                {
                    targetStartedEvent = buildEventManager.GetTargetStartedEvent(currentBuildEventContext);
                    // Some messages such as engine messages will not have a target started event, in their case, dont print the targetName
                    if (targetStartedEvent != null)
                    {
                        targetName = targetStartedEvent.TargetName;
                        writeTargetName = true;
                    }
                }
                else
                {
                    writeTargetName = false;
                }

                if (writeTargetName)
                {
                    bool prefixAlreadyWritten = WriteTargetMessagePrefix(e, targetStartedEvent.ProjectBuildEventContext, targetStartedEvent.TimeStamp);

                    setColor(ConsoleColor.Cyan);
                    if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic) || showEventId)
                    {
                        WriteMessageAligned(ResourceUtilities.FormatResourceString("TargetMessageWithId", targetName, e.BuildEventContext.TargetId), prefixAlreadyWritten);
                    }
                    else
                    {
                        WriteMessageAligned(targetName + ":", prefixAlreadyWritten);
                    }

                    if (lightenText)
                    {
                        setColor(ConsoleColor.DarkGray);
                    }
                    else
                    {
                        resetColor();
                    }
                }
        }

        private bool WriteTargetMessagePrefix(BuildEventArgs e, BuildEventContext context, DateTime timeStamp)
        {
            bool prefixAlreadyWritten = true;
            ProjectFullKey currentProjectFullKey = GetFullProjectKey(e.BuildEventContext);
            if (!(lastProjectFullKey.Equals(currentProjectFullKey)))
            {
                // Write the prefix information about the target for the message
                WriteLinePrefix(context, timeStamp, false);
                lastProjectFullKey = currentProjectFullKey;
            }
            else
            {
                prefixAlreadyWritten = false;
            }
            return prefixAlreadyWritten;
        }

        /// <summary>
        /// Writes a message to the console, aligned and formatted to fit within the console width
        /// </summary>
        /// <param name="message">Message to be formatted to fit on the console</param>
        /// <param name="prefixAlreadyWritten">Has the prefix(timestamp or key been written)</param>
        private void WriteMessageAligned(string message, bool prefixAlreadyWritten)
        {
            WriteMessageAligned(message, prefixAlreadyWritten, 0);
        }

        /// <summary>
        /// Writes a message to the console, aligned and formatted to fit within the console width
        /// </summary>
        /// <param name="message">Message to be formatted to fit on the console</param>
        /// <param name="prefixAlreadyWritten">Has the prefix(timestamp or key been written)</param>
        private void WriteMessageAligned(string message, bool prefixAlreadyWritten, int prefixAdjustment)
        {
            // This method may require the splitting of lines inorder to format them to the console, this must be an atomic operation
            lock (lockObject)
            {
                int adjustedPrefixWidth = prefixWidth + prefixAdjustment;

                // The string may contain new lines, treat each new line as a different string to format and send to the console
                string[] nonNullMessages = SplitStringOnNewLines(message);
                for (int i = 0; i < nonNullMessages.Length; i++)
                {
                    string nonNullMessage = nonNullMessages[i];
                    // Take into account the new line char which will be added to the end or each reformatted string
                    int bufferWidthMinusNewLine = bufferWidth - 1;

                    // If the buffer is larger then the prefix information (timestamp and key) then reformat the messages. 
                    // If there is not enough room just print the message out and let the console do the formatting
                    bool bufferIsLargerThanPrefix = bufferWidthMinusNewLine > adjustedPrefixWidth;
                    bool messageAndPrefixTooLargeForBuffer = (nonNullMessage.Length + adjustedPrefixWidth) > bufferWidthMinusNewLine;
                    if (bufferIsLargerThanPrefix && messageAndPrefixTooLargeForBuffer && alignMessages)
                    {
                        // Our message may have embedded tab characters, so expand those to their space
                        // equivalent so that wrapping works as expected.
                        nonNullMessage = nonNullMessage.Replace("\t", consoleTab);

                        // If the message and the prefix are too large for one line in the console, split the string to fit
                        int index = 0;
                        int messageLength = nonNullMessage.Length;
                        int amountToCopy = 0;
                        // Loop until all the string has been sent to the console
                        while (index < messageLength)
                        {
                            // Calculate how many chars will fit on the console buffer
                            amountToCopy = (messageLength - index) < (bufferWidthMinusNewLine - adjustedPrefixWidth) ? (messageLength - index) : (bufferWidthMinusNewLine - adjustedPrefixWidth);
                            WriteBasedOnPrefix(nonNullMessage.Substring(index, amountToCopy), (prefixAlreadyWritten && index == 0 && i == 0), adjustedPrefixWidth);
                            index = index + amountToCopy;
                        }
                    }
                    else
                    {
                        //there is not enough room just print the message out and let the console do the formatting
                        WriteBasedOnPrefix(nonNullMessage, prefixAlreadyWritten, adjustedPrefixWidth);
                    }
                }
            }
        }

        /// <summary>
        /// Write message takinginto account whether or not the prefix (timestamp and key) have already been written on the line
        /// </summary>
        /// <param name="nonNullMessage"></param>
        /// <param name="prefixAlreadyWritten"></param>
        private void WriteBasedOnPrefix(string nonNullMessage, bool prefixAlreadyWritten, int adjustedPrefixWidth)
        {
            if (prefixAlreadyWritten)
            {
                write(nonNullMessage);
                WriteNewLine();
            }
            else
            {
                // No prefix info has been written, indent the line to the proper location
                write(IndentString(nonNullMessage, adjustedPrefixWidth));
            }
        }

        /// <summary>
        /// Will display the target started event which was deferred until the first visible message for the target is ready to be displayed
        /// </summary>
        private void DisplayDeferredTargetStartedEvent(BuildEventContext e)
        {
            if (showOnlyErrors || showOnlyWarnings) return;

            // Get the deferred target started event
            TargetStartedEventMinimumFields targetStartedEvent = buildEventManager.GetTargetStartedEvent(e);

            //Make sure we have not shown the event before
            if (targetStartedEvent != null && !targetStartedEvent.ShowTargetFinishedEvent)
            {
                //Since the target started event has been shows, the target finished event should also be shown
                targetStartedEvent.ShowTargetFinishedEvent = true;
               
                // If there are any other started events waiting and we are the first message, show them
                DisplayDeferredStartedEvents(targetStartedEvent.ProjectBuildEventContext);

                WriteLinePrefix(targetStartedEvent.ProjectBuildEventContext, targetStartedEvent.TimeStamp, false);
                
                setColor(ConsoleColor.Cyan);
               
                ProjectStartedEventMinimumFields startedEvent = buildEventManager.GetProjectStartedEvent(e);
                ErrorUtilities.VerifyThrow(startedEvent != null, "Project Started should not be null in deferred target started");
                string currentProjectFile = startedEvent.ProjectFile == null ? string.Empty : startedEvent.ProjectFile;

                string targetName = null;
                if (IsVerbosityAtLeast(LoggerVerbosity.Diagnostic) || showEventId)
                {
                   targetName = ResourceUtilities.FormatResourceString("TargetMessageWithId", targetStartedEvent.TargetName, targetStartedEvent.ProjectBuildEventContext.TargetId);
                }
                else
                {
                    targetName = targetStartedEvent.TargetName;
                }

                if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
                {
                    WriteMessageAligned(ResourceUtilities.FormatResourceString("TargetStartedFromFileInProject", targetName, targetStartedEvent.TargetFile, currentProjectFile), true);
                }
                else
                {
                    WriteMessageAligned(ResourceUtilities.FormatResourceString("TargetStartedPrefixInProject", targetName, currentProjectFile), true);
                }

                resetColor();
                ShownBuildEventContext(e);
            }
        }

        /// <summary>
        /// Will display the project started event which was deferred until the first visible message for the project is ready to be displayed
        /// </summary>
        private void DisplayDeferredProjectStartedEvent(BuildEventContext e)
        {
            if (showOnlyErrors || showOnlyWarnings) return;

            if (!SkipProjectStartedText)
            {
                // Get the project started event which matched the passed in event context
                ProjectStartedEventMinimumFields projectStartedEvent = buildEventManager.GetProjectStartedEvent(e);

                // Make sure the project started event has not been show yet
                if (projectStartedEvent != null && !projectStartedEvent.ShowProjectFinishedEvent)
                {
                    projectStartedEvent.ShowProjectFinishedEvent = true;

                    ProjectStartedEventMinimumFields parentStartedEvent = projectStartedEvent.ParentProjectStartedEvent;
                    if (parentStartedEvent != null)
                    {
                        //Make sure that if there are any events deferred on this event to show them first
                        DisplayDeferredStartedEvents(parentStartedEvent.ProjectBuildEventContext);
                    }

                    string current = projectStartedEvent.ProjectFile == null ? string.Empty : projectStartedEvent.ProjectFile;
                    string previous = parentStartedEvent == null ? null : parentStartedEvent.ProjectFile;
                    string targetNames = projectStartedEvent.TargetNames;

                    // Log 0-based node id's, where 0 is the parent. This is a little unnatural for the reader,
                    // but avoids confusion by being consistent with the Engine and any error messages it may produce.
                    int currentProjectNodeId = (projectStartedEvent.ProjectBuildEventContext.NodeId);
                    if (previous == null)
                    {
                        WriteLinePrefix(projectStartedEvent.FullProjectKey, projectStartedEvent.TimeStamp, false);
                        setColor(ConsoleColor.Cyan);
                        string message = string.Empty;
                        if ((targetNames == null) || (targetNames.Length == 0))
                        {
                            message = ResourceUtilities.FormatResourceString("ProjectStartedTopLevelProjectWithDefaultTargets", current, currentProjectNodeId);
                        }
                        else
                        {
                            message = ResourceUtilities.FormatResourceString("ProjectStartedTopLevelProjectWithTargetNames", current, currentProjectNodeId, targetNames);
                        }

                        WriteMessageAligned(message, true);
                        resetColor();
                    }
                    else
                    {
                        WriteLinePrefix(parentStartedEvent.FullProjectKey, parentStartedEvent.TimeStamp, false);
                        setColor(ConsoleColor.Cyan);
                        if ((targetNames == null) || (targetNames.Length == 0))
                        {
                            WriteMessageAligned(ResourceUtilities.FormatResourceString("ProjectStartedWithDefaultTargetsMultiProc", previous, parentStartedEvent.FullProjectKey, current, projectStartedEvent.FullProjectKey, currentProjectNodeId), true);
                        }
                        else
                        {
                            WriteMessageAligned(ResourceUtilities.FormatResourceString("ProjectStartedWithTargetsMultiProc", previous, parentStartedEvent.FullProjectKey, current, projectStartedEvent.FullProjectKey, currentProjectNodeId, targetNames), true);
                        }
                        resetColor();
                    }

                    ShownBuildEventContext(e);
                }
            }
        }

        /// <summary>
        /// Prints a custom event
        /// </summary>
        public override void CustomEventHandler(object sender, CustomBuildEventArgs e)
        {
            if (showOnlyErrors || showOnlyWarnings) return;

            ErrorUtilities.VerifyThrowArgumentNull(e.BuildEventContext, "BuildEventContext");
            if (IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                // ignore custom events with null messages -- some other
                // logger will handle them appropriately
                if (e.Message != null)
                {
                    DisplayDeferredStartedEvents(e.BuildEventContext);
                    WriteLinePrefix(e.BuildEventContext, e.Timestamp, false);
                    WriteMessageAligned(e.Message, true);
                    ShownBuildEventContext(e.BuildEventContext);
                }
            }
        }

        /// <summary>
        /// Writes message contextual information for each message displayed on the console
        /// </summary>
        private void WriteLinePrefix(BuildEventContext e, DateTime eventTimeStamp, bool isMessagePrefix)
        {
            WriteLinePrefix(GetFullProjectKey(e).ToString(verbosity), eventTimeStamp, isMessagePrefix);
        }

        private void WriteLinePrefix(string key, DateTime eventTimeStamp, bool isMessagePrefix)
        {
            // Dont want any prefix for single proc
            if (numberOfProcessors == 1)
            {
                return;
            }

            setColor(ConsoleColor.Cyan);

            string context = string.Empty;
            if (showTimeStamp || IsVerbosityAtLeast(LoggerVerbosity.Diagnostic))
            {
                context = LogFormatter.FormatLogTimeStamp(eventTimeStamp);
            }

            string prefixString = string.Empty;

            if (!isMessagePrefix || IsVerbosityAtLeast(LoggerVerbosity.Detailed))
            {
                prefixString = ResourceUtilities.FormatResourceString("BuildEventContext", context, key) + ">";
            }
            else
            {
                prefixString = ResourceUtilities.FormatResourceString("BuildEventContext", context, string.Empty) + " ";
            }

            WritePretty(prefixString);
            resetColor();

            if (prefixWidth == 0)
            {
                prefixWidth = prefixString.Length;
            }
        }

        /// <summary>
        /// Extract the full project key from the BuildEventContext
        /// </summary>
        private ProjectFullKey GetFullProjectKey(BuildEventContext e)
        {
            ProjectStartedEventMinimumFields startedEvent = null;

            if (e != null)
            {
                startedEvent = buildEventManager.GetProjectStartedEvent(e);
            }

            //Project started event can be null, if the message has come before the project started event
            // or the message is not part of a project such as if the message came from the engine
            if (startedEvent == null)
            {
                return new ProjectFullKey(0, 0);
            }
            else
            {
                return new ProjectFullKey(startedEvent.ProjectKey, startedEvent.EntryPointKey);
            }
        }
        
        /// <summary>
        /// Returns a performance counter for a given scope (either task name or target name)
        /// from the given table.
        /// </summary>
        /// <param name="scopeName">Task name or target name.</param>
        /// <param name="table">Table that has tasks or targets.</param>
        internal new static MPPerformanceCounter GetPerformanceCounter(string scopeName, ref Hashtable table)
        {
            // Lazily construct the performance counter table.
            if (table == null)
            {
                table = new Hashtable(StringComparer.OrdinalIgnoreCase);
            }

            MPPerformanceCounter counter = (MPPerformanceCounter)table[scopeName];

            // And lazily construct the performance counter itself.
            if (counter == null)
            {
                counter = new MPPerformanceCounter(scopeName);
                table[scopeName] = counter;
            }

            return counter;
        }
        #endregion

        #region InternalClass
        /// <summary>
        /// Stores and calculates the performance numbers for the different events
        /// </summary>
        internal class MPPerformanceCounter : PerformanceCounter
        {
            // Set of performance counters for a project
            private Hashtable internalPerformanceCounters;
            // Dictionary mapping event context to the start number of ticks, this will be used to calculate the amount
            // of time between the start of the performance counter and the end
            // An object is being used to box the start time long value to prevent jitting when this code path is executed.
            private Dictionary<BuildEventContext, object> startedEvent;
            private int messageIdentLevel = 2;

            internal int MessageIdentLevel
            {
                get { return messageIdentLevel; }
                set { messageIdentLevel = value; }
            }

            internal MPPerformanceCounter(string scopeName)
                : base(scopeName)
            {
                // Do Nothing
            }

            /// <summary>
            /// Add a started event to the performance counter, by adding the event this sets the start time of the performance counter
            /// </summary>
            internal void AddEventStarted(string projectTargetNames, BuildEventContext buildEventContext, DateTime eventTimeStamp, IEqualityComparer<BuildEventContext> comparer)
            {
                //If the projectTargetNames are set then we should be a projectstarted event
                if (!string.IsNullOrEmpty(projectTargetNames))
                {
                    // Create a new performance counter for the project entry point to calculate how much time and how many calls
                    // were made to the entry point
                    MPPerformanceCounter entryPoint = GetPerformanceCounter(projectTargetNames, ref internalPerformanceCounters);
                    entryPoint.AddEventStarted(null, buildEventContext, eventTimeStamp, compareContextNodeIdTargetId);
                    // Indent the output so it is intented with respect to its parent project
                    entryPoint.messageIdentLevel = 7;
                }

                if (startedEvent == null)
                {
                    if (comparer == null)
                    {
                        startedEvent = new Dictionary<BuildEventContext, object>();
                    }
                    else
                    {
                        startedEvent = new Dictionary<BuildEventContext, object>(comparer);
                    }
                }

                if (!startedEvent.ContainsKey(buildEventContext))
                {
                    startedEvent.Add(buildEventContext, (object)eventTimeStamp.Ticks);
                    ++calls;
                }
            }

            /// <summary>
            ///  Add a finished event to the performance counter, so perf numbers can be calculated
            /// </summary>
            internal void AddEventFinished(string projectTargetNames, BuildEventContext buildEventContext, DateTime eventTimeStamp)
            {

                if (!string.IsNullOrEmpty(projectTargetNames))
                {
                    MPPerformanceCounter entryPoint = GetPerformanceCounter(projectTargetNames, ref internalPerformanceCounters);
                    entryPoint.AddEventFinished(null, buildEventContext, eventTimeStamp);
                }

                if (startedEvent == null)
                {
                    Debug.Assert(startedEvent != null, "Cannot have finished counter without started counter. ");
                }

                if (startedEvent.ContainsKey(buildEventContext))
                {
                    // Calculate the amount of time spent in the event based on the time stamp of when
                    // the started event was created and when the finished event was created
                    elapsedTime += (TimeSpan.FromTicks(eventTimeStamp.Ticks - (long)startedEvent[buildEventContext]));
                    startedEvent.Remove(buildEventContext);
                }
            }

            /// <summary>
            /// Print out the performance counter message
            /// </summary>
            internal override void PrintCounterMessage(WriteLinePrettyFromResourceDelegate WriteLinePrettyFromResource, ColorSetter setColor, ColorResetter resetColor)
            {
                // round: submillisecond values are not meaningful
                string time = String.Format(CultureInfo.CurrentCulture,
                       "{0,5}", Math.Round(elapsedTime.TotalMilliseconds, 0));

                WriteLinePrettyFromResource
                   (
                       messageIdentLevel,
                       "PerformanceLine",
                       time,
                       String.Format(CultureInfo.CurrentCulture,
                               "{0,-40}" /* pad to 40 align left */, scopeName),
                       String.Format(CultureInfo.CurrentCulture,
                               "{0,3}", calls)
                   );

                if (internalPerformanceCounters != null && internalPerformanceCounters.Count > 0)
                {
                    // For each of the entry points in the project print out the performance numbers for them
                    foreach (MPPerformanceCounter counter in internalPerformanceCounters.Values)
                    {
                        setColor(ConsoleColor.White);
                        counter.PrintCounterMessage(WriteLinePrettyFromResource, setColor, resetColor);
                        resetColor();
                    }
                }
            }
        }
        #endregion

        #region internal MemberData
        private static ComparerContextNodeId<BuildEventContext> compareContextNodeId = new ComparerContextNodeId<BuildEventContext>();
        private static ComparerContextNodeIdTargetId<BuildEventContext> compareContextNodeIdTargetId = new ComparerContextNodeIdTargetId<BuildEventContext>();
        private BuildEventContext lastDisplayedBuildEventContext;
        private int bufferWidth = -1;
        private object lockObject = new Object();
        private int prefixWidth = 0;
        private ProjectFullKey lastProjectFullKey = new ProjectFullKey(-1, -1);
        private bool alignMessages;
        private bool forceNoAlign;
        private bool showEventId;
        // According to the documentaion for ENABLE_PROCESSED_OUTPUT tab width for the console is 8 characters
        private const string consoleTab = "        ";
        #endregion

        #region Per-build Members
        //Holds messages that were going to be shown before the project started event, buffer them until the project started event is shown
        private Dictionary<BuildEventContext, List<BuildMessageEventArgs>> deferredMessages;
        private BuildEventManager buildEventManager;
        //  Has the build started
        private bool hasBuildStarted;
        private bool showCommandline;
        private bool showTimeStamp;
        #endregion
    }
}
