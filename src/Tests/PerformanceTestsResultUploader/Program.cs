using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Reporting;

namespace PerformanceTestsResultUploader
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            Option outputPath = new Option(
                "--output",
                "path of output file",
                new Argument<FileInfo>());

            Option repositoryRoot = new Option(
                "--repository-root",
                "repository root that contain .git directory",
                new Argument<DirectoryInfo>());

            Option sas = new Option(
                "--sas",
                "shared access signatures",
                new Argument<string>());

            // Add them to the root command
            RootCommand rootCommand = new RootCommand();
            rootCommand.Description = "Performance tests result generator";
            rootCommand.AddOption(outputPath);
            rootCommand.AddOption(repositoryRoot);
            rootCommand.AddOption(sas);

            var result = rootCommand.Parse(args);
            var outputPathValue = result.ValueForOption<FileInfo>("output");

            if (outputPathValue == null)
            {
                throw new PerformanceTestsResultUploaderException("--output option is required.");
            }

            var repositoryRootValue = result.ValueForOption<DirectoryInfo>("repository-root");

            if (repositoryRootValue == null)
            {
                throw new PerformanceTestsResultUploaderException("--repository-root is required.");
            }

            var sasValue = result.ValueForOption<string>("sas");

            if (sasValue == null)
            {
                throw new PerformanceTestsResultUploaderException("--sas is required.");
            }

            Reporter reporter = Reporter.CreateReporter(repositoryRootValue);
            string generatedJson = reporter.GetJson();
            Console.WriteLine("Generated json:" + Environment.NewLine + generatedJson);
            File.WriteAllText(outputPathValue.FullName, generatedJson);

            Uploader.Upload(outputPathValue, sasValue);
            return 0;
        }
    }
}
