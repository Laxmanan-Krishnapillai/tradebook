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
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + 1) # /20 subnets
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
  cidr_block        = cidrsubnet(var.vpc_cidr, 6, count.index + 20) # /22 subnets
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name = "tradebook-db-subnet-${var.availability_zones[count.index]}"
  }
}

# Private Streaming Subnets (Redpanda Cluster Brokers)
resource "aws_subnet" "streaming" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 6, count.index + 30) # /22 subnets
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

# S3 Gateway Endpoint (Bypasses NAT Gateway fees)
resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.us-east-1.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = aws_route_table.application[*].id

  tags = { Name = "tradebook-s3-endpoint" }
}

output "vpc_id" { value = aws_vpc.main.id }
output "application_subnet_ids" { value = aws_subnet.application[*].id }
output "database_subnet_ids" { value = aws_subnet.database[*].id }
output "streaming_subnet_ids" { value = aws_subnet.streaming[*].id }
