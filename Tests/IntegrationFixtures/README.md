# IntegrationFixtures

Solution de fixtures para validar o IntegrationDiscoveryEngine de forma determinística.

## Estrutura

```
Tests/IntegrationFixtures/
  IntegrationFixtures.sln
  Samples/
    Sample.SqlServer/
    Sample.Postgres/
    Sample.Mongo/
    Sample.Redis/
    Sample.RabbitMq/
    Sample.AzureServiceBus/
    Sample.AzureStorage/
    Sample.AzureCosmos/
    Sample.Oracle/
    Sample.AzureEventGrid/
    Sample.AzureKeyVault/
    Sample.APIM/
    Sample.MassTransit/
    Sample.OpenTelemetry/
    Sample.HttpInternal/
    Sample.HttpExternal/
    Sample.Mixed.Small/
```

## Build

```powershell
dotnet build Tests/IntegrationFixtures/IntegrationFixtures.sln
```

## Testes

```powershell
dotnet test Sharpscope.sln
```

## Gerar snapshot

```powershell
dotnet run --project Presentation/Sharpscope.Terminal analyze --solution Tests/IntegrationFixtures/IntegrationFixtures.sln --profile work --output ./integration-fixtures-snapshot.json
```

## Interpretar o snapshot

Campos principais:
- `integrations.candidates`: integrações inferidas (Kind/Technology/LogicalName/Confidence/Evidence).
- `integrations.usageByNodeId`: associa `methodId` (e também `typeId` quando aplicável) aos `candidate.Id` usados.
- `integrations.usageByTypeId` / `usageByNamespaceId` / `usageByProjectId`: agregações derivadas via edges `Contains`.
- `metadata.integrationProfile`: perfil utilizado (ex.: `work`).

## Perfil `work`

Tecnologias esperadas no perfil:
- SQL Server (EF, ADO.NET, Dapper)
- Oracle (ODP.NET, Dapper)
- Redis
- Azure Service Bus
- Azure Event Grid
- Cosmos DB
- HttpApi (interno/externo)
- Azure Storage (Blob)
- Azure API Management (APIM)
- Azure Key Vault
- MassTransit
- OpenTelemetry

## Verificações rápidas

```powershell
# candidatos detectados
Select-String -Path .\integration-fixtures-snapshot.json -Pattern "\"Integrations\"" -SimpleMatch

# não vazar segredos
Select-String -Path .\integration-fixtures-snapshot.json -Pattern "Password=" -SimpleMatch
Select-String -Path .\integration-fixtures-snapshot.json -Pattern "SharedAccessKey=" -SimpleMatch
Select-String -Path .\integration-fixtures-snapshot.json -Pattern "AccountKey=" -SimpleMatch
```

> Os valores sensíveis devem aparecer apenas redatados (ex.: `Password=***`).
