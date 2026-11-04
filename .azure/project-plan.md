# ACA E-Commerce Application Plan

## Goal

Build a runnable e-commerce storefront with an Angular frontend and four independent .NET microservices, deployable to Azure Container Apps (ACA). Provide Infrastructure as Code (IaC) for the application and its Azure dependencies.

## Decisions

- Checkout supports guests; no customer sign-in is required.
- Payment API uses a mock/sandbox provider. It must not collect or store real payment-card data.
- Use Azure SQL Database for transactional product, order, inventory, and payment-attempt data.
- Use Bicep for Azure infrastructure.

## Application

- **Storefront:** Angular single-page application for browsing products, managing a cart, and placing guest orders.
- **Product Catalog API:** .NET REST API for product listing and product details.
- **Inventory API:** .NET REST API for stock availability and reservation/release.
- **Order API:** .NET REST API to validate a cart, coordinate inventory and payment, and expose order status.
- **Payment API:** .NET REST API with a deterministic mock/sandbox payment result; no external payment gateway or real card handling.
- Each service owns its schema and migration history in the shared Azure SQL logical server, using separate databases or clearly isolated schemas as appropriate. Local development uses SQL Server containers or LocalDB-compatible configuration.
- Provide container builds, health endpoints, API documentation, configuration examples, and focused automated tests.

## Azure architecture and IaC

- One Azure Container Apps environment with external ingress for the Angular storefront and internal ingress for APIs. The frontend calls APIs through the ACA ingress; service-to-service traffic uses internal endpoints.
- Azure Container Registry for container images.
- Azure SQL logical server and database resources with secure connectivity, managed identities where supported, and secrets/configuration held in Key Vault.
- Log Analytics workspace and ACA diagnostics for centralized logs and basic operational visibility.
- Bicep modules and parameters for resource naming, region, environment, SQL sizing, and scaling limits. Include outputs and deployment instructions.
- ACA HTTP-based scaling for APIs and the storefront. Start with low minimum replica counts for non-critical workloads and configurable maximums; make the Order API, Inventory API, and Payment API scale-from-zero behavior configurable to avoid cold starts on critical checkout paths.
- Document that 20,000 requests/minute (about 333 requests/second) is a capacity target, not a guarantee: production replica limits and SQL sizing must be confirmed by representative load testing and tuning.

## Delivery sequence

1. Scaffold solution structure, shared contracts, and local development configuration.
2. Implement SQL migrations and the four APIs, including guest order flow and mock payment.
3. Implement and wire the Angular storefront to live API endpoints.
4. Add and validate Bicep deployment for ACA, ACR, SQL, Key Vault, and Log Analytics.
5. Run focused tests and build checks; document local run, deployment, and load-test steps.

## Out of scope

- Real payment processing, customer identity, production card-data handling, and a production-scale certification of the stated peak traffic.
- CI/CD pipeline creation unless requested separately.
