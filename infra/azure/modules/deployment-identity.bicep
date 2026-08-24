// The identity GitHub Actions deploys as.
//
// A user-assigned managed identity with a GitHub federated credential, so there is no Azure
// client secret anywhere: GitHub mints a short-lived OIDC token for the workflow run, Entra
// exchanges it, and nothing long-lived is stored in the repository.
//
// The federated subject names the GitHub Environment, not a branch. A workflow job that does
// not declare `environment: staging` cannot obtain a token at all, which is what makes the
// environment's own protection rules meaningful rather than decorative.
//
// The subject is built in GitHub's *immutable* form, which names the numeric owner id and
// repository id alongside the names:
//
//   repo:<owner>@<ownerId>/<name>@<repositoryId>:environment:<environment>
//
// The older mutable form — `repo:<owner>/<name>:environment:<environment>` — is what this
// module used to configure, and Entra rejected the real assertion with
// `AADSTS700213: No matching federated identity record found for presented assertion
// subject`. The numeric ids are what survive a rename of the account or of the repository:
// a name can be released and claimed by someone else, an id cannot. Both are public.

param location string

param identityName string

@description('owner/name.')
param gitHubRepository string

@description('''
The numeric id of the repository owner, from `GET /users/<owner>`. Not a secret — GitHub
publishes it — and it never changes, which is the point: it identifies the account even after
a rename, so a subject built from it cannot be claimed by whoever takes the old name.
''')
@minLength(1)
param gitHubRepositoryOwnerId string

@description('''
The numeric id of the repository itself, from `GET /repos/<owner>/<name>`. Non-secret and
immutable in the same way, and part of every subject GitHub now presents for this repository.
''')
@minLength(1)
param gitHubRepositoryId string

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

// Split rather than take owner and name as separate parameters, so `gitHubRepository` stays
// the single place the repository is named and the two halves cannot drift apart.
var repositoryOwner = split(gitHubRepository, '/')[0]
var repositoryName = split(gitHubRepository, '/')[1]

@description('''
The subject GitHub actually presents, assembled from its parts rather than pasted in whole,
so that changing the environment or the repository changes the credential with it.
''')
var federatedSubject = 'repo:${repositoryOwner}@${gitHubRepositoryOwnerId}/${repositoryName}@${gitHubRepositoryId}:environment:${gitHubEnvironment}'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-${gitHubEnvironment}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: federatedSubject
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

@description('''
Non-secret, and the one value worth reading back after a deployment: it must match the
`sub` claim of the token the workflow presents, character for character.
''')
output federatedSubject string = federatedSubject
