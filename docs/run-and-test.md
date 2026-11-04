# Local run and focused checks

## Local API and storefront

```powershell
docker compose up --build
```

This starts SQL Server plus Product Catalog (`5081`), Inventory (`5082`), Order (`5083`), and sandbox Payment (`5084`). On first startup, each API creates its local database and applies schema migrations. Databases contain no seed data, so product and stock tables are empty until explicitly provisioned. Start Angular in a second terminal:

```powershell
Set-Location services/web
npm install
npm start
```

Open `http://localhost:4200`. For local use, override the SQL Server password with `MSSQL_SA_PASSWORD`; this Compose-only SQL login must not be used in deployed environments.

## Validation

```powershell
dotnet build ACAECommerce.slnx
dotnet test tests/Storefront.Tests/Storefront.Tests.csproj
Set-Location services/web
npm run build
Set-Location ../..
az bicep build --file infra/main.bicep
```

The test project covers deterministic sandbox payment decisions and request validation. The storefront build verifies Angular compilation. API/database behavior requires a running SQL Server; Compose provides that local dependency. No data fixtures or seed scripts are included.

Quick local probes:

```powershell
Invoke-RestMethod http://localhost:5081/health
Invoke-RestMethod http://localhost:5081/api/products
Invoke-RestMethod http://localhost:5082/api/inventory/1
```

To try guest checkout, POST JSON to `http://localhost:5083/api/orders`:

```json
{ "items": [{ "productId": 1, "quantity": 1 }] }
```

## Load test

Use a representative non-production environment and synthetic catalog/order requests only. Increase load in stages toward the 333 RPS target, include realistic cart distributions and checkout concurrency, and monitor ACA replicas, SQL (once wired), queueing, errors, and p95/p99 latency. Never use real payment credentials; the payment stub accepts no card data. Do not treat successful scaffolding/build checks as a capacity test.
