# HTTP API reference

All JSON responses use camelCase. No endpoint handles customer identity or payment credentials.

| Service | Method and path | Behavior |
|---|---|---|
| Catalog | `GET /health` | Liveness response |
| Catalog | `GET /api/products?search={text}` | List active database products; optional case-insensitive search |
| Catalog | `GET /api/products/{id}` | Active product detail or `404` |
| Inventory | `GET /health` | Liveness response |
| Inventory | `GET /api/inventory/{productId}` | Current database availability (`OnHand - Reserved`) |
| Inventory | `POST /api/inventory/reservations` | Atomically reserve `{ "orderId": "<guid>", "items": [{ "productId": 1, "quantity": 1 }] }`; returns a reservation ID |
| Inventory | `POST /api/inventory/release` | Idempotently release `{ "reservationId": "<guid>" }` |
| Order | `GET /health` | Liveness response |
| Order | `POST /api/orders` | Guest checkout with `{ "items": [{ "productId": 1, "quantity": 1 }] }`; returns `201` and order summary |
| Order | `GET /api/orders/{id}` | Retrieve persisted order status and lines or `404` |
| Sandbox Payment | `GET /health` | Liveness response |
| Sandbox Payment | `POST /api/payments/authorize` | Internal sandbox call with `{ "orderId": "<guid>", "amount": 20.00, "currency": "USD" }`; amount ending in `.99` after a `$10` interval deterministically declines; persists attempt metadata only |

`POST /api/orders` obtains product prices from Catalog, reserves Inventory, calls the sandbox Payment API, and releases inventory on payment failure. Each service runs its embedded schema-only SQL migrations at startup and stores state in its own database. Migrations never insert seed rows; in a fresh installation the catalog and inventory APIs correctly return empty data.
