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

## Local Dependencies & Containerization (docker-compose)

`docker-compose.yml` at the repo root has two modes via the **`app` Compose profile**:

- **`docker compose up -d`** (no profile) — starts only the infra dependencies (Postgres, Kafka, Prometheus, Grafana). This is the mode for running `Solidary.Api`/`Solidary.Worker` yourself from an IDE (`dotnet run`) against real dependencies.
- **`docker compose --profile app up -d --build`** — additionally builds and starts `api` and `worker` as containers, giving a fully self-contained stack from one command.

| Service | Image | Host port | Notes |
|---|---|---|---|
| `postgres` | `postgres:16-alpine` | `5432` | db=`solidary`, user/pass=`solidary`/`solidary` (local dev only) |
| `kafka` | `apache/kafka:3.8.0` | `9094` | Single-node KRaft broker (no Zookeeper). In-network listener `kafka:9092`; host listener `localhost:9094` for running Api/Worker outside compose via `dotnet run`. |
| `prometheus` | `prom/prometheus:latest` | `9090` | Config at `config/prometheus/prometheus.yml`; scrape targets `api:8080` and `worker:8081` resolve once the `app` profile is active. |
| `grafana` | `grafana/grafana:latest` | `3000` | admin/admin; anonymous viewer access enabled for demo convenience; Prometheus datasource auto-provisioned via `config/grafana/provisioning/`. |
| `api` (`profiles: ["app"]`) | built from `src/Solidary.Api/Dockerfile` | `8080` | `ConnectionStrings__Postgres`/`Kafka__BootstrapServers` overridden via compose `environment:` to use in-network service names (`postgres`, `kafka:9092`) instead of the `localhost` defaults baked into `appsettings.json` for IDE mode. |
| `worker` (`profiles: ["app"]`) | built from `src/Solidary.Worker/Dockerfile` | `8081` | Same env override pattern as `api`. |

**Dockerfiles** (`src/Solidary.Api/Dockerfile`, `src/Solidary.Worker/Dockerfile`) are standard multi-stage .NET builds: `dotnet/sdk:10.0` to restore+publish, `dotnet/aspnet:10.0` for the runtime image (Worker needs the ASP.NET Core runtime too, not just the base .NET runtime, since it hosts `/health`+`/metrics` over Kestrel). Build `context: .` (repo root) with `dockerfile:` pointing into the project folder, since a multi-project solution needs the whole source tree in the build context, not just one project directory. A root `.dockerignore` excludes `bin/`/`obj/`/`.git/` etc. so host build artifacts never leak into the image (they'd have the wrong RID and bloat the context).

**Auto-migration on Api startup**: `Solidary.Api/DatabaseMigrator.cs` (`app.ApplyMigrationsAsync()`, called in `Program.cs` right after `app.Build()`) runs `dbContext.Database.MigrateAsync()` on every boot. This is what makes `docker compose --profile app up -d --build` work from a completely empty database with zero manual steps — no separate `dotnet ef database update` required. Worker does not run migrations itself (only Api owns schema changes); if Worker starts before Api finishes migrating, its consumer loop just logs and retries until the tables exist (already-established error-handling behavior, not special-cased for this).

**Known cosmetic issue**: both containers log a `Cannot load library libgssapi_krb5.so.2` warning from librdkafka (Confluent.Kafka's native dependency) on startup — it's a load probe for optional GSSAPI/Kerberos SASL support we don't use (only PLAINTEXT), and does not affect producing/consuming. Fixing it means installing `libgssapi-krb5-2` via `apt-get` in the final image stage, which failed in this sandbox due to a GPG/network issue with `apt-get update` against `ports.ubuntu.com` (not a project bug) — revisit in an environment with normal network access if the log noise matters for grading.

`docker compose down` / `docker compose down -v` (also drops volumes) from the repo root tears the stack down.

## Repository Structure

```
/
├── src/
│   ├── Solidary.Api/                 # ASP.NET Core minimal API host: DI composition, endpoint mapping, JWT bearer setup, Dockerfile
│   ├── Solidary.Worker/              # Kafka consumer host (WebApplication for /health+/metrics), Dockerfile
│   ├── Solidary.Application/         # MediatR use cases (CQRS commands/queries) — the "business logic" layer
│   ├── Solidary.Domain/              # Entities, enums, business rule validation, abstractions (interfaces)
│   ├── Solidary.Contracts/           # Shared event DTOs (ReceivedDonationEvent) + Kafka topic names
│   └── Solidary.Infrastructure/      # EF Core DbContext, migrations, Kafka producer/consumer setup, auth implementations
├── tests/
│   └── Solidary.Api.Tests/           # xUnit, one class per CQRS handler (mirrors Application/UseCases structure)
├── k8s/                              # Deployments, Services, ConfigMaps (Minikube target) — not started yet
├── docs/
│   ├── architecture.md               # Mermaid diagrams (C4 container, ER, sequence, deployment)
│   └── db-justification.pdf          # required "why Postgres for X and Y" doc — not written yet
├── .github/workflows/ci.yml          # build + docker image on push to main — not started yet
├── .dockerignore
├── docker-compose.yml                # `up -d` = deps only; `--profile app up -d --build` = deps + containerized Api/Worker
└── README.md                         # step-by-step local run instructions
```

## Layering Conventions

- **`Solidary.Application` holds all use cases**, under `UseCases/<Group>/<UseCase>/` (e.g. `UseCases/Auth/Register/RegisterDonorCommand.cs` + `RegisterDonorCommandHandler.cs`). Not called "Features" — always "UseCases". Each use case is a MediatR command/query + handler pair returning `Solidary.Application.Common.Result<T>` on expected failures (validation, not-found, wrong credentials) rather than throwing.
- **Every layer owns its own DI registration** via a `DependencyInjection.cs` at the project root, exposing a single `IServiceCollection` extension method named after the layer:
  - `Solidary.Application.DependencyInjection.AddApplication()` — registers MediatR against the Application assembly.
  - `Solidary.Infrastructure.DependencyInjection.AddInfrastructure(IConfiguration)` — DbContext, JWT settings binding, password hasher/token generator.
  - `Solidary.Api.DependencyInjection.AddApi(IConfiguration)` — OpenApi, JWT bearer authentication, authorization policies, health checks (i.e. presentation-layer/host concerns, not reusable by Worker).
  - `Solidary.Worker.DependencyInjection.AddWorker()` — registers the Kafka consumer hosted service + health checks. Worker does **not** call `AddApplication()` — it talks to `SolidaryDbContext` directly rather than through MediatR use cases (MediatR/CQRS is an Api-side pattern per the stack decisions; Worker's job is a single background reaction to one event type, not request handling).
  - `Program.cs` in each host project only *calls* these (`builder.Services.AddApi(...)`, `.AddInfrastructure(...)`, `.AddApplication()`) — it must never register services inline.
- **Endpoint mapping lives in `Solidary.Api/Endpoints/`, one file per endpoint group**, each exposing an `IEndpointRouteBuilder` extension (e.g. `AuthEndpoints.MapAuthEndpoints()`, `ObservabilityEndpoints.MapObservabilityEndpoints()` for `/health` + `/metrics`). `Program.cs` only calls `app.MapXxxEndpoints()` — no inline `app.MapPost(...)` calls.
- Net effect: `Program.cs` in the Api is pure composition — DI extension calls, middleware pipeline (`UseHttpMetrics`, `UseAuthentication`, `UseAuthorization`), then endpoint-group mapping calls. No business logic, no inline route handlers, no inline service registration.

## Entities

- **User**: `Id`, `FullName`, `Email` (unique), `PasswordHash` (BCrypt), `Role` (`Admin`/`Donor`), `Cpf` (nullable, format-validated, Donor only), `CreatedAt`.
- **Campaign**: `Id`, `Title`, `Description`, `StartDate`, `EndDate`, `FundingGoal`, `TotalRaised` (default 0, updated only by Worker), `Status` (`Active`/`Completed`/`Cancelled`), `CreatedByUserId`. Invariants: `EndDate` must be in the future at creation; `FundingGoal > 0`.
- **Donation**: `Id`, `CampaignId`, `DonorId`, `Amount`, `Status` (`Pending`/`Processed`/`Failed`), `CreatedAt`, `ProcessedAt` (nullable). Invariant: cannot be created against a `Completed`/`Cancelled` campaign.

Full diagrams (C4 container, ER, donation-flow sequence, deployment views for compose + Minikube) live in [docs/architecture.md](docs/architecture.md).

**Gap**: `Campaign.Status` has no domain method to transition to `Completed`/`Cancelled` yet (only `Create` → always `Active`). The "can't donate to a closed campaign" rule is implemented (`Campaign.CanReceiveDonations`) but currently unreachable/untestable since nothing can close a campaign. Add a `Complete()`/`Cancel()` method (and endpoint) when campaign management is built out further.

## Kafka / Donation Flow

- **Producer**: `Solidary.Domain.Abstractions.IEventPublisher` is a generic `PublishAsync<TEvent>(topic, key, event, ct)` contract (no dependency on `Solidary.Contracts` from Domain — stays generic). `Solidary.Infrastructure.Messaging.KafkaEventPublisher` implements it with a single long-lived `IProducer<string, string>` (JSON-serialized payload), registered as a DI singleton in `AddInfrastructure()`. Both Api and Worker get this registration since both call `AddInfrastructure()`, even though only Api's `SubmitDonationCommandHandler` currently uses it — harmless, just one extra idle producer client in Worker.
- **Topic name is a shared constant**: `Solidary.Contracts.KafkaTopics.DonationReceived = "ReceivedDonationEvent"` — both producer (Api) and consumer (Worker) reference this constant rather than a magic string, so they can't drift.
- **Consumer**: `Solidary.Worker.Consumers.DonationEventConsumer` is a `BackgroundService` using `Confluent.Kafka`'s `IConsumer<string, string>` directly (manual offset commit — `EnableAutoCommit = false`). Polls with `Consume(TimeSpan.FromSeconds(1))` in a loop (the sync blocking pattern, not `Consume(CancellationToken)`, to keep clean shutdown behavior simple). Only commits the offset after successfully processing the message — an exception during processing leaves the offset uncommitted so the message is retried on the next poll (at-least-once delivery).
- **Idempotency**: before applying an event, the consumer checks `Donation.Status == Processed` and skips if so — guards against duplicate delivery (a message reprocessed after a crash-before-commit, as happens routinely with at-least-once + manual commit). Verified this in practice: killing and restarting the Worker mid-flow replayed the last uncommitted message and it applied correctly without double-counting.
- **Use cases**: `UseCases/Campaigns/Create/CreateCampaignCommand` (Admin-only) and `UseCases/Donations/Submit/SubmitDonationCommand` (any authenticated user, acting as themselves) — the latter persists a `Donation` (`Pending`) then publishes `ReceivedDonationEvent` keyed by `CampaignId` (keeps all events for one campaign on the same partition, preserving per-campaign ordering). The Api endpoint never touches `Campaign.TotalRaised`.
- **Endpoints** (`Solidary.Api/Endpoints/CampaignEndpoints.cs`): `POST /campaigns` (`AdminOnly` policy) and `POST /campaigns/{campaignId:guid}/donations` (any authenticated user — the acting user's id is read from the JWT `sub` claim via `ClaimsPrincipal`, never trusted from the request body).
- **Worker's DB access**: no MediatR — `DonationEventConsumer` resolves a scoped `SolidaryDbContext` per message via `IServiceScopeFactory` (the consumer itself is a long-lived singleton `BackgroundService`, so it can't hold a scoped `DbContext` directly), applies `Campaign.ApplyDonation(amount)` + `Donation.MarkProcessed()`, wraps the two updates in an explicit transaction, matching the "single DB transaction" architecture decision even though a single `SaveChangesAsync` call would already be atomic on its own.
- **Gotcha hit during this work**: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 pins a transitive `Microsoft.EntityFrameworkCore.Relational` at exactly `10.0.4`, while other explicit references in the solution were pinned to `10.0.10` — this produced only a build-time `MSB3277` warning (easy to dismiss) but caused a real `FileNotFoundException` at runtime in the Worker once it actually touched `SolidaryDbContext` (the assembly shipped in Worker's own `bin/` didn't match what `Solidary.Infrastructure.dll` was compiled against). Fixed by adding an explicit `Microsoft.EntityFrameworkCore.Relational` `10.0.10` `PackageReference` to `Solidary.Infrastructure.csproj` and doing a full `bin`/`obj` clean before rebuilding. **Lesson: an EF Core version-conflict `MSB3277` warning in this solution is not safe to ignore — treat it as a build error and pin the version.**

## Persistence & Auth

- `SolidaryDbContext` lives in `Solidary.Infrastructure/Persistence`, with one `IEntityTypeConfiguration<T>` per entity under `Persistence/Configurations`. Migrations live in `Persistence/Migrations`; design-time context creation goes through `SolidaryDbContextFactory` (reads `SOLIDARY_DB_CONNECTION` env var, falls back to the local compose connection string) so `dotnet ef` works without spinning up the Api host.
- **Admin is seeded via `HasData`** in `UserConfiguration` with a fixed Guid (`00000000-0000-0000-0000-000000000001`), email `admin@solidary.local`, password `Admin@123` (BCrypt-hashed, dev-only — document in README). `HasData` requires fully deterministic values, which is why the id/hash/timestamp are hardcoded constants rather than generated at migration time.
- **CPF validation is a real checksum**, not just a regex — `Solidary.Domain.ValueObjects.CpfValidator` implements the standard Brazilian check-digit algorithm. Covered by its own test class since it's a business rule independent of any single handler.
- **Password hashing**: `IPasswordHasher`/`ITokenGenerator` interfaces live in `Solidary.Domain.Abstractions` (pure contracts, no infra dependency); `BCryptPasswordHasher`/`JwtTokenGenerator` implementations live in `Solidary.Infrastructure.Auth`. Registered as singletons in DI since both are stateless.
- **Auth use cases use MediatR** (`Solidary.Application/UseCases/Auth/Register/RegisterDonorCommand`, `UseCases/Auth/Login/LoginCommand`) returning a lightweight `Result<T>` (`Solidary.Application.Common.Result`) instead of throwing on expected validation failures (duplicate email, invalid CPF, wrong password) — keeps handlers testable without exception-driven control flow, and endpoints (`Solidary.Api/Endpoints/AuthEndpoints.cs`) map `Result` to the right HTTP status (400 for register failures, 401 for login failures). See "Layering Conventions" above.
- JWT claims: `sub` (user id), `email`, `name`, `role`. Signing key/issuer/audience/expiry come from the `Jwt` config section (`JwtSettings`) — the dev signing key in `appsettings.json` is explicitly marked dev-only and must be overridden via env var/K8s secret for anything beyond local use.
- `POST /auth/register` (public, Donor only) and `POST /auth/login` (public) are mapped in `Endpoints/AuthEndpoints.cs`; `/health` exposes a Postgres-backed health check via `AspNetCore.HealthChecks.NpgSql`.
- Verified end-to-end against the compose Postgres: migration applies cleanly, admin seed row present, register/login/duplicate-email/wrong-password all return the expected status codes and a decodable JWT with correct role claim.

## API Documentation (Swagger)

- **`Microsoft.AspNetCore.OpenApi`** (built-in, already present) generates the OpenAPI document at `/openapi/v1.json`; **`Swashbuckle.AspNetCore.SwaggerUI`** renders it as an interactive UI at `/swagger` — only the UI package is Swashbuckle, not its own document generator (avoids running two competing OpenAPI generators).
- **JWT auth in Swagger**: `Solidary.Api/OpenApi/BearerSecuritySchemeTransformer.cs` is an `IOpenApiDocumentTransformer` (registered via `AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>())` in `DependencyInjection.cs`) that adds a `Bearer`/JWT security scheme and requires it on every operation, so Swagger UI shows an **Authorize** button — paste the raw token from `POST /auth/login`, no `Bearer ` prefix needed. Gotcha: `OpenApiComponents.SecuritySchemes` is `null` on a fresh document, not just `Components` — both need `??=` or you get a 500 from the transformer.
- **Exposed unconditionally, not gated to `IsDevelopment()`** — the hackathon grading script explicitly demos "Autenticação via Postman/Swagger" (spec section on the demo video script), and the app runs via compose/K8s where `ASPNETCORE_ENVIRONMENT` won't be `Development` by default. This is a deliberate hackathon-demo choice, not an oversight — call it out if this code is ever adapted for a real production deployment.
- Mapped via `Endpoints/OpenApiEndpoints.cs` (`MapOpenApiEndpoints()`), following the same "dedicated file per endpoint group" convention as auth/observability.

## Observability

- **`prometheus-net.AspNetCore`** provides both services' `/metrics` endpoint (`app.UseHttpMetrics()` + `app.MapMetrics("/metrics")`), giving HTTP request count/duration/in-progress metrics for free — no custom instrumentation yet.
- **Worker is now a `WebApplication` host, not a plain generic `Host`** — needed Kestrel to expose `/health`/`/metrics` alongside the `BackgroundService`. This required adding `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `Solidary.Worker.csproj` (the `Microsoft.NET.Sdk.Worker` SDK doesn't pull in ASP.NET Core by default) and an explicit `using Microsoft.AspNetCore.Builder;` (no implicit usings for it outside `Sdk.Web`). Worker's `/health` is still liveness-only (no DB/Kafka connectivity check wired into the health check itself yet) even though the Worker now does real DB/Kafka work — see "Kafka / Donation Flow" above. The original placeholder `Worker.cs` (the logging-loop `BackgroundService` from the template) has been deleted and replaced by `DonationEventConsumer`.
- **Fixed ports, not launch-profile randoms**: Api listens on `8080`, Worker on `8081`, both via a `Kestrel:Endpoints:Http:Url` entry in `appsettings.json` (this config wins over `ASPNETCORE_URLS`/launch profiles, so don't pass `--urls` alongside it — that double-binds and crashes on startup). Matches the ports already declared in `config/prometheus/prometheus.yml`. `launchSettings.json` updated to match for consistency, though it no longer has any effect once the Kestrel config section is present.
- Dropped `UseHttpsRedirection()` from the Api — no HTTPS endpoint is configured (TLS termination is expected at the ingress/gateway layer in K8s, not in-process), so the redirect had nothing to redirect to.
- Verified locally: ran Api + Worker directly on the host (`dotnet run`) against the compose Postgres, both `/health` returned `Healthy`, both `/metrics` returned real Prometheus exposition text. Started the compose Prometheus and confirmed its config loads both scrape jobs (`solidary-api`, `solidary-worker`) without error — they show as `down` since Api/Worker aren't containerized yet (expected), but a container-to-host reachability check (`docker exec prometheus wget host.docker.internal:8080/metrics`) confirms scraping will work as soon as they join the compose network under their real service names.

## Status

Scaffolded so far: solution structure (`Solidary.sln` + 7 projects, references wired, builds clean — added `Solidary.Application`), domain entities/enums, `ReceivedDonationEvent` contract, `docs/architecture.md`, EF Core `SolidaryDbContext` + initial migration (auto-applied on Api startup), JWT auth with Register/Login MediatR use cases, `/health` + `/metrics` on both Api and Worker, layered architecture refactor — Application/UseCases layer, per-layer `DependencyInjection.cs` extensions, dedicated `Endpoints/` mapping files, Swagger UI at `/swagger` with JWT Authorize support, Kafka producer/consumer wiring end-to-end (`POST /campaigns` → `POST /campaigns/{id}/donations` → Kafka → Worker updates `Campaign.TotalRaised` idempotently), 20 passing xUnit tests, **Api and Worker fully containerized** with a `docker compose --profile app up -d --build` one-command path (verified: register/login/create-campaign/donate all work end-to-end through the containers, Worker consumes and updates the DB correctly) alongside the original `docker compose up -d` deps-only path for IDE-based development, and a project [README.md](README.md).

Not yet started: `k8s/` manifests, CI workflow, a real Grafana dashboard (datasource is provisioned but no dashboard JSON yet), public "list active campaigns" transparency endpoint, `Campaign.Complete()`/`Cancel()` domain methods (see "Gap" note under Entities), `docs/db-justification.pdf`. Known cosmetic issue: harmless `libgssapi_krb5` load warning in container logs (see "Local Dependencies & Containerization" above).
