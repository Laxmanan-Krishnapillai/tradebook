> ⚠️ **Known Inconsistency**: this document's infrastructure stack (ScyllaDB, Redpanda, ClickHouse, AWS Aurora PostgreSQL 16) is an alternative/exploratory topology from an earlier research pass. It does NOT match current authoritative stack in master-architecture-blueprint.md/root README.md (PostgreSQL 17 + TimescaleDB, NATS JetStream — no ScyllaDB/ClickHouse/Redpanda). Treat cost figures and module code below as reference material for a possible future high-scale phase, not the current build target.

# Production Infrastructure Architecture, AWS HCL Terraform Modules, Multi-Region Disaster Recovery & FinOps Cost Analysis

**Document Title**: Tradebook Production Infrastructure Architecture & Financial Engineering Specification
**File Path**: `research/infrastructure-terraform-and-cost-analysis.md`
**Author / Owner**: Infrastructure Architecture & FinOps Engineering Group
**Date**: August 5, 2026
**Status**: Publication-Grade Architectural Specification

---

## 1. Executive Summary & Production Infrastructure Vision

Tradebook: enterprise-grade, high-throughput financial trading, transactional ledger, analytical platform. Delivers sub-ms execution, continuous auditability, real-time data sync. Requires ultra-low latency (<20ms p99 REST/GraphQL, <5ms WebSocket broadcast), zero-data-loss durability (RPO ~0 for ledger entries), high-volume analytical processing ingesting billions financial events/day.

Production infra built on five pillars:

1. **Cellular & Tiered Isolation**: decouples stateless app layers (API gateways, WS distribution nodes, background workers) from stateful data layers (PostgreSQL system of record, ScyllaDB high-throughput NVMe ledger, Redpanda event streaming bus, ClickHouse analytical warehouse).
2. **Declarative IaC**: 100% cloud resources (networking, compute, storage, security, databases) managed via modular AWS HCL Terraform, S3 state locking, DynamoDB concurrency lock, KMS envelope encryption.
3. **Multi-AZ HA & Multi-Region Resiliency**: zero SPOF across 3-AZ regional footprint + multi-region DR — Active-Passive Pilot Light (Tier 1/2), Active-Passive Warm Standby w/ Aurora Global DB & ScyllaDB Multi-DC (Tier 3/4).
4. **Declarative Cloud-Native Compute via Karpenter**: eliminates legacy ASGs for Karpenter v1.0+ node auto-provisioning on EKS — sub-15s node spin-up, Graviton3 (ARM64) right-sizing, aggressive Spot orchestration.
5. **Rigorous FinOps & Unit Economics**: tracks $/MAU and $/1M Tx — cost per MAU scales down **6.45x** ($0.0807 → $0.0125) from 10k to 10M DAU.

---

## 2. Production Network & Compute Topology

### 2.1 Multi-AZ VPC Network Architecture

Network deployed in dedicated VPC CIDR `10.100.0.0/16` across 3 AZs (`us-east-1a`, `us-east-1b`, `us-east-1c`). Strict multi-tier isolation: public ingress separated from app compute, DB storage, streaming buses.

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
       +------------------------------------------------------+------------------------------------------------------+
       |                                                      |                                                      |
       v (AZ 1a)                                              v (AZ 1b)                                              v (AZ 1c)
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
| Public Subnet                      |                 | Public Subnet                      |                 | Public Subnet                      |
| 10.100.1.0/24                      |                 | 10.100.2.0/24                      |                 | 10.100.3.0/24                      |
| - NAT Gateway 1a                   |                 | - NAT Gateway 1b                   |                 | - NAT Gateway 1c                   |
| - Public ALB Ingress               |                 | - Public ALB Ingress               |                 | - Public ALB Ingress               |
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
       |                                                      |                                                      |
       v                                                      v                                                      v
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
| Private Application Subnet         |                 | Private Application Subnet         |                 | Private Application Subnet         |
| 10.100.16.0/20                     |                 | 10.100.32.0/20                     |                 | 10.100.48.0/20                     |
| - EKS Worker Nodes (Graviton3 ARM) |                 | - EKS Worker Nodes (Graviton3 ARM) |                 | - EKS Worker Nodes (Graviton3 ARM) |
| - API / WS Proxy Pods              |                 | - API / WS Proxy Pods              |                 | - API / WS Proxy Pods              |
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
       |                                                      |                                                      |
       v                                                      v                                                      v
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
| Private Database Subnet            |                 | Private Database Subnet            |                 | Private Database Subnet            |
| 10.100.64.0/22                     |                 | 10.100.68.0/22                     |                 | 10.100.72.0/22                     |
| - Aurora PostgreSQL Primary/Reader |                 | - Aurora PostgreSQL Reader         |                 | - Aurora PostgreSQL Reader         |
| - ScyllaDB NVMe Node 1             |                 | - ScyllaDB NVMe Node 2             |                 | - ScyllaDB NVMe Node 3             |
| - ClickHouse Server Node 1         |                 | - ClickHouse Server Node 2         |                 | - ClickHouse Keeper / Server 3     |
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
       |                                                      |                                                      |
       v                                                      v                                                      v
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
| Private Streaming Subnet           |                 | Private Streaming Subnet           |                 | Private Streaming Subnet           |
| 10.100.80.0/22                     |                 | 10.100.84.0/22                     |                 | 10.100.88.0/22                     |
| - Redpanda Broker 1 (AZ-1a)        |                 | - Redpanda Broker 2 (AZ-1b)        |                 | - Redpanda Broker 3 (AZ-1c)        |
+------------------------------------+                 +------------------------------------+                 +------------------------------------+
```

#### Subnet Allocation & IP Addressing Plan

| Subnet Tier | Subnet Mask | Host Capacity / AZ | Total IPs Across 3 AZs | Access Control & Routing Purpose |
|---|---|---|---|---|
| **Public Subnets** | `/24` (e.g. `10.100.1.0/24`) | 251 usable IPs | 753 IPs | External ALB endpoints, NAT Gateways, Internet Gateway (IGW) route (`0.0.0.0/0`). |
| **Application Subnets** | `/20` (e.g. `10.100.16.0/20`) | 4,091 usable IPs | 12,273 IPs | EKS worker nodes, Karpenter dynamic pods, API gateways, WebSocket servers. Route to NAT / VPC Endpoints. |
| **Database Subnets** | `/22` (e.g. `10.100.64.0/22`) | 1,019 usable IPs | 3,057 IPs | Aurora PostgreSQL, ScyllaDB nodes, ClickHouse cluster nodes. No internet route. Ingress strictly from App subnets. |
| **Streaming Subnets** | `/22` (e.g. `10.100.80.0/22`) | 1,019 usable IPs | 3,057 IPs | Dedicated Redpanda event brokers. Isolated network layer preventing inter-broker streaming traffic from choking DB bandwidth. |

### 2.2 AWS PrivateLink VPC Gateway & Interface Endpoints

Eliminates high NAT Gateway fees ($0.045/GB), reduces latency for intra-AWS calls via PrivateLink Endpoints:

1. **S3 Gateway Endpoint**: routes S3 traffic (analytical backups, ClickHouse cold partitions, trade export drops) over AWS internal backbone, free.
2. **DynamoDB Gateway Endpoint**: for Terraform backend state lock + app distributed locks.
3. **Interface Endpoints (PrivateLink ENIs)**: deployed across App subnets for ECR API & Docker registry (`com.amazonaws.us-east-1.ecr.api`, `com.amazonaws.us-east-1.ecr.dkr`), AWS STS (`com.amazonaws.us-east-1.sts`), Systems Manager (`ssm`), CloudWatch Logs (`logs`).

### 2.3 EKS Compute & Karpenter v1.0+ Dynamic Provisioning Architecture

Tradebook runs **Amazon EKS** on Kubernetes 1.30. Node mgmt bypasses traditional Auto Scaling Groups for **Karpenter v1.0+**, open-source declarative node auto-provisioner.

#### Karpenter NodePool Segmentation Strategy

Karpenter dynamically provisions Graviton3 (ARM64) EC2 instances based on pod resource requests/scheduling constraints:

1. `system-pool`: fixed On-Demand Graviton3 nodes (`t4g.medium`, `c7g.large`) spanning 3 AZs, system-critical daemons (CoreDNS, Karpenter Controller, AWS VPC CNI, AWS EBS CSI driver, Cilium CNI).
2. `stateless-api-pool`: mixed Spot (80%) and On-Demand (20%) compute (`c7g.xlarge` to `c7g.4xlarge`), stateless REST, GraphQL, business logic microservices.
3. `websocket-pool`: dedicated Memory/Network-Optimized On-Demand Graviton3 instances (`r7g.xlarge`, `r7g.2xlarge`) supporting high concurrent TCP connection counts (up to 500k concurrent WebSockets/node), optimized Linux kernel params (`net.core.somaxconn = 65535`, `net.ipv4.tcp_max_syn_backlog = 65535`).
4. `batch-worker-pool`: 100% Spot Graviton3 instances (`c7g.2xlarge`, `m7g.2xlarge`) processing async audit validation, CDC events, batch reporting.

### 2.4 Decoupled Stateful Data Tier Architecture

Avoids storage bottlenecks under peak trading loads (100,000+ TPS) via four purpose-built database technologies:

```
+---------------------------------------------------------------------------------------------------+
|                                  TRADEBOOK DATA STORAGE TIERING                                  |
+------------------------------------+--------------------------------------------------------------+
| Database Technology                | Engine Role & Workload Responsibility                        |
+------------------------------------+--------------------------------------------------------------+
| **AWS Aurora PostgreSQL 16**       | Systems of Record: User accounts, organizations, RBAC        |
| (Serverless v2 / Provisioned)      | permissions, tenant configurations, active order states.     |
+------------------------------------+--------------------------------------------------------------+
| **ScyllaDB Enterprise**            | High-Throughput Ledger: Ultra-low latency, append-only order |
| (Local NVMe `i4i` Instances)       | book execution history, immutable transaction audit logs.   |
+------------------------------------+--------------------------------------------------------------+
| **Redpanda Cluster**               | Event Streaming Bus: Real-time order routing, CDC log        |
| (Local NVMe / EBS `gp3`)           | fanout, market data distribution (Kafka API compatible).     |
+------------------------------------+--------------------------------------------------------------+
| **ClickHouse Engine**              | Analytical Warehouse: High-compression OLAP historical trade |
| (Local NVMe + S3 Storage Tiering)  | reports, volume aggregation, market depth metrics.          |
+------------------------------------+--------------------------------------------------------------+
```

---

## 3. Production-Ready AWS HCL Terraform Modules

### 3.1 Directory Structure & Infrastructure Blueprint

```
terraform-tradebook-infrastructure/
├── environments/
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

### 3.2 Backend State Configuration (`environments/prod/backend.tf`)

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

### 3.3 Production HCL Module Implementations

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

# Public Subnets (ALB, IGW, NAT)
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

# Private Application Subnets (EKS Worker Nodes, Karpenter)
resource "aws_subnet" "application" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + 1) # /20 subnets: 10.100.16.0/20, 10.100.32.0/20, 10.100.48.0/20
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name                                           = "tradebook-app-subnet-${var.availability_zones[count.index]}"
    "kubernetes.io/role/internal-elb"              = "1"
    "karpenter.sh/discovery"                       = "tradebook-${var.environment}"
    "kubernetes.io/cluster/tradebook-${var.environment}" = "shared"
  }
}

# Private Database Subnets (Aurora Postgres, ScyllaDB, ClickHouse)
resource "aws_subnet" "database" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 6, count.index + 16) # /22 subnets: 10.100.64.0/22, 10.100.68.0/22, 10.100.72.0/22
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name = "tradebook-db-subnet-${var.availability_zones[count.index]}"
  }
}

# Private Streaming Subnets (Redpanda Cluster Brokers)
resource "aws_subnet" "streaming" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 6, count.index + 20) # /22 subnets: 10.100.80.0/22, 10.100.84.0/22, 10.100.88.0/22
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name = "tradebook-streaming-subnet-${var.availability_zones[count.index]}"
  }
}

# Internet Gateway
resource "aws_internet_gateway" "igw" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "tradebook-igw-${var.environment}" }
}

# Elastic IPs for NAT Gateways
resource "aws_eip" "nat" {
  count  = length(var.availability_zones)
  domain = "vpc"
  tags   = { Name = "tradebook-nat-eip-${var.availability_zones[count.index]}" }
}

# Multi-AZ NAT Gateways
resource "aws_nat_gateway" "nat" {
  count         = length(var.availability_zones)
  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id
  tags          = { Name = "tradebook-nat-${var.availability_zones[count.index]}" }
}

# Route Tables
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

resource "aws_route_table" "database" {
  count  = length(var.availability_zones)
  vpc_id = aws_vpc.main.id

  tags = { Name = "tradebook-db-rt-${var.availability_zones[count.index]}" }
}

resource "aws_route_table" "streaming" {
  count  = length(var.availability_zones)
  vpc_id = aws_vpc.main.id

  tags = { Name = "tradebook-streaming-rt-${var.availability_zones[count.index]}" }
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

resource "aws_route_table_association" "database" {
  count          = length(var.availability_zones)
  subnet_id      = aws_subnet.database[count.index].id
  route_table_id = aws_route_table.database[count.index].id
}

resource "aws_route_table_association" "streaming" {
  count          = length(var.availability_zones)
  subnet_id      = aws_subnet.streaming[count.index].id
  route_table_id = aws_route_table.streaming[count.index].id
}

# S3 Gateway Endpoint (Bypasses NAT Gateway fees)
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

#### Module 2: EKS & Karpenter Cluster Infrastructure (`modules/eks/main.tf`)

```hcl
# modules/eks/main.tf
variable "cluster_name" { type = string }
variable "cluster_version" { default = "1.30" }
variable "vpc_id" { type = string }
variable "subnet_ids" { type = list(string) }
variable "environment" { type = string }

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

  # Native EKS Pod Identity Agent Addon
  cluster_addons = {
    coredns                = { resolve_conflicts = "OVERWRITE" }
    kube-proxy             = { resolve_conflicts = "OVERWRITE" }
    vpc-cni                = { resolve_conflicts = "OVERWRITE" }
    aws-ebs-csi-driver     = { resolve_conflicts = "OVERWRITE" }
    eks-pod-identity-agent = { resolve_conflicts = "OVERWRITE" }
  }

  # Fixed System Nodes Pool (Graviton3 On-Demand)
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

# Karpenter v1.0+ EC2NodeClass Manifest
resource "kubectl_manifest" "karpenter_node_class" {
  yaml_body = <<YAML
apiVersion: karpenter.k8s.aws/v1
kind: EC2NodeClass
metadata:
  name: default
spec:
  amiFamily: AL2023
  role: ${module.eks.node_iam_role_name}
  subnetSelectorTerms:
    - tags:
        karpenter.sh/discovery: "tradebook-${var.environment}"
  securityGroupSelectorTerms:
    - tags:
        aws:eks:cluster-name: "${var.cluster_name}"
  tags:
    KarpenterManaged = "true"
YAML
}

# Karpenter Stateless NodePool Manifest
resource "kubectl_manifest" "karpenter_node_pool_stateless" {
  yaml_body = <<YAML
apiVersion: karpenter.sh/v1
kind: NodePool
metadata:
  name: stateless-api
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
        group: karpenter.k8s.aws
        kind: EC2NodeClass
        name: default
  limits:
    cpu: "2000"
    memory: 4000Gi
  disruption:
    consolidationPolicy: WhenEmptyOrUnderutilized
    consolidateAfter: 1m
    expireAfter: 720h
YAML
}

output "cluster_endpoint" { value = module.eks.cluster_endpoint }
output "cluster_name" { value = module.eks.cluster_name }
```