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

## Local Dependencies (docker-compose)

`docker-compose.yml` at the repo root currently brings up **infra dependencies only** — Postgres, Kafka, Prometheus, Grafana — not the Api/Worker services themselves (they have no Dockerfiles or runtime config yet). Api/Worker will be added as compose services once EF Core config and Kafka wiring exist, so containerizing them isn't wasted rework.

| Service | Image | Host port | Notes |
|---|---|---|---|
| `postgres` | `postgres:16-alpine` | `5432` | db=`solidary`, user/pass=`solidary`/`solidary` (local dev only) |
| `kafka` | `apache/kafka:3.8.0` | `9094` | Single-node KRaft broker (no Zookeeper). In-network listener `kafka:9092`; host listener `localhost:9094` for running Api/Worker outside compose via `dotnet run`. |
| `prometheus` | `prom/prometheus:latest` | `9090` | Config at `config/prometheus/prometheus.yml`; scrape targets `api:8080` and `worker:8081` are pre-declared but will show as down until those services join compose. |
| `grafana` | `grafana/grafana:latest` | `3000` | admin/admin; anonymous viewer access enabled for demo convenience; Prometheus datasource auto-provisioned via `config/grafana/provisioning/`. |

`docker compose up -d` / `docker compose down` from the repo root manages the stack.

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

## Persistence & Auth

- `SolidaryDbContext` lives in `Solidary.Infrastructure/Persistence`, with one `IEntityTypeConfiguration<T>` per entity under `Persistence/Configurations`. Migrations live in `Persistence/Migrations`; design-time context creation goes through `SolidaryDbContextFactory` (reads `SOLIDARY_DB_CONNECTION` env var, falls back to the local compose connection string) so `dotnet ef` works without spinning up the Api host.
- **Admin is seeded via `HasData`** in `UserConfiguration` with a fixed Guid (`00000000-0000-0000-0000-000000000001`), email `admin@solidary.local`, password `Admin@123` (BCrypt-hashed, dev-only — document in README). `HasData` requires fully deterministic values, which is why the id/hash/timestamp are hardcoded constants rather than generated at migration time.
- **CPF validation is a real checksum**, not just a regex — `Solidary.Domain.ValueObjects.CpfValidator` implements the standard Brazilian check-digit algorithm. Covered by its own test class since it's a business rule independent of any single handler.
- **Password hashing**: `IPasswordHasher`/`ITokenGenerator` interfaces live in `Solidary.Domain.Abstractions` (pure contracts, no infra dependency); `BCryptPasswordHasher`/`JwtTokenGenerator` implementations live in `Solidary.Infrastructure.Auth`. Registered as singletons in DI since both are stateless.
- **Auth commands use MediatR** (`Features/Auth/Register/RegisterDonorCommand`, `Features/Auth/Login/LoginCommand` in the Api project) returning a lightweight `Result<T>` (`Solidary.Api.Common.Result`) instead of throwing on expected validation failures (duplicate email, invalid CPF, wrong password) — keeps handlers testable without exception-driven control flow, and endpoints map `Result` to the right HTTP status (400 for register failures, 401 for login failures).
- JWT claims: `sub` (user id), `email`, `name`, `role`. Signing key/issuer/audience/expiry come from the `Jwt` config section (`JwtSettings`) — the dev signing key in `appsettings.json` is explicitly marked dev-only and must be overridden via env var/K8s secret for anything beyond local use.
- `POST /auth/register` (public, Donor only) and `POST /auth/login` (public) are wired in `Program.cs`; `/health` exposes a Postgres-backed health check via `AspNetCore.HealthChecks.NpgSql`. Full `/metrics` (Prometheus exporter) isn't wired yet — still open.
- Verified end-to-end against the compose Postgres: migration applies cleanly, admin seed row present, register/login/duplicate-email/wrong-password all return the expected status codes and a decodable JWT with correct role claim.

## Status

Scaffolded so far: solution structure (`Solidary.sln` + 6 projects, references wired, builds clean), domain entities/enums, `ReceivedDonationEvent` contract, `docs/architecture.md`, `docker-compose.yml` with infra dependencies (Postgres, Kafka, Prometheus, Grafana — verified healthy locally), EF Core `SolidaryDbContext` + initial migration (applied and verified against Postgres), JWT auth with Register/Login MediatR handlers (verified end-to-end), 14 passing xUnit tests (auth handlers + CpfValidator).

Not yet started: Prometheus `/metrics` exporter wiring, Kafka producer/consumer wiring, adding Api/Worker to compose (+ Dockerfiles), `k8s/` manifests, CI workflow, README.
