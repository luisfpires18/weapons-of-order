// Account email: confirmation and password-reset delivery.
//
// Azure Communication Services with an Azure-managed domain, so staging needs no DNS work
// and no Weapons of Order domain. A managed domain is throttled to a handful of messages a
// minute and is explicitly test-only; a real player-facing environment needs a verified
// custom domain, which is a separate decision with a separate cost.
//
// The resources themselves are free. Sending is charged per message and per MB.

param communicationServiceName string

param emailServiceName string

@description('Where Communication Services stores data. Fixed at creation.')
param dataLocation string

// 'global' is the only location these accept; the data residency choice is dataLocation.
resource emailService 'Microsoft.Communication/emailServices@2023-06-01-preview' = {
  name: emailServiceName
  location: 'global'
  properties: {
    dataLocation: dataLocation
  }
}

resource managedDomain 'Microsoft.Communication/emailServices/domains@2023-06-01-preview' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
    // Off. This is a credential delivery: an open or click beacon on a password-reset mail
    // is both a privacy leak and the exact shape players are told to distrust.
    userEngagementTracking: 'Disabled'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-06-01-preview' = {
  name: communicationServiceName
  location: 'global'
  properties: {
    dataLocation: dataLocation
    linkedDomains: [
      managedDomain.id
    ]
  }
}

output communicationServiceName string = communicationService.name

@description('Authenticated with the App Service managed identity, so no access key exists to leak.')
output endpoint string = 'https://${communicationService.properties.hostName}'

@description('DoNotReply on the generated domain. The only address the managed domain will send as.')
output senderAddress string = 'DoNotReply@${managedDomain.properties.fromSenderDomain}'
