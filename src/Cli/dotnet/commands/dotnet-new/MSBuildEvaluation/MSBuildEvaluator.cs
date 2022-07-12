// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    internal class MSBuildEvaluator : IIdentifiedComponent
    {
        private readonly ProjectCollection _projectCollection = new ProjectCollection();
        private string _outputDirectory;
        internal MSBuildEvaluator()
        {
            _outputDirectory = Directory.GetCurrentDirectory();
        }

        internal MSBuildEvaluator(string outputDirectory)
        {
            _outputDirectory = outputDirectory;
        }

        public Guid Id => Guid.Parse("{6C2CB5CA-06C3-460A-8ADB-5F21E113AB24}");

        internal MSBuildEvaluationResult EvaluateProject(IEngineEnvironmentSettings engineEnvironmentSettings)
        {
            IReadOnlyList<string> foundFiles = Array.Empty<string>();
            try
            {
                foundFiles = FileFindHelpers.FindFilesAtOrAbovePath(engineEnvironmentSettings.Host.FileSystem, _outputDirectory, "*.*proj");
            }
            catch (Exception)
            {
                //do nothing
                //in case of exception, no project found result is used.
            }

            if (foundFiles.Count == 0)
            {
                return MSBuildEvaluationResult.CreateNoProjectFound(_outputDirectory);
            }
            if (foundFiles.Count > 1)
            {
                return MultipleProjectsEvaluationResult.Create(foundFiles);
            }

            string projectPath = foundFiles.Single();
            try
            {
                Project evaluatedProject = RunEvaluate(projectPath);

                //if project is using Microsoft.NET.Sdk, then it is SDK-style project.
                bool IsSdkStyleProject = evaluatedProject.GetProperty("UsingMicrosoftNETSDK")?.EvaluatedValue == "true";

                IReadOnlyList<string>? targetFrameworks = evaluatedProject.GetProperty("TargetFrameworks")?.EvaluatedValue?.Split(";");
                string? targetFramework = evaluatedProject.GetProperty("TargetFramework")?.EvaluatedValue;

                if (!IsSdkStyleProject || string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks == null)
                {
                    //For non SDK style project, we cannot evaluate more info. Also there is no indication, whether the project
                    //was restored or not, so it is not checked.
                    return NonSDKStyleEvaluationResult.CreateSuccess(projectPath, evaluatedProject);
                }

                //For SDK-style project, if the project was restored "RestoreSuccess" property will be set to true.
                if (!evaluatedProject.GetProperty("RestoreSuccess")?.EvaluatedValue.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true)
                {
                    return MSBuildEvaluationResult.CreateNoRestore(projectPath);
                }

                //If target framework is set, no further evaluation is needed.
                if (!string.IsNullOrWhiteSpace(targetFramework))
                {
                    return SDKStyleEvaluationResult.CreateSuccess(projectPath, targetFramework, evaluatedProject);
                }

                //If target framework is not set, then presumably it is multi-target project.
                //If there are no target frameworks, it is not expected.
                if (targetFrameworks == null)
                {
                    throw new Exception($"Project '{projectPath}' is a SDK-style project, but does not specify the framework.");
                }

                //For multi-target project, we need to do additional evaluation for each target framework.
                Dictionary<string, Project?> evaluatedTfmBasedProjects = new Dictionary<string, Project?>();
                foreach (string tfm in targetFrameworks)
                {
                    evaluatedTfmBasedProjects[tfm] = RunEvaluate(projectPath, tfm);
                }
                return MultiTargetEvaluationResult.CreateSuccess(projectPath, evaluatedProject, evaluatedTfmBasedProjects);

            }
            catch (Exception e)
            {
                return MSBuildEvaluationResult.CreateFailure(projectPath, e.Message);
            }
        }

        private Project RunEvaluate(string projectToLoad, string? tfm = null)
        {
            if (!File.Exists(projectToLoad))
            {
                throw new FileNotFoundException(message: null, projectToLoad);
            }

            Project? project = GetLoadedProject(projectToLoad, tfm);
            if (project != null)
            {
                return project;
            }
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(tfm))
            {
                globalProperties["TargetFramework"] = tfm;
            }

            //We do only best effort here, also the evaluation should be fast vs complete; therefore ignoring imports errors.
            //The result of evaluation is used for the following:
            // - determining if the template can be run in the following context(constraints) based on Project Capabilities
            // - determining properties values that will be used in template content
            //The cost of the error is not substantial:
            //- worst case scenario the user can create a template, which should not be allowed to and it fails to compile / build-- > likely user will remove it or fix it manually then
            //- or the template content will be corrupted and consequent build fails --> the user may fix the issues manually if needed
            //- or the user will not see that template that is expected --> but they can always override it with --force
            //Therefore, we should not fail on missing imports or invalid imports, if this is the case rather restore/build should fail.
            return new Project(
                    projectToLoad,
                    globalProperties,
                    toolsVersion: null,
                    subToolsetVersion: null,
                    _projectCollection,
                    ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports);
        }

        private Project? GetLoadedProject(string projectToLoad, string? tfm)
        {
            Project? project;
            ICollection<Project> loadedProjects = _projectCollection.GetLoadedProjects(projectToLoad);
            if (string.IsNullOrEmpty(tfm))
            {
                project = loadedProjects.FirstOrDefault(project => !project.GlobalProperties.ContainsKey("TargetFramework"));
            }
            else
            {
                project = loadedProjects.FirstOrDefault(project =>
                    project.GlobalProperties.TryGetValue("TargetFramework", out string? targetFramework)
                    && targetFramework.Equals(tfm, StringComparison.OrdinalIgnoreCase));
            }

            if (project != null)
            {
                return project;
            }
            if (loadedProjects.Any())
            {
                foreach (Project loaded in loadedProjects)
                {
                    _projectCollection.UnloadProject(loaded);
                }
            }
            return null;
        }
    }
}
