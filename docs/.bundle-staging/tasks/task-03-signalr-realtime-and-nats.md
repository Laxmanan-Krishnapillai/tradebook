# Task 03: SignalR Binary Push Protocol & NATS JetStream Outbox Processing Engine

- **Phase**: Real-Time Messaging & Streaming Engine
- **Lead / Owner**: Distributed Systems Architect / Real-Time Messaging Lead
- **Complexity**: High
- **Prerequisites**: Task 01 (PostgreSQL 17 Schema & `outbox_events` Table), Task 02 (.NET 9 Monolith Core)
- **Target Specification Path**: `tasks/task-03-signalr-realtime-and-nats.md`
- **Entity Model Reference**: `architecture/entity-model.md` — authoritative BioGem domain entity definitions
- **Target Implementation Files**:
  - `src/Backend/Tradebook.Infrastructure/Messaging/NatsJetStreamOptions.cs`
  - `src/Backend/Tradebook.Infrastructure/Messaging/NatsClientManager.cs`
  - `src/Backend/Tradebook.Infrastructure/Messaging/INatsPublisher.cs`
  - `src/Backend/Tradebook.Infrastructure/Messaging/NatsPublisher.cs`
  - `src/Backend/Tradebook.Infrastructure/BackgroundServices/OutboxProcessorWorker.cs`
  - `src/Backend/Tradebook.Infrastructure/Channels/BoundedMessageChannel.cs`
  - `src/Backend/Tradebook.Infrastructure/Channels/IChannelBackpressureStrategy.cs`
  - `src/Backend/Tradebook.Contracts/RealTime/MessagePackProtocol.cs`
  - `src/Backend/Tradebook.Contracts/RealTime/BioGemEventPayloads.cs`
  - `src/Backend/Tradebook.Api/Hubs/RealTimeTradebookHub.cs`
  - `src/Backend/Tradebook.Api/Hubs/ITradebookHubClient.cs`
  - `src/Backend/Tradebook.Api/Hubs/SignalRGroupManager.cs`
  - `src/Backend/Tradebook.Api/Configuration/SignalRConfigurationExtensions.cs`
  - `tests/Tradebook.IntegrationTests/Messaging/OutboxProcessorWorkerTests.cs`
  - `tests/Tradebook.IntegrationTests/RealTime/SignalRHubTests.cs`

---

## 1. Objectives, Scope, Dependencies & Prerequisites

### 1.1 Core Objectives
1. **Low-Latency Streaming Pipeline**: Sub-10ms end-to-end event push pipeline from PostgreSQL outbox writes to WebSocket-connected React 19 clients.
2. **Binary Wire Efficiency**: SignalR Core WebSockets integrate the **MessagePack** binary protocol (`Microsoft.AspNetCore.SignalR.Protocols.MessagePack`) for **60–70% payload size reduction** vs standard JSON serialization.
3. **Resilient Transactional Outbox Poller**: A multi-node safe background service (`OutboxProcessorWorker`) tails the PostgreSQL `outbox_events` table using `FOR UPDATE SKIP LOCKED` queries to publish events to NATS JetStream without duplicate delivery or lock contention.
4. **Non-Blocking Backpressure Engine**: A zero-allocation `System.Threading.Channels<T>` pipeline with bounded channel queues (`DropOldest` for `market_prices` index streams, `Wait` with async throttling for contract and delivery notifications) protects server memory during high-activity windows.
5. **BioGem Domain Group Topic Routing & Security**: Contract-level, balancing-group-level, GoO registry-level, and company-level group topic subscriptions with JWT claim validation and server-side keep-alive/ping-pong connection management.
6. **Salesforce Two-Way Sync via NATS JetStream**: Domain events route to Salesforce via `sf.outbound.*` NATS subjects (Tradebook → Salesforce); inbound events consume from `sf.inbound.*` subjects (Salesforce → Tradebook) — covering contracts, deliveries, invoices, counterparties, and GoO certificate transactions.

### 1.2 Domain Context: BioGem AS / BioGem AG

This task operates on the **BioGem renewable energy certificate and physical biomethane gas trading** domain. All entity references, NATS subjects, and SignalR group names reflect the authoritative entity model in [`architecture/entity-model.md`](../architecture/entity-model.md).

**Core BioGem entities emitting real-time events:**
- `contracts` — master bilateral trading agreements (`ARLA45.SC.2601.ETSS` naming convention)
- `physical_deliveries` — monthly physical gas delivery records per contract
- `capacity_bookings` — cross-border pipeline capacity reservations
- `goo_certificate_transactions` — GoO registry transactions from DENA (Germany) or AIB (International)
- `invoice_line_items` — financial invoice lines per delivery/month
- `market_prices` — TimescaleDB hypertable: TTF, EGSI ETF, BGO, FX rate time series (30-day chunks)

**Registry integration:**
- **DENA**: German registry for domestic biomethane certificates
- **AIB**: International hub for cross-border Guarantee of Origin (GoO) certificates
- Registry is identified by `goo_certificate_transactions.register` field (`'Dena'` or `'AIB'`)

### 1.3 Architectural Scope & Boundaries
- **In-Scope**:
  - ASP.NET Core SignalR Hub configuration with MessagePack binary protocol options.
  - Integration with `NATS.Client.Core` and `NATS.Client.JetStream` for distributed stream publishing and consumer management.
  - Tailing PostgreSQL 17 `outbox_events` table (`processed_at IS NULL`) with atomic batching.
  - Bounded `System.Threading.Channels.Channel<T>` for in-memory backpressure and subscriber dispatch.
  - Group join/leave authorization based on company claims (`company_id`).
  - SignalR connection lifecycle hooks (`OnConnectedAsync`, `OnDisconnectedAsync`), ping/pong intervals, and auto-reconnection parameters.
  - **Salesforce outbound NATS JetStream subjects** (`sf.outbound.*`) for pushing contract updates, invoice status, physical delivery confirmations, and GoO transaction status to Salesforce.
  - **Salesforce inbound NATS JetStream subjects** (`sf.inbound.*`) for consuming new contracts, counterparty updates, and GoO transaction status updates received from Salesforce.
- **Out-of-Scope**:
  - Direct client-side React UI rendering (covered in Task 05).
  - dbt Continuous Aggregate SQL definition (covered in Task 04).

### 1.4 Dependencies & Prerequisites
- **Prerequisites**:
  - **Task 01**: PostgreSQL 17 primary database setup with the `outbox_events` schema operational and BioGem domain migrations applied.
  - **Task 02**: .NET 9 FastEndpoints Backend solution structure and dependency injection setup.
- **NuGet Packages**:
  - `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` (v9.0.0+)
  - `MessagePack` (v2.5.0+)
  - `NATS.Client.Core` (v2.5.0+)
  - `NATS.Client.JetStream` (v2.5.0+)
  - `Npgsql` (v9.0.0+)
  - `Microsoft.Extensions.Hosting.Abstractions` (v9.0.0+)

---

## 2. SignalR Core WebSockets Hub & MessagePack Binary Protocol Serializer Configuration

### 2.1 Protocol Selection Rationale
BioGem's Tradebook platform streams market price index updates (TTF, EGSI ETF, BGO, FX rates), contract status changes, delivery confirmation events, and GoO registry transaction completions from DENA and AIB. Standard JSON WebSocket frames add significant CPU overhead from string formatting/parsing and consume excessive network bandwidth.

Tradebook standardizes on **MessagePack binary protocol** for SignalR:
- **Wire Payload Reduction**: Compact integer keys (`[Key(0)]`) and binary pack formats reduce payload sizes by up to 70%.
- **Zero-Allocation Deserialization**: MessagePack uses `ReadOnlySequence<byte>` and `ArraySegment<byte>` slices directly from WebSockets byte streams without allocating intermediate strings.

### 2.2 Hub Protocol & Pipeline Architecture

```
+---------------------------------------------------------------------------------------------------+
      OUTBOX & SIGNALR BINARY MESSAGEPACK REAL-TIME STREAMING PIPELINE — BioGem Domain              
+---------------------------------------------------------------------------------------------------+
                                                                                                    
   +-----------------------+           +-----------------------+          +----------------------+  
   |  PostgreSQL 17 DB     |           | OutboxProcessorWorker |          |  NATS JetStream      |  
   |                       |           | (BackgroundService)   |          |  Stream Broker       |  
   |  outbox_events table  |           |                       |          |                      |  
   |  (FOR UPDATE SKIP LOCK)──────────►| Batch Poller (100 ms) |─────────►| TRADEBOOK_EVENTS     |  
   +-----------------------+           +-----------------------+          | SF_OUTBOUND stream   |  
                                                                          +----------------------+  
                                                                                     |              
                                              ┌──────────────────────────────────────┘              
                                              │  Domain Events  +  SF Outbound Events               
                                              v                                                     
   +-----------------------+           +-----------------------+          +----------------------+  
   | React 19 Client       |           | RealTimeTradebookHub  |          | NATS Event Consumer  |  
   | (MessagePack WS Client|◄──────────| (SignalR Core Engine) |◄─────────| (Channel Pipeline)   |  
   |  Unpacks Binary DTO)  | WebSockets| Bounded Channel Push  |          | + SF Inbound Consumer|  
   +-----------------------+ Binary    +-----------------------+          +----------------------+  
                                                                                                    
   Salesforce Two-Way Sync (separate NATS subjects):                                                
   ┌─ TB → SF: sf.outbound.contracts  sf.outbound.deliveries  sf.outbound.invoices                 
   │           sf.outbound.goo_transactions                                                         
   └─ SF → TB: sf.inbound.contracts   sf.inbound.counterparties  sf.inbound.goo_transactions       
                                                                                                    
+---------------------------------------------------------------------------------------------------+
```

---

## 3. NATS JetStream Subject Taxonomy — BioGem Domain

### 3.1 Domain Event Subjects (`tradebook.events.*`)

All domain-generated events are published to the `TRADEBOOK_EVENTS` JetStream stream under the following subject hierarchy:

| Subject | Trigger | Description |
|---|---|---|
| `tradebook.events.contracts.created` | New contract saved | New bilateral contract created in Tradebook |
| `tradebook.events.contracts.updated` | Contract fields changed | Any field update on a `contracts` record (pricing, status, dates) |
| `tradebook.events.deliveries.confirmed` | Delivery status → CONFIRMED | Physical gas delivery confirmed for a supply month |
| `tradebook.events.deliveries.volume_updated` | Volume fields changed | `volume_realised_mwh`, `volume_corr1_mwh`, or `volume_corr2_mwh` updated |
| `tradebook.events.goo_transactions.processed` | GoO transaction completed | DENA or AIB registry transaction reaches Completed status |
| `tradebook.events.invoices.generated` | Invoice line item created | `invoice_line_items` record written after delivery confirmation |
| `tradebook.events.market_prices.updated` | New price row in hypertable | Daily TTF, EGSI ETF, BGO, FX rate row inserted into `market_prices` |

### 3.2 Salesforce Outbound Subjects — Tradebook → Salesforce (`sf.outbound.*`)

Events published by Tradebook that must be synced **to** Salesforce CRM. These flow through the `SF_OUTBOUND` JetStream stream and are consumed by the Salesforce integration adapter:

| Subject | Salesforce Target Object | Description |
|---|---|---|
| `sf.outbound.contracts` | `Contract__c` | Contract created or updated in Tradebook; pushed to SF |
| `sf.outbound.deliveries` | `Monthly_Quantity__c` | Physical delivery volume confirmed or corrected |
| `sf.outbound.invoices` | `Invoice__c` | Invoice generated or status changed (`invoice_status_enum`) |
| `sf.outbound.goo_transactions` | `Certificate_Transaction__c` | GoO transaction status update (DENA/AIB registry result) |

### 3.3 Salesforce Inbound Subjects — Salesforce → Tradebook (`sf.inbound.*`)

Events consumed **from** Salesforce that must be applied to the Tradebook database. These flow through the `SF_INBOUND` JetStream stream and are consumed by `SalesforceInboundConsumerWorker`:

| Subject | Salesforce Source Object | Description |
|---|---|---|
| `sf.inbound.contracts` | `Contract__c` | New contract initiated in Salesforce; pulled into Tradebook `contracts` table |
| `sf.inbound.counterparties` | `Account` | Salesforce Account updated; sync to Tradebook `counterparties` table |
| `sf.inbound.goo_transactions` | `Certificate_Transaction__c` | GoO transaction status updated in Salesforce (DENA via SF); applied to `goo_certificate_transactions` |

### 3.4 JetStream Stream Configuration

```
Stream: TRADEBOOK_EVENTS
  Subjects: tradebook.events.>
  Retention: WorkQueuePolicy
  MaxAge: 72h
  Replicas: 3

Stream: SF_OUTBOUND
  Subjects: sf.outbound.>
  Retention: WorkQueuePolicy
  MaxAge: 48h
  Replicas: 3
  DeliverPolicy: All (guaranteed at-least-once delivery to SF adapter)

Stream: SF_INBOUND
  Subjects: sf.inbound.>
  Retention: WorkQueuePolicy
  MaxAge: 48h
  Replicas: 3
  DeliverPolicy: All (guaranteed at-least-once processing from SF webhook relay)
```

---

## 4. SignalR Hub Group Naming Convention — BioGem Domain

### 4.1 Group Topic Design

BioGem's Tradebook uses granular group subscriptions aligned to the domain model. Clients subscribe to only the groups relevant to their trading context:

| Group Name | Scope | Triggered By |
|---|---|---|
| `contract:{contractId}` | Single contract | Any update, delivery confirmation, or invoice on a specific contract (UUID) |
| `balancing-group:{name}` | All contracts in a balancing group | Volume or delivery events for e.g. `balancing-group:NRGD` or `balancing-group:BGEM` |
| `market-prices` | All connected clients | New row inserted into `market_prices` TimescaleDB hypertable |
| `portfolio:{companyId}` | Company-level (BGEM AS / BGEM AG) | Aggregated P&L, invoice status, or capacity booking updates per BioGem legal entity |
| `goo-transactions:DENA` | All DENA registry subscribers | GoO transaction processed via the DENA (German) registry |
| `goo-transactions:AIB` | All AIB registry subscribers | GoO transaction processed via the AIB (International) registry |

### 4.2 Group Authorization Model

Unlike a multi-tenant SaaS, BioGem operates as a **single company group** (BGEM AS + BGEM AG). There is no tenant isolation requirement. Instead, group subscriptions are authorized by:
- **company_id claim**: Verifies the caller belongs to a recognized BioGem trading entity.
- **contract ownership**: The contract's `company_id` must match the caller's company.
- **balancing group membership**: Extracted from the caller's JWT roles or looked up from the `contracts` table.

---

## 5. System.Threading.Channels<T> Backpressure Handling & Bounded Channel Queue Strategy

### 5.1 The Backpressure Problem in BioGem Streaming Context
When daily market price updates arrive simultaneously with bulk delivery volume corrections (e.g. month-end reconciliation of 500 contracts) or when a GoO registry batch import processes hundreds of DENA transactions, pushing events synchronously to WebSocket client buffers can exhaust host RAM or cause unbounded task queues.

### 5.2 Bounded Channel Strategy (`System.Threading.Channels<T>`)
Tradebook implements a two-tier channel backpressure strategy:

1. **Loss-Tolerant High-Frequency Streams (Market Prices: TTF/EGSI ETF/BGO/FX Rates)**:
   - **Queue Policy**: `BoundedChannelFullMode.DropOldest`.
   - **Capacity**: Bounded to 1,000 messages per price-type channel.
   - **Behavior**: If the consumer cannot keep pace with rapid market price ingestion, older intermediate price rows are discarded. The client receives the latest index state without server memory leakage. Market price data can always be queried from the `market_prices` hypertable if a snapshot is needed.

2. **Loss-Intolerant Critical Domain Events (Contract Updates, Delivery Confirmations, GoO Transactions, Invoice Events)**:
   - **Queue Policy**: `BoundedChannelFullMode.Wait`.
   - **Capacity**: Bounded to 5,000 messages per company stream.
   - **Behavior**: If the channel buffer fills, the writer asynchronously yields (`WriteAsync`) until capacity frees up. This applies backpressure upstream to the NATS consumer, preserving exact event order without message loss for financially significant events.

---

## 6. NATS JetStream Transactional Outbox Processing Worker

### 6.1 Transactional Outbox Pattern Details
To achieve strict transactional consistency without expensive distributed 2PC (Two-Phase Commit) transactions, domain operations write business state mutations and event notifications atomically into a single PostgreSQL transaction:

```sql
BEGIN;
  -- BioGem domain write: confirm a physical gas delivery
  UPDATE physical_deliveries
  SET status = 'CONFIRMED', volume_realised_mwh = 12500.500000, updated_at = now()
  WHERE id = 'delivery-uuid-here';

  -- Atomic outbox event for NATS dispatch
  INSERT INTO outbox_events (event_id, aggregate_type, aggregate_id, event_type, payload)
  VALUES (
    gen_random_uuid(),
    'PHYSICAL_DELIVERY',
    'delivery-uuid-here',
    'DELIVERY_CONFIRMED',
    '{
      "contractId": "contract-uuid",
      "contractName": "ARLA45.SC.2601.ETSS",
      "supplyMonth": "2026-01-01",
      "volumeRealisedMwh": 12500.5,
      "balancingGroup": "NRGD"
    }'::jsonb
  );
COMMIT;
```

The same pattern applies for:
- `contracts` updates → `CONTRACTS` aggregate events → `tradebook.events.contracts.updated`
- `goo_certificate_transactions` completions → `GOO_TRANSACTION` events → `tradebook.events.goo_transactions.processed`
- `invoice_line_items` creation → `INVOICE` events → `tradebook.events.invoices.generated`
- `market_prices` row inserts → `MARKET_PRICE` events → `tradebook.events.market_prices.updated`

Salesforce sync events are also written atomically to the outbox with aggregate_type prefixed `SF_OUTBOUND_*`, then routed to `sf.outbound.*` NATS subjects by the `OutboxProcessorWorker`.

### 6.2 Tailing `outbox_events` safely across Multi-Node Clusters
The `OutboxProcessorWorker` runs as a hosted `.NET BackgroundService`. To support multi-replica deployment without duplicate event dispatch or database lock contention, the poller executes:

```sql
UPDATE outbox_events
SET processed_at = clock_timestamp()
WHERE event_id IN (
    SELECT event_id
    FROM outbox_events
    WHERE processed_at IS NULL
    ORDER BY created_at ASC
    LIMIT @BatchSize
    FOR UPDATE SKIP LOCKED
)
RETURNING event_id, aggregate_type, aggregate_id, event_type, payload, created_at;
```

- `FOR UPDATE SKIP LOCKED` guarantees that each replica worker locks distinct batches of unprocessed events.
- After fetching the batch, `OutboxProcessorWorker` routes each event to the correct NATS JetStream subject based on `aggregate_type` and `event_type`:
  - `PHYSICAL_DELIVERY` + `DELIVERY_CONFIRMED` → `tradebook.events.deliveries.confirmed` + `sf.outbound.deliveries`
  - `CONTRACT` + `CONTRACT_UPDATED` → `tradebook.events.contracts.updated` + `sf.outbound.contracts`
  - `GOO_TRANSACTION` + `TRANSACTION_COMPLETED` → `tradebook.events.goo_transactions.processed` + `sf.outbound.goo_transactions`
  - `INVOICE` + `INVOICE_GENERATED` → `tradebook.events.invoices.generated` + `sf.outbound.invoices`
  - `MARKET_PRICE` + `PRICE_UPDATED` → `tradebook.events.market_prices.updated` (no SF outbound for price data)
- If NATS publish succeeds, the updated `processed_at` timestamp is committed. If NATS is temporarily unavailable, the transaction rolls back, allowing retry on the next tick.

---

## 7. Connection Lifecycle, Group Topic Subscriptions & Keep-Alive

### 7.1 Connection Lifecycle & Group Topic Management
Clients connect to `RealTimeTradebookHub` via WebSockets. Upon connection, the Hub extracts company context from the validated JWT Principal (`Context.User`).

Group naming conventions (BioGem domain):
- **Company Group**: `portfolio:{companyId}` (receives aggregated company-level P&L updates, invoice status, and capacity booking events for BGEM AS or BGEM AG).
- **Contract Group**: `contract:{contractId}` (receives contract-specific delivery, invoice, and GoO transaction events for a specific `contracts.id`).
- **Balancing Group**: `balancing-group:{name}` (receives all volume and delivery events within a balancing group e.g. `NRGD`, `BGEM`).
- **Market Prices Group**: `market-prices` (receives all `market_prices` TimescaleDB hypertable insert events: TTF, EGSI ETF, BGO, FX rates).
- **GoO Registry Groups**: `goo-transactions:DENA` and `goo-transactions:AIB` (receives registry-specific GoO certificate transaction events).

#### Group Authorization Check
Before subscribing a connection to `contract:{contractId}`, the Hub verifies company ownership to prevent cross-entity information leakage:

```csharp
public async Task JoinContractGroup(Guid contractId)
{
    var companyId = GetCurrentCompanyId();
    var hasAccess = await _contractSecurityService.ValidateContractAccessAsync(companyId, contractId, Context.User);
    if (!hasAccess)
    {
        throw new HubException("Unauthorized: Company does not own or trade this contract.");
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, $"contract:{contractId}");
}
```

### 7.2 Client Ping/Pong Keep-Alive & Timeout Parameters
To avoid dead TCP sockets consuming server memory (e.g. abrupt client network disconnects), SignalR Core keep-alive parameters are strictly configured:

```csharp
options.ClientTimeoutInterval = TimeSpan.FromSeconds(30); // Max time without client ping before disconnect
options.HandshakeTimeout = TimeSpan.FromSeconds(15);      // Max WebSocket handshake completion time
options.KeepAliveInterval = TimeSpan.FromSeconds(15);     // Server ping interval to active clients
```

---

## 8. Step-by-Step Implementation Guide & C# Code Contracts

### Step 1: Define BioGem Domain MessagePack DTO Contracts (`BioGemEventPayloads.cs`)

Create `src/Backend/Tradebook.Contracts/RealTime/BioGemEventPayloads.cs`:

```csharp
using System;
using MessagePack;

namespace Tradebook.Contracts.RealTime;

/// <summary>
/// Generic outbox event envelope for all BioGem domain events.
/// Wraps domain-specific payloads for NATS JetStream dispatch and SignalR broadcast.
/// </summary>
[MessagePackObject]
public record DomainEventEnvelope(
    [Key(0)] Guid EventId,
    [Key(1)] string AggregateType,
    [Key(2)] string AggregateId,
    [Key(3)] string EventType,
    [Key(4)] byte[] SerializedPayload,
    [Key(5)] DateTime CreatedAtUtc
);

/// <summary>
/// Emitted when a contracts record is created or updated.
/// Subject: tradebook.events.contracts.{created|updated}
/// Also routed to: sf.outbound.contracts
/// </summary>
[MessagePackObject]
public record ContractUpdatedEvent(
    [Key(0)] Guid ContractId,
    [Key(1)] string ContractName,         // e.g. "ARLA45.SC.2601.ETSS"
    [Key(2)] string SfContractName,       // Salesforce contract name for two-way sync
    [Key(3)] string Action,               // "Buy" or "Sell" (action_enum)
    [Key(4)] string ProductType,          // "GoO", "Gas", "GoO+Gas", "Ticket" etc.
    [Key(5)] string BalancingGroup,       // e.g. "NRGD", "BGEM"
    [Key(6)] Guid CompanyId,             // BioGem entity: BGEM AS or BGEM AG
    [Key(7)] Guid CounterpartyId,
    [Key(8)] bool IsActive,
    [Key(9)] DateTime UpdatedAtUtc
);

/// <summary>
/// Emitted when a physical_deliveries record is confirmed or volume is updated.
/// Subject: tradebook.events.deliveries.confirmed | tradebook.events.deliveries.volume_updated
/// Also routed to: sf.outbound.deliveries (Monthly_Quantity__c)
/// </summary>
[MessagePackObject]
public record DeliveryConfirmedEvent(
    [Key(0)] Guid DeliveryId,
    [Key(1)] Guid ContractId,
    [Key(2)] string ContractName,         // Canonical contract name
    [Key(3)] DateTime SupplyMonth,        // First of month
    [Key(4)] string BalancingGroup,
    [Key(5)] decimal VolumeNominatedMwh,
    [Key(6)] decimal VolumeRealisedMwh,
    [Key(7)] decimal VolumeFinalMwh,      // physical_deliveries.volume_mwh (settled)
    [Key(8)] string Status,               // delivery_status_enum value
    [Key(9)] DateTime UpdatedAtUtc
);

/// <summary>
/// Emitted when a goo_certificate_transaction reaches Completed status.
/// Subject: tradebook.events.goo_transactions.processed
/// Also routed to: sf.outbound.goo_transactions (Certificate_Transaction__c)
/// Registry is either "Dena" (German) or "AIB" (International).
/// </summary>
[MessagePackObject]
public record GooTransactionProcessedEvent(
    [Key(0)] Guid TransactionId,
    [Key(1)] string SfTransactionId,      // Salesforce Certificate_Transaction__c.Id
    [Key(2)] string TransactionName,      // Registry name e.g. "7265-17552"
    [Key(3)] string Register,             // "Dena" or "AIB"
    [Key(4)] string BatchType,            // e.g. "Dena-Internal transaction"
    [Key(5)] decimal TransactionVolumeMwh,
    [Key(6)] string Status,               // transaction_status_enum value
    [Key(7)] Guid? ProducerContractId,    // FK to contracts (nullable)
    [Key(8)] Guid? CustomerContractId,    // FK to contracts (nullable)
    [Key(9)] string CountryOfProduction,  // ISO country code
    [Key(10)] DateTime UpdatedAtUtc
);

/// <summary>
/// Emitted when an invoice_line_items record is created or status changes.
/// Subject: tradebook.events.invoices.generated
/// Also routed to: sf.outbound.invoices (Invoice__c)
/// </summary>
[MessagePackObject]
public record InvoiceGeneratedEvent(
    [Key(0)] Guid InvoiceLineItemId,
    [Key(1)] Guid ContractId,
    [Key(2)] string ContractName,
    [Key(3)] DateTime SupplyMonth,
    [Key(4)] DateTime InvoiceDate,
    [Key(5)] DateTime PaymentDueDate,
    [Key(6)] decimal VolumeMwh,
    [Key(7)] decimal TotalEur,
    [Key(8)] decimal InvoicingAmountEur,
    [Key(9)] string Status,               // invoice_status_enum value
    [Key(10)] string SfInvoiceRef,        // Salesforce Invoice__c reference
    [Key(11)] DateTime CreatedAtUtc
);

/// <summary>
/// Emitted when a new row is inserted into the market_prices TimescaleDB hypertable.
/// Subject: tradebook.events.market_prices.updated
/// Contains all price indices for the given date: TTF, EGSI ETF, BGO, THE, FX rates.
/// No SF outbound — price data is sourced externally, not synced to Salesforce.
/// </summary>
[MessagePackObject]
public record MarketPriceUpdatedEvent(
    [Key(0)] DateTime PriceDate,          // market_prices.price_date (hypertable partition key)
    [Key(1)] decimal TtfEurMwh,           // TTF Day-Ahead
    [Key(2)] decimal EgsiEtfEurMwh,       // EGSI ETF index
    [Key(3)] decimal TheEurMwh,           // German hub THE
    [Key(4)] decimal BgoEurMwh,           // BGO index
    [Key(5)] decimal PgoEurMwh,           // PGO index
    [Key(6)] decimal EuaEurMwh,           // EU Allowances
    [Key(7)] decimal EurSek,              // FX rate EUR/SEK
    [Key(8)] decimal EurChf,              // FX rate EUR/CHF
    [Key(9)] decimal EurGbp,              // FX rate EUR/GBP
    [Key(10)] decimal EurUsd,             // FX rate EUR/USD
    [Key(11)] decimal EurDkk,             // FX rate EUR/DKK
    [Key(12)] DateTime CreatedAtUtc
);
```

---

### Step 2: Define Strongly-Typed SignalR Client Interface (`ITradebookHubClient.cs`)

Create `src/Backend/Tradebook.Api/Hubs/ITradebookHubClient.cs`:

```csharp
using System.Threading.Tasks;
using Tradebook.Contracts.RealTime;

namespace Tradebook.Api.Hubs;

public interface ITradebookHubClient
{
    /// <summary>Receive a contract create or update event.</summary>
    Task ReceiveContractUpdate(ContractUpdatedEvent payload);

    /// <summary>Receive a physical delivery confirmation or volume correction event.</summary>
    Task ReceiveDeliveryConfirmed(DeliveryConfirmedEvent payload);

    /// <summary>Receive a GoO certificate transaction completion (DENA or AIB registry).</summary>
    Task ReceiveGooTransactionProcessed(GooTransactionProcessedEvent payload);

    /// <summary>Receive an invoice line item generation or status change event.</summary>
    Task ReceiveInvoiceGenerated(InvoiceGeneratedEvent payload);

    /// <summary>Receive a new market price row (TTF, EGSI ETF, BGO, FX rates).</summary>
    Task ReceiveMarketPriceUpdate(MarketPriceUpdatedEvent payload);

    /// <summary>Receive a system notification (e.g. SF sync failure, registry timeout).</summary>
    Task ReceiveSystemNotification(string notificationType, string message);
}
```

---

### Step 3: Implement SignalR Core Real-Time Hub (`RealTimeTradebookHub.cs`)

Create `src/Backend/Tradebook.Api/Hubs/RealTimeTradebookHub.cs`:

```csharp
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Tradebook.Api.Hubs;

[Authorize]
public class RealTimeTradebookHub : Hub<ITradebookHubClient>
{
    private readonly ILogger<RealTimeTradebookHub> _logger;

    public RealTimeTradebookHub(ILogger<RealTimeTradebookHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var companyId = GetCompanyId();
        if (companyId != Guid.Empty)
        {
            // Auto-join the company-level portfolio group on connect
            var portfolioGroup = $"portfolio:{companyId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, portfolioGroup);
            _logger.LogInformation(
                "Connection {ConnectionId} bound to portfolio group {PortfolioGroup}",
                Context.ConnectionId, portfolioGroup);
        }

        // Auto-subscribe to market prices (all authenticated users receive price updates)
        await Groups.AddToGroupAsync(Context.ConnectionId, "market-prices");
        _logger.LogInformation("Connection {ConnectionId} subscribed to market-prices", Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(exception, "Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to events for a specific contract (by UUID).
    /// Hub verifies company ownership before adding to group.
    /// Group name: contract:{contractId}
    /// </summary>
    public async Task SubscribeContract(Guid contractId)
    {
        var groupName = $"contract:{contractId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} subscribed to {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task UnsubscribeContract(Guid contractId)
    {
        var groupName = $"contract:{contractId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} unsubscribed from {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Subscribe to all contract and delivery events within a named balancing group.
    /// Group name: balancing-group:{name}  (e.g. "balancing-group:NRGD", "balancing-group:BGEM")
    /// </summary>
    public async Task SubscribeBalancingGroup(string balancingGroupName)
    {
        var cleanName = balancingGroupName.Trim().ToUpperInvariant();
        var groupName = $"balancing-group:{cleanName}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} subscribed to {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task UnsubscribeBalancingGroup(string balancingGroupName)
    {
        var cleanName = balancingGroupName.Trim().ToUpperInvariant();
        var groupName = $"balancing-group:{cleanName}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Subscribe to GoO certificate transaction events for a specific registry.
    /// Group name: goo-transactions:DENA  or  goo-transactions:AIB
    /// </summary>
    public async Task SubscribeGooRegistry(string registry)
    {
        if (registry != "DENA" && registry != "AIB")
        {
            throw new HubException("Invalid registry. Must be 'DENA' or 'AIB'.");
        }
        var groupName = $"goo-transactions:{registry}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} subscribed to GoO registry group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task UnsubscribeGooRegistry(string registry)
    {
        if (registry != "DENA" && registry != "AIB")
        {
            throw new HubException("Invalid registry. Must be 'DENA' or 'AIB'.");
        }
        var groupName = $"goo-transactions:{registry}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    private Guid GetCompanyId()
    {
        var claim = Context.User?.FindFirst("company_id")?.Value
                 ?? Context.User?.FindFirst(ClaimTypes.GroupSid)?.Value;

        return Guid.TryParse(claim, out var companyId) ? companyId : Guid.Empty;
    }
}
```

---

### Step 4: Implement Bounded Channel Pipeline (`BoundedMessageChannel.cs`)

Create `src/Backend/Tradebook.Infrastructure/Channels/BoundedMessageChannel.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Tradebook.Contracts.RealTime;

namespace Tradebook.Infrastructure.Channels;

public interface IBoundedMessageChannel
{
    /// <summary>
    /// Publish a critical domain event (contract, delivery, GoO transaction, invoice).
    /// Uses Wait mode — no message loss permitted for financially significant events.
    /// </summary>
    ValueTask PublishDomainEventAsync(DomainEventEnvelope payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a market price update (TTF, EGSI ETF, BGO, FX rates).
    /// Uses DropOldest mode — stale intermediate price rows are discarded under backpressure.
    /// </summary>
    ValueTask PublishMarketPriceAsync(MarketPriceUpdatedEvent payload, CancellationToken cancellationToken = default);

    ChannelReader<DomainEventEnvelope> DomainEventReader { get; }
    ChannelReader<MarketPriceUpdatedEvent> MarketPriceReader { get; }
}

public class BoundedMessageChannel : IBoundedMessageChannel
{
    private readonly Channel<DomainEventEnvelope> _domainEventChannel;
    private readonly Channel<MarketPriceUpdatedEvent> _marketPriceChannel;

    public BoundedMessageChannel(int domainEventCapacity = 5000, int marketPriceCapacity = 1000)
    {
        // Critical domain events: Wait mode to prevent event loss under transient spikes
        // Covers: contract updates, delivery confirmations, GoO transactions, invoices
        var domainEventOptions = new BoundedChannelOptions(domainEventCapacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        };
        _domainEventChannel = Channel.CreateBounded<DomainEventEnvelope>(domainEventOptions);

        // Market price index streams (TTF/EGSI ETF/BGO/FX): DropOldest to bound memory
        // Clients can always re-query market_prices hypertable if a snapshot is needed
        var marketPriceOptions = new BoundedChannelOptions(marketPriceCapacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _marketPriceChannel = Channel.CreateBounded<MarketPriceUpdatedEvent>(marketPriceOptions);
    }

    public ValueTask PublishDomainEventAsync(DomainEventEnvelope payload, CancellationToken cancellationToken = default)
    {
        return _domainEventChannel.Writer.WriteAsync(payload, cancellationToken);
    }

    public ValueTask PublishMarketPriceAsync(MarketPriceUpdatedEvent payload, CancellationToken cancellationToken = default)
    {
        // TryWrite handles DropOldest inline without blocking execution threads
        if (!_marketPriceChannel.Writer.TryWrite(payload))
        {
            return _marketPriceChannel.Writer.WriteAsync(payload, cancellationToken);
        }
        return ValueTask.CompletedTask;
    }

    public ChannelReader<DomainEventEnvelope> DomainEventReader => _domainEventChannel.Reader;
    public ChannelReader<MarketPriceUpdatedEvent> MarketPriceReader => _marketPriceChannel.Reader;
}
```

---

### Step 5: Implement NATS JetStream Outbox Processing Background Service (`OutboxProcessorWorker.cs`)

Create `src/Backend/Tradebook.Infrastructure/BackgroundServices/OutboxProcessorWorker.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Tradebook.Contracts.RealTime;
using Tradebook.Infrastructure.Channels;

namespace Tradebook.Infrastructure.BackgroundServices;

/// <summary>
/// Polls the PostgreSQL outbox_events table and dispatches events to NATS JetStream
/// under the correct BioGem domain subjects, including Salesforce two-way sync subjects.
///
/// Domain event subjects:  tradebook.events.{aggregate}.{event}
/// SF Outbound subjects:   sf.outbound.{entity}   (Tradebook → Salesforce)
///
/// Inbound Salesforce events (sf.inbound.*) are handled by SalesforceInboundConsumerWorker.
/// </summary>
public class OutboxProcessorWorker : BackgroundService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly INatsConnection _natsConnection;
    private readonly IBoundedMessageChannel _messageChannel;
    private readonly ILogger<OutboxProcessorWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(100);
    private const int BatchSize = 100;

    // Maps aggregate_type + event_type → (domain subject, sf outbound subject or null)
    private static readonly Dictionary<string, (string DomainSubject, string? SfOutboundSubject)> SubjectMap = new()
    {
        ["CONTRACT:CONTRACT_CREATED"]             = ("tradebook.events.contracts.created",            "sf.outbound.contracts"),
        ["CONTRACT:CONTRACT_UPDATED"]             = ("tradebook.events.contracts.updated",            "sf.outbound.contracts"),
        ["PHYSICAL_DELIVERY:DELIVERY_CONFIRMED"]  = ("tradebook.events.deliveries.confirmed",         "sf.outbound.deliveries"),
        ["PHYSICAL_DELIVERY:VOLUME_UPDATED"]      = ("tradebook.events.deliveries.volume_updated",    "sf.outbound.deliveries"),
        ["GOO_TRANSACTION:TRANSACTION_COMPLETED"] = ("tradebook.events.goo_transactions.processed",   "sf.outbound.goo_transactions"),
        ["INVOICE:INVOICE_GENERATED"]             = ("tradebook.events.invoices.generated",           "sf.outbound.invoices"),
        ["INVOICE:INVOICE_STATUS_UPDATED"]        = ("tradebook.events.invoices.generated",           "sf.outbound.invoices"),
        ["MARKET_PRICE:PRICE_UPDATED"]            = ("tradebook.events.market_prices.updated",        null), // No SF sync for price data
    };

    public OutboxProcessorWorker(
        NpgsqlDataSource dataSource,
        INatsConnection natsConnection,
        IBoundedMessageChannel messageChannel,
        ILogger<OutboxProcessorWorker> logger)
    {
        _dataSource = dataSource;
        _natsConnection = natsConnection;
        _messageChannel = messageChannel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorWorker started. Tailing outbox_events table for BioGem domain events...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessOutboxBatchAsync(stoppingToken);
                if (processedCount == 0)
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events batch. Backing off 1s...");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessOutboxBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = @"
            UPDATE outbox_events
            SET processed_at = clock_timestamp()
            WHERE event_id IN (
                SELECT event_id
                FROM outbox_events
                WHERE processed_at IS NULL
                ORDER BY created_at ASC
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            RETURNING event_id, aggregate_type, aggregate_id, event_type, payload, created_at;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("BatchSize", BatchSize);

        var eventsToDispatch = new List<DomainEventEnvelope>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var eventId        = reader.GetGuid(0);
                var aggregateType  = reader.GetString(1);
                var aggregateId    = reader.GetString(2);
                var eventType      = reader.GetString(3);
                var payloadJson    = reader.GetString(4);
                var createdAt      = reader.GetDateTime(5);

                var rawPayloadBytes = Encoding.UTF8.GetBytes(payloadJson);

                eventsToDispatch.Add(new DomainEventEnvelope(
                    eventId,
                    aggregateType,
                    aggregateId,
                    eventType,
                    rawPayloadBytes,
                    createdAt
                ));
            }
        }

        if (eventsToDispatch.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var js = new NatsJSContext((NatsConnection)_natsConnection);

        foreach (var evt in eventsToDispatch)
        {
            var routingKey = $"{evt.AggregateType}:{evt.EventType}";

            if (!SubjectMap.TryGetValue(routingKey, out var subjects))
            {
                _logger.LogWarning(
                    "No NATS subject mapping found for aggregate_type={AggregateType}, event_type={EventType}. Skipping.",
                    evt.AggregateType, evt.EventType);
                continue;
            }

            // 1. Publish to domain event stream (tradebook.events.*)
            await js.PublishAsync(subjects.DomainSubject, evt, cancellationToken: cancellationToken);
            _logger.LogDebug("Published {EventType} to {Subject}", evt.EventType, subjects.DomainSubject);

            // 2. If SF outbound subject exists, also publish to Salesforce sync stream
            if (subjects.SfOutboundSubject is not null)
            {
                await js.PublishAsync(subjects.SfOutboundSubject, evt, cancellationToken: cancellationToken);
                _logger.LogDebug("Published {EventType} to SF outbound {Subject}", evt.EventType, subjects.SfOutboundSubject);
            }

            // 3. Push to local bounded channel pipeline for direct SignalR broadcast
            if (evt.AggregateType == "MARKET_PRICE")
            {
                // Market prices go to DropOldest channel
                var priceEvent = DeserializeMarketPriceEvent(evt.SerializedPayload);
                if (priceEvent is not null)
                    await _messageChannel.PublishMarketPriceAsync(priceEvent, cancellationToken);
            }
            else
            {
                // All other domain events go to Wait channel (no message loss)
                await _messageChannel.PublishDomainEventAsync(evt, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogDebug("Successfully processed and dispatched {Count} outbox events.", eventsToDispatch.Count);

        return eventsToDispatch.Count;
    }

    private static MarketPriceUpdatedEvent? DeserializeMarketPriceEvent(byte[] payload)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<MarketPriceUpdatedEvent>(payload);
        }
        catch
        {
            return null;
        }
    }
}
```

---

### Step 6: Configure DI & SignalR Startup Extensions (`SignalRConfigurationExtensions.cs`)

Create `src/Backend/Tradebook.Api/Configuration/SignalRConfigurationExtensions.cs`:

```csharp
using System;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Tradebook.Api.Hubs;
using Tradebook.Infrastructure.BackgroundServices;
using Tradebook.Infrastructure.Channels;

namespace Tradebook.Api.Configuration;

public static class SignalRConfigurationExtensions
{
    public static IServiceCollection AddRealTimeMessaging(this IServiceCollection services)
    {
        // Register bounded channels for BioGem domain event streams
        services.AddSingleton<IBoundedMessageChannel, BoundedMessageChannel>(sp =>
            new BoundedMessageChannel(domainEventCapacity: 5000, marketPriceCapacity: 1000));

        // Register Outbox Background Service (domain events + SF outbound routing)
        services.AddHostedService<OutboxProcessorWorker>();

        // Register SignalR Core with MessagePack Binary Protocol
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.MaximumReceiveMessageSize = 128 * 1024; // 128 KB
        })
        .AddMessagePackProtocol(options =>
        {
            options.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance)
                .WithSecurity(MessagePackSecurity.UntrustedData);
        });

        return services;
    }

    public static IApplicationBuilder MapRealTimeHubs(this WebApplication app)
    {
        app.MapHub<RealTimeTradebookHub>("/hubs/tradebook-stream");
        return app;
    }
}
```

---

## 9. Test Plan, Agent Verification Steps & Integrity Guardrails

### 9.1 Unit & Integration Test Plan

| Test ID | Test Target | Test Scenario | Expected Outcome |
| :--- | :--- | :--- | :--- |
| **UT-03-01** | `BoundedMessageChannel` | Write 1,500 `MarketPriceUpdatedEvent` items to channel bounded to 1,000 with `DropOldest`. | Channel accepts all writes without exception. Reader yields 1,000 items; older 500 stale price events are discarded. |
| **UT-03-02** | `BoundedMessageChannel` | Write 5,000 `DomainEventEnvelope` items to domain event channel bounded to 5,000 with `Wait`. Write 5,001st event. | Writer task yields until a reader reads an item. Zero message loss for contract/delivery/invoice/GoO events. |
| **IT-03-01** | `OutboxProcessorWorker` | Seed 500 unprocessed outbox records (`CONTRACT:CONTRACT_UPDATED`) into PostgreSQL. Run worker. | Worker processes 500 events in 5 batches of 100. `processed_at` populated. Events published to `tradebook.events.contracts.updated` AND `sf.outbound.contracts`. |
| **IT-03-02** | Multi-Replica Outbox Poller | Run 2 instances of `OutboxProcessorWorker` simultaneously against 1,000 outbox events. | `FOR UPDATE SKIP LOCKED` prevents lock contention. Exactly 1,000 total events processed without duplicate dispatch. |
| **IT-03-03** | `RealTimeTradebookHub` | Connect SignalR client using MessagePack protocol. Subscribe to `contract:{id}`. Publish `DeliveryConfirmedEvent`. | Client receives binary `DeliveryConfirmedEvent` message within <10ms with correct `ContractName`, `SupplyMonth`, and `VolumeFinalMwh`. |
| **IT-03-04** | GoO Registry Group Routing | Publish `GooTransactionProcessedEvent` with `Register = "DENA"`. Verify client subscribed to `goo-transactions:DENA` receives it; client subscribed to `goo-transactions:AIB` does not. | Group isolation confirmed. DENA event does not bleed to AIB group. |
| **IT-03-05** | SF Outbound NATS Routing | Seed one `INVOICE:INVOICE_GENERATED` outbox event. Run worker. | Event published to `tradebook.events.invoices.generated` AND `sf.outbound.invoices`. Verify NATS message headers contain `aggregate_type = INVOICE`. |
| **IT-03-06** | Market Prices Group Broadcast | All connected authenticated clients auto-join `market-prices` group on `OnConnectedAsync`. Insert new `market_prices` row. | `MarketPriceUpdatedEvent` received by all connected clients with correct TTF, EGSI ETF, BGO, FX rate values. |

---

### 9.2 Step-by-Step Agent Verification Commands

Subagents and automated auditors MUST execute the commands below to verify Task 03 implementation:

```bash
# Step 1: Start local PostgreSQL 17 and NATS JetStream container instances
docker compose up -d postgres nats

# Step 2: Build backend solution to verify MessagePack and SignalR contracts compile cleanly
dotnet build src/Backend/Tradebook.sln -c Release

# Step 3: Run Unit Tests for Bounded Channel Backpressure (BioGem domain channels)
dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj --filter "FullyQualifiedName~BoundedMessageChannel"

# Step 4: Run Integration Tests for Outbox Processor Worker with Testcontainers
dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj --filter "FullyQualifiedName~OutboxProcessorWorkerTests"

# Step 5: Run SignalR Hub Integration Tests (MessagePack Protocol & BioGem Group Subscriptions)
dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj --filter "FullyQualifiedName~SignalRHubTests"

# Step 6: Verify NATS JetStream streams are created with correct subject filters
nats stream ls
nats stream info TRADEBOOK_EVENTS
nats stream info SF_OUTBOUND
nats stream info SF_INBOUND
```

---

### 9.3 Anti-Cheating & Integrity Guardrails

To adhere strictly to the **Integrity Mandate**, all subagents implementing Task 03 must follow these strict rules:

1. **NO Hardcoded Test Returns**:
   - `OutboxProcessorWorker` MUST execute actual SQL queries against PostgreSQL `outbox_events` table and publish real byte payloads to NATS JetStream.
   - Creating dummy facade background services that return `Task.CompletedTask` or hardcoded event arrays is strictly prohibited.

2. **NO Mocking Away Database Transaction Locks**:
   - Integration tests MUST verify `FOR UPDATE SKIP LOCKED` behavior against a real PostgreSQL container.

3. **NO Substituting JSON for MessagePack**:
   - `AddMessagePackProtocol` and `[MessagePackObject]` attributes MUST be genuinely used in SignalR Hub registration and client DTO definitions.

4. **NO Unbounded Memory Queues**:
   - Backpressure channels MUST use `Channel.CreateBounded<T>` with explicit capacity limits. `Channel.CreateUnbounded<T>` is strictly forbidden for market streaming paths.

5. **NO Generic Entity Names**:
   - All code, SQL, and NATS subjects MUST use BioGem domain entity names: `contracts`, `physical_deliveries`, `goo_certificate_transactions`, `market_prices`, `invoice_line_items`. References to `trades`, `portfolios`, `market_ticks`, or `assets` are invalid.

6. **BOTH Salesforce Sync Directions Must Be Wired**:
   - `sf.outbound.*` subjects MUST be published by `OutboxProcessorWorker` for contract, delivery, invoice, and GoO transaction events.
   - `sf.inbound.*` subjects MUST be consumed by `SalesforceInboundConsumerWorker` (separate background service) and applied to the Tradebook database. Implementing only one direction is an integrity violation.

7. **DENA and AIB Must Both Be Handled**:
   - `goo-transactions:DENA` and `goo-transactions:AIB` are separate SignalR groups. Events MUST be routed to the correct group based on `GooTransactionProcessedEvent.Register`. A single catch-all group is not permitted.

---
*End of Task 03 Detailed Specification (`tasks/task-03-signalr-realtime-and-nats.md`).*
*Entity Model Reference: `architecture/entity-model.md`*
