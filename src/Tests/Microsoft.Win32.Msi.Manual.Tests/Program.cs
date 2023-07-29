// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi.Manual.Tests
{
    /// <summary>
    /// Simple test application to illustrate basic MSI API calls and wiring up an external UI handler to
    /// generate a progress bar.
    /// </summary>
    public class Program
    {
        int ProgressBarWidth;
        int ProgressTotal;
        int ProgressPhase;
        int ProgressCompleted;
        int ActionTop;
        int ProgressBarTop;
        bool ForwardProgress;
        bool ActionDataEnabled;
        string CurrentAction;
        bool ProgressDone;

        int ActionDataStep;
        double Progress;

        static void Main(string[] args)
        {
            Program p = new();
            p.Run(args);
        }

        void Run(string[] args)
        {
            try
            {
                Console.CursorVisible = false;
                ProgressBarWidth = Console.WindowWidth - 4;

                switch (args[0])
                {
                    case "install":
                        InstallMsi(args[1]);
                        break;
                    default:
                        Console.WriteLine($"Unknown command.");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Console.CursorVisible = true;
                Environment.Exit(e.HResult);
            }
            finally
            {
                Console.CursorVisible = true;
            }
        }

        void InstallMsi(string path)
        {
            // Configure event handler for install messages
            UserInterfaceHandler ux = new UserInterfaceHandler(InstallLogMode.ACTIONDATA | InstallLogMode.ACTIONSTART | InstallLogMode.PROGRESS);

            ux.ActionData += OnActionData;
            ux.ActionStart += OnActionStart;
            ux.Progress += OnProgress;

            ProgressPhase = 0;

            Console.WriteLine();
            ActionTop = Console.CursorTop;
            ProgressBarTop = ActionTop + 1;

            // Make sure we run quietly. Be careful, we won't be prompted to elevate.
            WindowsInstaller.SetInternalUI(InstallUILevel.None);

            uint error = WindowsInstaller.InstallProduct(path, "MSIFASTINSTALL=7 REBOOT=ReallySuppress");

            Console.CursorTop = ProgressBarTop + 1;
            Console.CursorLeft = 0;

            Console.CursorTop = ActionTop;
            ClearLine();
            Console.CursorTop = ProgressBarTop;
            ClearLine();

            if ((error != Error.SUCCESS) && (error != Error.SUCCESS_REBOOT_INITIATED) && (error != Error.SUCCESS_REBOOT_REQUIRED))
            {
                throw new WindowsInstallerException((int)error);
            }
            else
            {
                Console.WriteLine("Done!");
            }
        }

        void ClearLine()
        {
            int top = Console.CursorTop;
            Console.SetCursorPosition(0, top);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, top);
        }

        void OnActionData(Object sender, ActionDataEventArgs e)
        {
            if (ActionDataEnabled)
            {
                ProgressCompleted += ForwardProgress ? ActionDataStep : -ActionDataStep;
                UpdateProgress();
            }

            e.Result = DialogResult.IDOK;
        }

        void OnActionStart(Object send, ActionStartEventArgs e)
        {
            if (ActionDataEnabled)
            {
                ActionDataEnabled = false;
            }

            if (e.ActionName != CurrentAction)
            {
                CurrentAction = e.ActionName;
                Console.CursorTop = ActionTop;
                Console.CursorLeft = 0;
                ClearLine();
                Console.Write(e.ActionDescription);
            }

            e.Result = DialogResult.IDOK;
        }

        void OnProgress(Object send, ProgressEventArgs e)
        {
            e.Result = DialogResult.IDOK;

            switch (e.ProgressType)
            {
                case ProgressType.Reset:
                    DrawProgressBar();
                    ProgressTotal = e.Fields[1];
                    // Field 3 contains the direction.
                    ForwardProgress = e.Fields[2] == 0;
                    // Field 2 contains the total expected number of ticks. If we're moving forward, reset to 0,
                    // otherwise reset to the total ticks since we're going in reverse, e.g. install is rolling back.
                    ProgressCompleted = ForwardProgress ? 0 : ProgressTotal;
                    ActionDataEnabled = false;
                    ProgressPhase++;
                    UpdateProgress();
                    break;

                case ProgressType.ActionInfo:
                    ActionDataEnabled = e.Fields[2] != 0;

                    if (ActionDataEnabled)
                    {
                        // Field 2 indicates the amount of ticks to progress for each ActionData message
                        ActionDataStep = e.Fields[1];
                    }

                    break;

                case ProgressType.ProgressReport:
                    if ((ProgressTotal == 0) || (ProgressPhase == 0))
                    {
                        return;
                    }

                    ProgressCompleted += ForwardProgress ? e.Fields[1] : -e.Fields[1];
                    UpdateProgress();
                    break;

                default:
                    // Cancel the install if we get a bogus progress sub-type message.
                    e.Result = DialogResult.IDCANCEL;
                    break;
            }
        }

        void DrawProgressBar()
        {
            Console.CursorLeft = 0;
            Console.CursorTop = ProgressBarTop;
            ClearLine();
            Console.CursorTop = ProgressBarTop;
            ConsoleColor fg = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(new string('\u2591', ProgressBarWidth));
            Console.ForegroundColor = fg;
        }

        void UpdateProgress()
        {
            if (ProgressPhase == 0)
            {
                Progress = 0;
                DrawProgressBar();
            }
            else if (ProgressPhase == 1)
            {
                Progress = (double)Math.Min(ProgressCompleted, ProgressTotal) / ProgressTotal;

                int blocks = Convert.ToInt32(ProgressBarWidth * Progress);

                Console.CursorLeft = 0;
                Console.CursorTop = ProgressBarTop;
                ConsoleColor fg = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(new string('\u2588', blocks));
                Console.ForegroundColor = fg;
            }
            else
            {
                Progress = 100;

                if (!ProgressDone)
                {
                    Console.SetCursorPosition(0, ProgressBarTop);
                    ConsoleColor fg = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(new string('\u2588', ProgressBarWidth));
                    Console.ForegroundColor = fg;
                    ProgressDone = true;
                }
            }

            Thread.Sleep(15);
        }
    }
}
