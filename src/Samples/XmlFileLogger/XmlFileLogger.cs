// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// XML File Logger class to handle, parse, and route messages form the MSBuild logging system.
    /// </summary>
    public class XmlFileLogger : Logger
    {
        public const string OutputItemsMessagePrefix = @"Output Item(s): ";
        public const string OutputPropertyMessagePrefix = @"Output Property: ";
        public const string TaskParameterMessagePrefix = @"Task Parameter:";
        public const string PropertyGroupMessagePrefix = @"Set Property: ";
        public const string ItemGroupIncludeMessagePrefix = @"Added Item(s): ";
        public const string ItemGroupRemoveMessagePrefix = @"Removed Item(s): ";

        /// <summary>
        /// The path to the log file specified by the user
        /// </summary>
        private string _logFile;

        /// <summary>
        /// The build instance set when the build starts.
        /// </summary>
        private Build _build;

        private int _errors;
        private int _warings;

        /// <summary>
        /// Initializes the logger and subscribes to the relevant events.
        /// </summary>
        /// <param name="eventSource">The available events that processEvent logger can subscribe to.</param>
        public override void Initialize(IEventSource eventSource)
        {
            ProcessParameters();
            
            eventSource.BuildStarted    += (s, args) => _build = new Build(args);
            eventSource.BuildFinished   += (o, args) => _build.CompleteBuild(args, _logFile, _errors, _warings);

            eventSource.ProjectStarted  += (o, args) => TryProcessEvent(() => _build.AddProject(args));
            eventSource.ProjectFinished += (o, args) => TryProcessEvent(() => _build.CompleteProject(args));
            eventSource.TargetStarted   += (o, args) => TryProcessEvent(() => _build.AddTarget(args));
            eventSource.TargetFinished  += (o, args) => TryProcessEvent(() => _build.CompleteTarget(args));
            eventSource.TaskStarted     += (o, args) => TryProcessEvent(() => _build.AddTask(args));
            eventSource.TaskFinished    += (o, args) => TryProcessEvent(() => _build.CompleteTask(args));

            eventSource.TaskFinished += (o, args) => TryProcessEvent(() => _build.CompleteTask(args));

            eventSource.MessageRaised += HandleMessageRaised;

            eventSource.ErrorRaised += (o, args) =>
            {
                _errors++;
                _build.AddMessage(args, string.Format("Error {0}: {1}", args.Code, args.Message));
            };
            eventSource.WarningRaised += (o, args) =>
            {
                _warings++;
                _build.AddMessage(args, string.Format("Warning {0}: {1}", args.Code, args.Message));
            };
        }

        /// <summary>
        /// Tries to process an event (action). On exception, log as a build message so we don't crash.
        /// </summary>
        /// <param name="processEvent">Action/event to process.</param>
        private void TryProcessEvent(Action processEvent)
        {
            try
            {
                processEvent();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _build.AddMessage(new Message(string.Format("XmlFileLogger Error: {0}", e), DateTime.Now));
            }
        }

        /// <summary>
        /// Handles the generic message raised event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="messageArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        private void HandleMessageRaised(object sender, BuildMessageEventArgs messageArgs)
        {
            const string taskAssemblyPattern = "Using \"(?<task>.+)\" task from (assembly|the task factory) \"(?<assembly>.+)\"\\.";

            // Task Input / Outputs
            if (messageArgs.Message.StartsWith(TaskParameterMessagePrefix))
            {
                _build.AddTaskParameter(messageArgs, TaskParameterMessagePrefix);
            }
            else if (messageArgs.Message.StartsWith(OutputItemsMessagePrefix))
            {
                _build.AddTaskParameter(messageArgs, OutputItemsMessagePrefix);
            }
            else if (messageArgs.Message.StartsWith(OutputPropertyMessagePrefix))
            {
                _build.AddTaskParameter(messageArgs, OutputPropertyMessagePrefix);
            }

            // Item / Property groups
            else if (messageArgs.Message.StartsWith(PropertyGroupMessagePrefix))
            {
                _build.AddPropertyGroup(messageArgs, PropertyGroupMessagePrefix);
            }
            else if (messageArgs.Message.StartsWith(ItemGroupIncludeMessagePrefix))
            {
                _build.AddItemGroup(messageArgs, ItemGroupIncludeMessagePrefix);
            }
            else if (messageArgs.Message.StartsWith(ItemGroupRemoveMessagePrefix))
            {
                _build.AddItemGroup(messageArgs, ItemGroupRemoveMessagePrefix);
            }
            else
            {
                // This was command line arguments for processEvent task
                var args = messageArgs as TaskCommandLineEventArgs;
                if (args != null)
                {
                    _build.AddCommandLine(args);
                    return;
                }

                // A task from assembly message (parses out the task name and assembly path).
                var match = Regex.Match(messageArgs.Message, taskAssemblyPattern);
                if (match.Success)
                {
                    _build.SetTaskAssembly(match.Groups["task"].Value, match.Groups["assembly"].Value);
                }
                else
                {
                    // Just processEvent generic log message or something we currently don't handle in the object model.
                    _build.AddMessage(messageArgs, messageArgs.Message);
                }
            }
        }

        /// <summary>
        /// Processes the parameters given to the logger from MSBuild.
        /// </summary>
        /// <exception cref="LoggerException">
        /// </exception>
        private void ProcessParameters()
        {
            const string invalidParamSpecificationMessage = @"Need processEvent log file.  Specify using the following pattern: '/logger:XmlFileLogger,XmlFileLogger.dll;buildlog.xml";

            if (Parameters == null)
            {
                throw new LoggerException(invalidParamSpecificationMessage);
            }

            string[] parameters = Parameters.Split(';');

            if (parameters.Length != 1)
            {
                throw new LoggerException(invalidParamSpecificationMessage);
            }

            _logFile = parameters[0];
        }
    }
}
