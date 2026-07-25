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
├── k8s/                              # Deployments, Services, ConfigMaps, PVCs (Minikube target) — done, verified live
├── docs/
│   ├── architecture.md               # Mermaid diagrams (C4 container, ER, sequence, deployment)
│   └── db-justification.pdf          # required "why Postgres for X and Y" doc — not written yet
├── .github/workflows/ci.yml          # build + docker image on push to main — not started yet
├── .dockerignore
├── docker-compose.yml                # `up -d` = deps only; `--profile app up -d --build` = deps + containerized Api/Worker
└── README.md                         # step-by-step local run instructions
```

## Layering Conventions

- **`Solidary.Application` holds all use cases**, under `UseCases/<Group>/<UseCase>/` (e.g. `UseCases/Auth/Register/RegisterDonorCommand.cs` + `RegisterDonorCommandHandler.cs`). Not called "Features" — always "UseCases". Each use case is a MediatR command/query + handler pair returning `Solidary.Application.Common.Result<T>` on expected failures (validation, not-found, wrong credentials) rather than throwing. **Exception**: pure list/read queries with no expected-failure path (e.g. `ListActiveCampaignsQuery`) skip the `Result<T>` wrapper and just return the data directly — `Result<T>` exists to avoid exception-driven control flow for *expected* failures, not as a blanket wrapper for every handler return type.
- **Every layer owns its own DI registration** via a `DependencyInjection.cs` at the project root, exposing a single `IServiceCollection` extension method named after the layer:
  - `Solidary.Application.DependencyInjection.AddApplication()` — registers MediatR against the Application assembly.
  - `Solidary.Infrastructure.DependencyInjection.AddInfrastructure(IConfiguration)` — DbContext, JWT settings binding, password hasher/token generator.
  - `Solidary.Api.DependencyInjection.AddApi(IConfiguration)` — OpenApi, JWT bearer authentication, authorization policies, health checks, Hangfire (i.e. presentation-layer/host concerns, not reusable by Worker).
  - `Solidary.Worker.DependencyInjection.AddWorker()` — registers the Kafka consumer hosted service + health checks. Worker does **not** call `AddApplication()` — it talks to `SolidaryDbContext` directly rather than through MediatR use cases (MediatR/CQRS is an Api-side pattern per the stack decisions; Worker's job is a single background reaction to one event type, not request handling).
  - `Program.cs` in each host project only *calls* these (`builder.Services.AddApi(...)`, `.AddInfrastructure(...)`, `.AddApplication()`) — it must never register services inline.
- **Endpoint mapping lives in `Solidary.Api/Endpoints/`, one file per endpoint group**, each exposing an `IEndpointRouteBuilder` extension (e.g. `AuthEndpoints.MapAuthEndpoints()`, `ObservabilityEndpoints.MapObservabilityEndpoints()` for `/health` + `/metrics`). `Program.cs` only calls `app.MapXxxEndpoints()` — no inline `app.MapPost(...)` calls.
- Net effect: `Program.cs` in the Api is pure composition — DI extension calls, middleware pipeline (`UseHttpMetrics`, `UseAuthentication`, `UseAuthorization`), then endpoint-group mapping calls. No business logic, no inline route handlers, no inline service registration.

## Entities

- **User**: `Id`, `FullName`, `Email` (unique), `PasswordHash` (BCrypt), `Role` (`Admin`/`Donor`), `Cpf` (nullable, format-validated, Donor only), `CreatedAt`.
- **Campaign**: `Id`, `Title`, `Description`, `StartDate`, `EndDate`, `FundingGoal`, `TotalRaised` (default 0, updated only by Worker), `Status` (`Active`/`Completed`/`Cancelled`), `CreatedByUserId`. Invariants: `EndDate` must be in the future at creation; `FundingGoal > 0`.
- **Donation**: `Id`, `CampaignId`, `DonorId`, `Amount`, `Status` (`Pending`/`Processed`/`Failed`), `CreatedAt`, `ProcessedAt` (nullable). Invariant: cannot be created against a `Completed`/`Cancelled` campaign.

Full diagrams (C4 container, ER, donation-flow sequence, deployment views for compose + Minikube) live in [docs/architecture.md](docs/architecture.md).

`Campaign.Complete()` and `Campaign.Cancel()` (both guarded — only callable from `Active`, mirroring the `Create` invariant style) close the loop on the previously-flagged gap: campaigns can now actually leave `Active`, which makes `Campaign.CanReceiveDonations`/the "no donations to a closed campaign" rule reachable and tested.

## Campaign Lifecycle & Background Jobs

- **Auto-close on expiry**: `Solidary.Api/BackgroundJobs/CloseExpiredCampaignsJob.cs` is a **Hangfire recurring job** (`*/5 * * * *`, id `close-expired-campaigns`) that finds `Active` campaigns whose `EndDate` has passed and calls `Campaign.Complete()` on each. Scheduled via `IRecurringJobManager` (the DI/service-based API) in `BackgroundJobScheduler.ScheduleRecurringJobs()`, called from `Program.cs` right after migrations run. **Use the service-based `IRecurringJobManager`, not the static `RecurringJob.AddOrUpdate<T>()`** — the static API depends on `JobStorage.Current` being set as a global, which isn't guaranteed with the `services.AddHangfire(...)` DI registration path and throws `"Current JobStorage instance has not been initialized"` at runtime (hit this directly — the static call compiles fine and only fails when actually invoked).
- **Where it runs**: inside the `Solidary.Api` process, not Worker. Decision: Hangfire is a natural fit for "the web host also runs its own scheduled application jobs" (a very common .NET pattern), and keeping it out of Worker preserves Worker's single responsibility (react to Kafka events only) as already documented above.
- **Storage**: `Hangfire.PostgreSql`, same shared Postgres connection string as EF Core. Hangfire creates and owns its own tables (`hangfire` schema) automatically on startup — outside EF Core's migration control, by design (standard Hangfire behavior, not a gap).
- **No dashboard UI** (`app.UseHangfireDashboard()` intentionally not mapped) — pairing Hangfire's dashboard with our stateless Bearer-JWT auth isn't a good fit (the dashboard expects browser/cookie-style auth for its authorization filter), and it wasn't asked for. Job execution is observable via Api logs (`CloseExpiredCampaignsJob` logs how many campaigns it closed) and by querying `Campaigns.Status` directly.
- **Admin-only cancel**: `POST /campaigns/{campaignId}/cancel` (`UseCases/Campaigns/Cancel/CancelCampaignCommand`, `AdminOnly` policy) — same guard pattern as everywhere else (pre-check `campaign.CanReceiveDonations` before calling `Cancel()`, return `Result.Failure` instead of catching the domain exception).
- **Public transparency listing**: `GET /campaigns` (`UseCases/Campaigns/ListActive/ListActiveCampaignsQuery`, no auth) returns `Title`/`FundingGoal`/`TotalRaised` for `Active` campaigns only — this is the query that skips the `Result<T>` wrapper (see "Layering Conventions").
- **Fixed: `+00:00`-offset timestamp bug.** `Campaign.StartDate`/`EndDate` were plain `DateTime`, and ASP.NET Core's default `System.Text.Json` binding parsed an ISO-8601 timestamp with an explicit `+00:00` offset differently from one with a literal `Z` suffix (`Z` → `DateTimeKind.Utc` reliably; `+00:00` could come through local-adjusted). Fixed by switching both properties (and everything downstream — `CreateCampaignCommand`, `CreateCampaignRequest`, `CloseExpiredCampaignsJob`'s `now` comparison) to `DateTimeOffset`, which has no `Kind`-ambiguity: the offset is always explicit and unambiguous regardless of how it's written (`Z`, `+00:00`, `-03:00`, `+05:30` — all verified working end-to-end after the fix).
  - **EF Core/Postgres side needed a second fix, not just the type change**: switching the CLR property to `DateTimeOffset` alone produced a `DbUpdateException` — *"Cannot write DateTimeOffset with Offset=-03:00:00 to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported"*. Npgsql's `timestamptz` writer requires the `DateTimeOffset` to already be UTC-normalized (`Offset=0`); it does not convert non-UTC offsets for you. Fixed in `Campaign.Create()` by calling `.ToUniversalTime()` on both dates before persisting (preserves the same instant, just re-expressed with `Offset=0`) — **domain entities should always normalize `DateTimeOffset` values to UTC before they reach Npgsql**, don't rely on the caller having done it.
  - **No DB schema migration was actually needed**: Npgsql already mapped plain `DateTime` to `timestamp with time zone` by default (not `timestamp without time zone`), so the CLR-type-only change produced an *empty* migration (`ChangeCampaignDatesToDateTimeOffset`) — kept anyway (rather than deleted) purely to keep `SolidaryDbContextModelSnapshot.cs` in sync with the real model; deleting it after `dotnet ef migrations add` reverts the snapshot back to the stale `DateTime` state, which would make the *next* real migration's diff noisy/wrong.
  - Verified end-to-end with `Z`, `+00:00`, `-03:00`, and `+05:30` timestamps, confirming Postgres stores the correct UTC-normalized instant in every case (checked via direct `psql` query).

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
- **Default prometheus-net metrics confirmed available for dashboards**: `up{job=...}` (Prometheus-injected), `process_cpu_seconds_total`, `process_working_set_bytes`, `http_requests_received_total`, `http_request_duration_seconds` (histogram), `http_requests_in_progress` — all present out of the box, no extra package needed for CPU/memory/HTTP panels.

## Custom Metrics

- **`Solidary.Worker.Metrics.DonationMetrics`** adds two custom counters on top of prometheus-net's defaults, both labeled `campaign_id` + `campaign_title`: `solidary_donations_processed_total` (count) and `solidary_donation_amount_total` (sum of amounts). Registered as a DI singleton (`AddWorker()`), injected into `DonationEventConsumer`, and recorded **only after** a donation's DB transaction commits — i.e. never on a retried/duplicate delivery, since the idempotency check short-circuits before reaching that point. This means the metrics and the DB are always consistent with each other by construction.
- **Testability over the static registry pattern**: `DonationMetrics` takes a `CollectorRegistry` in its constructor (defaulting to `Prometheus.Metrics.DefaultRegistry` in production via a parameterless constructor) instead of using `Prometheus.Metrics.CreateCounter(...)` against static fields directly. This avoids two problems with the static-field approach: cross-test pollution of shared global state, and no clean way to read a counter's current value back out for assertions without wrestling with `CollectFamilies()`. Tests construct `new DonationMetrics(new CollectorRegistry())` for full isolation and read values back via `GetProcessedCount`/`GetTotalAmount`.
- Verified live (via the `--profile app` containerized stack, so Prometheus could actually reach `worker:8081` under its real service name): raw counters incremented correctly per donation, and `increase(solidary_donation_amount_total[1h])` correctly reflected a fresh donation after Prometheus had scraped a baseline sample — a counter's `increase()` needs at least one earlier sample to compute movement from, so donations issued right as a container starts up (before the first scrape lands) won't show a jump until the *next* donation on top of that baseline. Not a bug, just how counters + `increase()` work — worth remembering if a demo shows "0" right after a fresh `docker compose --profile app up`.

## Grafana Dashboard

- **`config/grafana/provisioning/dashboards/solidary-overview.json`** — auto-provisioned (no manual import step) via the existing `provider.yml` file-provisioner. Two **rows** (Grafana has no native "tabs" within a single dashboard; rows are the closest equivalent and were used deliberately for "Service Health" vs "Donations"):
  - **Service Health**: Api/Worker up-status, HTTP request rate (by status code), HTTP p95 latency, CPU usage, memory (working set), in-progress requests — all from prometheus-net's default metrics, no custom instrumentation needed.
  - **Donations**: amount donated in the last hour (stat), total donations processed (stat), amount by campaign in the last hour (bar gauge), donations-by-campaign over time (time series), amount-by-campaign over time (time series) — all from the custom `DonationMetrics` counters above.
- **Datasource pinned to a fixed `uid: prometheus`** in `config/grafana/provisioning/datasources/prometheus.yml` (was previously auto-generated/unstable) so dashboard panels can reference `{"type": "prometheus", "uid": "prometheus"}` reliably instead of depending on datasource name resolution.
- **Gotcha hit while wiring this up**: after changing the datasource's `uid`, Grafana's persisted SQLite state (the `grafana_data` volume) still referenced the old auto-generated UID and the whole provisioning module failed to start (`"Datasource provisioning error: data source not found"`) — Grafana's provisioning reconciliation doesn't cleanly handle a UID change on an already-provisioned datasource. Fixed by dropping the `grafana_data` volume and letting it re-provision from scratch (Grafana's local state is fully disposable/reproducible from the provisioning files — never store anything there that isn't reproducible that way).
- Verified live end-to-end via the `--profile app` containerized stack: `up{job="solidary-api"}`/`up{job="solidary-worker"}` = 1, HTTP request rate query returned real per-status-code series, and the donation amount/count panels reflected real donations made through the API, queried directly through Grafana's own datasource-proxy API (not just Prometheus directly) to confirm the dashboard's actual query path works, not just the underlying data.

## Kubernetes (`k8s/`, Minikube)

Mirrors the `--profile app` compose stack 1:1 in a dedicated `solidary` namespace: `00-namespace`, `01-configmap` / `02-secret` (same dev-only values already committed elsewhere — connection string, JWT signing key, Postgres creds), `03-postgres` / `04-kafka` (each with a PVC), `05-kafka-ui` (demo-only, shows messages on the topic — not used by the app), `06-prometheus`, `07-grafana` (same dashboard JSON as compose, delivered as ConfigMaps instead of bind-mounts), `08-api` (NodePort), `09-worker` (ClusterIP — stays internal per the existing architecture decision). Local dependencies (`docker`, `minikube`, `kubectl`) were already installed; no new tooling needed. Plain YAML, no Helm/Kustomize — matches the spec's literal "Deployments, Services, ConfigMaps" ask.

**Local workflow**: `minikube start --driver=docker --cpus=3 --memory=4500` (Docker Desktop's own memory allocation caps how much Minikube can request — check `docker info` or just try a value and let Minikube's error tell you the ceiling), then `eval $(minikube docker-env)` before `docker build` so images land in Minikube's own daemon (`imagePullPolicy: IfNotPresent` on Api/Worker is what makes K8s use that local image instead of trying to pull from a registry), then `kubectl apply -f k8s/`.

**Three real bugs hit and fixed while standing this up** (all specific to Kubernetes — none of these show up in docker-compose, which routes container-to-container traffic completely differently):

1. **Kafka self-registration hairpin-NAT deadlock.** A single-node KRaft broker acts as its own controller and has to connect to itself at its advertised address (`kafka:9093`) to complete startup registration. Routing that through a normal `ClusterIP` Service — pod calls the Service IP, which maps back to the same pod — hit a hairpin-NAT dead end with Minikube's bridge CNI and timed out every time, crash-looping. **Fixed with a headless Service** (`clusterIP: None`), which makes `kafka` resolve straight to the pod IP via DNS instead of routing through kube-proxy — the same pattern real Kafka Helm charts use (StatefulSet + headless Service), just applied to a single-replica Deployment here.
2. **...which then created a second, stacked deadlock**: headless-Service DNS only publishes a pod's address once it's `Ready` — but the pod can't become `Ready` until it resolves its own `kafka` DNS name to finish that same registration. Chicken-and-egg. **Fixed with `publishNotReadyAddresses: true`** on the Service, which publishes the DNS record immediately instead of waiting on readiness.
3. **Exec probe timeout too short for a JVM-spawning health check.** The readiness/liveness probes reused the docker-compose healthcheck command (`kafka-broker-api-versions.sh`, which starts a full JVM per invocation) but Kubernetes exec probes default `timeoutSeconds` to **1 second** — compose's healthcheck had explicitly set `timeout: 10s`, which I forgot to carry over. Under Minikube's constrained CPU, JVM startup alone routinely exceeded 1s, so Kubernetes killed an otherwise-healthy broker on a false-positive liveness failure, over and over. **Fixed by setting `timeoutSeconds: 10` and `failureThreshold: 5`** on both probes. Verified stable for 400+ seconds with 0 restarts after the fix (vs. crash-looping every ~90s before it).

Also needed a Service port fix along the way: the `kafka` Service originally only declared port `9092`, but `KAFKA_CONTROLLER_QUORUM_VOTERS` advertises the controller at `kafka:9093` — unlike compose (where container-to-container traffic reaches any port regardless of what's "published"), a K8s Service only forwards ports it explicitly lists. Added a second `9093` port entry.

**Debugging-loop lesson** (this took a few iterations to get right, worth remembering): when a wait/verification loop won't be bounded by anything external, always give it an explicit max iteration count and print incremental progress, even for "this should just take a few seconds" checks — an unbounded `until <condition>; do sleep N; done` with no visibility looks identical whether it's about to succeed or waiting on something that will never become true (like a pod that's crash-looping instead of becoming ready). A bounded loop that prints each check and always exits — success or not — turns "stuck, unclear why" into "here's the actual state, plus a paper trail of the last N checks" every time.

**Verified end-to-end on Minikube** (via `kubectl port-forward`, since Minikube's `docker` driver needs a long-lived tunnel process for `minikube service --url` on macOS — a background port-forward is simpler for scripted verification): register → login (seeded Admin + new Donor) → create campaign → donate → confirmed via direct Postgres query, the public `/campaigns` endpoint, Grafana's datasource-proxy (`up{job=...}=1` for both, dashboard panels showing the real donation), and Kafka UI showing the topic with the message (`offsetMax: 1`).

## Status

Scaffolded so far: solution structure (`Solidary.sln` + 7 projects, references wired, builds clean — added `Solidary.Application`), domain entities/enums, `ReceivedDonationEvent` contract, `docs/architecture.md`, EF Core `SolidaryDbContext` + initial migration (auto-applied on Api startup), JWT auth with Register/Login MediatR use cases, `/health` + `/metrics` on both Api and Worker, layered architecture refactor — Application/UseCases layer, per-layer `DependencyInjection.cs` extensions, dedicated `Endpoints/` mapping files, Swagger UI at `/swagger` with JWT Authorize support, Kafka producer/consumer wiring end-to-end, **Api and Worker fully containerized** with a `docker compose --profile app up -d --build` one-command path alongside the deps-only `docker compose up -d` path for IDE-based development, a project [README.md](README.md), **campaign lifecycle** — `Campaign.Complete()`/`Cancel()`, a Hangfire recurring job auto-closing expired campaigns (verified: a 25-second-lived test campaign was closed automatically), an Admin-only cancel endpoint, and a public list-active-campaigns transparency endpoint, **custom Prometheus metrics** for donations by campaign, and a **provisioned Grafana dashboard** with health + donations rows (both verified live with real data through the containerized stack). 30 passing xUnit tests.

`Campaign.StartDate`/`EndDate` switched to `DateTimeOffset` (see "Campaign Lifecycle & Background Jobs") — fixes the timestamp-offset bug, verified with `Z`/`+00:00`/`-03:00`/`+05:30` all producing correct UTC-normalized values in Postgres. 30 passing xUnit tests (unchanged count — no new tests added for this fix; existing coverage already exercises `Campaign.Create` via the implicit `DateTime`→`DateTimeOffset` conversion, and the fix was verified via live HTTP requests instead, per the request that prompted it).

**K8s manifests done and verified live on Minikube** — see "Kubernetes (`k8s/`, Minikube)" above for the full manifest list and the three real bugs found/fixed along the way (Kafka hairpin-NAT self-registration deadlock, headless-Service readiness deadlock, exec-probe timeout too short for a JVM-spawning health check).

Not yet started: CI workflow, `docs/db-justification.pdf`. Known cosmetic issue: harmless `libgssapi_krb5` load warning in container logs (see "Local Dependencies & Containerization").
