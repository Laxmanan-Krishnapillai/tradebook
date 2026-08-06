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
    cidr_blocks = ["10.100.10.0/20", "10.100.26.0/20", "10.100.42.0/20"]
  }
}

output "aurora_endpoint" { value = aws_rds_cluster.aurora.endpoint }
output "aurora_reader_endpoint" { value = aws_rds_cluster.aurora.reader_endpoint }
