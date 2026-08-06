# Workflow Engine Alternatives - Restate.dev

*Part of [Alternative Architecture Exploration](README.md).*

### 7.6 Actor-model durable execution for the workflow engine — Restate.dev

This one targets Section 2B's Hangfire layer specifically, not the UI stack. Restate uses "Virtual Objects" (single-writer-per-key actors) for durable execution and is serverless-native, deploying as a sidecar rather than requiring long-running workers the way Temporal does. Built by ex-Apache Flink engineers, with at least one tier-1 bank reportedly in production, but it's a small project relative to Temporal by adoption metrics. A credible modernization of the workflow-automation execution engine underneath the canvas feature, but an early bet relative to Hangfire's maturity. **Not for teams wanting the most battle-tested, largest-community option** — stick with Hangfire (with the §6.5 storage fix) unless the workflow engine's durability requirements outgrow it.
