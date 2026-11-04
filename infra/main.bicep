targetScope = 'resourceGroup'

@description('Short lowercase prefix for resource names (3-12 characters).')
@minLength(3)
@maxLength(12)
param namePrefix string = 'acaecom'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('SQL Server administrator login used only for initial server provisioning. Prefer Entra ID auth for applications.')
param sqlAdminLogin string

@secure()
@description('SQL Server administrator password. Supply securely at deployment time; never commit this value.')
param sqlAdminPassword string

@description('Container image references, built and pushed to the created ACR before deployment.')
param catalogImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param inventoryImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param orderImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param paymentImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param webImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@minValue(0)
@maxValue(20)
param webMinReplicas int = 1
@minValue(1)
@maxValue(100)
param webMaxReplicas int = 10
@minValue(0)
@maxValue(20)
param catalogMinReplicas int = 0
@minValue(1)
@maxValue(100)
param catalogMaxReplicas int = 20
@minValue(0)
@maxValue(20)
param inventoryMinReplicas int = 1
@minValue(1)
@maxValue(100)
param inventoryMaxReplicas int = 30
@minValue(0)
@maxValue(20)
param orderMinReplicas int = 1
@minValue(1)
@maxValue(100)
param orderMaxReplicas int = 30
@minValue(0)
@maxValue(20)
param paymentMinReplicas int = 1
@minValue(1)
@maxValue(100)
param paymentMaxReplicas int = 20

var suffix = uniqueString(subscription().id, resourceGroup().id)
var baseName = toLower('${namePrefix}${substring(suffix, 0, 5)}')
var sqlServerName = '${baseName}-sql'
var keyVaultName = '${baseName}-kv'
var logName = '${baseName}-logs'
var registryName = take(replace('${baseName}acr', '-', ''), 50)

module foundation './modules/foundation.bicep' = {
  name: 'foundation'
  params: {
    location: location
    logName: logName
    registryName: registryName
    sqlServerName: sqlServerName
    keyVaultName: keyVaultName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
  }
}

module web './modules/container-app.bicep' = {
  name: 'web'
  params: {
    appName: '${baseName}-web'
    location: location
    environmentId: foundation.outputs.environmentId
    registryName: foundation.outputs.registryName
    image: webImage
    targetPort: 8080
    externalIngress: true
    minReplicas: webMinReplicas
    maxReplicas: webMaxReplicas
    env: [
      { name: 'NG_APP_CATALOG_API', value: 'https://${catalog.outputs.fqdn}' }
      { name: 'NG_APP_INVENTORY_API', value: 'https://${inventory.outputs.fqdn}' }
      { name: 'NG_APP_ORDER_API', value: 'https://${order.outputs.fqdn}' }
    ]
  }
}

module catalog './modules/container-app.bicep' = {
  name: 'catalog'
  params: {
    appName: '${baseName}-catalog'
    location: location
    environmentId: foundation.outputs.environmentId
    registryName: foundation.outputs.registryName
    image: catalogImage
    targetPort: 8080
    externalIngress: true
    minReplicas: catalogMinReplicas
    maxReplicas: catalogMaxReplicas
    env: [
      { name: 'StorefrontOrigin', value: '*' }
      { name: 'ConnectionStrings__CatalogDb', value: 'Server=tcp:${foundation.outputs.sqlServerFqdn},1433;Initial Catalog=CatalogDb;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity;' }
    ]
  }
}

module inventory './modules/container-app.bicep' = {
  name: 'inventory'
  params: {
    appName: '${baseName}-inventory'
    location: location
    environmentId: foundation.outputs.environmentId
    registryName: foundation.outputs.registryName
    image: inventoryImage
    targetPort: 8080
    externalIngress: true
    minReplicas: inventoryMinReplicas
    maxReplicas: inventoryMaxReplicas
    env: [
      { name: 'StorefrontOrigin', value: '*' }
      { name: 'ConnectionStrings__InventoryDb', value: 'Server=tcp:${foundation.outputs.sqlServerFqdn},1433;Initial Catalog=InventoryDb;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity;' }
    ]
  }
}

module payment './modules/container-app.bicep' = {
  name: 'payment'
  params: {
    appName: '${baseName}-payment'
    location: location
    environmentId: foundation.outputs.environmentId
    registryName: foundation.outputs.registryName
    image: paymentImage
    targetPort: 8080
    externalIngress: true
    minReplicas: paymentMinReplicas
    maxReplicas: paymentMaxReplicas
    env: [
      { name: 'StorefrontOrigin', value: '*' }
      { name: 'ConnectionStrings__PaymentsDb', value: 'Server=tcp:${foundation.outputs.sqlServerFqdn},1433;Initial Catalog=PaymentsDb;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity;' }
    ]
  }
}

module order './modules/container-app.bicep' = {
  name: 'order'
  params: {
    appName: '${baseName}-order'
    location: location
    environmentId: foundation.outputs.environmentId
    registryName: foundation.outputs.registryName
    image: orderImage
    targetPort: 8080
    externalIngress: true
    minReplicas: orderMinReplicas
    maxReplicas: orderMaxReplicas
    env: [
      { name: 'CatalogApiUrl', value: 'http://${baseName}-catalog' }
      { name: 'InventoryApiUrl', value: 'http://${baseName}-inventory' }
      { name: 'PaymentApiUrl', value: 'http://${baseName}-payment' }
      { name: 'ConnectionStrings__OrdersDb', value: 'Server=tcp:${foundation.outputs.sqlServerFqdn},1433;Initial Catalog=OrdersDb;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity;' }
    ]
  }
}

output registryName string = foundation.outputs.registryName
output sqlServerFqdn string = foundation.outputs.sqlServerFqdn
output keyVaultUri string = foundation.outputs.keyVaultUri
output storefrontUrl string = 'https://${web.outputs.fqdn}'
output catalogApiUrl string = 'https://${catalog.outputs.fqdn}'
output orderApiUrl string = 'https://${order.outputs.fqdn}'
