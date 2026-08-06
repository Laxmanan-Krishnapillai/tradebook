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
apiVersion: karpenter.k8s.aws/v1beta1
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
apiVersion: karpenter.sh/v1beta1
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
        apiVersion: karpenter.k8s.aws/v1beta1
        kind: EC2NodeClass
        name: default
  limits:
    cpu: "2000"
    memory: 4000Gi
  disruption:
    consolidationPolicy: WhenUnderutilized
    expireAfter: 720h
YAML
}

output "cluster_endpoint" { value = module.eks.cluster_endpoint }
output "cluster_name" { value = module.eks.cluster_name }
