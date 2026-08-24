// The Linux App Service that serves the whole game from one origin: the built React client
// and the PWA files as static content, and /api from the same process.

param location string

param planName string

param siteName string

param planSku string

param linuxRuntimeStack string

param startupCommand string

param workspaceId string

@secure()
param applicationInsightsConnectionString string

param emailSenderAddress string

param emailEndpoint string

@description('So the site identity can be granted the right to send on it.')
param communicationServiceName string

param databaseHost string

param databaseName string

@description('The restricted role the application connects as. Not the server administrator.')
param databaseRuntimeLogin string

@secure()
param databaseRuntimePassword string

@description('''
Built-in role allowing an identity to send through a Communication Services resource.
Communication and Email Service Owner.
''')
var communicationSenderRoleId = '09976791-48a7-449e-bb21-39d1a415f350'

// F1 has no Always On, so asking for it fails the deployment rather than being ignored.
var alwaysOn = planSku != 'F1'

// Maximum Pool Size is well under what a Burstable B1ms server allows (its max_connections
// is in the low tens), so a pool that fills does not lock the administrator out of the
// server it needs to be repaired from.
//
// VerifyFull, not Require: Require encrypts without checking who answered. Azure's
// certificate chain is in the platform trust store, so there is no reason to accept less.
var runtimeConnectionString = join(
  [
    'Host=${databaseHost}'
    'Port=5432'
    'Database=${databaseName}'
    'Username=${databaseRuntimeLogin}'
    'Password=${databaseRuntimePassword}'
    'SSL Mode=VerifyFull'
    'Maximum Pool Size=20'
    'Timeout=15'
  ],
  ';'
)

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: planSku
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
    // Used to send account email. Nothing else: the database is reached with a password,
    // and the application holds no Azure subscription credential of any kind.
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
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      // Liveness, not readiness. App Service recycles an instance that fails this check,
      // and a database outage is not something restarting the process fixes. Readiness is
      // checked by the deployment smoke test instead.
      healthCheckPath: '/api/health'
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
    ConnectionStrings__WeaponsOfOrder: runtimeConnectionString
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
// saw the process at all.
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

@description('Every address the site can leave from on this plan, comma-separated.')
output possibleOutboundIpAddresses string = app.properties.possibleOutboundIpAddresses
