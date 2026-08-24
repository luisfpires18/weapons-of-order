// Azure Database for PostgreSQL Flexible Server, and the one staging database on it.
//
// Public network access with an explicit allow list, not a private endpoint: migrations run
// from a GitHub-hosted runner whose address is not known until the job starts, and a private
// server would need either a self-hosted runner or a VNet the rest of this environment has
// no use for. Access mode cannot be changed after creation, so this is a decision, not a
// default.
//
// No firewall rule is created here. The server therefore starts unreachable, and every rule
// that exists is written deliberately: infra/azure/modules/database-firewall.bicep opens the
// App Service outbound addresses, and the deployment workflow opens and closes its own.

param location string

param serverName string

param databaseName string

param postgresVersion string

param skuName string

param skuTier string

param storageGb int

param backupRetentionDays int

param administratorLogin string

@secure()
param administratorPassword string

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: serverName
  location: location
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    version: postgresVersion
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    storage: {
      storageSizeGB: storageGb
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: backupRetentionDays
      // Staging has one region and no disaster-recovery requirement; geo-redundant backup
      // would roughly double the backup line on the bill for nothing.
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
    authConfig: {
      passwordAuth: 'Enabled'
      activeDirectoryAuth: 'Disabled'
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

@description('''
Refuses any connection that is not TLS. The server default already requires it; this makes
it explicit and survives a future default change.
''')
resource requireTls 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: server
  name: 'require_secure_transport'
  properties: {
    value: 'on'
    source: 'user-override'
  }
}

output name string = server.name

output fullyQualifiedDomainName string = server.properties.fullyQualifiedDomainName

output databaseName string = database.name
