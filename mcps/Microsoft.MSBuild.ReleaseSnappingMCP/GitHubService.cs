using Octokit;

namespace Microsoft.MSBuild.ReleaseSnappingMCP;

/// <summary>
/// Service for interacting with GitHub API to create issues and PRs.
/// </summary>
public sealed class GitHubService
{
    private const string Owner = "dotnet";
    private const string Repo = "msbuild";
    private const string GitHubTokenEnvVar = "GITHUB_TOKEN";

    private GitHubClient? _client;

    public GitHubService()
    {
        // Try to auto-initialize from environment variable
        TryInitializeFromEnvironment();
    }

    /// <summary>
    /// Attempts to initialize from the GITHUB_TOKEN environment variable.
    /// </summary>
    public bool TryInitializeFromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable(GitHubTokenEnvVar);
        if (!string.IsNullOrEmpty(token))
        {
            Initialize(token);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Initializes the GitHub client with a personal access token.
    /// </summary>
    public void Initialize(string token)
    {
        _client = new GitHubClient(new ProductHeaderValue("MSBuildReleaseSnappingMCP"))
        {
            Credentials = new Credentials(token)
        };
    }

    /// <summary>
    /// Gets whether the client is authenticated.
    /// </summary>
    public bool IsAuthenticated => _client is not null;

    private GitHubClient GetClient()
    {
        if (_client is null)
        {
            throw new InvalidOperationException(
                "GitHub client not initialized. Set the GITHUB_TOKEN environment variable or call the 'github_authenticate' tool with your token.");
        }
        return _client;
    }

    /// <summary>
    /// Creates a new GitHub issue.
    /// </summary>
    public async Task<Issue> CreateIssueAsync(string title, string body, string[]? labels = null)
    {
        var client = GetClient();
        var newIssue = new NewIssue(title)
        {
            Body = body
        };

        if (labels is not null)
        {
            foreach (var label in labels)
            {
                newIssue.Labels.Add(label);
            }
        }

        return await client.Issue.Create(Owner, Repo, newIssue);
    }

    /// <summary>
    /// Creates a new branch from an existing ref.
    /// </summary>
    public async Task<Reference> CreateBranchAsync(string newBranchName, string fromRef = "main")
    {
        var client = GetClient();

        // Get the reference for the source branch
        var sourceRef = await client.Git.Reference.Get(Owner, Repo, $"heads/{fromRef}");

        // Create the new branch
        var newRef = new NewReference($"refs/heads/{newBranchName}", sourceRef.Object.Sha);
        return await client.Git.Reference.Create(Owner, Repo, newRef);
    }

    /// <summary>
    /// Creates or updates a file in a repository.
    /// </summary>
    public async Task<RepositoryContentChangeSet> CreateOrUpdateFileAsync(
        string path,
        string content,
        string commitMessage,
        string branch)
    {
        var client = GetClient();

        try
        {
            // Try to get existing file
            var existingFile = await client.Repository.Content.GetAllContentsByRef(Owner, Repo, path, branch);
            var file = existingFile.FirstOrDefault();

            if (file is not null)
            {
                // Update existing file
                return await client.Repository.Content.UpdateFile(
                    Owner, Repo, path,
                    new UpdateFileRequest(commitMessage, content, file.Sha, branch));
            }
        }
        catch (NotFoundException)
        {
            // File doesn't exist, create it
        }

        // Create new file
        return await client.Repository.Content.CreateFile(
            Owner, Repo, path,
            new CreateFileRequest(commitMessage, content, branch));
    }

    /// <summary>
    /// Creates a pull request.
    /// </summary>
    public async Task<PullRequest> CreatePullRequestAsync(
        string title,
        string body,
        string headBranch,
        string baseBranch = "main")
    {
        var client = GetClient();

        var newPr = new NewPullRequest(title, headBranch, baseBranch)
        {
            Body = body
        };

        return await client.PullRequest.Create(Owner, Repo, newPr);
    }

    /// <summary>
    /// Gets the content of a file from the repository.
    /// </summary>
    public async Task<string> GetFileContentAsync(string path, string branch = "main")
    {
        var client = GetClient();
        var contents = await client.Repository.Content.GetAllContentsByRef(Owner, Repo, path, branch);
        var file = contents.FirstOrDefault()
            ?? throw new InvalidOperationException($"File not found: {path}");

        return file.Content;
    }

    /// <summary>
    /// Gets the current user's login name.
    /// </summary>
    public async Task<string> GetCurrentUserAsync()
    {
        var client = GetClient();
        var user = await client.User.Current();
        return user.Login;
    }
}
