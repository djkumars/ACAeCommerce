# ACA e-commerce reference application

Angular guest storefront and four independently containerized .NET APIs (catalog, inventory, order, and sandbox payment), with Azure Container Apps Bicep infrastructure. This is a runnable development scaffold, not a production payment or capacity certification.

## Requirements

- .NET 10 SDK
- Node.js 20.19+ and npm
- Docker Desktop (optional; required for SQL Server and the full compose stack)
- Azure CLI with Bicep (`az bicep install`) only to validate/deploy infrastructure

## Run locally

1. Start SQL Server and all four APIs from the repository root with `docker compose up --build`. The APIs listen on product catalog `http://localhost:5081`, inventory `http://localhost:5082`, order `http://localhost:5083`, and sandbox payment `http://localhost:5084`. API health probes are available at `/health`.
2. In another terminal, enter `services/web`, run `npm install`, then `npm start`. The storefront is at `http://localhost:4200`.
3. For frontend-only development, run `npm run build` from `services/web` to make a production build. The checked-in `services/web/src/assets/config.js` points at local API ports; container deployments override these URLs at startup with the `NG_APP_CATALOG_API` and `NG_APP_ORDER_API` environment variables.

Each API persists its owned data in a separate Azure SQL database. Schema-only SQL migrations are embedded in each API and applied at startup with a migration history table. In local Compose, the SQL Server 2022 container creates the four databases and each API applies its schema. Schemas start empty by design; catalog products and inventory must be provisioned through an approved operational process before customers can check out.

## Guest checkout and safety

Checkout accepts a product ID and quantity only. It never accepts, stores, or forwards card numbers, CVVs, or bank credentials. The Payment API returns a deterministic sandbox outcome and records only payment-attempt metadata; it is not connected to a payment provider. Do not send sensitive payment data to this application.

Order creation fetches authoritative catalog prices, requests a durable inventory reservation, and calls the sandbox payment API. Failed payment triggers an idempotent reservation release. The sample APIs are unauthenticated and are for local development/demo use only.

## Tests

Run focused API tests with `dotnet test tests/Storefront.Tests/Storefront.Tests.csproj`. They cover sandbox behavior and input validation. Build all APIs using `dotnet build ACAECommerce.slnx`.

## Azure deployment

See [docs/deployment.md](docs/deployment.md). Bicep provisions ACA, ACR, Key Vault, Log Analytics, and Azure SQL resources, with scaling bounds and managed identities. Deployment is intentionally not performed by this scaffold. Set `sqlAdminPassword` through a secure parameter mechanism; do not commit secrets. Replace the sample container image references with images pushed to your registry before deployment.

The 20,000 requests/minute (~333 requests/second) figure is a capacity target only, not a guarantee. Validate with representative load testing and tune replica limits, SQL sizing, indexes, connection limits, and timeouts before production.
