// Log Analytics and the workspace-based Application Insights resource over it.
//
// Workspace-based rather than classic: classic Application Insights resources are retired,
// and a workspace is what App Service platform and console logs can be routed to, which is
// how a container that refuses to start is diagnosed.

param location string

param workspaceName string

param applicationInsightsName string

param retentionDays int

@description('Hard daily ingestion cap. Data is dropped past it until the next day.')
param dailyQuotaGb int

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionDays
    // The only unbounded cost in this environment is telemetry volume, so it is bounded
    // here rather than watched. Staging losing spans past the cap is the intended trade.
    workspaceCapping: {
      dailyQuotaGb: dailyQuotaGb
    }
    features: {
      // No reason for anything outside Azure to query this directly.
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    // The application authenticates its own exporter with the connection string; nothing
    // needs the local ingestion key path.
    DisableLocalAuth: false
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output workspaceId string = workspace.id

output applicationInsightsName string = applicationInsights.name

@description('Carries an ingestion key. Delivered straight into an application setting, never printed.')
output connectionString string = applicationInsights.properties.ConnectionString
