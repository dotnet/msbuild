# Microsoft.MSBuild.ReleaseSnappingMCP

A Model Context Protocol (MCP) server for automating MSBuild release management tasks. This server helps create release checklists, GitHub issues, and PRs needed during the MSBuild release process.

## Overview

The release checklist template is sourced from [`documentation/release-checklist.md`](../../documentation/release-checklist.md), which serves as the **primary source of truth** for the release process. When that file is updated, rebuild this MCP server to pick up the changes automatically.

## Features

- **Release Checklist Generation** - Generate release checklists with version placeholders filled in
- **GitHub Issue Creation** - Create release tracking issues with the checklist
- **Branding PRs** - Create version branding PRs (both regular and final branding)
- **VS Insertion Pipeline Updates** - Update pipeline YAML with new branch mappings
- **Merge Flow Configuration** - Update the git merge flow config for new branches
- **Localization Configuration** - Enable/disable localization for release branches
- **Branch Management** - Create release branches and get DARC command references

## Prerequisites

- .NET 10.0 SDK (or the version matching the MSBuild repo)
- GitHub Personal Access Token with `repo` scope (for creating issues and PRs)

## Building

```powershell
cd mcps/Microsoft.MSBuild.ReleaseSnappingMCP
dotnet build
```

## Running the MCP Server

The MCP server communicates via stdio and is designed to be used with AI assistants that support the Model Context Protocol.

### VS Code Configuration

Add to your VS Code `settings.json` or create a `.vscode/mcp.json` file in the repository root:

```json
{
  "mcpServers": {
    "msbuild-release": {
      "command": "dotnet",
      "args": ["run", "--project", "mcps/Microsoft.MSBuild.ReleaseSnappingMCP/Microsoft.MSBuild.ReleaseSnappingMCP.csproj"],
      "cwd": "${workspaceFolder}"
    }
  }
}
```

### Alternative: Run the Built Executable

```powershell
# Build first
dotnet build mcps/Microsoft.MSBuild.ReleaseSnappingMCP

# Run the server
dotnet mcps/Microsoft.MSBuild.ReleaseSnappingMCP/bin/Debug/net10.0/Microsoft.MSBuild.ReleaseSnappingMCP.dll
```

## Usage with GitHub Copilot

Once configured, you can use the MCP tools through GitHub Copilot in VS Code. Here are example prompts:

### Generate a Release Checklist

```
Generate a release checklist for MSBuild 18.4
```

### Create a GitHub Issue

```
Authenticate with GitHub using my token: ghp_xxxx
Then create a release checklist issue for version 18.4
```

### Create Branding PRs

```
Create a branding PR to bump MSBuild to version 18.5 with baseline 18.4.0-preview.25123.4
```

### Update Pipeline Configuration

```
Update the VS insertion pipeline for the 18.4 release
```

## Available Tools

### Authentication

| Tool | Description |
|------|-------------|
| `github_authenticate` | Authenticate with GitHub using a personal access token |
| `github_status` | Check authentication status |

### Release Checklist

| Tool | Description |
|------|-------------|
| `generate_release_checklist` | Generate a release checklist markdown for a version |
| `create_release_checklist_issue` | Create a GitHub issue with the release checklist |
| `get_version_info` | Get computed version information (previous, next, branches, etc.) |

### Branding PRs

| Tool | Description |
|------|-------------|
| `create_branding_pr` | Create a branding PR to bump version in eng/Versions.props |
| `create_final_branding_pr` | Create final branding PR with DotNetFinalVersionKind=release |

### Pipeline Configuration

| Tool | Description |
|------|-------------|
| `create_insertion_pipeline_update_pr` | Update VS insertion pipeline YAML with new branch mappings |

### Merge Flow

| Tool | Description |
|------|-------------|
| `create_merge_flow_update_pr` | Update git merge flow configuration |
| `get_merge_flow_config` | View current merge flow configuration |

### Localization

| Tool | Description |
|------|-------------|
| `create_enable_localization_pr` | Enable localization for a release branch |
| `create_disable_localization_pr` | Disable localization for an old release branch |
| `create_localization_comment_update_pr` | Update localization comment in main |

### Branch Management

| Tool | Description |
|------|-------------|
| `create_release_branch` | Create a new release branch |
| `get_darc_commands` | Get DARC commands needed for release channel setup |

## Release Workflow Example

Here's a typical workflow for cutting a new MSBuild release (e.g., 18.4):

1. **Authenticate with GitHub:**
   ```
   github_authenticate token=<your-github-token>
   ```

2. **Generate and review the checklist:**
   ```
   generate_release_checklist version=18.4
   ```

3. **Create the tracking issue:**
   ```
   create_release_checklist_issue version=18.4
   ```

4. **Create the release branch:**
   ```
   create_release_branch version=18.4
   ```

5. **Create the next version branding PR:**
   ```
   create_branding_pr newVersion=18.5 baselineVersion=18.4.0-preview.25xxx.x
   ```

6. **Update merge flow configuration:**
   ```
   create_merge_flow_update_pr newVersion=18.4 previousVersion=18.3
   ```

7. **Update VS insertion pipeline:**
   ```
   create_insertion_pipeline_update_pr currentVersion=18.4 nextVersion=18.5
   ```

8. **Configure localization:**
   ```
   create_enable_localization_pr version=18.4
   create_disable_localization_pr version=18.3
   ```

9. **At GA time, create final branding:**
   ```
   create_final_branding_pr version=18.4
   ```

## Updating the Release Checklist

The release checklist template is maintained in [`documentation/release-checklist.md`](../../documentation/release-checklist.md). This is the **single source of truth** for the release process.

To update the checklist:
1. Edit `documentation/release-checklist.md`
2. Rebuild this MCP server to embed the updated template
3. The generator will automatically use the new template

### Placeholder Variables

The template uses these placeholders that get replaced at generation time:

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{{THIS_RELEASE_VERSION}}` | Current release version | `18.4` |
| `{{PREVIOUS_RELEASE_VERSION}}` | Previous release version | `18.3` |
| `{{NEXT_VERSION}}` | Next release version | `18.5` |
| `{{THIS_RELEASE_EXACT_VERSION}}` | Exact version (with .0) | `18.4.0` |
| `{{URL_OF_*}}` | Various URL placeholders | `<!-- TODO: Add URL -->` |

## Troubleshooting

### MCP Server Not Starting

Ensure you have the correct .NET SDK version installed:
```powershell
dotnet --version
```

### GitHub Authentication Fails

- Verify your token has the `repo` scope
- Check if the token is expired
- Ensure you're calling `github_authenticate` before other GitHub operations

### Template Not Found

If the checklist template is not found, ensure:
1. The project was built successfully
2. `documentation/release-checklist.md` exists in the repository
3. The embedded resource is correctly configured in the `.csproj` file

## Architecture

```
Microsoft.MSBuild.ReleaseSnappingMCP/
├── Program.cs                      # MCP server entry point
├── ReleaseChecklistGenerator.cs    # Reads template from embedded resource
├── ReleaseVersion.cs               # Version parsing and calculation
├── GitHubService.cs                # GitHub API interactions
└── Tools/
    ├── AuthTools.cs                # GitHub authentication tools
    ├── ReleaseChecklistTools.cs    # Checklist generation tools
    ├── BranchTools.cs              # Branch management tools
    ├── BrandingPrTools.cs          # Branding PR tools
    ├── InsertionPipelineTools.cs   # VS insertion config tools
    ├── MergeFlowTools.cs           # Merge flow config tools
    └── LocalizationTools.cs        # Localization config tools
```

## Contributing

When updating this MCP server:

1. Keep the tools focused and single-purpose
2. Ensure error messages are helpful and actionable
3. Update this README if adding new tools
4. If changing the release process, update `documentation/release-checklist.md` first
