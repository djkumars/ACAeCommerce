# Build and deployment guide

Infrastructure is described by `infra/main.bicep` and `infra/modules/`. This scaffold does **not** deploy or provision Azure resources.

## Validate and build images

From the repository root:

```powershell
az bicep build --file infra/main.bicep
docker compose build
```

After creating a resource group and deploying the foundation, build and push the five service images to the created ACR. `infra/parameters.example.json` intentionally contains placeholder image names; replace them with fully qualified image references. For local dev, use `docker compose up --build` instead.

Example deployment command after setting a secure password and replacing parameters (do not save the password in the parameter file):

```powershell
az deployment group create --resource-group <existing-resource-group> --template-file infra/main.bicep --parameters @infra/parameters.example.json sqlAdminPassword='<secure value supplied interactively>'
```

The command above is documentation only; do not run it without reviewing cost, access, and network settings. Use a secure deployment pipeline or prompt to provide the secret in real operations.

## Resources and configuration

- One Container Apps environment and HTTP scale rules for storefront and APIs. Replica minimums/maximums are Bicep parameters; Inventory, Order, and Payment default to one minimum replica to avoid checkout cold starts. Catalog can scale to zero.
- Basic ACR with admin credentials disabled. Container Apps use system-assigned identities and `AcrPull`.
- Log Analytics workspace with 30-day retention; ACA sends its application logs there.
- Key Vault with RBAC enabled; Azure SQL administrator password is a secure Bicep parameter, not committed to the repository. API SQL connection strings are non-secret and configure each app's own database with managed identity authentication.
- Azure SQL server and separate `CatalogDb`, `InventoryDb`, `OrdersDb`, and `PaymentsDb` resources. Embedded schema migrations create the schema and migration history without inserting data.

The APIs apply embedded SQL migrations and use database-backed repositories at runtime. A fresh schema is intentionally empty: provision catalog products and inventory through an approved process; no migration or seed script creates demo rows. Before starting deployed APIs, create a contained SQL user for each Container App system-assigned identity in its own database and grant only required runtime and migration permissions. For example, connect as the SQL Entra administrator and, in the corresponding database, create its service principal user with `CREATE USER [<container-app-identity-name>] FROM EXTERNAL PROVIDER;`; grant `db_datareader` and `db_datawriter`, plus schema migration DDL rights because migrations run at startup. Repeat separately for CatalogDb, InventoryDb, OrdersDb, and PaymentsDb; never share one app's SQL principal across databases. The connection strings use managed identity and contain no SQL passwords. Deployment's default container image references remain placeholders.

## Security and operations before production

The example SQL server allows Azure services through the SQL firewall and has public network access enabled. That is a connectivity-oriented sample, not a production network boundary. Restrict connectivity using ACA VNet integration/private endpoints and SQL private endpoints, disable public access, use managed identity/Entra authentication, and grant database-scoped least privilege.

API ingress is external so the browser storefront can call Catalog, Inventory, and Order. The APIs are unauthenticated demo endpoints; `StorefrontOrigin=*` is set for the sample deployment to avoid a dependency cycle during initial provisioning. Replace with explicit storefront origins and add rate limits, authentication/authorization where appropriate, request validation, and abuse protection before public use. Sandbox payment is deterministic and does not connect to a processor.

The `20,000 requests/minute` (~333 requests/second) requirement is a **capacity target, not a guarantee**. The default Basic SQL tier and replica limits are not sized or certified for that rate. Run representative load tests at increasing traffic and tune SQL tier, query plans/indexes, pool limits, ACA CPU/memory, concurrency thresholds, min/max replicas, and downstream timeouts. Track p95/p99 latency, errors, throttling, and resource saturation.
