#!/usr/bin/env bash
#
# Creates the Weapons of Order staging environment from nothing.
#
#   export WOO_PG_ADMIN_PASSWORD="$(openssl rand -base64 30)"
#   export WOO_PG_APP_PASSWORD="$(openssl rand -base64 30)"
#   infra/azure/bootstrap.sh
#
# Runs anywhere the Azure CLI is signed in, including the Azure Portal's Cloud Shell, which
# already has az, bicep and psql and is already authenticated as you.
#
# What it does, in order: checks the subscription, registers the resource providers, confirms
# the runtime and database versions exist in the region, shows a what-if, deploys, creates the
# application's restricted database role, and prints everything the GitHub `staging`
# Environment needs.
#
# THIS CREATES BILLABLE RESOURCES. See docs/deployment/AZURE_STAGING.md for what they cost.
#
# Safe to re-run. The deployment is declarative and the role script resets a password rather
# than failing on an existing role.
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly TEMPLATE="$ROOT/infra/azure/main.bicep"
readonly PARAMETERS="$ROOT/infra/azure/main.bicepparam"
readonly DEPLOYMENT_NAME="${WOO_DEPLOYMENT_NAME:-woo-staging}"
readonly LOCATION="${WOO_LOCATION:-westeurope}"

# Named after nothing in particular, and removed in a trap, so an interrupted run does not
# leave the database reachable from an address nobody is watching.
readonly BOOTSTRAP_RULE="bootstrap-$(date +%s)"

# Parsing for the Azure CLI listings below. Kept in its own file because Azure changes the
# shape of that output, and a check that reads it has to be verifiable without an Azure
# subscription: see lib/az-output.test.sh.
# shellcheck source=./lib/az-output.sh
. "$ROOT/infra/azure/lib/az-output.sh"

die() { printf '\nERROR: %s\n' "$1" >&2; exit 1; }

step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }

# --- Preconditions -----------------------------------------------------------------------

az account show --output none 2>/dev/null || die "Not signed in. Run 'az login' first."

[ -n "${WOO_PG_ADMIN_PASSWORD:-}" ] || die "WOO_PG_ADMIN_PASSWORD is not set. Generate one with: openssl rand -base64 30"
[ -n "${WOO_PG_APP_PASSWORD:-}" ] || die "WOO_PG_APP_PASSWORD is not set. Generate one with: openssl rand -base64 30"
[ "$WOO_PG_ADMIN_PASSWORD" != "$WOO_PG_APP_PASSWORD" ] || die "The two passwords must differ. The point of the second role is that it is not the administrator."

command -v psql >/dev/null || die "psql is not on PATH. It is needed to create the application's database role."

subscription="$(az account show --query name --output tsv)"
step "Subscription: $subscription"
printf 'Region:       %s\n' "$LOCATION"
printf 'Template:     %s\n' "$TEMPLATE"
read -r -p $'\nProvision staging into this subscription? Type yes to continue: ' answer
[ "$answer" = "yes" ] || die "Cancelled."

# --- Resource providers ------------------------------------------------------------------

step "Registering resource providers"
for provider in Microsoft.Web Microsoft.DBforPostgreSQL Microsoft.Communication \
                Microsoft.OperationalInsights Microsoft.Insights Microsoft.ManagedIdentity; do
    printf '    %s\n' "$provider"
    az provider register --namespace "$provider" --wait --output none
done

# --- Availability. Do not assume; both lists move. ----------------------------------------

step "Checking the runtime and database versions are available in $LOCATION"

# Both checks read what the repository actually asks for rather than a copy of it, so a
# parameter change cannot leave the check verifying the previous value.
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

pg_version="$(az_bicepparam_value postgresVersion "$PARAMETERS")"
[ -n "$pg_version" ] || die "Could not read 'postgresVersion' from $PARAMETERS."

pg_versions="$(az postgres flexible-server list-skus --location "$LOCATION" \
    --query "[].supportedServerVersions[].name" --output tsv)"
[ -n "$pg_versions" ] || die "'az postgres flexible-server list-skus --location $LOCATION' returned nothing. Check the region name and that the CLI is signed in."

if printf '%s\n' "$pg_versions" | az_postgres_major_versions | az_contains_exactly "$pg_version"; then
    printf '    PostgreSQL %s: available\n' "$pg_version"
else
    printf '\n    PostgreSQL %s was NOT found in %s.\n' "$pg_version" "$LOCATION" >&2
    printf '    Do NOT change the major version to match; local development and CI run %s.\n' "$pg_version" >&2
    printf '    Available major versions:\n' >&2
    printf '%s\n' "$pg_versions" | az_postgres_major_versions | sort -u | sed 's/^/      /' >&2
    die "Stop here and decide with the creator: a different region, or a different major version."
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
db_server="$(read_output databaseServerName)"
db_host="$(read_output databaseHost)"
db_name="$(read_output databaseName)"
db_admin="$(read_output databaseAdministratorLogin)"
db_runtime="$(read_output databaseRuntimeLogin)"
sender="$(read_output emailSenderAddress)"
client_id="$(read_output deploymentIdentityClientId)"
tenant_id="$(read_output tenantId)"
subscription_id="$(read_output subscriptionId)"

# --- The one part Resource Manager cannot do ----------------------------------------------

close_firewall() {
    az postgres flexible-server firewall-rule delete \
        --resource-group "$resource_group" --name "$db_server" \
        --rule-name "$BOOTSTRAP_RULE" --yes --output none 2>/dev/null || true
}
trap close_firewall EXIT

step "Creating the application's restricted database role"
my_ip="$(curl --fail --silent --show-error --max-time 15 https://api.ipify.org)"
az postgres flexible-server firewall-rule create \
    --resource-group "$resource_group" --name "$db_server" \
    --rule-name "$BOOTSTRAP_RULE" \
    --start-ip-address "$my_ip" --end-ip-address "$my_ip" \
    --output none

PGPASSWORD="$WOO_PG_ADMIN_PASSWORD" psql \
    "host=$db_host port=5432 dbname=$db_name user=$db_admin sslmode=verify-full" \
    --set=ON_ERROR_STOP=1 \
    --set=runtime_role="$db_runtime" \
    --set=admin_role="$db_admin" \
    --set=runtime_password="$WOO_PG_APP_PASSWORD" \
    --file "$ROOT/infra/azure/database/create-runtime-role.sql"

close_firewall
trap - EXIT

# --- What the creator still has to do ------------------------------------------------------

cat <<SUMMARY

================================================================================
 Staging is provisioned.

 Origin: $staging_url
 (Nothing is deployed to it yet. The first push to master will deploy the game.)
================================================================================

NEXT: create a GitHub Environment named exactly  staging
      Settings -> Environments -> New environment

Variables (none of these is a secret):

  AZURE_CLIENT_ID          $client_id
  AZURE_TENANT_ID          $tenant_id
  AZURE_SUBSCRIPTION_ID    $subscription_id
  AZURE_RESOURCE_GROUP     $resource_group
  AZURE_WEBAPP_NAME        $web_app
  AZURE_POSTGRES_SERVER    $db_server
  POSTGRES_DATABASE        $db_name
  POSTGRES_ADMIN_LOGIN     $db_admin
  POSTGRES_RUNTIME_LOGIN   $db_runtime

Secrets (one):

  POSTGRES_ADMIN_PASSWORD  the value of \$WOO_PG_ADMIN_PASSWORD

There is deliberately no Azure client secret. Deployment authenticates by exchanging a
short-lived GitHub OIDC token for the identity above.

Keep both database passwords in your password manager. The runtime one is already an
App Service application setting and is not needed by GitHub.

Account email will arrive from:  $sender
Expect it in the spam folder — the generated domain has no reputation.

Verify a deployment afterwards with:
  scripts/smoke-staging.sh $staging_url

Tear the whole thing down with:
  az group delete --name $resource_group --yes

SUMMARY
