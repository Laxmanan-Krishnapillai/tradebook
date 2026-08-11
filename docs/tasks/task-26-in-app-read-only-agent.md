# Task 26 - Feature-gated in-app read-only agent

## Outcome

Add an authenticated in-app assistant that streams through AG-UI and can execute the
existing read-only analytics capability. The feature is disabled by default and adds no
mutation surface, model-authored markup, direct database access, or API loopback.

This task implements the in-app direction recorded by D22. It does not change the JWT
actor contract owned by Task 25: capability execution derives the actor from the
validated Entra tenant and `oid` through `ActorId.From`.

## Pinned stack

| Package | Version | Purpose |
| :--- | :--- | :--- |
| `Microsoft.Agents.AI` | `1.17.0` | Agent runtime |
| `Microsoft.Agents.AI.OpenAI` | `1.17.0` | Azure OpenAI agent adapter |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | `1.17.0-preview.260804.1` | Authenticated AG-UI endpoint |
| `Azure.AI.OpenAI` | `2.9.0-beta.1` | Responses client used by the selected MAF adapter |
| `Azure.Identity` | `1.21.0` | Managed identity / developer credential chain |
| `@ag-ui/client` | `0.0.57` | Browser AG-UI transport |
| `@assistant-ui/react` | `0.15.13` | Accessible conversation primitives |
| `@assistant-ui/react-ag-ui` | `0.0.53` | assistant-ui AG-UI runtime bridge |
| `zustand` | `5.0.14` | assistant-ui-compatible state runtime |

## Configuration

`InAppAgent:Enabled` defaults to `false`. When enabled, startup requires an absolute
HTTPS `InAppAgent:Endpoint` and a non-empty `InAppAgent:DeploymentName`. Production
uses managed identity; no model API key is accepted by this slice. An optional
`ManagedIdentityClientId` selects a user-assigned identity.

## Acceptance criteria

| ID | Requirement | Verification |
| :--- | :--- | :--- |
| AINATIVE-26-01 | The in-app agent is disabled by default and `/api/v1/agent/run` is not mapped while disabled. | `InAppAgentIntegrationTests.RunRouteIsAbsentWhenFeatureIsDisabled`; `appsettings.json` |
| AINATIVE-26-02 | Enabled configuration is startup-validated; disabled configuration requires no provider values. | `InAppAgentOptionsValidatorTests`; `OptionsValidatorTests` |
| AINATIVE-26-03 | The status and run surfaces require JWT read authorization. | `InAppAgentIntegrationTests.StatusRequiresAuthentication`; `EnabledRunRouteStillRequiresAuthentication` |
| AINATIVE-26-04 | The agent exposes only the existing read-only analytics capability through `AnalyticsQueryRunner`, with no direct DB or HTTP loopback. | `AiNativeCapabilityTests.McpAdaptersHaveNoDirectDatabaseOrEndpointTransportDependencies`; `AnalyticsAgentTool` |
| AINATIVE-26-05 | Agent capability execution validates the current authenticated actor through the canonical Task 25 claim path. | `AnalyticsAgentTool.QueryAnalyticsAsync`; Task 25 actor regression suite |
| AINATIVE-26-06 | The frontend uses a fresh MSAL API token per AG-UI run and never automatically replays a POST after 401. | `agentClient.test.ts` |
| AINATIVE-26-07 | The authenticated `/assistant` route shows no composer while disabled and mounts a read-only AG-UI conversation when enabled; hidden reasoning is not rendered. | `InAppAgentPage.test.tsx`; `router.test.tsx` |
| AINATIVE-26-08 | The REST status contract is TypeSpec-owned/generated, and all agent-stack versions are exact pins. | `AiNativeCapabilityTests.InAppAgentStackAndFrontendBridgeAreExactlyPinned`; contract-drift gate |

## Explicitly deferred

- Mutation tools, approval UX, and durable multi-step runs.
- Model-generated dashboards or arbitrary markup.
- A constrained `DashboardSpecification` renderer bakeoff (`json-render` candidate).
- OpenUI adoption; it remains a research candidate, not a dependency of this slice.
- Chain-of-thought storage or display. Operational telemetry may record run/tool status,
  latency, and errors, but not hidden reasoning.
