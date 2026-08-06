# Workflow Engine Alternatives - Restate.dev

*Part of [Alternative Architecture Exploration](README.md).*

### 7.6 Actor-model durable execution for workflow engine — Restate.dev

Targets Section 2B's Hangfire layer specifically, not UI stack. Restate uses "Virtual Objects" (single-writer-per-key actors) for durable execution, serverless-native, deploying as sidecar rather than requiring long-running workers way Temporal does. Built by ex-Apache Flink engineers, at least one tier-1 bank reportedly in production, but small project relative to Temporal by adoption metrics. Credible modernization of workflow-automation execution engine underneath canvas feature, but early bet relative to Hangfire's maturity. **Not for teams wanting most battle-tested, largest-community option** — stick with Hangfire (with §6.5 storage fix) unless workflow engine's durability requirements outgrow it.
