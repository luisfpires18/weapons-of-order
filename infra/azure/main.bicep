// Weapons of Order staging infrastructure.
//
// One deployment creates everything staging needs and nothing it does not: a resource group
// of its own, one Linux App Service on the Free tier serving both the React client and /api,
// Application Insights over a capped Log Analytics workspace, Azure Communication Services
// for account email, and the federated identity GitHub Actions deploys with.
//
// There is no database resource. Browser V1 is a prototype with one player, so its store is a
// SQLite file on the App Service instance's own persistent /home share — nothing to run,
// nothing to pay for, and nothing to keep patched. PostgreSQL remains the intended direction
// for a real production environment; see docs/architecture/TECH_STACK.md.
//
//   az deployment sub what-if --location westeurope --template-file infra/azure/main.bicep --parameters infra/azure/main.bicepparam
//
// Every SKU, version, region and name is a parameter, and nothing here is a secret.
targetScope = 'subscription'

// ---------------------------------------------------------------------------------------
// Naming and placement
// ---------------------------------------------------------------------------------------

@description('Short prefix for every resource name. Lowercase letters and digits only.')
@minLength(2)
@maxLength(8)
param namePrefix string = 'woo'

@description('Environment discriminator. Part of every resource name.')
@minLength(3)
@maxLength(12)
param environmentName string = 'staging'

@description('Azure region. Verify runtime availability before changing.')
param location string = 'westeurope'

@description('Resource group holding the whole environment. Deleting it deletes the environment.')
param resourceGroupName string = 'rg-weaponsoforder-staging'

@description('''
Suffix for the names Azure requires to be globally unique. Defaults to a hash of the
subscription and resource group, so a redeployment reuses the same names and a different
environment gets different ones.
''')
@minLength(4)
@maxLength(8)
param uniqueSuffix string = substring(uniqueString(subscription().subscriptionId, resourceGroupName), 0, 6)

// ---------------------------------------------------------------------------------------
// Application hosting
// ---------------------------------------------------------------------------------------

@description('''
App Service plan SKU. F1 is the Free tier and is what Browser V1 staging runs on: no Always
On, cold starts, and a daily CPU quota, all of which are acceptable for a prototype with one
player. B1 buys Always On and a health check probe if that ever matters.
''')
@allowed([
  'F1'
  'B1'
  'B2'
  'P0v3'
])
param appServicePlanSku string = 'F1'

@description('Must match the SKU. Free for F1, Basic for B1/B2, PremiumV3 for P0v3.')
@allowed([
  'Free'
  'Basic'
  'PremiumV3'
])
param appServicePlanTier string = 'Free'

@description('''
Linux runtime stack. Confirm against `az webapp list-runtimes --os-type linux` for the
target region before changing; the repository targets .NET 10 and must not be downgraded to
match a stale stack list.
''')
param linuxRuntimeStack string = 'DOTNETCORE|10.0'

@description('''
Startup command. Explicit rather than relying on platform auto-detection, so a published
artifact that gains a second assembly still starts the right one.
''')
param appStartupCommand string = 'dotnet WeaponsOfOrder.Api.dll'

// ---------------------------------------------------------------------------------------
// Persistence
// ---------------------------------------------------------------------------------------

@description('''
The SQLite database file.

It must stay under /home, which is the only persistent filesystem an App Service instance
has, and it must stay OUTSIDE the deployed application: a zip deployment replaces the
application directory wholesale, and a database inside it would be replaced with it. /home
is persistent by default for a built-in Linux runtime, so no storage setting is needed.

Backed up by nothing. Prototype data is disposable by decision.
''')
param databasePath string = '/home/data/weapons-of-order.db'

// ---------------------------------------------------------------------------------------
// Telemetry
// ---------------------------------------------------------------------------------------

@description('Log Analytics retention in days. 30 is the free floor for a PerGB2018 workspace.')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

@description('''
Hard daily ingestion cap in GiB. Past it the workspace drops data until the next day, which
is the intended behaviour for staging: losing telemetry is preferable to an unbounded bill.
''')
param logDailyQuotaGb int = 1

// ---------------------------------------------------------------------------------------
// Email
// ---------------------------------------------------------------------------------------

@description('''
Where Azure Communication Services stores its data. Cannot be changed after creation.
''')
@allowed([
  'Europe'
  'UnitedStates'
  'Australia'
  'UnitedKingdom'
])
param emailDataLocation string = 'Europe'

// ---------------------------------------------------------------------------------------
// GitHub deployment identity
// ---------------------------------------------------------------------------------------

@description('The repository GitHub Actions deploys from, as owner/name.')
param gitHubRepository string = 'luisfpires18/weapons-of-order'

@description('''
The repository owner's numeric id, as `gh api users/luisfpires18 --jq .id` prints it.

Not a secret; GitHub publishes it. It is here because GitHub's OIDC subject for this
repository is the immutable form, which carries the numeric ids as well as the names:
`repo:<owner>@<ownerId>/<name>@<repositoryId>:environment:<environment>`. A credential
configured without them does not match the presented assertion and Entra refuses the
exchange.
''')
@minLength(1)
param gitHubRepositoryOwnerId string = '29492601'

@description('''
The repository's own numeric id, as `gh api repos/luisfpires18/weapons-of-order --jq .id`
prints it. Non-secret and immutable in the same way, and the other half of the pair the
immutable subject is built from.
''')
@minLength(1)
param gitHubRepositoryId string = '1329180174'

@description('''
The GitHub Environment the deployment job declares. The federated credential is bound to it,
so a workflow that does not run in this environment cannot obtain a token.
''')
param gitHubEnvironment string = 'staging'

// ---------------------------------------------------------------------------------------

var affix = '${namePrefix}-${environmentName}'

resource group 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: {
    application: 'weapons-of-order'
    environment: environmentName
    managedBy: 'infra/azure/main.bicep'
  }
}

module monitoring 'modules/monitoring.bicep' = {
  scope: group
  params: {
    location: location
    workspaceName: 'log-${affix}'
    applicationInsightsName: 'appi-${affix}'
    retentionDays: logRetentionDays
    dailyQuotaGb: logDailyQuotaGb
  }
}

module email 'modules/email.bicep' = {
  scope: group
  params: {
    communicationServiceName: 'acs-${affix}-${uniqueSuffix}'
    emailServiceName: 'acsemail-${affix}-${uniqueSuffix}'
    dataLocation: emailDataLocation
  }
}

module app 'modules/app.bicep' = {
  scope: group
  params: {
    location: location
    planName: 'plan-${affix}'
    siteName: 'app-${affix}-${uniqueSuffix}'
    planSku: appServicePlanSku
    planTier: appServicePlanTier
    linuxRuntimeStack: linuxRuntimeStack
    startupCommand: appStartupCommand
    databasePath: databasePath
    workspaceId: monitoring.outputs.workspaceId
    applicationInsightsConnectionString: monitoring.outputs.connectionString
    emailSenderAddress: email.outputs.senderAddress
    emailEndpoint: email.outputs.endpoint
    communicationServiceName: email.outputs.communicationServiceName
  }
}

module deployment 'modules/deployment-identity.bicep' = {
  scope: group
  params: {
    location: location
    identityName: 'id-${affix}-deploy'
    gitHubRepository: gitHubRepository
    gitHubRepositoryOwnerId: gitHubRepositoryOwnerId
    gitHubRepositoryId: gitHubRepositoryId
    gitHubEnvironment: gitHubEnvironment
    siteName: app.outputs.name
  }
}

// ---------------------------------------------------------------------------------------
// Everything the creator has to put into the GitHub `staging` Environment, and nothing that
// is a secret. `az deployment sub show ... --query properties.outputs` prints these again.
// ---------------------------------------------------------------------------------------

output resourceGroup string = resourceGroupName
output stagingUrl string = 'https://${app.outputs.defaultHostName}'
output webAppName string = app.outputs.name
output databasePath string = app.outputs.databasePath
output emailSenderAddress string = email.outputs.senderAddress
output applicationInsightsName string = monitoring.outputs.applicationInsightsName

@description('Non-secret. Goes into the GitHub Environment as AZURE_CLIENT_ID.')
output deploymentIdentityClientId string = deployment.outputs.clientId

@description('Non-secret. Goes into the GitHub Environment as AZURE_TENANT_ID.')
output tenantId string = subscription().tenantId

@description('Non-secret. Goes into the GitHub Environment as AZURE_SUBSCRIPTION_ID.')
output subscriptionId string = subscription().subscriptionId

@description('''
Non-secret. Not a GitHub setting — the subject the federated credential was configured with,
printed so it can be compared against the `sub` claim of a real workflow token. They must be
identical; when they are not, Entra answers AADSTS700213 and the deployment job fails at
sign-in.
''')
output deploymentFederatedSubject string = deployment.outputs.federatedSubject
