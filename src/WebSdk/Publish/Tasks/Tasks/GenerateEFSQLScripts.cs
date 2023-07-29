// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public class GenerateEFSQLScripts : Task
    {
        [Required]
        public string ProjectDirectory { get; set; }
        [Required]
        public string EFPublishDirectory { get; set; }
        [Required]
        public ITaskItem[] EFMigrations { get; set; }
        [Required]
        public string Configuration { get; set; }
        public string EFSQLScriptsFolderName { get; set; }
        public string EFMigrationsAdditionalArgs { get; set; }
        [Output]
        public ITaskItem[] EFSQLScripts { get; set; }

        public override bool Execute()
        {
            bool isSuccess = true;
            Log.LogMessage(MessageImportance.High, Resources.EFSCRIPT_Generating);
            isSuccess = GenerateEFSQLScriptsInternal();
            if (isSuccess)
            {
                Log.LogMessage(MessageImportance.High, Resources.EFSCRIPT_GenerationCompleted);
            }

            return isSuccess;
        }

        public bool GenerateEFSQLScriptsInternal(bool isLoggingEnabled = true)
        {
            InitializeProperties();
            EFSQLScripts = new ITaskItem[EFMigrations.Length];
            int index = 0;
            foreach (ITaskItem dbContext in EFMigrations)
            {
                string outputFileFullPath = Path.Combine(EFPublishDirectory, EFSQLScriptsFolderName, dbContext.ItemSpec + ".sql");
                bool isScriptGenerationSuccessful = GenerateSQLScript(outputFileFullPath, dbContext.ItemSpec, isLoggingEnabled);
                if (!isScriptGenerationSuccessful)
                {
                    return false;
                }

                ITaskItem sqlScriptItem = new TaskItem(outputFileFullPath);
                sqlScriptItem.SetMetadata("DBContext", dbContext.ItemSpec);
                string connectionStringEscaped = ProjectCollection.Escape(dbContext.GetMetadata("Value"));
                sqlScriptItem.SetMetadata("ConnectionString", connectionStringEscaped);
                EFSQLScripts[index] = sqlScriptItem;

                index++;
            }

            return true;
        }

        private void InitializeProperties()
        {
            if (string.IsNullOrEmpty(EFSQLScriptsFolderName))
            {
                EFSQLScriptsFolderName = "EFSQLScripts";
            }
        }

        private object _sync = new object();
        private Process _runningProcess;
        private int _processExitCode;
        private StringBuilder _standardOut = new StringBuilder();
        private StringBuilder _standardError = new StringBuilder();
        private const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
        private bool GenerateSQLScript(string sqlFileFullPath, string dbContextName, bool isLoggingEnabled = true)
        {
            string previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable(AspNetCoreEnvironment); 
            Environment.SetEnvironmentVariable(AspNetCoreEnvironment, "Development");
            ProcessStartInfo psi = new ProcessStartInfo("dotnet", $@"ef migrations script --no-build --idempotent --configuration {Configuration} --output ""{sqlFileFullPath}"" --context {dbContextName} {EFMigrationsAdditionalArgs}")
            {
                WorkingDirectory = ProjectDirectory,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            Process proc = null;

            try
            {
                if (isLoggingEnabled)
                {
                    Log.LogMessage(MessageImportance.High, string.Format("Executing command: {0} {1}", psi.FileName, psi.Arguments));
                }

                proc = new Process();
                proc.StartInfo = psi;
                proc.EnableRaisingEvents = true;
                proc.OutputDataReceived += Proc_OutputDataReceived;
                proc.ErrorDataReceived += Proc_ErrorDataReceived;
                proc.Exited += Proc_Exited;
                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();
                _runningProcess = proc;
            }
            catch (Exception e)
            {
                if (isLoggingEnabled)
                {
                    Log.LogError(e.ToString());
                }
                proc = null;
            }

            bool isProcessExited = false;
            if (proc != null)
            {
                isProcessExited = proc.WaitForExit(300000);
            }

            Environment.SetEnvironmentVariable(AspNetCoreEnvironment, previousAspNetCoreEnvironment);
            if (!isProcessExited || _processExitCode != 0)
            {
                if (isLoggingEnabled)
                {
                    Log.LogMessage(MessageImportance.High, _standardOut.ToString());
                    Log.LogError(Resources.EFSCRIPT_GenerationFailed);
                }
                return false;
            }

            return true;
        }

        private void Proc_Exited(object sender, EventArgs e)
        {
            if (_runningProcess != null)
            {
                try
                {
                    _processExitCode = _runningProcess.ExitCode;
                    _runningProcess.Exited -= Proc_Exited;
                    _runningProcess.ErrorDataReceived -= Proc_ErrorDataReceived;
                    _runningProcess.OutputDataReceived -= Proc_OutputDataReceived;
                }
                catch
                {
                }
                finally
                {
                    _runningProcess.Dispose();
                    _runningProcess = null;
                }
            }

        }

        private void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                lock (_sync)
                {
                    _standardError.AppendLine(e.Data);
                }
            }
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                lock (_sync)
                {
                    _standardOut.AppendLine(e.Data);
                }
            }
        }
    }
}
