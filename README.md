# Conexão Solidária

A donation-management MVP for the fictional NGO "Esperança Solidária" — built as a hackathon project for a post-grad .NET software architecture course. Donors can register and donate to campaigns; NGO admins manage campaigns; donation totals are updated asynchronously through Kafka rather than synchronously in the request path.

See [docs/architecture.md](docs/architecture.md) for the full C4 container diagram, ER diagram, donation-flow sequence diagram, and deployment views. See [CLAUDE.md](CLAUDE.md) for the running log of architecture decisions and their rationale.

## Architecture at a Glance

- **Two services**: `Solidary.Api` (public REST API — auth, campaigns, donation intake) and `Solidary.Worker` (internal-only Kafka consumer that applies donation totals). Worker has no public-facing purpose; it only talks to Kafka and Postgres.
- **Donations are asynchronous**: submitting a donation persists it as `Pending` and publishes a `ReceivedDonationEvent` to Kafka — the Api never updates a campaign's total directly. The Worker consumes the event, updates `Campaign.TotalRaised`, and marks the donation `Processed`.
- **Stack**: .NET 10, EF Core + PostgreSQL (single shared database), MediatR/CQRS use cases, Kafka via Confluent's client, self-issued JWT auth, Hangfire for scheduled jobs, Prometheus + Grafana for observability.
- **Campaigns close themselves**: a Hangfire recurring job (every 5 minutes, inside the Api process) marks expired `Active` campaigns as `Completed`; Admins can also cancel a campaign directly via `POST /campaigns/{id}/cancel`.
- **Layering**: `Solidary.Domain` → `Solidary.Application` (MediatR use cases) → `Solidary.Api` / `Solidary.Worker` (hosts), with `Solidary.Infrastructure` (EF Core, Kafka, auth) and `Solidary.Contracts` (shared event DTOs) referenced by both. Each layer owns its own dependency-injection setup (`DependencyInjection.cs`), and Api endpoints are mapped in dedicated files under `Endpoints/`.

## Prerequisites

- [Docker](https://www.docker.com/) with Compose v2 (`docker compose`, not `docker-compose`)
- [.NET 10 SDK](https://dotnet.microsoft.com/) — only needed if you want to run Api/Worker from your IDE instead of in a container

## Running It

The compose stack has two modes, controlled by the `app` [Compose profile](https://docs.docker.com/compose/how-tos/profiles/):

### Option A — Everything in Docker

Builds and runs Postgres, Kafka, Prometheus, Grafana, Api, and Worker together:

```bash
docker compose --profile app up -d --build
```

The Api applies EF Core migrations (and seeds the Admin account) automatically on startup, so this works from a completely empty database with no extra steps.

### Option B — Dependencies in Docker, Api/Worker in your IDE

Starts only Postgres, Kafka, Prometheus, and Grafana — for when you want to run/debug `Solidary.Api` and `Solidary.Worker` yourself (e.g. from Rider/VS/VS Code):

```bash
docker compose up -d
```

Then run each project from your IDE, or:

```bash
dotnet run --project src/Solidary.Api
dotnet run --project src/Solidary.Worker
```

Both projects read their Postgres/Kafka connection settings from `appsettings.json`, which already default to the ports Compose exposes on `localhost` (`5432` for Postgres, `9094` for Kafka's host listener) — no extra configuration needed. The Api applies migrations on startup either way.

### Stopping

```bash
docker compose down          # stop everything Compose started, keep volumes
docker compose down -v       # also wipe Postgres/Grafana volumes
```

## Ports & URLs

| Service | URL | Notes |
|---|---|---|
| Api | http://localhost:8080 | |
| Api Swagger UI | http://localhost:8080/swagger | Includes an **Authorize** button for JWT |
| Api health | http://localhost:8080/health | |
| Api metrics | http://localhost:8080/metrics | Prometheus exposition format |
| Worker health | http://localhost:8081/health | |
| Worker metrics | http://localhost:8081/metrics | |
| Postgres | localhost:5432 | db=`solidary`, user/pass=`solidary`/`solidary` |
| Kafka (host access) | localhost:9094 | In-network service name is `kafka:9092` |
| Prometheus | http://localhost:9090 | |
| Grafana | http://localhost:3000 | admin/admin, or anonymous viewer access. A "Conexão Solidária — Overview" dashboard is pre-provisioned (Service Health + Donations rows) |

## Trying the API

A seeded Admin account exists out of the box (see `UserConfiguration.cs` — local/dev credentials only): `admin@solidary.local` / `Admin@123`.

```bash
# Register a Donor
curl -X POST http://localhost:8080/auth/register \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Ana Souza","email":"ana@example.com","cpf":"529.982.247-25","password":"SecurePass1"}'

# Log in as Admin
ADMIN_TOKEN=$(curl -s -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@solidary.local","password":"Admin@123"}' | jq -r .token)

# Log in as the Donor you just registered
DONOR_TOKEN=$(curl -s -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"ana@example.com","password":"SecurePass1"}' | jq -r .token)

# Create a campaign (Admin only)
CAMPAIGN_ID=$(curl -s -X POST http://localhost:8080/campaigns/ \
  -H "Content-Type: application/json" -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"title":"Winter Coat Drive","description":"Coats for kids","startDate":"2026-08-01T00:00:00Z","endDate":"2026-12-31T00:00:00Z","fundingGoal":5000}' \
  | jq -r .campaignId)

# Submit a donation (any authenticated user) — this publishes to Kafka;
# the Worker picks it up asynchronously and updates the campaign total.
curl -X POST "http://localhost:8080/campaigns/$CAMPAIGN_ID/donations" \
  -H "Content-Type: application/json" -H "Authorization: Bearer $DONOR_TOKEN" \
  -d '{"amount": 150.50}'

# Public transparency listing — no auth required, only shows Active campaigns
curl http://localhost:8080/campaigns/

# Cancel a campaign (Admin only)
curl -X POST "http://localhost:8080/campaigns/$CAMPAIGN_ID/cancel" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

`startDate`/`endDate` accept any valid ISO-8601 offset (`Z`, `+00:00`, `-03:00`, etc.) — they're stored as UTC internally regardless of which offset you send.

Campaigns also close themselves automatically: a Hangfire recurring job runs every 5 minutes inside the Api process and marks any `Active` campaign whose `endDate` has passed as `Completed` — no manual step needed.

Or just open [Swagger UI](http://localhost:8080/swagger), click **Authorize**, paste a token from `/auth/login`, and try the endpoints interactively.

## Running Tests

```bash
dotnet test tests/Solidary.Api.Tests
```

Unit tests cover every MediatR use case handler (auth, campaigns, donations), the Hangfire campaign-closing job, the custom Prometheus donation metrics, and the CPF checksum validator — using EF Core's InMemory provider, a fake `IEventPublisher`, and an isolated `CollectorRegistry` per test. No external dependencies required.

## Project Structure

```
/
├── src/
│   ├── Solidary.Api/            # Public REST API host — DI composition, endpoint mapping, JWT bearer setup
│   ├── Solidary.Worker/         # Internal Kafka consumer (not exposed externally)
│   ├── Solidary.Application/    # MediatR use cases (CQRS commands/queries)
│   ├── Solidary.Domain/         # Entities, enums, business rules, abstractions
│   ├── Solidary.Contracts/      # Shared event DTOs (ReceivedDonationEvent) and topic names
│   └── Solidary.Infrastructure/ # EF Core DbContext + migrations, Kafka producer, JWT/BCrypt implementations
├── tests/
│   └── Solidary.Api.Tests/      # xUnit — mirrors Solidary.Application/UseCases structure
├── config/
│   ├── prometheus/              # Scrape config
│   └── grafana/provisioning/    # Auto-provisioned Prometheus datasource
├── docs/
│   └── architecture.md          # Mermaid diagrams: C4 container, ER, sequence, deployment
├── docker-compose.yml
└── CLAUDE.md                    # Living log of architecture decisions and why they were made
```

## What's Not Built Yet

- Kubernetes manifests (`k8s/`) — targeting Minikube, planned but not started.
- CI pipeline (GitHub Actions) building the solution and producing Docker images on push.
- `docs/db-justification.pdf` (required deliverable for the hackathon spec, not written yet).
- A Hangfire dashboard UI — the recurring job runs and logs its results, but there's no `/hangfire` UI (didn't pair well with stateless JWT auth; not asked for).

## Known Issues

- Container logs show a harmless `Cannot load library libgssapi_krb5.so.2` warning from librdkafka (Confluent.Kafka's native dependency) — it's an optional GSSAPI/Kerberos probe we don't use (PLAINTEXT only) and doesn't affect producing/consuming.
