# Reading AzDO PerfStar Pipeline Data from GitHub Actions (No PAT)

This document describes how the `read-azdo-perfstar.yml` workflow authenticates
to Azure DevOps and reads pipeline run data — without any PAT or stored credentials.

## Architecture

```
GitHub Actions ──► GitHub OIDC Provider ──► Azure AD (federated credential) ──► AzDO REST API
                   (JWT id-token)           (exchange for bearer token)          (Read Pipelines)
```

1. The workflow requests an OIDC JWT from GitHub's token endpoint
2. The JWT is exchanged with Azure AD via the managed identity's federated credential
3. Azure AD returns a bearer token scoped to Azure DevOps
4. The bearer token calls the AzDO REST API to read pipeline runs, logs, and artifacts

> **Important:** The `azure/login` GitHub Action is **blocked by org policy**
> in the `dotnet` org. The workflow uses **manual OIDC token exchange via `curl`**
> instead — no third-party action dependencies.

## Components

| Component | Value |
|-----------|-------|
| Managed Identity | `msbuild-azdo-reader` |
| Client ID | Stored in `AZDO_READER_CLIENT_ID` secret |
| Tenant | Microsoft (`72f988bf-86f1-41af-91ab-2d7cd011db47`) |
| Subscription | `CodeTestingAgentDev` (`bb947664-5d18-4aaa-8bbe-40dde6075462`) |
| Resource Group | `CodeTestingAgent` |
| AzDO Org/Project | `DevDiv` / `DevDiv` |
| Target Pipelines | 25429 (PerfStar-Scheduled), 25430 (PerfStar-Branch-Trigger) |
| Access Level | Read-only (View builds) |

## Setup Steps (Already Completed)

### 1. Create the Managed Identity

```bash
az account set --subscription "CodeTestingAgentDev"
az identity create --name "msbuild-azdo-reader" --resource-group "CodeTestingAgent" --location "eastus"
```

### 2. Add OIDC Federated Credentials

These allow GitHub Actions in `dotnet/msbuild` to authenticate as the identity:

```bash
# Main branch
az identity federated-credential create \
  --name github-dotnet-msbuild-main \
  --identity-name "msbuild-azdo-reader" \
  --resource-group "CodeTestingAgent" \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:dotnet/msbuild:ref:refs/heads/main" \
  --audiences "api://AzureADTokenExchange"

# Pull requests
az identity federated-credential create \
  --name github-dotnet-msbuild-pr \
  --identity-name "msbuild-azdo-reader" \
  --resource-group "CodeTestingAgent" \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:dotnet/msbuild:pull_request" \
  --audiences "api://AzureADTokenExchange"
```

> **Subject claim is case-sensitive.** The repo name in the subject must match
> exactly (e.g. `dotnet/msbuild`).

> **Microsoft tenant restriction:** Only repos in GitHub Enterprise orgs
> (`dotnet`, `microsoft`, etc.) work — personal forks fail with `AADSTS7002381`.

### 3. Register in AzDO

File a Service Ticket in DevDiv (Area: `DevDiv\VSEng\DDBuild\Operations`,
type: "AzDO Administration Request") to add the MI to the DevDiv org with
read access to pipelines 25429 and 25430.

### 4. GitHub Secrets

| Secret | Value |
|--------|-------|
| `AZDO_READER_CLIENT_ID` | Managed identity Client ID |
| `AZDO_READER_TENANT_ID` | `72f988bf-86f1-41af-91ab-2d7cd011db47` |
| `AZDO_READER_SUBSCRIPTION_ID` | `bb947664-5d18-4aaa-8bbe-40dde6075462` |

## How the Token Flow Works

```
1. Workflow declares `permissions: { id-token: write }` at workflow level
2. Step "Get OIDC Token" requests a JWT from GitHub's token endpoint
   ($ACTIONS_ID_TOKEN_REQUEST_URL, audience: api://AzureADTokenExchange)
3. Step "Exchange for AzDO Token" POSTs the JWT to Azure AD as a client_assertion
   (grant_type=client_credentials, scope: 499b84ac-.../.default)
4. Azure AD validates the JWT against the federated credential, returns a bearer token
5. Subsequent steps call AzDO REST APIs with the bearer token
```

## Usage

Trigger via `workflow_dispatch` from the Actions tab:

- **pipeline**: `scheduled` (25429) or `branch-trigger` (25430)
- **run_id**: Specific build ID (leave empty for latest)
- **count**: Number of recent runs to list (default: 10)

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `startup_failure` (no logs) | Third-party Action blocked by org policy | Use manual `curl` OIDC exchange, not `azure/login` |
| `AADSTS70021: No matching federated identity record` | Subject claim mismatch | Check exact casing and event type in federated credential |
| `AADSTS7002381: enterprise claim` | Personal fork | Only enterprise org repos work with Microsoft tenant |
| `403` from AzDO API | MI not added to DevDiv org, or no "View builds" permission | File DDBuild Operations ticket |
| `Failed to get OIDC token` | Missing `id-token: write` permission | Ensure permissions block is present |

## References

- [Use service principals and managed identities in Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/service-principal-managed-identity)
- [GitHub OIDC docs](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [AzDO Pipelines REST API](https://learn.microsoft.com/en-us/rest/api/azure/devops/pipelines/runs)
