# Task 07: Multi-Tier Infrastructure as Code (Terraform) & Local Docker Compose Setup

- **Phase**: Infrastructure & DevOps
- **Lead / Owner**: DevOps & Cloud Infrastructure Lead
- **Complexity**: Medium
- **Prerequisites**: Task 01 (PostgreSQL & TimescaleDB Setup), Task 02 (.NET 9 Backend Core)
- **Entity Model Reference**: `architecture/entity-model.md` — authoritative BioGem domain entity definitions (contracts, physical_deliveries, goo_certificate_transactions, market_prices, companies, counterparties, trading_points)
- **Target Files**:
  - `infra/terraform/tier1_lean/main.tf`
  - `infra/terraform/tier2_growth/main.tf`
  - `infra/terraform/tier3_scale/main.tf`
  - `infra/terraform/modules/networking/main.tf`
  - `infra/terraform/modules/networking/variables.tf`
  - `infra/terraform/modules/networking/outputs.tf`
  - `infra/terraform/modules/database_pg/main.tf`
  - `infra/terraform/modules/database_pg/variables.tf`
  - `infra/terraform/modules/database_pg/outputs.tf`
  - `infra/terraform/modules/compute_ecs/main.tf`
  - `infra/terraform/modules/compute_ecs/variables.tf`
  - `infra/terraform/modules/compute_ecs/outputs.tf`
  - `infra/terraform/modules/caching/main.tf`
  - `infra/terraform/modules/caching/variables.tf`
  - `infra/terraform/modules/caching/outputs.tf`
  - `infra/terraform/modules/iam/main.tf`
  - `infra/terraform/modules/iam/variables.tf`
  - `infra/terraform/modules/iam/outputs.tf`
  - `docker-compose.yml`
  - `infra/postgres/init-extensions.sql`
  - `.devcontainer/devcontainer.json`

---

## 1. Executive Summary & Infrastructure Vision

Task 07 defines production-ready, declarative Infrastructure as Code (IaC) foundation using **Terraform (HCL)** plus hermetic **Docker Compose** local dev/subagent environment for Tradebook.

Tradebook needs infra architecture that evolves from zero-maintenance MVP to high-throughput, multi-region containerized system without destructive refactor. Task 07 specifies **three concrete cloud architecture tiers** (Tier 1 MVP, Tier 2 Growth Containers, Tier 3 Scale K8s) with production-grade HCL Terraform module skeletons, itemized monthly cost models with explicit formulas, trade-off matrices, single-command local orchestration (`docker compose up`).

---

## 2. Detailed Scope & Feature Coverage

### 2.1 Three Cloud Architecture Tiers

Tradebook defines 3 progressive infra tiers optimized for cost efficiency, operational complexity, SLA growth:

```
+---------------------------------------------------------------------------------------------------------+
|                                    TRADEBOOK CLOUD ARCHITECTURE TIERS                                   |
+---------------------------------------------------------------------------------------------------------+
|                                                                                                         |
|  TIER 1: LEAN MVP (Serverless / PaaS)                                                                   |
|  - Target Scale: 100 to 1,000 Active Users (100 TPS Peak)                                               |
|  - Compute: AWS App Runner / GCP Cloud Run (Serverless container deployment)                             |
|  - Database: Aurora Serverless v2 PostgreSQL 17 / Supabase / Neon (0.5 to 4 ACUs)                       |
|  - Caching: Managed Upstash / ElastiCache Serverless Redis                                              |
|  - Objectives: Zero infrastructure maintenance overhead, sub-minute cold setup, minimum monthly spend. |
|                                                                                                         |
|  TIER 2: GROWTH CONTAINERS (AWS ECS Fargate / GCP Cloud Run)                                           |
|  - Target Scale: 10,000 to 100,000 Active Users (10,000 TPS Peak)                                       |
|  - Compute: AWS ECS Fargate running .NET 9 Native AOT containers in Multi-AZ VPC                        |
|  - Database: Provisioned/Serverless AWS Aurora PostgreSQL 17 Multi-AZ (2-16 ACUs) + TimescaleDB          |
|  - Event Bus: NATS JetStream container cluster (3 nodes) for pub/sub & CDC outbox streaming             |
|  - Caching: AWS ElastiCache Redis (2-node cluster with in-transit TLS)                                  |
|  - Objectives: High availability (99.95% SLA), autoscaling, predictable performance, <20ms p99 REST.    |
|                                                                                                         |
|  TIER 3: SCALE KUBERNETES (Multi-AZ EKS / GKE)                                                          |
|  - Target Scale: 1,000,000+ Active Users (100,000+ TPS Peak)                                            |
|  - Compute: AWS EKS (Kubernetes 1.30+) with Karpenter v1.0+ dynamic Graviton3 (ARM64) node provisioning  |
|  - Database: Aurora Global Database (us-east-1 + us-west-2 DR) + ScyllaDB local NVMe ledger              |
|  - Event Bus: Redpanda Event Streaming Bus (Multi-AZ NVMe storage)                                      |
|  - Analytics: ClickHouse OLAP cluster + S3 Cold Parquet Data Lakehouse                                  |
|  - Objectives: Maximum throughput (1M+ TPS), sub-5ms WebSocket broadcasts, 99.99% SLA, RPO < 1s.        |
|                                                                                                         |
+---------------------------------------------------------------------------------------------------------+
```

### 2.2 Production Terraform HCL Module Skeletons

5 modular, production-ready Terraform HCL skeletons:

1. **`networking` Module**:
   - Multi-AZ VPC spanning 3 AZs (`us-east-1a`, `us-east-1b`, `us-east-1c`).
   - Subnet tiering: public subnets (`/24`), private app subnets (`/20`), private DB subnets (`/22`).
   - Multi-AZ NAT Gateways, IGW, route tables, route table associations.
   - S3 + DynamoDB Gateway Endpoints, bypass NAT Gateway data transfer fees ($0.045/GB).
   - Ingress/egress security groups enforce strict zero-trust network perimeter.

2. **`database_pg` Module**:
   - AWS Aurora PostgreSQL 17 DB cluster (`aurora-postgresql`, engine version `17.0`).
   - Serverless v2 scaling config (`min_capacity = 0.5`, `max_capacity = 16.0`).
   - Encrypted DB storage via KMS (`storage_encrypted = true`).
   - DB subnet group bound exclusively to private DB subnets.
   - Custom PostgreSQL parameter group enforces `shared_preload_libraries = "timescaledb,pg_stat_statements,btree_gist"`.
   - Security group restricts 5432 ingress exclusively to private app subnet CIDRs.

3. **`compute_ecs` Module**:
   - AWS ECS cluster (`tradebook-cluster`) with Fargate capacity providers (`FARGATE`, `FARGATE_SPOT`).
   - ALB in public subnets, HTTP/HTTPS listeners, target group health check (`/healthz`).
   - ECS Fargate task definition running ASP.NET Core Native AOT app container.
   - ECS service with auto-scaling policies (`TargetTrackingScaling` on CPU/Memory, request counts).
   - AWS CloudWatch log group, 30-day retention, KMS encryption.

4. **`caching` Module**:
   - AWS ElastiCache Redis replication group (`redis7`, node type `cache.t4g.medium`).
   - Auto failover enabled across Multi-AZ subnet groups.
   - Encryption in-transit (`transit_encryption_enabled = true`) and at-rest (`at_rest_encryption_enabled = true`).
   - Security group restricts port 6379 ingress strictly to ECS task security groups.

5. **`iam` Module**:
   - Least-privilege IAM roles for ECS task execution (`ecs-task-execution-role`) and ECS task (`ecs-task-role`).
   - Policy attachments: AWS ECR pull, CloudWatch Logs append, AWS Secrets Manager read, KMS decrypt.
   - Zero wildcards (`*`) in production action statements.

---

## 3. Itemized Monthly Cost Scaling Model & Formulas

### 3.1 Monthly Cost Table Across Scale Stages ($ USD / Month)

Figures calculated from AWS US-East public list rates (2026 model), 730 hours/month.

| Cost Component Category | Stage 1: MVP (100 Users) | Stage 2: Growth (10,000 Users) | Stage 3: Scale (1,000,000 Users) |
| :--- | :--- | :--- | :--- |
| **Compute (App Runner / ECS / EKS)** | $25.00 (App Runner 1 GB / 0.5 vCPU) | $280.00 (4x `c7g.xlarge` Fargate) | $8,960.00 (64x `c7g.2xlarge` EKS) |
| **Database (PostgreSQL / Aurora)** | $43.80 (Aurora Serverless 0.5 ACU) | $350.00 (Aurora Serverless 2-16 ACU) | $5,600.00 (Provisioned `db.r7g.4xlarge`) |
| **Caching (ElastiCache Redis)** | $15.00 (ElastiCache Serverless 1GB) | $120.00 (2x `cache.t4g.medium`) | $960.00 (4x `cache.r7g.xlarge`) |
| **Storage (EBS gp3 + S3)** | $5.00 (50 GB gp3 + 10 GB S3) | $180.00 (2 TB gp3 + 5 TB S3) | $4,200.00 (50 TB gp3 + 250 TB S3) |
| **Bandwidth & NAT Gateways** | $2.00 (10 GB Egress, direct internet) | $210.00 (2 TB NAT + 1 TB Egress) | $11,800.00 (100 TB NAT + 80 TB Egress) |
| **CDN & WAF Security** | $5.00 (CloudFront Free Tier + WAF) | $120.00 (WAF v2 + CloudFront) | $2,800.00 (WAF v2 + Edge Shield) |
| **Observability (Logs & Metrics)** | $5.00 (Basic CloudWatch Logs) | $150.00 (CloudWatch + OTEL) | $4,500.00 (AMP + CloudWatch Logs) |
| **TOTAL ESTIMATED MONTHLY COST** | **$100.80 / month** | **$1,410.00 / month** | **$38,820.00 / month** |

---

### 3.2 Unit Economic Cost Formulas

#### 1. Cost Per Monthly Active User (MAU) Formula:

$$\text{Cost}_{\text{MAU}} = \frac{\text{Total Monthly Infrastructure Spend}}{\text{DAU} \times 3.0}$$

* **Stage 1 (100 DAU / 300 MAU)**: $\frac{\$100.80}{300} = \mathbf{\$0.3360\text{ per MAU}}$
* **Stage 2 (10,000 DAU / 30,000 MAU)**: $\frac{\$1,410.00}{30,000} = \mathbf{\$0.0470\text{ per MAU}}$
* **Stage 3 (1,000,000 DAU / 3,000,000 MAU)**: $\frac{\$38,820.00}{3,000,000} = \mathbf{\$0.0129\text{ per MAU}}$
* **Efficiency Scaling**: cost per MAU drops **26.0x** from Stage 1 to Stage 3, fixed platform cost amortization.

#### 2. Cost Per 1 Million Transactions Formula:

$$\text{Cost}_{\text{1M Tx}} = \frac{\text{Total Monthly Cost}}{\text{Average TPS} \times 86,400 \times 30.4} \times 1,000,000$$

* **Stage 1 (1 TPS Avg)**: $\frac{\$100.80}{2,626,560} \times 1,000,000 = \mathbf{\$38.38\text{ per 1M Transactions}}$
* **Stage 2 (100 TPS Avg)**: $\frac{\$1,410.00}{262,656,000} \times 1,000,000 = \mathbf{\$5.37\text{ per 1M Transactions}}$
* **Stage 3 (10,000 TPS Avg)**: $\frac{\$38,820.00}{26,265,600,000} \times 1,000,000 = \mathbf{\$1.48\text{ per 1M Transactions}}$

---

## 4. Resource Cost vs Performance vs Resiliency Trade-Off Matrix

| Architectural Metric / Dimension | Tier 1: Lean MVP | Tier 2: Growth Containers | Tier 3: Scale K8s |
| :--- | :--- | :--- | :--- |
| **Monthly Cost Range** | $50 – $150 / mo | $1,000 – $2,500 / mo | $25,000 – $60,000 / mo |
| **Target Scale (Users / TPS)** | 100–1,000 Users (<100 TPS) | 10k–100k Users (10k TPS) | 1M+ Users (100k+ TPS) |
| **p50 Latency (REST API)** | ~45 ms | < 12 ms | < 3 ms |
| **p99 Latency (REST API)** | ~180 ms (Cold starts possible) | < 25 ms | < 8 ms |
| **WebSocket Push Latency** | ~120 ms | < 15 ms | < 2 ms |
| **Target Availability SLA** | 99.5% (Single AZ PaaS) | 99.95% (Multi-AZ ECS Fargate) | 99.99% (Multi-Region Active-Passive) |
| **RPO (Recovery Point Objective)** | < 15 minutes (Snapshot) | < 5 seconds (Aurora WAL) | < 1 second (Aurora Global DB) |
| **RTO (Recovery Time Objective)** | < 1 hour (Manual redeploy) | < 5 minutes (Auto service heal) | < 60 seconds (Automated DNS failover) |
| **Operational Complexity (1–10)** | **1 / 10** (Zero ops, managed PaaS) | **4 / 10** (Standard Docker/ECS) | **8 / 10** (Kubernetes/Karpenter/KMS/Mesh) |

---

## 5. Local Docker Compose & Subagent Environment

### 5.1 Local Architecture Topology

AI agents and devs run full platform locally via single command (`docker compose up`). Spins up 4 isolated, health-checked containers on private bridge network (`tradebook_net`):

```
+---------------------------------------------------------------------------------------------------+
|                                LOCAL DOCKER COMPOSE NETWORK                                       |
+---------------------------------------------------------------------------------------------------+
|                                                                                                   |
|   +-----------------------+     +-----------------------+     +-------------------------------+   |
|   | tradebook_postgres    |     | tradebook_redis       |     | tradebook_localstack          |   |
|   | - PostgreSQL 17       |     | - Redis 7 (Alpine)    |     | - AWS S3, SQS, KMS            |   |
|   | - init-extensions.sql |     | - Health: redis-cli   |     | - Port: 4566                  |   |
|   | - Port: 5432          |     | - Port: 6379          |     | - Health: curl /_localstack   |   |
|   +-----------------------+     +-----------------------+     +-------------------------------+   |
|               ^                             ^                                 ^                   |
|               |                             |                                 |                   |
|               +-----------------------------+---------------------------------+                   |
|                                             |                                                     |
|                                             v                                                     |
|   +-------------------------------------------------------------------------------------------+   |
|   | tradebook_api                                                                             |   |
|   | - .NET 9 ASP.NET Core Native AOT Web API (FastEndpoints REPR)                             |   |
|   | - Depends on: postgres (healthy), redis (healthy), localstack (healthy)                   |   |
|   | - Port: 5000                                                                              |   |
|   +-------------------------------------------------------------------------------------------+   |
|                                                                                                   |
+---------------------------------------------------------------------------------------------------+
```

---

### 5.2 `docker-compose.yml` Specification

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:17-alpine
    container_name: tradebook_postgres
    environment:
      POSTGRES_DB: tradebook_dev
      POSTGRES_USER: tradebook_app
      POSTGRES_PASSWORD: dev_password_123
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      # 01-init-extensions.sql: installs uuid-ossp, pgcrypto, btree_gist, timescaledb extensions.
      # BioGem schema migrations (contracts, physical_deliveries, goo_certificate_transactions,
      # market_prices hypertable, companies, counterparties, trading_points, outbox_events) are
      # applied separately via Fluent Migrator at application startup — NOT in this init script.
      # Migration files follow the naming convention:
      #   V{number}__{description}.sql  (e.g. V001__create_companies.sql, V002__create_contracts.sql)
      - ./infra/postgres/init-extensions.sql:/docker-entrypoint-initdb.d/01-init-extensions.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tradebook_app -d tradebook_dev"]
      interval: 5s
      timeout: 5s
      retries: 5
      start_period: 10s
    networks:
      - tradebook_net

  redis:
    image: redis:7-alpine
    container_name: tradebook_redis
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5
      start_period: 5s
    networks:
      - tradebook_net

  localstack:
    image: localstack/localstack:latest
    container_name: tradebook_localstack
    ports:
      - "4566:4566"
    environment:
      - SERVICES=s3,sqs,kms
      - AWS_DEFAULT_REGION=us-east-1
      - DOCKER_HOST=unix:///var/run/docker.sock
    volumes:
      - localstack_data:/var/lib/localstack
      - "/var/run/docker.sock:/var/run/docker.sock"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:4566/_localstack/health"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    networks:
      - tradebook_net

  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: tradebook_api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=tradebook_dev;Username=tradebook_app;Password=dev_password_123
      - Redis__ConnectionString=redis:6379
      - AWS__ServiceURL=http://localstack:4566
      - AWS__Region=us-east-1
      # Salesforce integration: Tradebook pushes contract updates, invoice status, physical
      # delivery confirmations, and GoO transaction results to Salesforce via HTTPS.
      # The API container requires outbound HTTPS egress (port 443) to api.salesforce.com.
      # In production (ECS Fargate / EKS), ensure the ECS task security group egress rule
      # permits TCP 443 to 0.0.0.0/0 or to the Salesforce IP range specifically.
      # Locally (Docker Compose), outbound HTTPS traffic routes through the host network by default.
      - Salesforce__BaseUrl=https://api.salesforce.com
      - Salesforce__AuthUrl=https://login.salesforce.com/services/oauth2/token
    ports:
      - "5000:5000"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      localstack:
        condition: service_healthy
    networks:
      - tradebook_net

volumes:
  postgres_data:
  localstack_data:

networks:
  tradebook_net:
    driver: bridge
```

---

### 5.3 PostgreSQL Extensions Initialization (`infra/postgres/init-extensions.sql`)

Script installs PostgreSQL extensions required by BioGem domain schema. Runs **once** at container first-boot via Docker's `docker-entrypoint-initdb.d` mechanism.

> **Important**: File installs **extensions only**. Full BioGem domain schema — `contracts`, `physical_deliveries`, `capacity_bookings`, `goo_certificate_transactions`, `market_prices` (TimescaleDB hypertable), `companies`, `counterparties`, `trading_points`, `invoice_line_items`, `outbox_events`, all `*_enum` types — applied by Fluent Migrator at app startup. Migration files follow convention `V{NNN}__{description}.sql` (e.g. `V001__create_enum_types.sql`, `V002__create_companies.sql`, `V003__create_contracts.sql`, `V010__create_market_prices_hypertable.sql`).

See [`architecture/entity-model.md`](../architecture/entity-model.md) for complete authoritative entity definitions.

```sql
-- PostgreSQL 17 Init Script for Tradebook Local & Agent Environment
-- Installs extensions required by the BioGem domain schema.
-- BioGem domain: renewable energy certificate and physical biomethane gas trading.
--
-- Extensions:
--   uuid-ossp    : gen_random_uuid() for all UUID PKs (companies, contracts, physical_deliveries, etc.)
--   pgcrypto     : cryptographic functions for secure credential hashing
--   btree_gist   : required for bi-temporal exclusion constraints (valid_time TSTZRANGE)
--   timescaledb  : market_prices hypertable (30-day chunks for TTF/EGSI ETF/BGO/FX rate series)

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- TimescaleDB must be loaded via shared_preload_libraries in postgresql.conf.
-- The CREATE EXTENSION call below activates it once the library is preloaded.
CREATE EXTENSION IF NOT EXISTS "timescaledb" CASCADE;

-- Verify extension instantiation
DO $$
BEGIN
    RAISE NOTICE 'Tradebook BioGem extensions (uuid-ossp, pgcrypto, btree_gist, timescaledb) initialized successfully.';
    RAISE NOTICE 'BioGem domain schema migrations will be applied by Fluent Migrator at application startup.';
END $$;
```

---

### 5.4 Salesforce HTTPS Egress Requirements for the API Container

Tradebook integrates with Salesforce CRM bidirectionally via NATS JetStream outbox events (see Task 03 for NATS subject details). **API container** acts as Salesforce HTTP client — must reach Salesforce REST/OAuth endpoints over HTTPS (port 443).

#### Local Docker Compose
In `docker-compose.yml`, `api` service uses bridge network (`tradebook_net`). Docker's default bridge network routes outbound internet traffic through host. No special config needed locally — `api.salesforce.com` and `login.salesforce.com` reachable by default.

#### Production: AWS ECS Fargate / Tier 2 Growth Architecture
In ECS Fargate deployment (Tier 2), `tradebook-api` container runs in **private application subnets**, no public IP (`assign_public_ip = false`). Outbound traffic routes through **NAT Gateway**. Ensure ECS task security group permits:

```hcl
# In compute_ecs/main.tf — ecs_tasks_sg egress rule for Salesforce HTTPS
resource "aws_security_group_rule" "ecs_egress_salesforce_https" {
  type              = "egress"
  from_port         = 443
  to_port           = 443
  protocol          = "tcp"
  cidr_blocks       = ["0.0.0.0/0"]   # Or restrict to Salesforce IP ranges if using IP allowlisting
  security_group_id = aws_security_group.ecs_tasks_sg.id
  description       = "Allow outbound HTTPS to api.salesforce.com for two-way CRM sync"
}
```

**Salesforce Endpoints Requiring Egress**:
| Endpoint | Purpose |
|---|---|
| `https://login.salesforce.com/services/oauth2/token` | OAuth 2.0 JWT Bearer token acquisition |
| `https://api.salesforce.com/services/data/v60.0/` | REST API for Contract__c, Account, Certificate_Transaction__c reads |
| `https://{instance}.salesforce.com` | Instance-specific API calls after OAuth |

**Secrets**: Salesforce client credentials (`client_id`, `client_secret`, `private_key_pem`) MUST inject via AWS Secrets Manager — never hardcode in env vars or Terraform code.

---

### 5.5 VS Code Devcontainer Definition (`.devcontainer/devcontainer.json`)

```json
{
  "name": "Tradebook Agent & Developer Environment",
  "dockerComposeFile": "../docker-compose.yml",
  "service": "api",
  "workspaceFolder": "/workspace",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csdevkit",
        "hashicorp.terraform",
        "ms-azuretools.vscode-docker",
        "golang.Go",
        "esbenp.prettier-vscode"
      ]
    }
  },
  "postCreateCommand": "dotnet restore && terraform fmt -check -recursive",
  "remoteUser": "vscode"
}
```

---

## 6. Key Deliverables & File Layout

```text
c:\Users\LaxmananKrishnapilla\tradebook\
├── docker-compose.yml
├── .devcontainer/
│   └── devcontainer.json
├── infra/
│   ├── postgres/
│   │   └── init-extensions.sql
│   └── terraform/
│       ├── tier1_lean/
│       │   └── main.tf
│       ├── tier2_growth/
│       │   └── main.tf
│       ├── tier3_scale/
│       │   └── main.tf
│       └── modules/
│           ├── networking/
│           │   ├── main.tf
│           │   ├── variables.tf
│           │   └── outputs.tf
│           ├── database_pg/
│           │   ├── main.tf
│           │   ├── variables.tf
│           │   └── outputs.tf
│           ├── compute_ecs/
│           │   ├── main.tf
│           │   ├── variables.tf
│           │   └── outputs.tf
│           ├── caching/
│           │   ├── main.tf
│           │   ├── variables.tf
│           │   └── outputs.tf
│           └── iam/
│               ├── main.tf
│               ├── variables.tf
│               └── outputs.tf
```

---

## 7. Production HCL Terraform Code Skeletons

### 7.1 Networking Module (`infra/terraform/modules/networking/main.tf`)

```hcl
terraform {
  required_version = ">= 1.9.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }
}

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name        = "tradebook-vpc-${var.environment}"
    Environment = var.environment
    ManagedBy   = "Terraform"
  }
}

resource "aws_subnet" "public" {
  count                   = length(var.availability_zones)
  vpc_id                  = aws_vpc.main.id
  cidr_block              = cidrsubnet(var.vpc_cidr, 8, count.index + 1)
  availability_zone       = var.availability_zones[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name        = "tradebook-public-subnet-${var.availability_zones[count.index]}"
    Environment = var.environment
  }
}

resource "aws_subnet" "application" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + 1)
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name        = "tradebook-app-subnet-${var.availability_zones[count.index]}"
    Environment = var.environment
  }
}

resource "aws_subnet" "database" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 6, count.index + 16)
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name        = "tradebook-db-subnet-${var.availability_zones[count.index]}"
    Environment = var.environment
  }
}

resource "aws_internet_gateway" "igw" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name        = "tradebook-igw-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_eip" "nat" {
  count  = length(var.availability_zones)
  domain = "vpc"

  tags = {
    Name        = "tradebook-nat-eip-${var.availability_zones[count.index]}"
    Environment = var.environment
  }
}

resource "aws_nat_gateway" "nat" {
  count         = length(var.availability_zones)
  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id

  tags = {
    Name        = "tradebook-nat-${var.availability_zones[count.index]}"
    Environment = var.environment
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.igw.id
  }

  tags = { Name = "tradebook-public-rt-${var.environment}" }
}

resource "aws_route_table" "application" {
  count  = length(var.availability_zones)
  vpc_id = aws_vpc.main.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.nat[count.index].id
  }

  tags = { Name = "tradebook-app-rt-${var.availability_zones[count.index]}" }
}

resource "aws_route_table_association" "public" {
  count          = length(var.availability_zones)
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "application" {
  count          = length(var.availability_zones)
  subnet_id      = aws_subnet.application[count.index].id
  route_table_id = aws_route_table.application[count.index].id
}

resource "aws_vpc_endpoint" "s3" {
  vpc_id          = aws_vpc.main.id
  service_name    = "com.amazonaws.${var.aws_region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids = aws_route_table.application[*].id

  tags = { Name = "tradebook-s3-endpoint-${var.environment}" }
}
```

#### Networking Variables (`infra/terraform/modules/networking/variables.tf`)

```hcl
variable "environment" {
  type        = string
  description = "Target environment name (e.g. dev, staging, prod)"
}

variable "aws_region" {
  type        = string
  description = "AWS region for deployment"
  default     = "us-east-1"
}

variable "vpc_cidr" {
  type        = string
  description = "VPC CIDR block"
  default     = "10.100.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "Must be a valid IPv4 CIDR block."
  }
}

variable "availability_zones" {
  type        = list(string)
  description = "List of Availability Zones"
  default     = ["us-east-1a", "us-east-1b", "us-east-1c"]
}
```

#### Networking Outputs (`infra/terraform/modules/networking/outputs.tf`)

```hcl
output "vpc_id" {
  value       = aws_vpc.main.id
  description = "The ID of the VPC"
}

output "public_subnet_ids" {
  value       = aws_subnet.public[*].id
  description = "List of public subnet IDs"
}

output "application_subnet_ids" {
  value       = aws_subnet.application[*].id
  description = "List of private application subnet IDs"
}

output "database_subnet_ids" {
  value       = aws_subnet.database[*].id
  description = "List of private database subnet IDs"
}
```

---

### 7.2 Database Module (`infra/terraform/modules/database_pg/main.tf`)

```hcl
terraform {
  required_version = ">= 1.9.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }
}

resource "aws_db_subnet_group" "aurora" {
  name       = "tradebook-aurora-subnet-group-${var.environment}"
  subnet_ids = var.database_subnet_ids

  tags = {
    Name        = "tradebook-aurora-subnet-group-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_security_group" "postgres" {
  name        = "tradebook-postgres-sg-${var.environment}"
  description = "Allow inbound PostgreSQL traffic from application subnets"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = var.application_subnet_cidrs
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "tradebook-postgres-sg-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_rds_cluster" "aurora" {
  cluster_identifier      = "tradebook-postgres-${var.environment}"
  engine                  = "aurora-postgresql"
  engine_version          = "17.0"
  database_name           = var.db_name
  master_username         = var.master_username
  master_password         = var.master_password
  db_subnet_group_name    = aws_db_subnet_group.aurora.name
  vpc_security_group_ids  = [aws_security_group.postgres.id]

  storage_encrypted   = true
  kms_key_id          = var.kms_key_arn
  deletion_protection = var.environment == "prod" ? true : false
  skip_final_snapshot = var.environment == "prod" ? false : true

  serverlessv2_scaling_configuration {
    min_capacity = var.min_acu
    max_capacity = var.max_acu
  }

  tags = {
    Name        = "tradebook-postgres-cluster-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_rds_cluster_instance" "instances" {
  count              = var.instance_count
  identifier         = "tradebook-postgres-${var.environment}-${count.index}"
  cluster_identifier = aws_rds_cluster.aurora.id
  instance_class     = "db.serverless"
  engine             = aws_rds_cluster.aurora.engine
  engine_version     = aws_rds_cluster.aurora.engine_version

  tags = {
    Name        = "tradebook-postgres-instance-${count.index}"
    Environment = var.environment
  }
}
```

---

### 7.3 Compute ECS Module (`infra/terraform/modules/compute_ecs/main.tf`)

```hcl
terraform {
  required_version = ">= 1.9.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }
}

resource "aws_ecs_cluster" "main" {
  name = "tradebook-cluster-${var.environment}"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = {
    Environment = var.environment
  }
}

resource "aws_lb" "alb" {
  name               = "tradebook-alb-${var.environment}"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb_sg.id]
  subnets            = var.public_subnet_ids

  tags = {
    Environment = var.environment
  }
}

resource "aws_security_group" "alb_sg" {
  name        = "tradebook-alb-sg-${var.environment}"
  description = "Allow inbound HTTP/HTTPS traffic"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_lb_target_group" "api" {
  name        = "tradebook-api-tg-${var.environment}"
  port        = 5000
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
    path                = "/healthz"
    healthy_threshold   = 3
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 15
    matcher             = "200"
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.alb.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

resource "aws_ecs_task_definition" "api" {
  family                   = "tradebook-api-${var.environment}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.task_cpu
  memory                   = var.task_memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name      = "tradebook-api"
      image     = var.container_image
      essential = true
      portMappings = [
        {
          containerPort = 5000
          hostPort      = 5000
        }
      ]
      environment = var.container_environment
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = "/ecs/tradebook-api-${var.environment}"
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "api"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "api" {
  name            = "tradebook-api-service-${var.environment}"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.application_subnet_ids
    security_groups  = [aws_security_group.ecs_tasks_sg.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "tradebook-api"
    container_port   = 5000
  }
}

resource "aws_security_group" "ecs_tasks_sg" {
  name        = "tradebook-ecs-tasks-sg-${var.environment}"
  description = "Allow inbound traffic from ALB only"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 5000
    to_port         = 5000
    protocol        = "tcp"
    security_groups = [aws_security_group.alb_sg.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}
```

---

### 7.4 IAM Module (`infra/terraform/modules/iam/main.tf`)

```hcl
terraform {
  required_version = ">= 1.9.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }
}

resource "aws_iam_role" "ecs_execution_role" {
  name = "tradebook-ecs-execution-role-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "ecs_execution_standard" {
  role       = aws_iam_role.ecs_execution_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role" "ecs_task_role" {
  name = "tradebook-ecs-task-role-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      }
    ]
  })
}

output "execution_role_arn" {
  value = aws_iam_role.ecs_execution_role.arn
}

output "task_role_arn" {
  value = aws_iam_role.ecs_task_role.arn
}
```

---

## 8. Subagent Implementation Step-by-Step Workflow

```
+---------------------------------------------------------------------------------------------------------+
|                                    SUBAGENT IMPLEMENTATION WORKFLOW                                     |
+---------------------------------------------------------------------------------------------------------+
|                                                                                                         |
|  STEP 1: Local Docker Compose Verification                                                              |
|  - Boot container stack: `docker compose up --build -d`                                                 |
|  - Assert all 4 containers (postgres, redis, localstack, api) pass healthchecks (`docker compose ps`).   |
|  - Execute extension verification on Postgres: `docker exec -it tradebook_postgres psql -U tradebook_app|
|    -d tradebook_dev -c "\dx"` confirming uuid-ossp, pgcrypto, btree_gist.                                |
|                                                                                                         |
|  STEP 2: Terraform Module Authoring                                                                     |
|  - Create module directories under `infra/terraform/modules/` for networking, database_pg, compute_ecs,|
|    caching, and iam.                                                                                    |
|  - Populate `main.tf`, `variables.tf`, and `outputs.tf` for each module with strict variable validation |
|    blocks.                                                                                              |
|                                                                                                         |
|  STEP 3: Composition Root Authoring                                                                     |
|  - Compose Tier 1, Tier 2, and Tier 3 environment files (`infra/terraform/tier1_lean/main.tf`,          |
|    `tier2_growth/main.tf`, `tier3_scale/main.tf`).                                                      |
|                                                                                                         |
|  STEP 4: Static Validation & Format Verification                                                        |
|  - Run `terraform fmt -check -recursive infra/terraform`                                                |
|  - Run `terraform validate` across tier directories                                                      |
|  - Run `tflint --recursive` to ensure zero static infrastructure errors.                                 |
|                                                                                                         |
+---------------------------------------------------------------------------------------------------------+
```

---

## 9. Independent Verification & Acceptance Workflow

### 9.1 Verification Command Matrix

Subagents and independent auditors must execute following commands verbatim to confirm compliance:

```bash
# 1. Format Check for Terraform Code
terraform fmt -check -recursive infra/terraform

# 2. Syntax & Dependency Validation (Tier 2 Growth Example)
cd infra/terraform/tier2_growth
terraform init -backend=false
terraform validate

# 3. Static Security & Best Practice Analysis
tflint --init
tflint --recursive

# 4. Local Container Stack Bootstrap & Verification
docker compose up --build -d
docker compose ps

# 5. Database Healthcheck & Extension Assertion
docker exec tradebook_postgres pg_isready -U tradebook_app -d tradebook_dev
docker exec tradebook_postgres psql -U tradebook_app -d tradebook_dev -c "SELECT extname FROM pg_extension;"

# 6. Redis Ping Test
docker exec tradebook_redis redis-cli ping

# 7. LocalStack Healthcheck
curl -s http://localhost:4566/_localstack/health
```

### 9.2 Quantitative Acceptance Criteria

1. **Zero Terraform Validation Errors**: `terraform validate` returns `Success! The configuration is valid.` across all 3 tiers.
2. **Zero Formatting Drift**: `terraform fmt -check -recursive` exits status code 0.
3. **Container Boot Time**: all `docker-compose.yml` services reach `healthy` status within **30 seconds**.
4. **Extension Integrity**: PostgreSQL 17 catalog confirms `uuid-ossp`, `pgcrypto`, `btree_gist` extensions exist in `public` schema.

---

## 10. Anti-Cheating & Integrity Guardrails

To preserve system integrity, following practices strictly prohibited:

- ❌ **No Hardcoded Credentials or Secrets**: passwords/KMS keys must inject via env vars, Terraform variables (`sensitive = true`), or AWS Secrets Manager.
- ❌ **No Dummy Healthcheck Wrappers**: Docker healthcheck commands must test true port connectivity (`pg_isready`, `redis-cli ping`), not shell `exit 0` stubs.
- ❌ **No Monolithic Single-File Terraform Specs**: IaC must adhere strictly to specified modular directory layout (`modules/networking`, `database_pg`, `compute_ecs`, `caching`, `iam`).
- ❌ **No Wildcards in Production IAM Policies**: IAM policy statements must define explicit ARNs and action scopes.

---
*End of Task 07 Specification.*
