#!/usr/bin/env bash
#
# Creates the Weapons of Order staging environment from nothing.
#
#   infra/azure/bootstrap.sh
#
# Runs anywhere the Azure CLI is signed in, including the Azure Portal's Cloud Shell, which
# already has az, bicep and python3 and is already authenticated as you.
#
# There is nothing to pass it. Browser V1 keeps its data in a SQLite file on the App Service
# instance, so there is no database server, no administrator password and no application
# password to generate or remember.
#
# What it does, in order: checks the subscription, registers the resource providers, confirms
# the .NET runtime is offered in the region, shows a what-if, deploys, and prints everything
# the GitHub `staging` Environment needs.
#
# The App Service plan is the Free tier, so this environment has no fixed monthly cost. See
# docs/deployment/AZURE_STAGING.md for what is still usage-based.
#
# Safe to re-run: the deployment is declarative.
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly TEMPLATE="$ROOT/infra/azure/main.bicep"
readonly PARAMETERS="$ROOT/infra/azure/main.bicepparam"
readonly DEPLOYMENT_NAME="${WOO_DEPLOYMENT_NAME:-woo-staging}"
readonly LOCATION="${WOO_LOCATION:-westeurope}"

# Parsing for the Azure CLI listing below. Kept in its own file because Azure changes the
# shape of that output, and a check that reads it has to be verifiable without an Azure
# subscription: see lib/az-output.test.sh.
# shellcheck source=./lib/az-output.sh
. "$ROOT/infra/azure/lib/az-output.sh"

die() { printf '\nERROR: %s\n' "$1" >&2; exit 1; }

step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }

# --- Preconditions -----------------------------------------------------------------------

az account show --output none 2>/dev/null || die "Not signed in. Run 'az login' first."

subscription="$(az account show --query name --output tsv)"
step "Subscription: $subscription"
printf 'Region:       %s\n' "$LOCATION"
printf 'Template:     %s\n' "$TEMPLATE"
read -r -p $'\nProvision staging into this subscription? Type yes to continue: ' answer
[ "$answer" = "yes" ] || die "Cancelled."

# --- Resource providers ------------------------------------------------------------------

step "Registering resource providers"
for provider in Microsoft.Web Microsoft.Communication \
                Microsoft.OperationalInsights Microsoft.Insights Microsoft.ManagedIdentity; do
    printf '    %s\n' "$provider"
    az provider register --namespace "$provider" --wait --output none
done

# --- Availability. Do not assume; the stack list moves. ------------------------------------

step "Checking the .NET runtime is available in $LOCATION"

# Read from the parameter file rather than a copy of it, so a parameter change cannot leave
# the check verifying the previous value.
runtime="$(az_bicepparam_value linuxRuntimeStack "$PARAMETERS")"
[ -n "$runtime" ] || die "Could not read 'linuxRuntimeStack' from $PARAMETERS."

# Captured once, then parsed. An empty answer means the CLI failed, which is a different
# problem from the runtime being unavailable and must not be reported as one.
runtimes="$(az webapp list-runtimes --os-type linux --output tsv)"
[ -n "$runtimes" ] || die "'az webapp list-runtimes --os-type linux' returned nothing. Check the Azure CLI is signed in."

if printf '%s\n' "$runtimes" | az_runtime_identifiers | az_contains_exactly "$runtime"; then
    printf '    App Service stack %s: available\n' "$runtime"
else
    printf '\n    App Service stack %s was NOT found.\n' "$runtime" >&2
    printf '    Do NOT downgrade the repository target framework to match.\n' >&2
    printf '    Available .NET stacks:\n' >&2
    printf '%s\n' "$runtimes" | az_runtime_identifiers | grep '^DOTNETCORE' | sed 's/^/      /' >&2
    die "Stop here and decide with the creator: a different region, or a self-contained publish."
fi

# --- Preview, then deploy -----------------------------------------------------------------

step "What-if: nothing is created by this"
az deployment sub what-if \
    --location "$LOCATION" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$TEMPLATE" \
    --parameters "$PARAMETERS"

read -r -p $'\nApply this? Type yes to create the resources: ' answer
[ "$answer" = "yes" ] || die "Cancelled. Nothing was created."

step "Deploying"
az deployment sub create \
    --location "$LOCATION" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$TEMPLATE" \
    --parameters "$PARAMETERS" \
    --output none

outputs="$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs --output json)"

read_output() { printf '%s' "$outputs" | python3 -c "import json,sys;print(json.load(sys.stdin)['$1']['value'])"; }

resource_group="$(read_output resourceGroup)"
staging_url="$(read_output stagingUrl)"
web_app="$(read_output webAppName)"
database_path="$(read_output databasePath)"
sender="$(read_output emailSenderAddress)"
client_id="$(read_output deploymentIdentityClientId)"
tenant_id="$(read_output tenantId)"
subscription_id="$(read_output subscriptionId)"

# --- What the creator still has to do ------------------------------------------------------

cat <<SUMMARY

================================================================================
 Staging is provisioned.

 Origin: $staging_url
 (Nothing is deployed to it yet. The first push to master will deploy the game.)
================================================================================

NEXT: create a GitHub Environment named exactly  staging
      Settings -> Environments -> New environment

Variables (all five; none of them is a secret):

  AZURE_CLIENT_ID          $client_id
  AZURE_TENANT_ID          $tenant_id
  AZURE_SUBSCRIPTION_ID    $subscription_id
  AZURE_RESOURCE_GROUP     $resource_group
  AZURE_WEBAPP_NAME        $web_app

Secrets: none.

There is no Azure client secret: deployment exchanges a short-lived GitHub OIDC token for
the identity above. There is no database secret either, because there is no database server.

The database is a SQLite file at:

  $database_path

It is on the instance's persistent /home share and outside the deployed application, so a
redeployment leaves it alone. The application applies its own migrations at startup. It is
not backed up: prototype data is disposable by decision.

Account email will arrive from:  $sender
Expect it in the spam folder — the generated domain has no reputation.

Verify a deployment afterwards with:
  scripts/smoke-staging.sh $staging_url

Tear the whole thing down with:
  az group delete --name $resource_group --yes

SUMMARY
