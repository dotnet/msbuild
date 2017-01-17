using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
        public string EFSQLScriptsFolderName { get; set; }
        [Output]
        public ITaskItem[] EFSQLScripts { get; set; }

        public override bool Execute()
        {
            bool isSuccess = true;
            Log.LogMessage(MessageImportance.High, $"Generating Entity framework SQL Scripts...");
            isSuccess = GenerateEFSQLScriptsInternal();
            if (isSuccess)
            {
                Log.LogMessage(MessageImportance.High, $"Generating Entity framework SQL Scripts completed successfully");
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
                bool isScriptGeneratioNSuccessful = GenerateSQLScript(outputFileFullPath, dbContext.ItemSpec, isLoggingEnabled);
                if (!isScriptGeneratioNSuccessful)
                {
                    return false;
                }

                ITaskItem sqlScriptItem = new TaskItem(outputFileFullPath);
                sqlScriptItem.SetMetadata("DBContext", dbContext.ItemSpec);
                sqlScriptItem.SetMetadata("ConnectionString", dbContext.GetMetadata("Value"));
                sqlScriptItem.SetMetadata("EscapedPath", Regex.Escape(outputFileFullPath));
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

        private bool GenerateSQLScript(string sqlFileFullPath, string dbContextName, bool isLoggingEnabled = true)
        {
            ProcessStartInfo psi = new ProcessStartInfo("dotnet", string.Format("ef migrations script --idempotent --output \"{0}\" --context {1}", sqlFileFullPath, dbContextName))
            {
                WorkingDirectory = ProjectDirectory,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                UseShellExecute = false
            };
            psi.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "true";

            Process proc = null;

            try
            {
                if (isLoggingEnabled)
                {
                    Log.LogMessage(MessageImportance.High, string.Format("Executing command: {0} {1}", psi.FileName, psi.Arguments));
                }
                proc = Process.Start(psi);
            }
            catch(Exception e)
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

            if (!isProcessExited || proc == null || proc.ExitCode != 0)
            {
                if (isLoggingEnabled)
                {
                    Log.LogMessage(MessageImportance.High, $"Entity framework SQL Script generation failed");
                }
                return false;
            }

            return true;
        }
    }
}
