# Conexão Solidária — Project Directives

Living reference of decisions made for this project. Update this file whenever a new directive is agreed on, instead of letting decisions live only in chat history.

## Domain

Donation platform MVP for the fictional NGO "Esperança Solidária" (hackathon spec: `HACKATHON NETT.pdf`), built for a post-grad .NET software architecture course. Evaluated on architecture, not just functionality.

All naming is in **English**, translated from the (Portuguese) spec:

| Spec term (pt-BR) | Project term (en) |
|---|---|
| Usuário | `User` |
| GestorONG | `UserRole.Admin` |
| Doador | `UserRole.Donor` |
| Campanha | `Campaign` |
| Meta Financeira | `Campaign.FundingGoal` |
| Valor Total Arrecadado | `Campaign.TotalRaised` |
| Doação | `Donation` |
| DoacaoRecebidaEvent | `ReceivedDonationEvent` |

## Stack

- **.NET 10**, EF Core, PostgreSQL.
- **MediatR** with CQRS in the Api.
- **Kafka** via Confluent's `Confluent.Kafka` client. Event topic: `ReceivedDonationEvent`.
- **JWT auth** with self-issued tokens (Api signs its own tokens; no external IdP).
- **xUnit** unit tests for every CQRS handler — mandatory for this project (spec lists this as bonus, we're treating it as required).
- **docker-compose** for local dev.
- **Kubernetes manifests** (Deployments, Services, ConfigMaps) targeting **Minikube**.
- **GitHub Actions** CI: builds the .NET solution and produces Docker images on push to main. Automated K8s deploy is out of scope for now (deployment automation deferred).
- **Prometheus + Grafana** for observability (`/health`, `/metrics` on both services). Chosen as the idiomatic .NET/OpenTelemetry substitute for the spec's literal "Zabbix and Grafana" — to be justified in the architecture doc.
- **No API Gateway** for the MVP (Api called directly). Optional bonus, revisit later if time allows.

## Architecture Decisions

- **Two microservices**: `Api` (public-facing, campaigns/users/donation intake) and `Worker` (internal-only Kafka consumer that applies donation totals). Worker is never exposed externally.
- **Single shared PostgreSQL database** for both Api and Worker (not database-per-service). Simplest topology for a hackathon timeframe; documented as a conscious tradeoff in the required "why DB X/Y" justification doc.
- **Donation flow is async**: Api never updates `Campaign.TotalRaised` directly. It persists a `Donation` (status `Pending`) and publishes `ReceivedDonationEvent` to Kafka. Worker consumes the event, updates `Campaign.TotalRaised`, and marks the `Donation` `Processed` — in a single DB transaction. This guards against duplicate processing (Worker checks `Status != Processed` before applying).
- **GestorONG-equivalent (Admin) accounts are seeded via EF Core migration/seed data** — no public registration endpoint for that role. Only `Donor` has public self-registration.
- **Worker exposes its own `/health` and `/metrics`** endpoints (via a lightweight Kestrel listener alongside the BackgroundService), same as Api.
- Kafka/Postgres in Minikube: run however is simplest for the hackathon demo (plain Deployments in-cluster, or left external if faster to demo) — not a blocking design decision, finalize during implementation.

## Repository Structure

```
/
├── src/
│   ├── Solidary.Api/                 # ASP.NET Core minimal API + MediatR handlers, JWT auth
│   ├── Solidary.Worker/              # BackgroundService Kafka consumer
│   ├── Solidary.Domain/              # Entities, enums, business rule validation
│   ├── Solidary.Contracts/           # Shared event DTOs (ReceivedDonationEvent)
│   └── Solidary.Infrastructure/      # EF Core DbContext, migrations, Kafka producer/consumer setup
├── tests/
│   └── Solidary.Api.Tests/           # xUnit, one class per CQRS handler
├── k8s/                              # Deployments, Services, ConfigMaps (Minikube target)
├── docs/
│   ├── architecture.md               # Mermaid diagrams (C4 container, ER, sequence, deployment)
│   └── db-justification.pdf          # required "why Postgres for X and Y" doc
├── .github/workflows/ci.yml          # build + docker image on push to main
├── docker-compose.yml
└── README.md                         # step-by-step local run instructions
```

## Entities

- **User**: `Id`, `FullName`, `Email` (unique), `PasswordHash` (BCrypt), `Role` (`Admin`/`Donor`), `Cpf` (nullable, format-validated, Donor only), `CreatedAt`.
- **Campaign**: `Id`, `Title`, `Description`, `StartDate`, `EndDate`, `FundingGoal`, `TotalRaised` (default 0, updated only by Worker), `Status` (`Active`/`Completed`/`Cancelled`), `CreatedByUserId`. Invariants: `EndDate` must be in the future at creation; `FundingGoal > 0`.
- **Donation**: `Id`, `CampaignId`, `DonorId`, `Amount`, `Status` (`Pending`/`Processed`/`Failed`), `CreatedAt`, `ProcessedAt` (nullable). Invariant: cannot be created against a `Completed`/`Cancelled` campaign.

Full diagrams (C4 container, ER, donation-flow sequence, deployment views for compose + Minikube) live in [docs/architecture.md](docs/architecture.md).

## Status

Scaffolded so far: solution structure (`Solidary.sln` + 6 projects, references wired, builds clean), domain entities/enums, `ReceivedDonationEvent` contract, `docs/architecture.md`.

Not yet started: EF Core `DbContext`/migrations, MediatR handlers + JWT auth, Kafka producer/consumer wiring, `docker-compose.yml`, `k8s/` manifests, CI workflow, README.
