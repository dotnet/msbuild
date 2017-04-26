using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;
using System.IO;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public class AddProjectsToSolutionPostAction : IPostActionProcessor
    {
        public static readonly Guid ActionProcessorId = new Guid("D396686C-DE0E-4DE6-906D-291CD29FC5DE");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (string.IsNullOrEmpty(outputBasePath))
            {
                environment.Host.LogMessage(string.Format(LocalizableStrings.AddProjToSlnPostActionUnresolvedSlnFile));
                return false;
            }

            IReadOnlyList<string> nearestSlnFilesFould = FindSolutionFilesAtOrAbovePath(environment.Host.FileSystem, outputBasePath);
            if (nearestSlnFilesFould.Count != 1)
            {
                environment.Host.LogMessage(LocalizableStrings.AddProjToSlnPostActionUnresolvedSlnFile);
                return false;
            }

            if (!TryGetProjectFilesToAdd(environment, actionConfig, templateCreationResult, outputBasePath, out IReadOnlyList<string> projectFiles))
            {
                environment.Host.LogMessage(LocalizableStrings.AddProjToSlnPostActionNoProjFiles);
                return false;
            }

            Dotnet addProjToSlnCommand = Dotnet.AddProjectsToSolution(nearestSlnFilesFould[0], projectFiles);
            addProjToSlnCommand.CaptureStdOut();
            addProjToSlnCommand.CaptureStdErr();
            environment.Host.LogMessage(string.Format(LocalizableStrings.AddProjToSlnPostActionRunning, nearestSlnFilesFould[0], string.Join(" ", projectFiles)));
            Dotnet.Result commandResult = addProjToSlnCommand.Execute();

            if (commandResult.ExitCode != 0)
            {
                environment.Host.LogMessage(string.Format(LocalizableStrings.AddProjToSlnPostActionFailed, string.Join(" ", projectFiles), nearestSlnFilesFould[0]));
                environment.Host.LogMessage(string.Format(LocalizableStrings.CommandOutput, commandResult.StdOut + Environment.NewLine + Environment.NewLine + commandResult.StdErr));
                environment.Host.LogMessage(string.Empty);
                return false;
            }
            else
            {
                environment.Host.LogMessage(string.Format(LocalizableStrings.AddProjToSlnPostActionSucceeded, string.Join(" ", projectFiles), nearestSlnFilesFould[0]));
                return true;
            }
        }

        internal static IReadOnlyList<string> FindSolutionFilesAtOrAbovePath(IPhysicalFileSystem fileSystem, string outputBasePath)
        {
            return FileFindHelpers.FindFilesAtOrAbovePath(fileSystem, outputBasePath, "*.sln");
        }

        // The project files to add are a subset of the primary outputs, specifically the primary outputs indicated by the primaryOutputIndexes post action arg (semicolon separated)
        // If any indexes are out of range or non-numeric, thsi method returns false and projectFiles is set to null.
        internal static bool TryGetProjectFilesToAdd(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath, out IReadOnlyList<string> projectFiles)
        {
            if ((actionConfig.Args != null) && actionConfig.Args.TryGetValue("primaryOutputIndexes", out string projectIndexes))
            {
                List<string> filesToAdd = new List<string>();

                foreach (string indexString in projectIndexes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(indexString.Trim(), out int index))
                    {
                        if (templateCreationResult.PrimaryOutputs.Count <= index || index < 0)
                        {
                            projectFiles = null;
                            return false;
                        }

                        filesToAdd.Add(Path.Combine(outputBasePath, templateCreationResult.PrimaryOutputs[index].Path));
                    }
                    else
                    {
                        projectFiles = null;
                        return false;
                    }
                }

                projectFiles = filesToAdd;
                return true;
            }
            else
            {
                projectFiles = templateCreationResult.PrimaryOutputs.Select(x => x.Path).ToList();
                return true;
            }
        }
    }
}
