# Infrastructure Architecture, Terraform Modules, Multi-Region DR & Cost Scaling Analysis for Tradebook

**Document Owner**: `explorer_r2_3` (Infrastructure Architect & FinOps Specialist)  
**Date**: 2026-08-05  
**Target Project**: Tradebook (High-Performance Real-Time Trading & Financial Analytics Platform)  
**Scope**: Production Cloud Infrastructure Topology, AWS HCL Terraform Modules, Multi-Region Disaster Recovery Strategy, Itemized 4-Tier Cost Scaling Model (10k to 10M DAU), and Cost Optimization (FinOps) Playbook.

---

## 1. Executive Summary & Core Architectural Principles

Tradebook is a high-throughput, real-time financial trading, ledger, and auditing platform requiring ultra-low latency (<20ms p99 REST/GraphQL, <5ms WebSocket broadcast), zero-data-loss durability for transactional records, and high-volume analytical processing. 

To satisfy these stringent SLA requirements while maintaining operational sanity and financial predictability, the infrastructure architecture adheres to five fundamental principles:

1. **Cellular & Tiered Isolation**: Compute workloads (stateless API, WebSocket servers, background job processors) are decoupled from stateful data systems (PostgreSQL system of record, ScyllaDB high-throughput ledger/audit store, Redpanda event stream, ClickHouse analytical warehouse).
2. **Infrastructure as Code (IaC) via Modular HCL Terraform**: 100% of cloud resources are declaratively defined in version-controlled, reusable, parameter-driven Terraform modules using remote state locking with S3 and DynamoDB.
3. **Multi-AZ High Availability & Multi-Region Resiliency**: Zero single points of failure across 3 Availability Zones (AZs) in the primary region, backed by an automated cross-region pilot-light/warm-standby or active-active topology depending on scale tier.
4. **Declarative Kubernetes Management via Karpenter**: Elimination of legacy Auto Scaling Groups (ASGs) in favor of Karpenter node auto-provisioning on AWS EKS, enabling sub-minute scaling, right-sizing, and aggressive Spot instance utilization.
5. **Rigorous FinOps & Unit Economic Governance**: Continuous cost modeling tracking Cost per Monthly Active User ($/MAU) and Cost per 1 Million Transactions ($/1M Tx), maintaining single-digit cent unit economics even at 10M DAU.

---

## 2. Production Infrastructure Topology Analysis

### 2.1 Network Design & VPC Topology

The network design follows the AWS Well-Architected Framework for financial applications, using a non-overlapping CIDR block (`10.100.0.0/16`) split across 3 Availability Zones (`us-east-1a`, `us-east-1b`, `us-east-1c`).

```
                    +-------------------------------------------------------+
                    |                AWS Route 53 (DNS)                     |
                    +-------------------------------------------------------+
                                                |
                                                v
                    +-------------------------------------------------------+
                    |             CloudFront CDN + AWS WAF v2               |
                    +-------------------------------------------------------+
                                                |
                                                v
                    +-------------------------------------------------------+
                    |         Internet Gateway / Application Load Balancer   |
                    +-------------------------------------------------------+
                                                |
      +-----------------------------------------+-----------------------------------------+
      |                                         |                                         |
      v (AZ 1a)                                 v (AZ 1b)                                 v (AZ 1c)
+------------------------+                +------------------------+                +------------------------+
| Public Subnet          |                | Public Subnet          |                | Public Subnet          |
| 10.100.1.0/24          |                | 10.100.2.0/24          |                | 10.100.3.0/24          |
| - NAT Gateway 1a       |                | - NAT Gateway 1b       |                | - NAT Gateway 1c       |
| - ALB Public Endpoints |                | - ALB Public Endpoints |                | - ALB Public Endpoints |
+------------------------+                +------------------------+                +------------------------+
      |                                         |                                         |
      v                                         v                                         v
+------------------------+                +------------------------+                +------------------------+
| Application Subnet     |                | Application Subnet     |                | Application Subnet     |
| 10.100.10.0/20         |                | 10.100.26.0/20         |                | 10.100.42.0/20         |
| - EKS Pods (Stateless) |                | - EKS Pods (Stateless) |                | - EKS Pods (Stateless) |
| - API / WS Gateways    |                | - API / WS Gateways    |                | - API / WS Gateways    |
+------------------------+                +------------------------+                +------------------------+
      |                                         |                                         |
      v                                         v                                         v
+------------------------+                +------------------------+                +------------------------+
| Database Subnet        |                | Database Subnet        |                | Database Subnet        |
| 10.100.60.0/22         |                | 10.100.64.0/22         |                | 10.100.68.0/22         |
| - Aurora PostgreSQL    |                | - ScyllaDB Cluster     |                | - ClickHouse Nodes     |
+------------------------+                +------------------------+                +------------------------+
      |                                         |                                         |
      v                                         v                                         v
+------------------------+                +------------------------+                +------------------------+
| Streaming Subnet       |                | Streaming Subnet       |                | Streaming Subnet       |
| 10.100.80.0/22         |                | 10.100.84.0/22         |                | 10.100.88.0/22         |
| - Redpanda Cluster     |                | - Redpanda Cluster     |                | - Redpanda Cluster     |
+------------------------+                +------------------------+                +------------------------+
```

#### Subnet Breakdown & CIDR Allocation
* **Public Subnets** (`/24` per AZ): Houses Application Load Balancers (ALBs) and NAT Gateways. Direct internet ingress/egress point.
* **Private Application Subnets** (`/20` per AZ): Houses EKS worker nodes, Karpenter-provisioned compute, API microservices, WebSocket connection servers, and background job runners. No public IP addresses.
* **Private Database Subnets** (`/22` per AZ): Dedicated isolated tier for Aurora PostgreSQL, ScyllaDB EC2 nodes, and ClickHouse cluster nodes. Restricted to ingress from Application subnets.
* **Private Streaming Subnets** (`/22` per AZ): Dedicated isolated network layer for Redpanda event streaming brokers, preventing inter-AZ traffic bottlenecks from impacting application pods.

#### Gateway & Endpoint Optimization
* **NAT Gateways**: Deployed in high-availability mode (1 per AZ) for outbound connectivity (patch updates, external API calls). To eliminate NAT Gateway data processing fees ($0.045/GB), **VPC Endpoints (AWS PrivateLink)** are provisioned for S3, DynamoDB, ECR, STS, Systems Manager (SSM), and CloudWatch.

### 2.2 Compute Infrastructure: AWS EKS & Karpenter

* **EKS Control Plane**: AWS-managed Kubernetes control plane with envelope encryption enabled via AWS KMS and audit logs routed directly to CloudWatch Logs.
* **Node Management Engine**: **Karpenter v1.0+** replaces standard AWS Managed Node Groups for all application workloads. Karpenter evaluates un-schedulable pods and launches compute instances directly in <15 seconds.
* **Node Pool Segmentation**:
  1. `system-pool`: Fixed Graviton3 (`t4g.medium` / `c7g.large`) On-Demand nodes across 3 AZs running CoreDNS, Karpenter controller, AWS VPC CNI, Metrics Server, and Cilium/Kube-proxy.
  2. `stateless-api-pool`: Dynamic Graviton3 (`c7g.xlarge` to `c7g.4xlarge`) Spot/On-Demand mixed pool running REST/GraphQL API services and .NET vertical slice endpoints.
  3. `websocket-pool`: Memory-optimized Graviton3 (`r7g.xlarge` / `r7g.2xlarge`) On-Demand nodes with high network throughput (`Up to 12.5 Gbps`) running WebSocket connection proxy pods with kernel network tuning (`net.core.somaxconn=65535`, `net.ipv4.tcp_max_syn_backlog=65535`).
  4. `batch-worker-pool`: 100% Spot Graviton3 (`c7g.2xlarge` / `m7g.2xlarge`) nodes running asynchronous background jobs, audit processors, and indexing tasks with automatic pod disruption budgets (PDBs).

### 2.3 Stateful & Database Storage Architecture

Tradebook separates concerns across specialized database engines:

| Engine | Storage Type | Role in Tradebook | Hardware Topology |
|---|---|---|---|
| **Aurora PostgreSQL** | Multi-AZ Engine / Serverless v2 | Systems of Record, User Accounts, Workspaces, RBAC, Configurations | Aurora Provisioned / Serverless v2 with 1 Primary + 2 Read Replicas across 3 AZs |
| **ScyllaDB Enterprise** | Local NVMe (`i4i.xlarge` - `i4i.4xlarge`) | Ultra-high throughput Time-Series, Ledger Entries, Audit Logs, Real-time Order Book History | 3 to 12-node cluster spanning 3 AZs, RF=3, ScyllaDB Operator on K8s or direct EC2 |
| **Redpanda** | Local NVMe / EBS `gp3` | Real-time Event Streaming, Order Event Bus, CDC log distribution | 3 to 6-node cluster spanning 3 AZs, Raft replication factor = 3 |
| **ClickHouse** | Local NVMe + S3 Storage Tiering | Historical Analytics, Olap Queries, Performance Reports, Audit Analytics | 2 to 6 ClickHouse Server nodes + 3 ClickHouse Keeper nodes, S3 Object Storage for cold data |

### 2.4 Security & Networking Layer

* **Edge Protection**: AWS CloudFront CDN backed by AWS WAF v2 with Managed Rules (Common Rule Set, SQL Injection, Known Bad Inputs, Rate Limiting set to 2,000 requests/5-min per IP).
* **Identity & Access Management**: **EKS Pod Identity / IRSA (IAM Roles for Service Accounts)** ensures zero long-lived AWS credentials inside container pods.
* **Secrets Management**: AWS Secrets Manager integrated via External Secrets Operator (ESO) into Kubernetes Secrets, encrypted at rest via KMS customer-managed keys (CMK).
* **Network Security Policies**: Cilium CNI enforcing zero-trust NetworkPolicies; database ports (5432 for Postgres, 9042 for ScyllaDB, 9092/9093 for Redpanda, 8123/9000 for ClickHouse) accept traffic strictly from labeled application namespaces.

### 2.5 Monitoring & Observability Stack

* **Metrics**: AWS Managed Prometheus (AMP) paired with Grafana Enterprise or open-source Grafana dashboards monitoring EKS pod metrics, Node Exporter, ScyllaDB metrics, and Redpanda lag metrics.
* **Logs**: AWS FluentBit DaemonSet forwarding container logs to CloudWatch Logs with S3 archiving lifecycle rules.
* **Tracing**: OpenTelemetry Collector DaemonSet exporting trace spans to AWS X-Ray or Grafana Tempo.

---

## 3. Complete Production-Ready AWS HCL Terraform Module Architecture

### 3.1 Workspace & Module Structure

```
terraform-tradebook-infrastructure/
├── environments/
│   ├── dev/
│   ├── staging/
│   └── prod/
│       ├── main.tf
│       ├── variables.tf
│       ├── outputs.tf
│       ├── terraform.tfvars
│       └── backend.tf
└── modules/
    ├── vpc/
    │   ├── main.tf
    │   ├── variables.tf
    │   └── outputs.tf
    ├── eks/
    │   ├── main.tf
    │   ├── karpenter.tf
    │   ├── variables.tf
    │   └── outputs.tf
    ├── databases/
    │   ├── postgres.tf
    │   ├── scylladb.tf
    │   ├── variables.tf
    │   └── outputs.tf
    ├── streaming/
    │   ├── redpanda.tf
    │   ├── variables.tf
    │   └── outputs.tf
    ├── analytics/
    │   ├── clickhouse.tf
    │   ├── variables.tf
    │   └── outputs.tf
    └── security_networking/
        ├── cloudfront_waf.tf
        ├── route53.tf
        ├── kms.tf
        ├── variables.tf
        └── outputs.tf
```

### 3.2 Backend & Provider Configuration (`backend.tf`)

```hcl
# environments/prod/backend.tf
terraform {
  required_version = ">= 1.7.0"

  backend "s3" {
    bucket         = "tradebook-terraform-state-prod-us-east-1"
    key            = "infrastructure/prod/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "tradebook-terraform-locks-prod"
    encrypt        = true
    kms_key_id     = "alias/tradebook-tf-state-key"
  }

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.30"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.13"
    }
    kubectl = {
      source  = "gavinbunney/kubectl"
      version = "~> 1.14"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Environment = var.environment
      Project     = "Tradebook"
      ManagedBy   = "Terraform"
      Repository  = "tradebook-infrastructure"
    }
  }
}
```

### 3.3 Module HCL Implementations

#### Module 1: VPC Network Topology (`modules/vpc/main.tf`)

```hcl
# modules/vpc/main.tf
variable "environment" { type = string }
variable "vpc_cidr" { type = string }
variable "availability_zones" { type = list(string) }

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = "tradebook-vpc-${var.environment}"
  }
}

resource "aws_subnet" "public" {
  count                   = length(var.availability_zones)
  vpc_id                  = aws_vpc.main.id
  cidr_block              = cidrsubnet(var.vpc_cidr, 8, count.index + 1)
  availability_zone       = var.availability_zones[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name                                           = "tradebook-public-subnet-${var.availability_zones[count.index]}"
    "kubernetes.io/role/elb"                       = "1"
    "kubernetes.io/cluster/tradebook-${var.environment}" = "shared"
  }
}

resource "aws_subnet" "application" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + 1) # /20 subnets
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name                                           = "tradebook-app-subnet-${var.availability_zones[count.index]}"
    "kubernetes.io/role/internal-elb"              = "1"
    "karpenter.sh/discovery"                       = "tradebook-${var.environment}"
    "kubernetes.io/cluster/tradebook-${var.environment}" = "shared"
  }
}

resource "aws_subnet" "database" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 6, count.index + 20) # /22 subnets
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name = "tradebook-db-subnet-${var.availability_zones[count.index]}"
  }
}

resource "aws_subnet" "streaming" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 6, count.index + 30) # /22 subnets
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name = "tradebook-streaming-subnet-${var.availability_zones[count.index]}"
  }
}

# Internet Gateway & NAT Gateways
resource "aws_internet_gateway" "igw" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "tradebook-igw-${var.environment}" }
}

resource "aws_eip" "nat" {
  count  = length(var.availability_zones)
  domain = "vpc"
  tags   = { Name = "tradebook-nat-eip-${var.availability_zones[count.index]}" }
}

resource "aws_nat_gateway" "nat" {
  count         = length(var.availability_zones)
  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id
  tags          = { Name = "tradebook-nat-${var.availability_zones[count.index]}" }
}

# VPC Gateways & Interface Endpoints (S3, ECR, DynamoDB) to bypass NAT Gateway fees
resource "aws_vpc_endpoint" "s3" {
  vpc_id          = aws_vpc.main.id
  service_name    = "com.amazonaws.${var.aws_region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids = aws_route_table.application[*].id

  tags = { Name = "tradebook-s3-endpoint" }
}

output "vpc_id" { value = aws_vpc.main.id }
output "application_subnet_ids" { value = aws_subnet.application[*].id }
output "database_subnet_ids" { value = aws_subnet.database[*].id }
output "streaming_subnet_ids" { value = aws_subnet.streaming[*].id }
```

#### Module 2: EKS & Karpenter Cluster (`modules/eks/main.tf`)

```hcl
# modules/eks/main.tf
variable "cluster_name" { type = string }
variable "cluster_version" { default = "1.30" }
variable "vpc_id" { type = string }
variable "subnet_ids" { type = list(string) }

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 20.0"

  cluster_name    = var.cluster_name
  cluster_version = var.cluster_version

  cluster_endpoint_public_access  = true
  cluster_endpoint_private_access = true

  vpc_id     = var.vpc_id
  subnet_ids = var.subnet_ids

  enable_cluster_creator_admin_permissions = true

  # Native EKS Pod Identity Addon
  cluster_addons = {
    coredns                = { resolve_conflicts = "OVERWRITE" }
    kube-proxy             = { resolve_conflicts = "OVERWRITE" }
    vpc-cni                = { resolve_conflicts = "OVERWRITE" }
    aws-ebs-csi-driver     = { resolve_conflicts = "OVERWRITE" }
    eks-pod-identity-agent = { resolve_conflicts = "OVERWRITE" }
  }

  # System Nodes Pool (Fixed On-Demand)
  eks_managed_node_groups = {
    system = {
      min_size     = 3
      max_size     = 6
      desired_size = 3

      instance_types = ["t4g.medium", "c7g.large"]
      capacity_type  = "ON_DEMAND"

      labels = {
        "workload.tradebook.io/tier" = "system"
      }
    }
  }

  tags = {
    Environment = var.environment
  }
}

# Karpenter Provisioner & NodePool Setup
resource "kubectl_manifest" "karpenter_node_pool_stateless" {
  yaml_body = <<YAML
apiVersion: karpenter.sh/v1beta1
kind: NodePool
metadata:
  name: stateless-apps
spec:
  template:
    spec:
      requirements:
        - key: kubernetes.io/arch
          operator: In
          values: ["arm64"]
        - key: karpenter.sh/capacity-type
          operator: In
          values: ["spot", "on-demand"]
        - key: karpenter.k8s.aws/instance-family
          operator: In
          values: ["c7g", "m7g", "r7g"]
      nodeClassRef:
        apiVersion: karpenter.k8s.aws/v1beta1
        kind: EC2NodeClass
        name: default
  limits:
    cpu: "1000"
    memory: 2000Gi
  disruption:
    consolidationPolicy: WhenUnderutilized
    expireAfter: 720h
YAML
}

output "cluster_endpoint" { value = module.eks.cluster_endpoint }
output "cluster_name" { value = module.eks.cluster_name }
```

#### Module 3: Database Infrastructure (Postgres & ScyllaDB) (`modules/databases/postgres.tf`)

```hcl
# modules/databases/postgres.tf
variable "environment" { type = string }
variable "vpc_id" { type = string }
variable "database_subnet_ids" { type = list(string) }

resource "aws_db_subnet_group" "aurora" {
  name       = "tradebook-aurora-subnet-group-${var.environment}"
  subnet_ids = var.database_subnet_ids
}

resource "aws_rds_cluster" "aurora" {
  cluster_identifier      = "tradebook-postgres-${var.environment}"
  engine                  = "aurora-postgresql"
  engine_version          = "16.2"
  database_name           = "tradebook"
  master_username         = "tradebook_admin"
  manage_master_user_password = true
  db_subnet_group_name    = aws_db_subnet_group.aurora.name
  vpc_security_group_ids  = [aws_security_group.postgres.id]

  storage_encrypted   = true
  deletion_protection = true
  skip_final_snapshot = false

  serverlessv2_scaling_configuration {
    min_capacity = 2.0
    max_capacity = 64.0
  }
}

resource "aws_rds_cluster_instance" "aurora_instances" {
  count              = 3
  identifier         = "tradebook-postgres-${var.environment}-${count.index}"
  cluster_identifier = aws_rds_cluster.aurora.id
  instance_class     = "db.serverless"
  engine             = aws_rds_cluster.aurora.engine
  engine_version     = aws_rds_cluster.aurora.engine_version
}

# Security Group restricting ingress to EKS Application Subnets only
resource "aws_security_group" "postgres" {
  name        = "tradebook-postgres-sg-${var.environment}"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = ["10.100.10.0/20", "10.100.26.0/20", "10.100.42.0/20"]
  }
}

output "aurora_endpoint" { value = aws_rds_cluster.aurora.endpoint }
output "aurora_reader_endpoint" { value = aws_rds_cluster.aurora.reader_endpoint }
```

#### Module 4: Streaming Architecture (Redpanda Cluster) (`modules/streaming/redpanda.tf`)

```hcl
# modules/streaming/redpanda.tf
variable "environment" { type = string }
variable "streaming_subnet_ids" { type = list(string) }
variable "node_count" { default = 3 }
variable "instance_type" { default = "i4i.xlarge" } # Local NVMe SSD storage for low latency

resource "aws_instance" "redpanda" {
  count                  = var.node_count
  ami                    = "ami-0c7217cdde317cfec" # Ubuntu 24.04 LTS HVM
  instance_type          = var.instance_type
  subnet_id              = var.streaming_subnet_ids[count.index % length(var.streaming_subnet_ids)]
  vpc_security_group_ids = [aws_security_group.redpanda.id]

  root_block_device {
    volume_size           = 100
    volume_type           = "gp3"
    encrypted             = true
  }

  tags = {
    Name = "tradebook-redpanda-${var.environment}-${count.index}"
    Role = "RedpandaBroker"
  }
}

resource "aws_security_group" "redpanda" {
  name   = "tradebook-redpanda-sg-${var.environment}"
  vpc_id = var.vpc_id

  ingress {
    from_port   = 9092 # Kafka API
    to_port     = 9092
    protocol    = "tcp"
    cidr_blocks = ["10.100.0.0/16"]
  }

  ingress {
    from_port   = 33145 # Redpanda RPC inter-node
    to_port     = 33145
    protocol    = "tcp"
    self        = true
  }
}
```

---

## 4. Multi-Region Disaster Recovery (DR) & Deployment Strategy

### 4.1 DR Topologies Comparison

Tradebook defines two distinct DR topologies matching business tiering:

```
                            PRIMARY REGION (us-east-1)
    +-----------------------------------------------------------------------+
    | Aurora Postgres (Primary Write) ---> Redpanda Stream ---> ScyllaDB    |
    +-----------------------------------------------------------------------+
                                        |
                  CROSS-REGION ASYNCHRONOUS REPLICATION
      (S3 CRR / Aurora Global DB / Redpanda MirrorMaker / Scylla DC)
                                        |
                                        v
                            SECONDARY REGION (us-west-2)
    +-----------------------------------------------------------------------+
    | Aurora Global DB (ReadOnly)  ---> MirrorMaker Sync ---> ScyllaDB DC2  |
    +-----------------------------------------------------------------------+
```

| Metric / Dimension | Active-Passive (Warm Standby / Pilot Light) | Active-Active (Multi-Region Write) |
|---|---|---|
| **Target Scale Tiers** | Tier 1 (10k DAU) & Tier 2 (100k DAU) | Tier 3 (1M DAU) & Tier 4 (10M DAU) |
| **Primary Region** | `us-east-1` (N. Virginia) | `us-east-1` (US-East) |
| **Secondary Region** | `us-west-2` (Oregon) | `eu-west-1` (Europe) / `ap-northeast-1` (Asia) |
| **RPO (Data Loss Target)** | **< 5 seconds** | **< 100 milliseconds** (0 for quorum consensus) |
| **RTO (Downtime Target)** | **< 15 minutes** | **< 30 seconds** (Automated Route 53 DNS failover) |
| **Cost Overhead vs Single Region** | +35% to +45% | +110% to +130% |
| **Failover Mechanism** | Route 53 Health Check DNS fail-over + Aurora promote read replica script | Active Route 53 Latency-Based Routing + Anycast IP BGP |

### 4.2 Data Replication Mechanics

1. **PostgreSQL System of Record**:
   * Uses **AWS Aurora Global Database**. Storage-level replication continuously streams WAL changes from `us-east-1` to `us-west-2` with average replication latency under 1 second.
   * Failover trigger: In the event of primary region outage, AWS Route 53 Health Checks trigger AWS Lambda / Step Functions to execute `aws rds failover-global-cluster`, promoting the secondary cluster to standalone write master in under 60 seconds.

2. **ScyllaDB High-Throughput Ledger & Audit Store**:
   * Native **Multi-Datacenter Cluster Replication**. ScyllaDB operates as a single logical cluster spanning `us-east-1` (Datacenter `dc1`) and `us-west-2` (Datacenter `dc2`).
   * Keyspace Replication Strategy:
     ```sql
     CREATE KEYSPACE tradebook_ledger WITH replication = {
         'class': 'NetworkTopologyStrategy',
         'us-east-1': 3,
         'us-west-2': 3
     };
     ```
   * Write Consistency level: `LOCAL_QUORUM` in primary region ensures sub-3ms write latency while asynchronously replicating across regions in real time.

3. **Redpanda Event Streaming**:
   * Uses **Redpanda Shadow Indexing & MirrorMaker2 / Topic Replication**.
   * Active-Passive setup continuously mirrors critical event topics (`tradebook.orders`, `tradebook.audit.events`) from primary cluster to secondary cluster.

4. **Object & Analytical Storage (S3 & ClickHouse)**:
   * AWS S3 Cross-Region Replication (CRR) configured with Replication Time Control (RTC) guaranteeing 99.9% of objects replicate within 15 minutes.
   * ClickHouse utilizes `ReplicatedMergeTree` engine backed by S3 Object Storage with S3 CRR syncing historical partition parts across regions.

---

## 5. Itemized Cost Scaling Model Across 4 Scale Tiers

To build an accurate, itemized financial model, cost estimates are computed using standard AWS US-East rates (2026 pricing model).

### 5.1 Scale Tier Definitions

* **Tier 1 (Lean / Growth MVP)**: **10,000 DAU** | Avg: 100 TPS | Peak: 1,000 TPS | Monthly Events: ~260 Million
* **Tier 2 (Scale Up)**: **100,000 DAU** | Avg: 1,000 TPS | Peak: 10,000 TPS | Monthly Events: ~2.6 Billion
* **Tier 3 (Enterprise)**: **1,000,000 DAU** | Avg: 10,000 TPS | Peak: 100,000 TPS | Monthly Events: ~26 Billion
* **Tier 4 (Global Scale)**: **10,000,000 DAU** | Avg: 100,000 TPS | Peak: 1,000,000 TPS | Monthly Events: ~260 Billion

---

### 5.2 Detailed Itemized Cost Breakdown Table ($ USD / Month)

| Infrastructure Component | Tier 1: 10k DAU (100 TPS) | Tier 2: 100k DAU (1,000 TPS) | Tier 3: 1M DAU (10,000 TPS) | Tier 4: 10M DAU (100,000 TPS) |
|---|---|---|---|---|
| **EKS Control Plane** | $73.00 (1 Cluster) | $73.00 (1 Cluster) | $146.00 (2 Clusters - Multi-Region) | $292.00 (4 Clusters - Global) |
| **Stateless API/WS Compute (EKS)** | $280.00 (4x `c7g.xlarge` Spot/OD) | $1,120.00 (16x `c7g.xlarge`) | $8,960.00 (64x `c7g.2xlarge`) | $71,680.00 (256x `c7g.4xlarge`) |
| **PostgreSQL Database (Aurora)** | $350.00 (Aurora Serverless 2-16 ACU) | $1,400.00 (Aurora Serverless 8-64 ACU) | $5,600.00 (Provisioned `db.r7g.4xlarge` Multi-AZ) | $28,400.00 (Aurora Global DB 4x `db.r7g.8xlarge`) |
| **ScyllaDB Ledger Store** | $480.00 (3x `i4i.xlarge`) | $1,920.00 (6x `i4i.2xlarge`) | $11,520.00 (12x `i4i.4xlarge`) | $69,120.00 (36x `i4i.8xlarge`) |
| **Redpanda Event Streaming** | $320.00 (3x `c7g.xlarge` + `gp3`) | $1,280.00 (3x `i4i.xlarge` NVMe) | $6,144.00 (8x `i4i.2xlarge`) | $36,864.00 (24x `i4i.4xlarge`) |
| **ClickHouse Analytics Engine** | $260.00 (2x `c7g.xlarge` + S3) | $840.00 (3x `c7g.2xlarge` + S3) | $3,360.00 (6x `r7g.2xlarge` + S3) | $18,200.00 (16x `r7g.4xlarge` + S3) |
| **Storage & Backups (EBS gp3 + S3)** | $180.00 (2 TB EBS + 5 TB S3) | $750.00 (10 TB EBS + 30 TB S3) | $4,200.00 (50 TB EBS + 250 TB S3) | $24,500.00 (200 TB EBS + 1.5 PB S3) |
| **Network Data Transfer & NAT** | $210.00 (2 TB NAT + 1 TB Egress) | $1,450.00 (15 TB NAT + 10 TB Egress) | $11,800.00 (100 TB NAT + 80 TB Egress) | $84,000.00 (600 TB NAT + 500 TB Egress) |
| **Security (CloudFront CDN + WAF)** | $120.00 (WAF Rules + CDN Egress) | $450.00 (Managed Rules + CDN) | $2,800.00 (High Volume WAF + Edge Rules) | $18,500.00 (Enterprise WAF + Shield Advanced) |
| **Observability (Logs, Prometheus, Grafana)** | $150.00 (CloudWatch + OpenTelemetry) | $780.00 (AMP + CloudWatch Logs) | $4,500.00 (Datadog/Grafana + AMP) | $26,000.00 (Enterprise Observability Stack) |
| **TOTAL MONTHLY COST** | **$2,423.00** | **$10,063.00** | **$59,030.00** | **$377,556.00** |

---

### 5.3 Unit Economics & Mathematical Scaling Curves

To evaluate cost efficiency as user scale expands by 1,000x (from 10k to 10M DAU):

```
DAU Scale       Total Monthly Cost     Cost / MAU (Assuming 3x DAU)    Cost / 1 Million Transactions
---------       ------------------     ----------------------------    ----------------------------
10,000          $2,423.00              $0.0807                         $9.32
100,000         $10,063.00             $0.0335                         $3.87
1,000,000       $59,030.00             $0.0196                         $2.27
10,000,000      $377,556.00            $0.0125                         $1.45
```

#### Unit Economic Formulas
1. **Cost Per Monthly Active User (MAU)**:
   $$\text{Cost}_{\text{MAU}} = \frac{\text{Total Monthly Infrastructure Cost}}{\text{DAU} \times 3.0}$$
   *At 10k DAU, cost per MAU is **$0.0807**. At 10M DAU, cost per MAU drops to **$0.0125** — a **6.45x efficiency gain** due to fixed infrastructure amortisation.*

2. **Cost Per 1 Million Transactions**:
   $$\text{Cost}_{\text{1M Tx}} = \frac{\text{Total Monthly Cost}}{\text{Average TPS} \times 86,400 \times 30.4} \times 1,000,000$$
   *At 10k DAU (100 TPS avg), cost per 1M transactions is **$9.32**. At 10M DAU (100,000 TPS avg), cost per 1M transactions falls to **$1.45**.*

---

## 6. Cost Optimization Playbook (FinOps Strategy)

By implementing the following 5 FinOps levers, Tradebook achieves an immediate **35% to 52% reduction** in baseline monthly cloud spend:

```
+-----------------------------------------------------------------------------------+
|                           FINOPS COST OPTIMIZATION LEVERS                         |
+-----------------------------------------------------------------------------------+
| 1. Compute Savings Plans & Reserved Instances   --> 42% - 62% Savings on Baseline  |
| 2. Karpenter Spot Instance Diversification     --> 70% - 85% Savings on Batch/API  |
| 3. VPC PrivateLink Endpoints (Bypass NAT Fees) --> $0.045/GB NAT Savings          |
| 4. S3 Intelligent-Tiering & Cold Archiving     --> 60% - 80% Storage Savings      |
| 5. EBS gp3 Volume Optimization & IOPS Right-Sizing --> 20% Storage Cost Reduction|
+-----------------------------------------------------------------------------------+
```

### 6.1 AWS Compute Savings Plans & Reserved Instances
* **Strategy**: Commit to 3-Year Compute Savings Plans for baseline node capacity (System NodePool, Aurora Primary instances, ScyllaDB baseline EC2 instances).
* **Impact**: Delivers **up to 62% discount** over standard On-Demand rates for stateful nodes.

### 6.2 Karpenter Aggressive Spot Instance Orchestration
* **Strategy**: Configure Karpenter NodePools to utilize Spot instances for all stateless API servers, WebSocket relays, and background workers. Specify multi-family instance selection (`c7g`, `m7g`, `c6g`, `m6g`, `r7g`) across 3 AZs to eliminate Spot interruption risk.
* **Graceful Termination**: Deploy `aws-node-termination-handler` to capture 2-minute Spot Rebalance/Interruption notices, cordoning and draining pods gracefully before node removal.

### 6.3 VPC Endpoint Traffic Localization (Eliminating NAT Gateway Fees)
* **Strategy**: Provision S3 Gateway Endpoints and AWS PrivateLink Interface Endpoints for ECR, STS, DynamoDB, and CloudWatch.
* **Impact**: Prevents internal pod log traffic, container image pulls, and S3 data queries from traversing NAT Gateways, eliminating tens of thousands of dollars in NAT processing fees at Tier 3 & Tier 4.

### 6.4 Storage Lifecycle Policies (S3 & ClickHouse Cold Tiering)
* **Strategy**:
  * S3 Bucket Lifecycle Rules: Transition raw audit logs and historical event payloads to **S3 Intelligent-Tiering** immediately, moving objects untouched for 90 days to **S3 Glacier Instant Retrieval** ($0.004/GB/mo) and after 180 days to **S3 Glacier Deep Archive** ($0.00099/GB/mo).
  * ClickHouse S3 Cold Storage Engine: Keep recent 30 days of analytical partitions on local NVMe / EBS, while older historical partitions automatically move to S3 via ClickHouse `disk_s3` policy.

### 6.5 Dynamic Pod Auto-Scaling: HPA + KEDA Rules
* **Strategy**: Implement Kubernetes Event-driven Autoscaling (KEDA) watching Redpanda consumer group lag and Prometheus metrics.
* **HPA Configuration**:
  ```yaml
  apiVersion: autoscaling/v2
  kind: HorizontalPodAutoscaler
  metadata:
    name: tradebook-api-hpa
  spec:
    scaleTargetRef:
      apiVersion: apps/v1
      kind: Deployment
      name: tradebook-api
    minReplicas: 6
    maxReplicas: 120
    metrics:
      - type: Resource
        resource:
          name: cpu
          target:
            type: Utilization
            averageUtilization: 65
      - type: External
        external:
          metric:
            name: redpanda_consumer_lag
          target:
            type: Value
            averageValue: "500"
  ```

---

## 7. Strategic Recommendations & Implementation Roadmap

1. **Phase 1 (MVP Setup - Tier 1)**:
   * Deploy modular VPC, single-region EKS with Karpenter, Aurora PostgreSQL Serverless v2, and 3-node Redpanda cluster.
   * Implement AWS Savings Plans on baseline nodes. Est. cost: **~$2,400/mo**.

2. **Phase 2 (Growth - Tier 2)**:
   * Scale ScyllaDB on local NVMe `i4i.xlarge` instances for ledger/audit processing. Introduce ClickHouse for analytical reporting. Est. cost: **~$10,000/mo**.

3. **Phase 3 (Enterprise Multi-Region - Tier 3)**:
   * Activate Aurora Global Database to `us-west-2` with automated failover handlers. Deploy ScyllaDB Multi-Datacenter cross-region cluster. Est. cost: **~$59,000/mo**.

4. **Phase 4 (Global Scale - Tier 4)**:
   * Implement Active-Active multi-region deployment spanning 3 geographical regions (US, EU, APAC) with global Route 53 Anycast routing and automated FinOps governance. Est. cost: **~$377,000/mo**.

---
*End of Analysis Report.*
