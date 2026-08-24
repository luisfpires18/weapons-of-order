// The identity GitHub Actions deploys as.
//
// A user-assigned managed identity with a GitHub federated credential, so there is no Azure
// client secret anywhere: GitHub mints a short-lived OIDC token for the workflow run, Entra
// exchanges it, and nothing long-lived is stored in the repository.
//
// The federated subject names the GitHub Environment, not a branch. A workflow job that does
// not declare `environment: staging` cannot obtain a token at all, which is what makes the
// environment's own protection rules meaningful rather than decorative.

param location string

param identityName string

@description('owner/name.')
param gitHubRepository string

param gitHubEnvironment string

param siteName string

@description('''
Built-in Website Contributor: publish, restart and read a site's configuration. Not
Contributor on the resource group, which would also let a deployment delete the telemetry
and the email resources.

This is the only permission the deployment has. The database is a file inside the site, so
there is nothing else in the subscription for a deployment to reach.
''')
var websiteContributorRoleId = 'de139f84-1756-47ae-9be6-808fbbe84772'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-${gitHubEnvironment}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${gitHubRepository}:environment:${gitHubEnvironment}'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

resource site 'Microsoft.Web/sites@2024-11-01' existing = {
  name: siteName
}

resource publishAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: site
  name: guid(site.id, identity.id, websiteContributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', websiteContributorRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

@description('Not a secret. An identity without its federated trust proves nothing.')
output clientId string = identity.properties.clientId

output principalId string = identity.properties.principalId
