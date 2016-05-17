using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    public class VersionRepoUpdater
    {
        private static Regex s_nugetFileRegex = new Regex("^(.*?)\\.(([0-9]+\\.)?[0-9]+\\.[0-9]+(-([A-z0-9-]+))?)\\.nupkg$");

        private HttpClient _client = new HttpClient();
        private string _gitHubUser;
        private string _gitHubEmail;
        private string _versionsRepoOwner;
        private string _versionsRepo;

        public VersionRepoUpdater(
            string gitHubUser = null, 
            string gitHubEmail = null, 
            string versionRepoOwner = null, 
            string versionsRepo = null, 
            string gitHubAuthToken = null)
        {
            _gitHubUser = gitHubUser ?? "dotnet-bot";
            _gitHubEmail = gitHubEmail ?? "dotnet-bot@microsoft.com";
            _versionsRepoOwner = versionRepoOwner ?? "dotnet";
            _versionsRepo = versionsRepo ?? "versions";

            gitHubAuthToken = gitHubAuthToken ?? Environment.GetEnvironmentVariable("GITHUB_PASSWORD");

            if (string.IsNullOrEmpty(gitHubAuthToken))
            {
                throw new ArgumentException("A GitHub auth token is required and wasn't provided. Set 'GITHUB_PASSWORD' environment variable.", nameof(gitHubAuthToken));
            }

            _client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _client.DefaultRequestHeaders.Add("Authorization", $"token {gitHubAuthToken}");
            _client.DefaultRequestHeaders.Add("User-Agent", _gitHubUser);
        }

        public async Task UpdatePublishedVersions(string nupkgFilePath, string versionsRepoPath)
        {
            List<Tuple<string, string>> publishedPackages = GetPackageInfo(nupkgFilePath);

            string packageInfoFileContent = string.Join(
                Environment.NewLine,
                publishedPackages
                    .OrderBy(t => t.Item1)
                    .Select(t => $"{t.Item1} {t.Item2}"));

            string firstVersionWithPrerelease = publishedPackages
                .FirstOrDefault(t => t.Item2.Contains('-'))
                ?.Item2;

            string prereleaseVersion = null;
            if (!string.IsNullOrEmpty(firstVersionWithPrerelease))
            {
                prereleaseVersion = firstVersionWithPrerelease.Substring(firstVersionWithPrerelease.IndexOf('-') + 1);
            }

            string packageInfoFilePath = $"{versionsRepoPath}_Packages.txt";
            string message = $"Adding package info to {packageInfoFilePath} for {prereleaseVersion}";

            await UpdateGitHubFile(packageInfoFilePath, packageInfoFileContent, message);
        }

        private static List<Tuple<string, string>> GetPackageInfo(string nupkgFilePath)
        {
            List<Tuple<string, string>> packages = new List<Tuple<string, string>>();

            foreach (string filePath in Directory.GetFiles(nupkgFilePath, "*.nupkg"))
            {
                Match match = s_nugetFileRegex.Match(Path.GetFileName(filePath));

                packages.Add(Tuple.Create(match.Groups[1].Value, match.Groups[2].Value));
            }

            return packages;
        }

        private async Task UpdateGitHubFile(string path, string newFileContent, string commitMessage)
        {
            string fileUrl = $"https://api.github.com/repos/{_versionsRepoOwner}/{_versionsRepo}/contents/{path}";

            Console.WriteLine($"Getting the 'sha' of the current contents of file '{_versionsRepoOwner}/{_versionsRepo}/{path}'");

            string currentFile = await _client.GetStringAsync(fileUrl);
            string currentSha = JObject.Parse(currentFile)["sha"].ToString();

            Console.WriteLine($"Got 'sha' value of '{currentSha}'");

            Console.WriteLine($"Request to update file '{_versionsRepoOwner}/{_versionsRepo}/{path}' contents to:");
            Console.WriteLine(newFileContent);

            string updateFileBody = $@"{{
  ""message"": ""{commitMessage}"",
  ""committer"": {{
    ""name"": ""{_gitHubUser}"",
    ""email"": ""{_gitHubEmail}""
  }},
  ""content"": ""{ToBase64(newFileContent)}"",
  ""sha"": ""{currentSha}""
}}";

            Console.WriteLine("Sending request...");
            StringContent content = new StringContent(updateFileBody);

            using (HttpResponseMessage response = await _client.PutAsync(fileUrl, content))
            {
                response.EnsureSuccessStatusCode();
                Console.WriteLine("Updated the file successfully...");
            }
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }
    }
}
