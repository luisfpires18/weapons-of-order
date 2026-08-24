// The Linux App Service that is the whole of Browser V1 staging: the built React client, the
// PWA files, /api, and the SQLite database, in one process on one instance.

param location string

param planName string

param siteName string

param planSku string

param planTier string

param linuxRuntimeStack string

param startupCommand string

@description('''
Where the SQLite database lives. Under /home, which is App Service's persistent share, and
deliberately NOT under the deployed application: a zip deployment replaces wwwroot, and a
database inside it would be replaced with it.
''')
param databasePath string

param workspaceId string

@secure()
param applicationInsightsConnectionString string

param emailSenderAddress string

param emailEndpoint string

@description('So the site identity can be granted the right to send on it.')
param communicationServiceName string

@description('''
Built-in role allowing an identity to send through a Communication Services resource.
Communication and Email Service Owner.
''')
var communicationSenderRoleId = '09976791-48a7-449e-bb21-39d1a415f350'

// Free and Shared plans do not offer Always On, and asking for it fails the deployment
// rather than being ignored. A cold start on the first request after an idle period is the
// price of the tier, and is acceptable for a prototype nobody is waiting on.
var alwaysOn = planTier != 'Free' && planTier != 'Shared'

// Health check is a Basic-and-above feature. On Free there is nothing to configure, and the
// deployment's own smoke test is what proves the application came up.
var healthCheckPath = alwaysOn ? '/api/health' : null

// Default Timeout is SQLite's busy timeout. One process, but many concurrent requests within
// it: a write that arrives during another write should wait rather than fail. Write-ahead
// logging, which is what keeps reads out of that queue entirely, is turned on by the
// application at startup.
var connectionString = 'Data Source=${databasePath};Default Timeout=30'

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: planSku
    tier: planTier
  }
  properties: {
    reserved: true
  }
}

resource app 'Microsoft.Web/sites@2024-11-01' = {
  name: siteName
  location: location
  kind: 'app,linux'
  identity: {
    // Used to send account email. Nothing else: the database is a file on this instance, and
    // the application holds no Azure subscription credential of any kind.
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    // Plain HTTP is refused at the platform edge, which is also why the application does
    // not run UseHttpsRedirection: a redirect behind a proxy that already redirects is how
    // loops happen.
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxRuntimeStack
      appCommandLine: startupCommand
      alwaysOn: alwaysOn
      healthCheckPath: healthCheckPath
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      // The client is precompiled and published; there is nothing for the platform's build
      // system to do, and letting it try is a way to fail a deployment for no reason.
      remoteDebuggingEnabled: false
    }
  }
}

// Separate from the site so it can read the site's own hostname. Auth:ClientBaseUrl has to
// be the real origin, and it is never derived from a request.
resource settings 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: app
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Staging'

    // Not Development, so: no development notification endpoint, no captured links, no
    // developer exception page, Secure cookies, and account email through a real provider.
    ConnectionStrings__WeaponsOfOrder: connectionString

    // One instance, one file, and the file lives on the instance. The schema travels with
    // the code, so the application applies its own migrations before serving anything. A
    // real PostgreSQL production environment would turn this off and migrate from outside.
    Database__MigrateOnStartup: 'true'

    Auth__ClientBaseUrl: 'https://${app.properties.defaultHostName}'

    // This process sits behind the App Service front end, so the scheme and the caller's
    // address both arrive in headers. See Hosting/ForwardedHeadersConfiguration.cs.
    Hosting__UseForwardedHeaders: 'true'

    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString

    Email__Provider: 'AzureCommunicationServices'
    Email__SenderAddress: emailSenderAddress
    Email__AzureCommunicationServices__Endpoint: emailEndpoint

    // The artifact is already published. Without this the platform tries to build it again
    // from sources that are not in the package.
    SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
  }
}

@description('''
Both deployment credential paths are disabled. The only way in is a Microsoft Entra token,
which means every deployment is attributable and there is no static credential to leak.
''')
resource disableScmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-11-01' = {
  parent: app
  name: 'scm'
  properties: {
    allow: false
  }
}

resource disableFtpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-11-01' = {
  parent: app
  name: 'ftp'
  properties: {
    allow: false
  }
}

// Platform and console logs to the workspace. Without these, a container that exits before
// the application starts leaves nothing behind but a 503, and Application Insights never
// saw the process at all — which on a tier with cold starts is the failure to expect.
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: app
  name: 'to-log-analytics'
  properties: {
    workspaceId: workspaceId
    logs: [
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
      }
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
    ]
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-06-01-preview' existing = {
  name: communicationServiceName
}

// Scoped to the one Communication Services resource. The site identity can send mail and
// reach nothing else in the subscription.
resource emailSenderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: communicationService
  name: guid(communicationService.id, app.id, communicationSenderRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', communicationSenderRoleId)
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output name string = app.name

output defaultHostName string = app.properties.defaultHostName

output databasePath string = databasePath
