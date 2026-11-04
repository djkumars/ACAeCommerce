Scenario 1: E-Commerce Order Processing (ACA)
Business Requirement

An online retailer has:

Angular storefront
Product Catalog API
Order API
Payment API
Inventory API

Traffic patterns:

Plain Text
Normal Day : 500 requests/min
Sale Day : 20,000 requests/min
 

The company wants:

Auto-scaling
Minimal infrastructure management
Microservices architecture
Solution using ACA
Plain Text
Angular App
|
v
ACA Ingress
|
------------------------------------------------
| | | |
v v v v
Product API Order API Inventory API Payment API
|
v
Azure SQL / Cosmos DB
Why ACA?

✅ Runs multiple microservices

✅ HTTP auto-scaling

✅ Scale-to-zero (for non-critical services)

✅ Built-in HTTPS

✅ Easy CI/CD

✅ No Kubernetes management

Interview Statement

"For customer-facing microservices such as e-commerce APIs, I would choose ACA because it provides PaaS simplicity while supporting containerized workloads with automatic scaling."