# Conexão Solidária — Architecture

MVP donation platform for the NGO "Esperança Solidária". Two microservices (`Api`, `Worker`) communicating asynchronously via Kafka, backed by a shared PostgreSQL database, observed with Prometheus + Grafana.

## 1. Container Diagram

```mermaid
flowchart TB
    Donor((Donor))
    Admin((Admin))

    subgraph Cluster["Kubernetes Cluster (Minikube)"]
        Api["Solidary.Api\n.NET 10 / MediatR / CQRS\nJWT auth"]
        Worker["Solidary.Worker\n.NET 10\nKafka consumer\n(internal only)"]
        DB[("PostgreSQL\nshared database")]
        Kafka["Kafka\ntopic: ReceivedDonationEvent"]
        Prometheus["Prometheus"]
        Grafana["Grafana"]
    end

    Donor -->|"HTTPS: register, donate,\nview active campaigns"| Api
    Admin -->|"HTTPS: create/edit campaigns\n(JWT, role=Admin)"| Api

    Api -->|"EF Core"| DB
    Api -->|"publish ReceivedDonationEvent"| Kafka
    Kafka -->|"consume"| Worker
    Worker -->|"EF Core: update Campaign.TotalRaised,\nmark Donation Processed"| DB

    Api -->|"/metrics"| Prometheus
    Worker -->|"/metrics"| Prometheus
    Prometheus --> Grafana
```

**Notes**
- `Worker` has no external Service/Ingress exposure — it only talks to Kafka and Postgres.
- Both services expose `/health` (liveness/readiness) and `/metrics` (Prometheus scrape target).
- Single shared Postgres database for the MVP: simpler to run/demo in a hackathon timeframe than database-per-service; the tradeoff is documented in the DB justification doc since `Worker` writes into the same schema `Api` owns migrations for.

## 2. Entity-Relationship Diagram

```mermaid
erDiagram
    USER ||--o{ CAMPAIGN : creates
    USER ||--o{ DONATION : makes
    CAMPAIGN ||--o{ DONATION : receives

    USER {
        guid Id PK
        string FullName
        string Email UK
        string PasswordHash
        string Cpf "nullable, Donor only"
        enum Role "Admin | Donor"
        datetime CreatedAt
    }

    CAMPAIGN {
        guid Id PK
        string Title
        string Description
        datetime StartDate
        datetime EndDate
        decimal FundingGoal
        decimal TotalRaised "updated only by Worker"
        enum Status "Active | Completed | Cancelled"
        guid CreatedByUserId FK
    }

    DONATION {
        guid Id PK
        guid CampaignId FK
        guid DonorId FK
        decimal Amount
        enum Status "Pending | Processed | Failed"
        datetime CreatedAt
        datetime ProcessedAt "nullable"
    }
```

## 3. Sequence Diagram — Donation Flow

```mermaid
sequenceDiagram
    actor Donor
    participant Api as Solidary.Api
    participant DB as PostgreSQL
    participant Kafka
    participant Worker as Solidary.Worker

    Donor->>Api: POST /donations {campaignId, amount} (JWT)
    Api->>DB: validate campaign is Active
    Api->>DB: insert Donation (Status=Pending)
    Api->>Kafka: publish ReceivedDonationEvent
    Api-->>Donor: 202 Accepted

    Kafka->>Worker: consume ReceivedDonationEvent
    Worker->>DB: begin transaction
    Worker->>DB: Campaign.TotalRaised += Amount
    Worker->>DB: Donation.Status = Processed
    Worker->>DB: commit

    Donor->>Api: GET /campaigns (public)
    Api->>DB: select Active campaigns
    Api-->>Donor: [{Title, FundingGoal, TotalRaised}]
```

This matches the demo video script: show the donation payload being sent, open the Kafka UI to show the message on the topic, then call the public campaigns endpoint to prove the Worker updated the total.

## 4. Deployment Views

### Local development (docker-compose)

```mermaid
flowchart LR
    subgraph "docker-compose"
        api[api]
        worker[worker]
        postgres[(postgres)]
        kafka[kafka]
        prometheus[prometheus]
        grafana[grafana]
    end
    api --> postgres
    worker --> postgres
    api --> kafka
    kafka --> worker
    prometheus --> api
    prometheus --> worker
    grafana --> prometheus
```

### Minikube

```mermaid
flowchart LR
    subgraph "Minikube cluster"
        subgraph "Deployments"
            apiDep["api Deployment"]
            workerDep["worker Deployment"]
            pgDep["postgres Deployment"]
            kafkaDep["kafka Deployment"]
            promDep["prometheus Deployment"]
            grafDep["grafana Deployment"]
        end
        apiSvc["api Service\n(ClusterIP/NodePort)"]
        pgSvc["postgres Service"]
        kafkaSvc["kafka Service"]
        cm["ConfigMap\nconnection strings, Kafka broker,\nJWT settings"]
    end
    apiSvc --> apiDep
    apiDep --> pgSvc --> pgDep
    apiDep --> kafkaSvc --> kafkaDep
    kafkaSvc --> workerDep
    workerDep --> pgSvc
    cm -.-> apiDep
    cm -.-> workerDep
```

`Worker` has a Deployment but no externally-reachable Service (ClusterIP only for Prometheus scraping), consistent with "not exposed externally" in the requirements.

## 5. Naming Reference (Portuguese spec → English domain model)

| Spec term (pt-BR) | Domain model (en) |
|---|---|
| Usuário | `User` |
| GestorONG | `UserRole.Admin` |
| Doador | `UserRole.Donor` |
| Campanha | `Campaign` |
| Meta Financeira | `Campaign.FundingGoal` |
| Valor Total Arrecadado | `Campaign.TotalRaised` |
| Doação | `Donation` |
| DoacaoRecebidaEvent | `ReceivedDonationEvent` |
