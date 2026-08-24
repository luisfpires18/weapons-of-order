// Opens the PostgreSQL server to the App Service outbound addresses, and to nothing else.
//
// Separate from both the database and the app module because it depends on both: the
// addresses come from the site, and the rules live on the server.
//
// `possibleOutboundIpAddresses` is the full set the site can leave from on its current plan
// and tier — the reason it is used rather than `outboundIpAddresses`, which is only the
// currently assigned subset and changes without warning. The full set still changes if the
// plan is scaled or moved, which is a redeployment of this module, not an outage to debug
// blind: the symptom is a readiness check that starts failing after a plan change.
//
// Deliberately NOT the 0.0.0.0 "allow all Azure services" rule. That rule admits every
// Azure resource in every tenant, which is a larger surface than the public internet minus
// a password.

param serverName string

@description('Comma-separated, as App Service reports it.')
param outboundIpAddresses string

var addresses = split(outboundIpAddresses, ',')

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' existing = {
  name: serverName
}

resource rules 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = [
  for (address, index) in addresses: {
    parent: server
    name: 'appservice-${index}'
    properties: {
      startIpAddress: trim(address)
      endIpAddress: trim(address)
    }
  }
]

output allowedAddressCount int = length(addresses)
