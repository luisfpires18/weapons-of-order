// Parameters for the Weapons of Order staging environment.
//
// No secret is here and none is needed. There is no database server to hold a password for:
// Browser V1 stores its data in a SQLite file on the App Service instance, and the deployment
// authenticates with a short-lived GitHub OIDC token rather than a stored credential.
using 'main.bicep'

param location = 'westeurope'
param resourceGroupName = 'rg-weaponsoforder-staging'
param environmentName = 'staging'

// Free tier. No Always On, cold starts, a daily CPU quota — all acceptable for a prototype
// with one player, and the App Service plan was the largest recurring cost in this
// environment.
param appServicePlanSku = 'F1'
param appServicePlanTier = 'Free'
param linuxRuntimeStack = 'DOTNETCORE|10.0'

// Under /home, which persists across deployments and restarts, and outside the deployed
// application, which a deployment replaces wholesale.
param databasePath = '/home/data/weapons-of-order.db'

param emailDataLocation = 'Europe'

// The federated credential's subject is assembled from these four values. GitHub presents
// the immutable subject for this repository — names *and* numeric ids —
//
//   repo:luisfpires18@29492601/weapons-of-order@1329180174:environment:staging
//
// and Entra matches it literally, so an id that is wrong or missing fails the deployment's
// sign-in with AADSTS700213 rather than anything that reads as a configuration problem.
// Neither id is a secret and neither changes when the account or the repository is renamed.
param gitHubRepository = 'luisfpires18/weapons-of-order'
param gitHubRepositoryOwnerId = '29492601'
param gitHubRepositoryId = '1329180174'
param gitHubEnvironment = 'staging'
