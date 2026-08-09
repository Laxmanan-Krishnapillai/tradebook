# Aspire deployment reconciliation

Aspire is the local orchestration graph and the source for application deployment
artifacts. Run `aspire publish -o artifacts/aspire` to inspect its manifest, or `azd up`
to deploy the AppHost described by `azure.yaml` to Azure Container Apps.

The Terraform under `infra/terraform` remains the production infrastructure source of
truth. In particular, it provisions PostgreSQL Flexible Server 17, Container Apps, and
the Key Vault. Do not provision Aspire's PostgreSQL container in production. Map the
AppHost `tradebook` connection reference to the Terraform PostgreSQL output at deploy
time, and source its credential from the existing Key Vault secret reference. No secret
value belongs in `azure.yaml`, an Aspire manifest, or Terraform state.

Before applying an `azd` deployment, compare the generated Container Apps resources
with `infra/terraform/containerapp.tf` and the database reference with
`infra/terraform/database.tf`. Terraform retains ownership when the two paths differ.
