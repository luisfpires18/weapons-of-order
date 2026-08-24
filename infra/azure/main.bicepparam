// Parameters for the Weapons of Order staging environment.
//
// No secret is written here. The two passwords are read from the environment at deployment
// time, so this file is safe to commit and the values never touch the repository:
//
//   export WOO_PG_ADMIN_PASSWORD='...'
//   export WOO_PG_APP_PASSWORD='...'
//
// Generate them with a password manager, keep them there, and put the same two values into
// the GitHub `staging` Environment secrets. See docs/deployment/AZURE_STAGING.md.
using 'main.bicep'

param location = 'westeurope'
param resourceGroupName = 'rg-weaponsoforder-staging'
param environmentName = 'staging'

param appServicePlanSku = 'B1'
param linuxRuntimeStack = 'DOTNETCORE|10.0'

param postgresVersion = '18'
param postgresSkuName = 'Standard_B1ms'
param postgresSkuTier = 'Burstable'
param postgresStorageGb = 32
param databaseName = 'weapons_of_order_staging'
param postgresAdministratorLogin = 'woo_admin'
param postgresRuntimeLogin = 'woo_app'

param emailDataLocation = 'Europe'

param gitHubRepository = 'luisfpires18/weapons-of-order'
param gitHubEnvironment = 'staging'

param postgresAdministratorPassword = readEnvironmentVariable('WOO_PG_ADMIN_PASSWORD')
param postgresRuntimePassword = readEnvironmentVariable('WOO_PG_APP_PASSWORD')
