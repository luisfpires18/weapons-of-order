// Weapons of Order staging infrastructure.
//
// One deployment creates everything staging needs and nothing it does not: a resource group
// of its own, one Linux App Service serving both the React client and /api, one PostgreSQL
// Flexible Server, Application Insights over a capped Log Analytics workspace, Azure
// Communication Services for account email, and the federated identity GitHub Actions
// deploys with.
//
//   az deployment sub what-if --location westeurope --template-file infra/azure/main.bicep --parameters infra/azure/main.bicepparam
//
// Every SKU, version, region and name is a parameter. Nothing here is a secret except
// postgresAdministratorPassword, which is @secure() and must be supplied at deployment time
// — never from a committed parameter file.
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

@description('Azure region. Verify SKU and PostgreSQL version availability before changing.')
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
App Service plan SKU. B1 is the smallest that supports Always On, which a game with a cold
EF Core and PixiJS start wants. F1 is free but has a daily CPU quota, no Always On and
constant cold starts.
''')
@allowed([
  'F1'
  'B1'
  'B2'
  'P0v3'
])
param appServicePlanSku string = 'B1'

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
// Database
// ---------------------------------------------------------------------------------------

@description('PostgreSQL major version. Local development and CI run 18.')
param postgresVersion string = '18'

@description('Compute SKU. Burstable B1ms is the smallest that exists; staging has one player.')
param postgresSkuName string = 'Standard_B1ms'

@allowed([
  'Burstable'
  'GeneralPurpose'
  'MemoryOptimized'
])
param postgresSkuTier string = 'Burstable'

@description('Storage in GiB. 32 is the smallest Flexible Server accepts. It cannot be shrunk later.')
param postgresStorageGb int = 32

@description('Backup retention in days. 7 is the minimum and is included in the storage price.')
@minValue(7)
@maxValue(35)
param postgresBackupRetentionDays int = 7

@description('The staging database. Deliberately not the local or CI database name.')
param databaseName string = 'weapons_of_order_staging'

@description('''
Server administrator login. Used by migrations only. The application connects as a separate
restricted role created by infra/azure/database/create-runtime-role.sql.
''')
param postgresAdministratorLogin string = 'woo_admin'

@description('Server administrator password. Supply at deployment time; never commit it.')
@secure()
param postgresAdministratorPassword string

@description('''
The restricted role the running application connects as. It is created by
infra/azure/database/create-runtime-role.sql, not by Bicep, because Azure Resource Manager
cannot create a PostgreSQL role. Use the same login and password in both places.
''')
param postgresRuntimeLogin string = 'woo_app'

@description('''
Password for the application database role. Supply at deployment time; never commit it.
Separate from the administrator password on purpose: the running application can read and
write the game rows and cannot create, drop or alter a table.
''')
@secure()
param postgresRuntimePassword string

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

module database 'modules/database.bicep' = {
  scope: group
  params: {
    location: location
    serverName: 'psql-${affix}-${uniqueSuffix}'
    databaseName: databaseName
    postgresVersion: postgresVersion
    skuName: postgresSkuName
    skuTier: postgresSkuTier
    storageGb: postgresStorageGb
    backupRetentionDays: postgresBackupRetentionDays
    administratorLogin: postgresAdministratorLogin
    administratorPassword: postgresAdministratorPassword
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
    linuxRuntimeStack: linuxRuntimeStack
    startupCommand: appStartupCommand
    workspaceId: monitoring.outputs.workspaceId
    applicationInsightsConnectionString: monitoring.outputs.connectionString
    emailSenderAddress: email.outputs.senderAddress
    emailEndpoint: email.outputs.endpoint
    communicationServiceName: email.outputs.communicationServiceName
    databaseHost: database.outputs.fullyQualifiedDomainName
    databaseName: databaseName
    databaseRuntimeLogin: postgresRuntimeLogin
    databaseRuntimePassword: postgresRuntimePassword
  }
}

// Separate from the app module so it can depend on both: the rules are built from the
// site's own outbound addresses, which do not exist until the site does.
module databaseAccess 'modules/database-firewall.bicep' = {
  scope: group
  params: {
    serverName: database.outputs.name
    outboundIpAddresses: app.outputs.possibleOutboundIpAddresses
  }
}

// A role that can manage this server's firewall rules and read nothing else. Exists because
// migrations run from a GitHub-hosted runner whose address is not known in advance, so the
// deployment opens a rule for itself and removes it again. Contributor would let the same
// identity delete the database.
resource databaseFirewallRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(subscription().id, resourceGroupName, 'postgres-firewall')
  properties: {
    roleName: 'Weapons of Order PostgreSQL firewall (${resourceGroupName})'
    description: 'Create, read and delete firewall rules on a PostgreSQL Flexible Server. Nothing else.'
    type: 'CustomRole'
    assignableScopes: [
      subscription().id
    ]
    permissions: [
      {
        actions: [
          'Microsoft.DBforPostgreSQL/flexibleServers/read'
          'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules/read'
          'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules/write'
          'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules/delete'
        ]
        notActions: []
      }
    ]
  }
}

module deployment 'modules/deployment-identity.bicep' = {
  scope: group
  params: {
    location: location
    identityName: 'id-${affix}-deploy'
    gitHubRepository: gitHubRepository
    gitHubEnvironment: gitHubEnvironment
    siteName: app.outputs.name
    databaseServerName: database.outputs.name
    databaseFirewallRoleDefinitionId: databaseFirewallRole.id
  }
}

// ---------------------------------------------------------------------------------------
// Everything the creator has to put into the GitHub `staging` Environment, and nothing that
// is a secret. `az deployment sub show ... --query properties.outputs` prints these again.
// ---------------------------------------------------------------------------------------

output resourceGroup string = resourceGroupName
output stagingUrl string = 'https://${app.outputs.defaultHostName}'
output webAppName string = app.outputs.name
output databaseServerName string = database.outputs.name
output databaseHost string = database.outputs.fullyQualifiedDomainName
output databaseName string = databaseName
output databaseAdministratorLogin string = postgresAdministratorLogin
output databaseRuntimeLogin string = postgresRuntimeLogin
output emailSenderAddress string = email.outputs.senderAddress
output applicationInsightsName string = monitoring.outputs.applicationInsightsName

@description('Non-secret. Goes into the GitHub Environment as AZURE_CLIENT_ID.')
output deploymentIdentityClientId string = deployment.outputs.clientId

@description('Non-secret. Goes into the GitHub Environment as AZURE_TENANT_ID.')
output tenantId string = subscription().tenantId

@description('Non-secret. Goes into the GitHub Environment as AZURE_SUBSCRIPTION_ID.')
output subscriptionId string = subscription().subscriptionId
