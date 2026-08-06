# Production Infrastructure Architecture, AWS HCL Terraform Modules, Multi-Region Disaster Recovery & FinOps Cost Analysis

**Document Title**: Tradebook Production Infrastructure Architecture & Financial Engineering Specification  
**File Path**: `research/infrastructure-terraform-and-cost-analysis.md`  
**Author / Owner**: Infrastructure Architecture & FinOps Engineering Group  
**Date**: August 5, 2026  
**Status**: Publication-Grade Architectural Specification  

---

## 1. Executive Summary & Production Infrastructure Vision

Tradebook is an enterprise-grade, high-throughput financial trading, transactional ledger, and analytical platform engineered to deliver sub-millisecond execution, continuous auditability, and real-time data synchronization. The platform requires ultra-low latency (<20ms p99 REST/GraphQL response, <5ms WebSocket broadcast), strict zero-data-loss durability (RPO ~ 0 for transactional ledger entries), and high-volume analytical processing capable of ingesting billions of financial events daily.

To achieve these extreme SLA targets while maintaining operational efficiency, security compliance, and predictable financial unit economics, Tradebook's production infrastructure is designed around five foundational architectural pillars:

1. **Cellular & Tiered Isolation**: Complete decoupling of stateless application layers (API gateways, WebSocket distribution nodes, background execution workers) from specialized stateful data layers (PostgreSQL system of record, ScyllaDB high-throughput NVMe ledger, Redpanda event streaming bus, and ClickHouse analytical warehouse).
2. **Declarative Infrastructure as Code (IaC)**: 100% of cloud resources across networking, compute, storage, security, and databases are managed declaratively via modular, version-controlled AWS HCL Terraform modules backed by S3 state locking, DynamoDB concurrency protection, and KMS envelope encryption.
3. **Multi-AZ High Availability & Multi-Region Resiliency**: Zero single points of failure (SPOF) across a 3 Availability Zone (AZ) regional footprint, paired with a multi-region disaster recovery (DR) strategy supporting Active-Passive Pilot Light (Tier 1/2) and Active-Passive Warm Standby with Aurora Global Database & ScyllaDB Multi-DC (Tier 3/4) topologies.
4. **Declarative Cloud-Native Compute via Karpenter**: Elimination of legacy AWS Auto Scaling Groups (ASGs) in favor of Karpenter v1.0+ node auto-provisioning on AWS EKS, enabling sub-15-second node spin-up, instant Graviton3 (ARM64) right-sizing, and aggressive Spot instance orchestration.
5. **Rigorous FinOps & Unit Economic Governance**: Financial modeling anchored in unit economics—tracking Cost per Monthly Active User ($/MAU) and Cost per 1 Million Transactions ($/1M Tx)—ensuring that cost per MAU scales down by **6.45x** (from $0.0807 down to $0.0125 per MAU) as traffic scales from 10k to 10M Daily Active Users (DAU).

---

## 2. Production Network & Compute Topology

### 2.1 Multi-AZ VPC Network Architecture

The network infrastructure is deployed within a dedicated, non-overlapping Virtual Private Cloud (VPC) CIDR block (`10.100.0.0/16`) spanning three Availability Zones (`us-east-1a`, `us-east-1b`, `us-east-1c`). The network design strictly enforces multi-tiered security isolation, separating public ingress from application compute, database storage, and high-volume streaming buses.

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

To eliminate high AWS NAT Gateway processing charges ($0.045/GB) and reduce latency for intra-AWS API calls, Tradebook provisions AWS PrivateLink Endpoints directly inside the VPC:

1. **S3 Gateway Endpoint**: Routes all S3 object storage traffic (analytical backups, ClickHouse cold partitions, trade export drops) directly over the AWS internal backbone free of charge.
2. **DynamoDB Gateway Endpoint**: Used for Terraform backend state lock evaluation and application distributed locks.
3. **Interface Endpoints (PrivateLink ENIs)**: Deployed across Application subnets for ECR API & Docker registry (`com.amazonaws.us-east-1.ecr.api`, `com.amazonaws.us-east-1.ecr.dkr`), AWS STS (`com.amazonaws.us-east-1.sts`), Systems Manager (`ssm`), and CloudWatch Logs (`logs`).

### 2.3 EKS Compute & Karpenter v1.0+ Dynamic Provisioning Architecture

Tradebook utilizes **Amazon Elastic Kubernetes Service (EKS)** running Kubernetes 1.30. Node management completely bypasses traditional Auto Scaling Groups in favor of **Karpenter v1.0+**, an open-source, highly declarative node auto-provisioner.

#### Karpenter NodePool Segmentation Strategy

Karpenter dynamically provisions AWS Graviton3 (ARM64) EC2 instances based on pod resource requests and scheduling constraints:

1. `system-pool`: Fixed On-Demand Graviton3 nodes (`t4g.medium`, `c7g.large`) spanning 3 AZs running system-critical daemons (CoreDNS, Karpenter Controller, AWS VPC CNI, AWS EBS CSI driver, Cilium CNI).
2. `stateless-api-pool`: Mixed Spot (80%) and On-Demand (20%) compute (`c7g.xlarge` to `c7g.4xlarge`) running stateless REST, GraphQL, and business logic microservices.
3. `websocket-pool`: Dedicated Memory/Network-Optimized On-Demand Graviton3 instances (`r7g.xlarge`, `r7g.2xlarge`) supporting high concurrent TCP connection counts (up to 500k concurrent WebSockets per node) with optimized Linux kernel parameters (`net.core.somaxconn = 65535`, `net.ipv4.tcp_max_syn_backlog = 65535`).
4. `batch-worker-pool`: 100% Spot Graviton3 instances (`c7g.2xlarge`, `m7g.2xlarge`) processing asynchronous audit validation, CDC events, and batch reporting.

### 2.4 Decoupled Stateful Data Tier Architecture

To avoid storage bottlenecks under peak trading loads (100,000+ TPS), Tradebook decouples state across four purpose-built database technologies:

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

#### Module 3: Database Tier (Aurora Postgres & ScyllaDB) (`modules/databases/postgres.tf`)

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
  cluster_identifier          = "tradebook-postgres-${var.environment}"
  engine                      = "aurora-postgresql"
  engine_version              = "16.2"
  database_name               = "tradebook"
  master_username             = "tradebook_admin"
  manage_master_user_password = true
  db_subnet_group_name        = aws_db_subnet_group.aurora.name
  vpc_security_group_ids      = [aws_security_group.postgres.id]

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

resource "aws_security_group" "postgres" {
  name        = "tradebook-postgres-sg-${var.environment}"
  vpc_id      = var.vpc_id
  description = "Restrict Postgres ingress strictly to EKS application subnets"

  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = ["10.100.16.0/20", "10.100.32.0/20", "10.100.48.0/20"]
  }
}

output "aurora_endpoint" { value = aws_rds_cluster.aurora.endpoint }
output "aurora_reader_endpoint" { value = aws_rds_cluster.aurora.reader_endpoint }
```

#### Module 4: Redpanda Event Streaming Cluster (`modules/streaming/redpanda.tf`)

```hcl
# modules/streaming/redpanda.tf
variable "environment" { type = string }
variable "vpc_id" { type = string }
variable "streaming_subnet_ids" { type = list(string) }
variable "node_count" { default = 3 }
variable "instance_type" { default = "i4i.xlarge" } # Local NVMe SSD instance

# Dynamic AMI Lookup (Multi-Region Compatible)
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd-gp3/ubuntu-noble-24.04-amd64-server-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_instance" "redpanda" {
  count                  = var.node_count
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  subnet_id              = var.streaming_subnet_ids[count.index % length(var.streaming_subnet_ids)]
  vpc_security_group_ids = [aws_security_group.redpanda.id]

  user_data = <<-EOF
              #!/bin/bash
              set -euo pipefail
              # Format and mount local NVMe SSD storage (/dev/nvme1n1) for Redpanda broker
              if [ -b /dev/nvme1n1 ]; then
                mkfs.xfs -f /dev/nvme1n1
                mkdir -p /var/lib/redpanda/data
                mount -o noatime /dev/nvme1n1 /var/lib/redpanda/data
                echo "/dev/nvme1n1 /var/lib/redpanda/data xfs defaults,noatime 0 2" >> /etc/fstab
              fi
              EOF

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
    from_port   = 9092 # Kafka API Port
    to_port     = 9092
    protocol    = "tcp"
    cidr_blocks = ["10.100.0.0/16"]
  }

  ingress {
    from_port   = 33145 # Redpanda RPC inter-node communications
    to_port     = 33145
    protocol    = "tcp"
    self        = true
  }
}

output "redpanda_broker_ips" { value = aws_instance.redpanda[*].private_ip }
```

#### Module 5: ClickHouse Analytical Warehouse (`modules/analytics/clickhouse.tf`)

```hcl
# modules/analytics/clickhouse.tf
variable "environment" { type = string }
variable "vpc_id" { type = string }
variable "database_subnet_ids" { type = list(string) }

resource "aws_s3_bucket" "clickhouse_cold_storage" {
  bucket = "tradebook-clickhouse-cold-${var.environment}"
}

resource "aws_s3_bucket_server_side_encryption_configuration" "clickhouse_s3" {
  bucket = aws_s3_bucket.clickhouse_cold_storage.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_iam_role" "clickhouse" {
  name = "tradebook-clickhouse-role-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ec2.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy" "clickhouse_s3" {
  name = "tradebook-clickhouse-s3-policy-${var.environment}"
  role = aws_iam_role.clickhouse.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "s3:PutObject",
        "s3:GetObject",
        "s3:ListBucket",
        "s3:DeleteObject"
      ]
      Resource = [
        aws_s3_bucket.clickhouse_cold_storage.arn,
        "${aws_s3_bucket.clickhouse_cold_storage.arn}/*"
      ]
    }]
  })
}

resource "aws_iam_instance_profile" "clickhouse" {
  name = "tradebook-clickhouse-profile-${var.environment}"
  role = aws_iam_role.clickhouse.name
}

resource "aws_instance" "clickhouse" {
  count                  = 3
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = "c7g.2xlarge"
  subnet_id              = var.database_subnet_ids[count.index % length(var.database_subnet_ids)]
  vpc_security_group_ids = [aws_security_group.clickhouse.id]
  iam_instance_profile   = aws_iam_instance_profile.clickhouse.name

  root_block_device {
    volume_size = 500
    volume_type = "gp3"
    iops        = 12000
    throughput  = 500
  }

  tags = {
    Name = "tradebook-clickhouse-${var.environment}-${count.index}"
  }
}

resource "aws_security_group" "clickhouse" {
  name   = "tradebook-clickhouse-sg-${var.environment}"
  vpc_id = var.vpc_id

  ingress {
    from_port   = 8123 # HTTP Interface
    to_port     = 8123
    protocol    = "tcp"
    cidr_blocks = ["10.100.0.0/16"]
  }

  ingress {
    from_port   = 9000 # Native Client Protocol
    to_port     = 9000
    protocol    = "tcp"
    cidr_blocks = ["10.100.0.0/16"]
  }
}
```

#### Module 6: Security & Edge Networking (`modules/security_networking/cloudfront_waf.tf`)

```hcl
# modules/security_networking/cloudfront_waf.tf
variable "environment" { type = string }

# AWS WAF v2 Web ACL (Must be deployed to us-east-1 for CloudFront scope)
resource "aws_wafv2_web_acl" "main" {
  provider    = aws.us_east_1
  name        = "tradebook-waf-${var.environment}"
  scope       = "CLOUDFRONT"
  description = "WAF v2 protecting Tradebook CDN Ingress"

  default_action {
    allow {}
  }

  # Common Rule Set
  rule {
    name     = "AWSManagedRulesCommonRuleSet"
    priority = 1

    override_action { none {} }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesCommonRuleSet"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "WAFCommonRules"
      sampled_requests_enabled   = true
    }
  }

  # Rate Limiting Rule: Max 2000 requests per 5 minutes per IP
  rule {
    name     = "IPRateLimit"
    priority = 2

    action { block {} }

    statement {
      rate_based_statement {
        limit              = 2000
        aggregate_key_type = "IP"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "WAFRateLimit"
      sampled_requests_enabled   = true
    }
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "TradebookWAF"
    sampled_requests_enabled   = true
  }
}

output "waf_web_acl_arn" { value = aws_wafv2_web_acl.main.arn }
```

---

## 4. Multi-Region Disaster Recovery (DR) & Deployment Strategy

### 4.1 DR Topologies Comparison

Tradebook defines two distinct DR operational models based on business tiering:

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

| Operational Metric / Dimension | Active-Passive Pilot Light | Active-Passive Warm Standby (Aurora Global DB + ScyllaDB Multi-DC) |
|---|---|---|
| **Target Scale Tiers** | Tier 1 (10k DAU) & Tier 2 (100k DAU) | Tier 3 (1M DAU) & Tier 4 (10M DAU) |
| **Primary Region (Write Head)** | `us-east-1` (N. Virginia) | `us-east-1` (N. Virginia Primary Write Head) |
| **Secondary Region (Read/DR)** | `us-west-2` (Oregon - On-Demand Spinup) | `us-west-2` (Oregon Read-Only Warm Standby) |
| **Aurora PostgreSQL Topology** | Single-Region Multi-AZ Aurora Serverless | Active-Passive Aurora Global Database (Storage-level WAL replication < 1s) |
| **Target RPO (Recovery Point Objective)** | **< 15 seconds** | **< 1 second** (Aurora WAL), **< 5 seconds** (ScyllaDB `LOCAL_QUORUM` async) |
| **Target RTO (Recovery Time Objective)** | **< 15 minutes** | **< 60 seconds** (Automated Global Database promotion & Route 53 DNS switch) |
| **Infrastructure Cost Premium** | +35% to +45% cost overhead | +110% to +130% cost overhead |
| **Failover Execution Mechanism** | Infrastructure provisioning via Terraform + DB snapshot restore. | Route 53 Health Checks + Lambda automated `aws rds failover-global-cluster` promotion. |

### 4.2 Data Replication Mechanics & Consistency Model Across Systems

1. **Aurora PostgreSQL System of Record (Active-Passive Global Database)**:
   * **Engine Constraint**: AWS Aurora PostgreSQL does **NOT** support concurrent active-active multi-region write operations across regions. All transactional writes route strictly to the primary writer instance in `us-east-1`.
   * **Replication Architecture**: Tradebook deploys **Aurora Global Database** with storage-level physical WAL replication to `us-west-2` with sub-second replication latency (< 1s RPO).
   * **Failover Mechanics**: In a regional outage, Route 53 health checks trigger an automated AWS Lambda failover handler that executes `aws rds failover-global-cluster --allow-data-loss`, promoting `us-west-2` to the primary write cluster within 60 seconds (RTO < 60s).

2. **ScyllaDB Enterprise Ledger Store (Multi-DC Async Consistency Model)**:
   * Operates as a single multi-datacenter cluster spanning `us-east-1` (`dc1`) and `us-west-2` (`dc2`).
   * Keyspace configuration:
     ```sql
     CREATE KEYSPACE tradebook_ledger WITH replication = {
         'class': 'NetworkTopologyStrategy',
         'us-east-1': 3,
         'us-west-2': 3
     };
     ```
   * **Consistency & DR SLA**: Writes execute with `LOCAL_QUORUM` consistency, guaranteeing sub-3ms write confirmation in the primary region while background inter-datacenter streams replicate mutations asynchronously.
   * **RPO & Latency Trade-Off**: Under `LOCAL_QUORUM`, a catastrophic regional disaster in `us-east-1` achieves **RPO < 5 seconds** (RPO > 0 because un-replicated in-flight mutations in `dc1` are lost during abrupt crashes). Achieving true RPO = 0 requires cross-region synchronous `EACH_QUORUM` consensus, which forces every write to incur ~60-70ms WAN round-trip latency, directly violating the platform's <20ms p99 execution SLA. Therefore, `LOCAL_QUORUM` is chosen to deliver sub-millisecond local throughput while maintaining an RPO < 5s DR SLA.

3. **Redpanda Event Streaming Bus**:
   * Deploys **Redpanda MirrorMaker2 / Shadow Indexing**. Event payloads published to primary topics (`tradebook.orders.v1`, `tradebook.audit.events`) are streamed cross-region to secondary brokers with consumer offset translation enabled.

4. **S3 Object Storage & ClickHouse Analytics**:
   * S3 buckets use **S3 Cross-Region Replication (CRR)** with Replication Time Control (RTC), enforcing a 99.9% replication SLA within 15 minutes.
   * ClickHouse utilizes `ReplicatedMergeTree` engine definitions pointing to multi-region S3 storage locations.

---

## 5. Itemized Cost Scaling Model Across 4 Scale Tiers

All cost calculations are derived from AWS US-East public list rates (2026 model), assuming 24/7 cluster operations.

### 5.1 Scale Tier Workload Specifications

* **Tier 1 (Lean / MVP)**: **10,000 DAU** | 100 TPS Avg / 1,000 TPS Peak | ~260 Million Events/mo
* **Tier 2 (Growth)**: **100,000 DAU** | 1,000 TPS Avg / 10,000 TPS Peak | ~2.6 Billion Events/mo
* **Tier 3 (Enterprise)**: **1,000,000 DAU** | 10,000 TPS Avg / 100,000 TPS Peak | ~26 Billion Events/mo
* **Tier 4 (Global Scale)**: **10,000,000 DAU** | 100,000 TPS Avg / 1,000,000 TPS Peak | ~260 Billion Events/mo

### 5.2 Itemized Monthly Cost Matrix ($ USD / Month)

| Infrastructure Component Category | Tier 1: 10k DAU (100 TPS) | Tier 2: 100k DAU (1,000 TPS) | Tier 3: 1M DAU (10,000 TPS) | Tier 4: 10M DAU (100,000 TPS) |
|---|---|---|---|---|
| **EKS Control Plane** | $73.00 (1 Cluster) | $73.00 (1 Cluster) | $146.00 (2 Clusters Multi-Region) | $292.00 (4 Clusters Global Multi-Region) |
| **Stateless API/WS Compute (EKS)** | $280.00 (4x `c7g.xlarge` Spot/OD) | $1,120.00 (16x `c7g.xlarge`) | $8,960.00 (64x `c7g.2xlarge`) | $71,680.00 (256x `c7g.4xlarge`) |
| **PostgreSQL System of Record (Aurora)** | $350.00 (Serverless 2-16 ACU) | $1,400.00 (Serverless 8-64 ACU) | $5,600.00 (Provisioned `db.r7g.4xlarge`) | $28,400.00 (Global DB 4x `db.r7g.8xlarge`) |
| **ScyllaDB Ledger & Audit Store** | $480.00 (3x `i4i.xlarge`) | $1,920.00 (6x `i4i.2xlarge`) | $11,520.00 (12x `i4i.4xlarge`) | $69,120.00 (36x `i4i.8xlarge`) |
| **Redpanda Event Streaming Bus** | $320.00 (3x `c7g.xlarge` + `gp3`) | $1,280.00 (3x `i4i.xlarge` NVMe) | $6,144.00 (8x `i4i.2xlarge`) | $36,864.00 (24x `i4i.4xlarge`) |
| **ClickHouse Analytical Warehouse** | $260.00 (2x `c7g.xlarge` + S3) | $840.00 (3x `c7g.2xlarge` + S3) | $3,360.00 (6x `r7g.2xlarge` + S3) | $18,200.00 (16x `r7g.4xlarge` + S3) |
| **Storage & Backups (EBS gp3 + S3)** | $180.00 (2 TB EBS + 5 TB S3) | $750.00 (10 TB EBS + 30 TB S3) | $4,200.00 (50 TB EBS + 250 TB S3) | $24,500.00 (200 TB EBS + 1.5 PB S3) |
| **Network Data Transfer & NAT** | $210.00 (2 TB NAT + 1 TB Egress) | $1,450.00 (15 TB NAT + 10 TB Egress) | $11,800.00 (100 TB NAT + 80 TB Egress) | $84,000.00 (600 TB NAT + 500 TB Egress) |
| **Security (CloudFront CDN + WAF v2)** | $120.00 (WAF Rules + CDN Egress) | $450.00 (Managed Rules + CDN) | $2,800.00 (High-Vol WAF + Edge) | $18,500.00 (Enterprise WAF + Shield) |
| **Observability (Prometheus, Logs, Grafana)** | $150.00 (CloudWatch + OTEL) | $780.00 (AMP + CloudWatch Logs) | $4,500.00 (Datadog/Grafana + AMP) | $26,000.00 (Enterprise Observability Stack) |
| **TOTAL MONTHLY INFRASTRUCTURE COST** | **$2,423.00** | **$10,063.00** | **$59,030.00** | **$377,556.00** |

### 5.3 Unit Economics & Mathematical Scaling Curves

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
   $$\text{Cost}_{\text{MAU}} = \frac{\text{Total Monthly Infrastructure Spend}}{\text{DAU} \times 3.0}$$
   * At 10k DAU, cost per MAU is **$0.0807**. At 10M DAU, cost per MAU drops to **$0.0125**—delivering a **6.45x efficiency gain** as fixed platform overhead amortizes across user scale.

2. **Cost Per 1 Million Transactions**:
   $$\text{Cost}_{\text{1M Tx}} = \frac{\text{Total Monthly Cost}}{\text{Average TPS} \times 86,400 \times 30.4} \times 1,000,000$$
   * At 10k DAU (100 TPS avg), cost per 1M transactions is **$9.32**. At 10M DAU (100,000 TPS avg), cost per 1M transactions drops to **$1.45**.

---

## 6. FinOps Cost Optimization Playbook

By applying the following five core FinOps cost reduction levers, Tradebook cuts baseline monthly cloud spend by **35% to 52%**:

```
+-----------------------------------------------------------------------------------+
|                           FINOPS COST OPTIMIZATION LEVERS                         |
+-----------------------------------------------------------------------------------+
| 1. Compute Savings Plans & Reserved Instances   --> 42% - 62% Savings on Baseline  |
| 2. Karpenter Spot Instance Diversification     --> 70% - 85% Savings on Batch/API  |
| 3. VPC PrivateLink Endpoints (Bypass NAT Fees) --> $0.045/GB NAT Processing Saved |
| 4. S3 Intelligent-Tiering & Cold Archiving     --> 60% - 80% Storage Cost Reduction|
| 5. Dynamic Pod Scaling via HPA + KEDA Rules    --> Right-sizes Pod Compute Demand |
+-----------------------------------------------------------------------------------+
```

### 6.1 AWS 3-Year Compute Savings Plans & RIs
* **Execution**: Commit to 3-Year Compute Savings Plans for deterministic baseline instance capacity (System NodePools, Aurora Postgres instances, ScyllaDB baseline nodes).
* **Financial Impact**: Delivers **up to 62% discount** over standard On-Demand pricing.

### 6.2 Karpenter Spot Instance Orchestration & Graceful Termination
* **Execution**: Karpenter NodePools target Spot instances across `c7g`, `m7g`, `r7g`, `c6g`, and `m6g` instance families across all 3 AZs.
* **Resiliency**: The `aws-node-termination-handler` listens to 2-minute AWS EventBridge Spot interruption warnings, gracefully cordoning and draining workloads onto remaining nodes prior to termination.
* **Financial Impact**: Achieves **70% to 85% cost savings** on stateless API, WebSocket, and worker compute.

### 6.3 VPC Endpoint Traffic Localization (NAT Fee Bypass)
* **Execution**: Route S3, ECR, STS, DynamoDB, and CloudWatch traffic through VPC Gateway and Interface Endpoints.
* **Financial Impact**: Eliminates NAT Gateway data transfer charges ($0.045/GB), saving over **$25,000/month** at Tier 4 scale.

### 6.4 Storage Lifecycle Policies (S3 & ClickHouse Cold Tiering)
* **Execution**: Raw audit logs and historical event payloads in S3 transition to **S3 Intelligent-Tiering** immediately. Objects untouched after 90 days transition to **S3 Glacier Instant Retrieval** ($0.004/GB/month), and after 180 days to **S3 Glacier Deep Archive** ($0.00099/GB/month). ClickHouse automatically offloads historical partition data to S3 after 30 days.

* **CRITICAL FINOPS WARNING: S3 Intelligent-Tiering Monitoring Fees & Object Aggregation Requirement**:
  * **Fee Traps**: S3 Intelligent-Tiering charges a per-object monitoring and automation fee of **$0.0025 per 1,000 objects per month** and **ignores objects smaller than 128 KB** (they remain in Frequent Access tier forever).
  * **Cost Impact**: Uploading 1 billion small, unaggregated audit log objects (e.g. 10 KB each = 10 TB total storage = $230/mo standard storage) results in **$2,500/month in monitoring fees alone**—a **1,000%+ financial penalty** that invalidates all storage savings.
  * **Mandatory Optimization Protocol**: All application audit logs, CDC events, and streaming micro-batches MUST be aggregated into Parquet files or Tar archives exceeding **128 KB** (ideally **128 MB to 512 MB** file sizes) using Vector, Fluentbit, or Kinesis Data Firehose batching prior to S3 upload.

### 6.5 Dynamic Event-Driven Auto-Scaling: HPA + KEDA Manifest

```yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: tradebook-api-scaler
  namespace: tradebook-prod
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: tradebook-api
  minReplicaCount: 6
  maxReplicaCount: 150
  cooldownPeriod: 300
  triggers:
    - type: prometheus
      metadata:
        serverAddress: http://prometheus-k8s.monitoring.svc:9090
        metricName: http_requests_per_second
        query: sum(rate(http_requests_total{job="tradebook-api"}[2m]))
        threshold: "1500"
    - type: prometheus
      metadata:
        serverAddress: http://prometheus-k8s.monitoring.svc:9090
        metricName: redpanda_consumer_lag
        query: sum(redpanda_consumergroup_lag{topic="tradebook.orders.v1"})
        threshold: "500"
```

---

## 7. Verification & Implementation Roadmap

1. **Phase 1 (MVP Setup - Tier 1)**: Deploy modular VPC, single-region EKS with Karpenter, Aurora PostgreSQL Serverless v2, 3-node Redpanda cluster. Est. cost: **~$2,400/mo**.
2. **Phase 2 (Growth - Tier 2)**: Provision ScyllaDB on local NVMe `i4i.xlarge` instances for ledger audit processing. Add ClickHouse cluster for analytical queries. Est. cost: **~$10,000/mo**.
3. **Phase 3 (Enterprise Multi-Region - Tier 3)**: Enable Aurora Global Database to `us-west-2` with automated Lambda failover. Deploy ScyllaDB multi-datacenter cluster. Est. cost: **~$59,000/mo**.
4. **Phase 4 (Global Active-Active - Tier 4)**: Expand into 3 geographical regions (US, EU, APAC) with Route 53 Anycast routing and enterprise FinOps governance. Est. cost: **~$377,000/mo**.

---
*End of Publication Specification.*
