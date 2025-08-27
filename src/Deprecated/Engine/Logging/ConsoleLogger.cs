// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    #region Delegates

    /// <summary>
    /// Delegate to use for writing a string to some location like
    /// the console window or the IDE build window.
    /// </summary>
    /// <param name="message"></param>
    public delegate void WriteHandler(string message);

    /// <summary>
    /// Type of delegate used to set console color.
    /// </summary>
    /// <param name="color">Text color</param>
    public delegate void ColorSetter(ConsoleColor color);

    /// <summary>
    /// Type of delegate used to reset console color.
    /// </summary>
    public delegate void ColorResetter();

    #endregion

    /// <summary>
    /// This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
    /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
    /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
    /// 
    /// This class implements the default logger that outputs event data
    /// to the console (stdout).
    /// It is a facade: it creates, wraps and delegates to a kind of BaseConsoleLogger,
    /// either SerialConsoleLogger or ParallelConsoleLogger.
    /// </summary>
    /// <remarks>
    /// <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// > [!WARNING]
    /// > This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// > <xref:Microsoft.Build.Construction>
    /// > <xref:Microsoft.Build.Evaluation>
    /// > <xref:Microsoft.Build.Execution>
    /// ]]></format>
    /// </remarks>
    /// <remarks>This class is not thread safe.</remarks>
    public class ConsoleLogger : INodeLogger
    {
        private BaseConsoleLogger consoleLogger;
        private int numberOfProcessors = 1;
        private LoggerVerbosity verbosity;
        private WriteHandler write;
        private ColorSetter colorSet;
        private ColorResetter colorReset;
        private string parameters;
        private bool skipProjectStartedText = false;
        private bool? showSummary;

        #region Constructors

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public ConsoleLogger()
            : this(LoggerVerbosity.Normal)
        {
            // do nothing
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Create a logger instance with a specific verbosity.  This logs to
        /// the default console.
        /// </summary>
        /// <param name="verbosity">Verbosity level.</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public ConsoleLogger(LoggerVerbosity verbosity)
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Initializes the logger, with alternate output handlers.
        /// </summary>
        /// <param name="verbosity"></param>
        /// <param name="write"></param>
        /// <param name="colorSet"></param>
        /// <param name="colorReset"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public ConsoleLogger
        (
            LoggerVerbosity verbosity,
            WriteHandler write,
            ColorSetter colorSet,
            ColorResetter colorReset
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(write, nameof(write));
            this.verbosity = verbosity;
            this.write = write;
            this.colorSet = colorSet;
            this.colorReset = colorReset;
        }

        /// <summary>
        /// This is called by every event handler for compat reasons -- see DDB #136924
        /// However it will skip after the first call
        /// </summary>
        private void InitializeBaseConsoleLogger()
        {
            if (consoleLogger == null)
            {
                bool useMPLogger = false;
                if (!string.IsNullOrEmpty(parameters))
                {
                    string[] parameterComponents = parameters.Split(BaseConsoleLogger.parameterDelimiters);
                    for (int param = 0; param < parameterComponents.Length; param++)
                    {
                        if (parameterComponents[param].Length > 0)
                        {
                            if (String.Equals(parameterComponents[param], "ENABLEMPLOGGING", StringComparison.OrdinalIgnoreCase))
                            {
                                useMPLogger = true;
                            }
                            if (String.Equals(parameterComponents[param], "DISABLEMPLOGGING", StringComparison.OrdinalIgnoreCase))
                            {
                                useMPLogger = false;
                            }
                        }
                    }
                }

                if (numberOfProcessors == 1 && !useMPLogger)
                {
                    consoleLogger = new SerialConsoleLogger(verbosity, write, colorSet, colorReset);
                }
                else
                {
                    consoleLogger = new ParallelConsoleLogger(verbosity, write, colorSet, colorReset);
                }

                if (!string.IsNullOrEmpty(parameters))
                {
                    consoleLogger.Parameters = parameters;
                    parameters = null;
                }

                if (showSummary != null)
                {
                    consoleLogger.ShowSummary = (bool)showSummary;
                }

                consoleLogger.SkipProjectStartedText = skipProjectStartedText;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Gets or sets the level of detail to show in the event log.
        /// </summary>
        /// <value>Verbosity level.</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public LoggerVerbosity Verbosity
        {
            get
            {
                return consoleLogger == null ? verbosity : consoleLogger.Verbosity;
            }

            set
            {
                if (consoleLogger == null)
                {
                    verbosity = value;
                }
                else
                {
                    consoleLogger.Verbosity = value;
                }
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// The console logger takes a single parameter to suppress the output of the errors
        /// and warnings summary at the end of a build.
        /// </summary>
        /// <value>null</value>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public string Parameters
        {
            get
            {
                return consoleLogger == null ? parameters : consoleLogger.Parameters;
            }

            set
            {
                if (consoleLogger == null)
                {
                    parameters = value;
                }
                else
                {
                    consoleLogger.Parameters = value;
                }
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Suppresses the display of project headers. Project headers are
        /// displayed by default unless this property is set.
        /// </summary>
        /// <remarks>This is only needed by the IDE logger.
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public bool SkipProjectStartedText
        {
            get
            {
                return consoleLogger == null ? skipProjectStartedText : consoleLogger.SkipProjectStartedText;
            }

            set
            {
                if (consoleLogger == null)
                {
                    skipProjectStartedText = value;
                }
                else
                {
                    consoleLogger.SkipProjectStartedText = value;
                }
            }
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Suppresses the display of error and warnings summary.
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public bool ShowSummary
        {
            get
            {
                return (consoleLogger == null ? showSummary : consoleLogger.ShowSummary) ?? false;
            }

            set
            {
                if (consoleLogger == null)
                {
                    showSummary = value;
                }
                else
                {
                    consoleLogger.ShowSummary = value;
                }
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
                return consoleLogger == null ? write : consoleLogger.write;
            }

            set
            {
                if (consoleLogger == null)
                {
                    write = value;
                }
                else
                {
                    consoleLogger.write = value;
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Apply a parameter.
        /// NOTE: This method was public by accident in Whidbey, so it cannot be made internal now. It has
        /// no good reason for being public.
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void ApplyParameter(string parameterName, string parameterValue)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(consoleLogger != null, "MustCallInitializeBeforeApplyParameter");
            consoleLogger.ApplyParameter(parameterName, parameterValue);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Signs up the console logger for all build events.
        /// </summary>
        /// <param name="eventSource">Available events.</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public virtual void Initialize(IEventSource eventSource)
        {
            InitializeBaseConsoleLogger();
            consoleLogger.Initialize(eventSource);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public virtual void Initialize(IEventSource eventSource, int nodeCount)
        {
            this.numberOfProcessors = nodeCount;
            InitializeBaseConsoleLogger();
            consoleLogger.Initialize(eventSource, nodeCount);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// The console logger does not need to release any resources.
        /// This method does nothing.
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public virtual void Shutdown()
        {
            consoleLogger?.Shutdown();
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for build started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void BuildStartedHandler(object sender, BuildStartedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.BuildStartedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for build finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void BuildFinishedHandler(object sender, BuildFinishedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.BuildFinishedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for project started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void ProjectStartedHandler(object sender, ProjectStartedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.ProjectStartedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for project finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.ProjectFinishedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for target started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void TargetStartedHandler(object sender, TargetStartedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.TargetStartedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for target finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void TargetFinishedHandler(object sender, TargetFinishedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.TargetFinishedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for task started events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void TaskStartedHandler(object sender, TaskStartedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.TaskStartedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Handler for task finished events
        /// </summary>
        /// <param name="sender">sender (should be null)</param>
        /// <param name="e">event arguments</param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void TaskFinishedHandler(object sender, TaskFinishedEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.TaskFinishedHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Prints an error event
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void ErrorHandler(object sender, BuildErrorEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.ErrorHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Prints a warning event
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void WarningHandler(object sender, BuildWarningEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.WarningHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Prints a message event
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void MessageHandler(object sender, BuildMessageEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.MessageHandler(sender, e);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Prints a custom event
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void CustomEventHandler(object sender, CustomBuildEventArgs e)
        {
            InitializeBaseConsoleLogger(); // for compat: see DDB#136924

            consoleLogger.CustomEventHandler(sender, e);
        }

        /// <summary>
        /// Sets foreground color to color specified
        /// </summary>
        /// <param name="c">foreground color</param>
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
        private static ConsoleColor TransformColor(ConsoleColor foreground,
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

        #endregion
    }
}
