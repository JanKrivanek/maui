# Triggering Azure DevOps Pipelines from GitHub Actions (No PAT)

This guide explains how to invoke Azure DevOps pipelines in the **DevDiv** organization
from GitHub Actions using **OIDC federated credentials** — no PAT or stored secrets needed.

## Architecture

```
GitHub Actions (OIDC) ──► Azure AD ──► Managed Identity ──► AzDO REST API
                                         (Bearer token)       (Run Pipeline)
```

1. GitHub Actions requests an OIDC token from GitHub's token service
2. `azure/login` exchanges it for an Azure AD session via federated credential
3. We acquire an Azure DevOps bearer token for the identity
4. We call the AzDO REST API to trigger the pipeline

## Prerequisites

- Azure CLI installed locally (for one-time setup)
- Access to an Azure subscription + resource group
- **Project Collection Administrator** (or delegated) access in the DevDiv AzDO org to add users
- GitHub repo admin access to configure secrets

---

## Step 1: Create a User-Assigned Managed Identity

```bash
# Choose your resource group and identity name
RG="rg-maui-automation"
IDENTITY_NAME="id-maui-azdo-trigger"
LOCATION="eastus"

# Create the resource group if it doesn't exist
az group create --name $RG --location $LOCATION

# Create the managed identity
az identity create --name $IDENTITY_NAME --resource-group $RG --location $LOCATION

# Capture the IDs you'll need
CLIENT_ID=$(az identity show --name $IDENTITY_NAME --resource-group $RG --query clientId -o tsv)
PRINCIPAL_ID=$(az identity show --name $IDENTITY_NAME --resource-group $RG --query principalId -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

echo "CLIENT_ID:       $CLIENT_ID"
echo "PRINCIPAL_ID:    $PRINCIPAL_ID"
echo "TENANT_ID:       $TENANT_ID"
echo "SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
```

## Step 2: Add OIDC Federated Credential for GitHub Actions

This lets GitHub Actions authenticate as the identity without storing any secrets.

```bash
# Allow from main branch
az identity federated-credential create \
  --name github-actions-main \
  --identity-name $IDENTITY_NAME \
  --resource-group $RG \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:dotnet/maui:ref:refs/heads/main" \
  --audiences "api://AzureADTokenExchange"
```

Add more federated credentials for other branches or trigger types as needed:

```bash
# Allow from any branch (workflow_dispatch)
az identity federated-credential create \
  --name github-actions-net11 \
  --identity-name $IDENTITY_NAME \
  --resource-group $RG \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:dotnet/maui:ref:refs/heads/net11.0" \
  --audiences "api://AzureADTokenExchange"

# Allow from pull_request events (if needed)
az identity federated-credential create \
  --name github-actions-pr \
  --identity-name $IDENTITY_NAME \
  --resource-group $RG \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:dotnet/maui:pull_request" \
  --audiences "api://AzureADTokenExchange"

# Allow from a specific environment (recommended for production triggers)
az identity federated-credential create \
  --name github-actions-env-azdo \
  --identity-name $IDENTITY_NAME \
  --resource-group $RG \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:dotnet/maui:environment:azdo-trigger" \
  --audiences "api://AzureADTokenExchange"
```

> **Tip:** Using a GitHub environment (`environment:azdo-trigger`) adds an extra
> layer of protection — you can require approvals and restrict which branches can
> trigger the pipeline. This is recommended for production use.

## Step 3: Add the Identity to Azure DevOps (DevDiv Organization)

The managed identity must be added as a user in the DevDiv AzDO organization:

1. Go to **https://dev.azure.com/DevDiv** → **Organization Settings** → **Users**
2. Click **Add users**
3. Search for the managed identity by its **display name** (e.g. `id-maui-azdo-trigger`)
4. Set **Access level** to **Basic** (required for build queue permissions)
5. Add the user to the **DevDiv** project
6. Click **Add**

### Grant Build Queue Permission

The identity needs the **"Queue builds"** permission on the target pipeline:

1. Go to **DevDiv** project → **Pipelines** → find pipeline **27723**
2. Click the **⋮** menu → **Manage security**
3. Find your managed identity user
4. Set **"Queue builds"** to **Allow**

Alternatively, add the identity to a group that already has queue permissions.

> **Important:** Use the identity's **Object (Principal) ID** from the
> **Enterprise Applications** pane in Entra admin center — NOT the App Registration object ID.

## Step 4: Set GitHub Repository Secrets

In **dotnet/maui** → **Settings** → **Secrets and variables** → **Actions**, add:

| Secret Name | Value |
|---|---|
| `AZDO_TRIGGER_CLIENT_ID` | The managed identity's Client ID |
| `AZDO_TRIGGER_TENANT_ID` | Your Azure AD Tenant ID |
| `AZDO_TRIGGER_SUBSCRIPTION_ID` | Your Azure Subscription ID |

> Using distinct secret names (prefixed with `AZDO_TRIGGER_`) avoids conflicts
> with any existing `AZURE_*` secrets in the repo.

## Step 5: Create the GitHub Actions Workflow

See [`.github/workflows/trigger-azdo-pipeline.yml`](../workflows/trigger-azdo-pipeline.yml) for a ready-to-use workflow.

## How It Works (Token Flow)

```
1. GitHub OIDC Provider issues a JWT for the workflow run
2. azure/login@v3 presents the JWT to Azure AD with the federated credential
3. Azure AD validates the JWT (issuer, subject, audience) and issues an Azure session
4. `az account get-access-token --resource 499b84ac-...` requests a token
   scoped to Azure DevOps (resource ID: 499b84ac-1321-427f-aa17-267ca6975798)
5. The token is used as a Bearer token in the AzDO REST API call
6. AzDO validates the token, checks the identity's permissions, and queues the build
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `AADSTS70021: No matching federated identity record found` | The federated credential `subject` doesn't match the workflow trigger. Check branch name, event type. |
| `403` from AzDO REST API | The identity doesn't have "Queue builds" permission, or hasn't been added to the DevDiv org. |
| `TF401444: Sign-in required` | The identity isn't added to the AzDO organization. |
| `The pipeline does not exist or you do not have permission` | Check pipeline ID and project name. The identity needs at least Reader + Queue builds. |
| `az account get-access-token` returns error | Ensure `azure/login` succeeded. Check that `allow-no-subscriptions` is set if the subscription has no resources. |

## References

- [Use service principals and managed identities in Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/service-principal-managed-identity)
- [AzDO Pipelines REST API — Run Pipeline](https://learn.microsoft.com/en-us/rest/api/azure/devops/pipelines/runs/run-pipeline?view=azure-devops-rest-7.1)
- [Azure Login GitHub Action](https://github.com/Azure/login)
- [GitHub OIDC token docs](https://docs.github.com/en/actions/security-guides/automatic-token-authentication)
