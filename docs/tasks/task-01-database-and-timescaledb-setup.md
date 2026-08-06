# Task 01: PostgreSQL 17 Entity Model, Bi-Temporal Audit & Data Migration Setup

> **DESCOPE NOTICE (2026-08-06 — applied to this spec)** — per [`architecture/decision-log.md`](../architecture/decision-log.md) **D3**: TimescaleDB was cut. The extension, the `market_prices` hypertable, and all continuous aggregates/policies were removed — `market_prices` is a plain table (~1 row/day). Base image is plain `postgres:17`. `btree_gist` and the bi-temporal `audit_log` trigger design are kept. Mutable entities carry a `version BIGINT NOT NULL DEFAULT 1` column for optimistic concurrency (D5). Future tick-data path is native declarative partitioning, not Timescale.

## 1. Overview & Objectives

### 1.1 Executive Summary
Task 01 establishes the foundational storage layer for the Tradebook gas, GoO (Guarantee of Origin) and bioticket trading platform: the Excel-verified entity-model relational DDL, bi-temporal audit tracking, a plain `market_prices` daily market/FX index table, transactional outbox infrastructure, custom field extensibility, and the master-data migration pipeline from the 5 source workbooks.

PostgreSQL 17 is the single write authority and system of record. The **domain source of truth is `architecture/entity-model.md` (v2.0, Excel-verified)**; the canonical DDL lives in `architecture/master-architecture-blueprint.md` §3 and this task implements it as ordered migration files, extends it with contract-instance generation, joined reporting materialized views, and the Excel migration pipeline.

### 1.2 Core System Objectives
1. **Excel-Verified Relational Schema**: Provision production-grade DDL for `companies` (trading centers), `counterparties`, `trading_points`, `contracts`, `certificate_contracts`, `physical_deliveries`, `capacity_bookings`, `transfers`, `bioticket_deliveries`, `tax_tariffs`, `hedges`, `market_prices`, `capacity_price_indexes`, `goo_certificate_transactions`, `invoice_line_items`, and `external_cogs` — mirroring the Tradebook workbooks (`Tradebook_Physical.xlsm`, `Tradebook_Certificates_v2.xlsx`, `Tradebook_Bioticket.xlsx`, `Tradebook_Masterdata.xlsx`, `Tradebook_Reports_V2_SF_integration.xlsx`).
2. **Domain Enums**: All 18 verified enum types (`action_enum`, `product_type_enum`, `contract_type_enum`, `segment_enum`, `client_type_enum`, `goo_quality_enum`, `subsidy_status_enum`, `price_mech_enum`, `gas_price_mech_enum`, `capacity_price_mech_enum`, `delivery_type_enum`, `invoicing_mech_enum`, `payment_mech_enum`, `book_type_enum`, `report_status_enum`, `transaction_status_enum`, `point_type_enum`) as first-class `CREATE TYPE` definitions.
3. **Contract Instance Generation**: Canonical contract naming (`{CounterpartyShorthand}{CountryDialCode}.{ContractTypeCode}.{YYmm}.{QualityCode}{SubsidyFlag}`) and instance naming (`{ContractName}-{DeliveryMonthNo}-{Year}`, e.g. `BFEX45.BT.2301.CO2E-9-2023`) enforced by a PL/pgSQL generator used by all delivery books.
4. **Bi-Temporal Audit Trail**: Native bi-temporal tracking ($V_t$ valid time and $S_t$ system time) with `btree_gist` composite exclusion constraints and a generic audit trigger for every audited entity table.
5. **Time-Travel SQL API**: Deterministic `get_entity_state_as_of(entity_name, entity_id, system_time, valid_time)` reconstruction function (single-tenant group — no `tenant_id`).
6. **Time-Series Approach (D3)**: `market_prices` as a plain table keyed on `price_date` (daily TTF/EGSI ETF/THE/BGO/PGO/EUA/within-day indexes + EUR→SEK/CHF/GBP/USD/DKK FX, ~1 row/day) with a plain-SQL monthly averages VIEW; native declarative partitioning is the future path if intraday ticks ever arrive.
7. **Transactional Outbox Resilience**: `outbox_events` with a `sequence_id BIGSERIAL` dispatch cursor and a `pg_notify` wake-up trigger, consumed by Task 03's in-process dispatcher; event rows are written by the application inside each mutation transaction.
8. **Master-Data Migration**: Repeatable seed + import pipeline (VAT rates, SEK-denominated tariffs, FX conversion via `market_prices.eur_sek`) from the source Excel workbooks into the entity model.

---

## 2. Prerequisites, Scope & Dependencies

### 2.1 Prerequisites
- **Database Engine**: PostgreSQL 17.0+ (x86_64 or arm64).
- **Extensions**: `btree_gist` only — `gen_random_uuid()` is built into PostgreSQL 17.
- **Host Tools**: `psql` (v17+), Docker 24.0+ / Docker Compose v2.20+, .NET 9 SDK, Python 3.13 + `openpyxl` (Excel migration runner only).

### 2.2 Scope Boundaries
- **In-Scope**:
  - Ordered entity-model migration scripts implementing blueprint §3 DDL.
  - Contract instance generation function + `UK` constraints (`uk_contract_instance`, `uk_capacity_instance`, `uk_transfer_instance`, `uk_bioticket_instance`).
  - Bi-temporal audit trigger system and time-travel function.
  - Transactional outbox schema (`sequence_id` dispatch cursor + NOTIFY wake-up trigger).
  - Plain `market_prices` daily index table + monthly averages VIEW; joined reporting materialized views.
  - Master-data seed + Excel import migration (companies, counterparties, contracts, enums, VAT/SEK/FX).
  - Automated unit, integration, and performance verification suites.
- **Out-of-Scope**: Outbox dispatch and SignalR fan-out (Task 03's in-process dispatcher), web visualization of audit diffs (Task 06), S3 Parquet export (Task 04), Salesforce push integration (Task 10).

### 2.3 Dependency Graph
```
┌─────────────────────────────────────────────────────────┐
│            Task 01 Storage & Entity Foundation          │
│ PostgreSQL 17, Excel-Verified Entity Model DDL           │
└────────────────────────────┬────────────────────────────┘
                             │
     ┌───────────────────────┼───────────────────────┐
     ▼                       ▼                       ▼
┌───────────────┐     ┌───────────────┐     ┌───────────────┐
│    Task 02    │     │    Task 03    │     │    Task 04    │
│ .NET 9 Slices │     │ SignalR Push  │     │ Semantic Data │
│ (entity DTOs) │     │ Outbox Worker │     │ Pipeline Layer│
└───────────────┘     └───────────────┘     └───────────────┘
```

---

## 3. Entity-Model DDL Migration Specification

The master DDL is organized into 10 ordered migration stages. **Canonical table definitions are the blueprint §3 code fence (`architecture/master-architecture-blueprint.md` §3), which is generated from `architecture/entity-model.md` (v2.0).** Migration files below implement it verbatim; do not invent tables, columns, or enum values outside the entity model.

**Optimistic concurrency (D5)**: every mutable entity table (master data, contracts, all four delivery books, `tax_tariffs`, `hedges`, `market_prices`, registry/financial tables) carries `version BIGINT NOT NULL DEFAULT 1`. The API update contract is `UPDATE ... SET ..., version = version + 1 WHERE id = $1 AND version = $2`; zero affected rows → HTTP 409 returning the current server state (Task 02). The DDL excerpts below show the column on the tables they spell out; the blueprint §3 canonical DDL carries it on every mutable table.

### 3.1 Migration File Map

| # | Migration File | Blueprint §3 Section | Entities |
| :--- | :--- | :--- | :--- |
| 001 | `001_extensions_and_enums.sql` | §3 §0 | Extensions + all 18 enums |
| 002 | `002_master_data.sql` | §3 §1 | `companies`, `counterparties`, `trading_points` |
| 003 | `003_contracts.sql` | §3 §2 | `contracts`, `certificate_contracts`, instance-generation function |
| 004 | `004_delivery_books.sql` | §3 §3 | `physical_deliveries`, `capacity_bookings`, `transfers`, `bioticket_deliveries`, `tax_tariffs`, `hedges` |
| 005 | `005_market_prices.sql` | §3 §4 | `market_prices` (plain table + monthly VIEW), `capacity_price_indexes`, joined matviews |
| 006 | `006_financial_registry.sql` | §3 §5 | `goo_certificate_transactions`, `invoice_line_items`, `external_cogs` |
| 007 | `007_bitemporal_audit_and_outbox.sql` | §3 §6 | `audit_log`, `outbox_events`, audit + outbox triggers |
| 008 | `008_custom_fields_semantic_models.sql` | §3 §7 | `custom_field_definitions`, `semantic_models` |
| 009 | `009_time_travel_recovery.sql` | §3 §8 | `get_entity_state_as_of` |
| 010 | `010_seed_and_excel_import.sql` | — | Master-data seed + Excel import staging |

### 3.2 Extensions & Domain Enums (`001_extensions_and_enums.sql`)

```sql
-- btree_gist is the ONLY required extension (D3); gen_random_uuid() is built into PostgreSQL 17.
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- All enum values verified from source Excel workbooks (entity-model.md §4)
CREATE TYPE action_enum AS ENUM ('Buy', 'Sell', 'Intercompany', 'Swap');
CREATE TYPE product_type_enum AS ENUM ('GoO', 'Gas', 'GoO+Gas', 'GoO+Gas+Shipping', 'Tickets');
CREATE TYPE contract_type_enum AS ENUM ('External', 'Intercompany');
CREATE TYPE segment_enum AS ENUM ('Utilities', 'Transport', 'Traders', 'Producers', 'Industry', 'Intercompany', 'Public', 'Storage', 'Market', 'OTC', 'Plant', 'Other');
CREATE TYPE client_type_enum AS ENUM ('End Consumer', 'Traders', 'Intercompany', 'Energinet Balgas', 'Storage');
CREATE TYPE goo_quality_enum AS ENUM ('RED', 'ETS', 'OZD', 'NMS', 'EWG', 'ISCC', 'NOQ', 'GEG', 'RTFO', 'BHG');
CREATE TYPE subsidy_status_enum AS ENUM ('SUB', 'UNS', 'None');
CREATE TYPE price_mech_enum AS ENUM ('FIXED', 'VARIABLE');
CREATE TYPE gas_price_mech_enum AS ENUM ('FIXED', 'VARIABLE', 'EGSI ETF', 'TTF', 'WITHIN-DAY MKT', 'BGO', 'PGO', 'THE');
CREATE TYPE capacity_price_mech_enum AS ENUM ('GTF/THE - Yearly', 'GTF/THE - Monthly', 'THE/GTF - Yearly', 'THE/GTF - Monthly');
CREATE TYPE delivery_type_enum AS ENUM ('Fixed', 'Variable');
CREATE TYPE invoicing_mech_enum AS ENUM ('Weekdays', 'Calender day', 'Running month + X');
CREATE TYPE payment_mech_enum AS ENUM ('Weekdays', 'Calender day');
CREATE TYPE book_type_enum AS ENUM ('Sourcing', 'Sales', 'Intercompany');
CREATE TYPE report_status_enum AS ENUM ('Completed - Payment Received/Sent', 'In Progress - Invoice Received/Sent', 'Pending - No Invoice', 'Cancelled', 'Awaiting', 'Issue');
CREATE TYPE transaction_status_enum AS ENUM ('Latest transaction', 'Batch export requested', 'Processing', 'Completed', 'Failed');
CREATE TYPE point_type_enum AS ENUM ('ENTRY', 'EXIT', 'VIRTUAL');
```

> The `S`/`U` suffix on quality codes (`REDS`/`REDU`, `OZDS`/`OZDU`, `NOQS`/`NOQU`, …) MUST be kept consistent with `subsidy_status` (`SUB`/`UNS`) — the suffix is derived from the status, never the reverse (entity-model.md §5).

### 3.3 Master Data (`002_master_data.sql`)

`companies` holds the internal trading centers (BioGem entities) referenced as `contracts.sourcing_center` / `contracts.sales_center`; `counterparties` holds external partners with VAT applicability; `trading_points` holds the verified gas network hubs (`GTF`, `ETF`, `JEZ`/`JBZ`, `THE`, `RES`, `CSP`, `GVM`, `OEP`).

```sql
CREATE TABLE companies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    shorthand VARCHAR(10) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    country_code CHAR(2),
    country_dial_code SMALLINT,
    vat_rate NUMERIC(5,4),
    default_currency CHAR(3),
    is_active BOOLEAN NOT NULL DEFAULT true,
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE counterparties (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) UNIQUE NOT NULL,
    shorthand VARCHAR(20) UNIQUE NOT NULL,
    segment segment_enum,
    country_code CHAR(2),
    country_dial_code SMALLINT,
    vat_applicable BOOLEAN NOT NULL DEFAULT false,
    salesforce_account_id VARCHAR(50),
    review_note TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE trading_points (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) UNIQUE NOT NULL,
    type point_type_enum NOT NULL,
    description VARCHAR(200),
    country VARCHAR(100),
    action VARCHAR(100),
    name VARCHAR(100),
    start_area VARCHAR(20),
    end_area VARCHAR(20),
    version BIGINT NOT NULL DEFAULT 1
);
```

### 3.4 Contracts & Certificate Extension (`003_contracts.sql`)

`contracts` is the master trading contract table (80+ columns covering price mechanisms per product line, invoicing/payment mechanics, and Salesforce mapping). The canonical full definition is blueprint §3 §2. Key structural guarantees:

```sql
CREATE TABLE contracts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contract_name VARCHAR(100) UNIQUE NOT NULL,
    company_shorthand VARCHAR(10),
    country_code CHAR(2),
    country_dial_code SMALLINT,
    contract_number SMALLINT,
    year_of_contract SMALLINT,
    counterparty_id UUID NOT NULL REFERENCES counterparties(id),
    contract_suffix VARCHAR(20),
    sourcing_center UUID REFERENCES companies(id),
    sales_center UUID REFERENCES companies(id),
    balancing_group VARCHAR(50),
    network_point VARCHAR(50),
    receiving_shipper VARCHAR(50),
    external_contract_ref VARCHAR(100),
    product_type product_type_enum NOT NULL,
    action action_enum NOT NULL,
    goo_quality goo_quality_enum,
    feedstock_quality VARCHAR(100),
    gas_quality VARCHAR(50),
    subsidy_status subsidy_status_enum,
    subsidy_type VARCHAR(50),
    vat_type VARCHAR(50),
    counterparty_segment segment_enum,
    signing_date DATE,
    price_mechanism_goo price_mech_enum,
    fixed_price_goo_eur_mwh NUMERIC(12,6),
    broker_fee_eur_mwh NUMERIC(12,6),
    price_mechanism_gas gas_price_mech_enum,
    fixed_price_gas_eur_mwh NUMERIC(12,6),
    price_mechanism_ticket price_mech_enum,
    fixed_price_ticket_eur_ton NUMERIC(12,6),
    invoicing_mechanism invoicing_mech_enum,
    payment_mechanism payment_mech_enum,
    days_to_invoice_after_delivery SMALLINT,
    days_to_payment_after_invoice SMALLINT,
    delivery_type delivery_type_enum,
    campaign VARCHAR(100),
    certification_quality VARCHAR(100),
    includes_goo BOOLEAN NOT NULL DEFAULT false,
    includes_gas BOOLEAN NOT NULL DEFAULT false,
    includes_ticket BOOLEAN NOT NULL DEFAULT false,
    contract_type contract_type_enum NOT NULL DEFAULT 'External',
    comment TEXT,
    sf_contract_name VARCHAR(100),
    old_contract_name VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT true,
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX idx_contracts_counterparty ON contracts(counterparty_id);
CREATE INDEX idx_contracts_product ON contracts(product_type);
CREATE INDEX idx_contracts_sourcing_center ON contracts(sourcing_center);
CREATE INDEX idx_contracts_sales_center ON contracts(sales_center);

CREATE TABLE certificate_contracts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contract_id UUID NOT NULL UNIQUE REFERENCES contracts(id) ON DELETE CASCADE,
    goo_quality goo_quality_enum,
    feedstock_quality VARCHAR(100),
    certification_quality VARCHAR(100),
    customer_segment segment_enum,
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);
```

**Contract Instance Generation** — implements the canonical instance format `{ContractName}-{DeliveryMonthNo}-{Year}`:

```sql
-- Migration File: src/Database/Functions/fn_generate_contract_instance.sql
CREATE OR REPLACE FUNCTION fn_generate_contract_instance(
    p_contract_name VARCHAR,
    p_supply_month DATE
)
RETURNS VARCHAR AS $$
SELECT p_contract_name || '-' || to_char(p_supply_month, 'FMMM') || '-' || to_char(p_supply_month, 'YYYY');
$$ LANGUAGE sql IMMUTABLE;

-- Usage: SELECT fn_generate_contract_instance('BFEX45.BT.2301.CO2E', '2023-09-01');
-- => 'BFEX45.BT.2301.CO2E-9-2023'
```

### 3.5 Delivery, Capacity & Transfer Books (`004_delivery_books.sql`)

The four book tables are implemented verbatim from blueprint §3 §3 and enforce instance uniqueness via exclusion on the generated instance ID:

| Table | Uniqueness Constraint | Key Columns (non-exhaustive) |
| :--- | :--- | :--- |
| `physical_deliveries` | `uk_contract_instance (contract_id, contract_instance_id, book_type)` | `book_type`, `supply_month`, `status`, `volume_nominated_mwh`, `volume_realised_mwh`, `volume_corr1/2_mwh`, `volume_intercompany_mwh`, `price_mechanism`, `start/end_day`, `start/end_hour`, `cost/revenue/handling_fee/broker_fee/tax/tso/dso/adm/bal/shipping` amounts, `subtotal_eur`, `vat_pct`, `vat_eur`, `invoice_amount_eur`, `client_type`, `document_no`, `invoice_date`, `payment_date*`, `bilagsdato`, `payment_diff_days` |
| `capacity_bookings` | `uk_capacity_instance (contract_id, contract_instance_id)` | `supply_month`, `balancing_group`, `price_mechanism` (`capacity_price_mech_enum`), `start/end_area`, `ship_fix`, `border_point`, `hours`, `capacity_mw`, `capacity_price_eur_mwh`, `capacity_cost_eur`, `weighted_cost_eur`, invoicing/payment mechanics |
| `transfers` | `uk_transfer_instance (contract_id, contract_instance_id)` | `booked_capacity_mw`, `volume_mwh`, `transfer_balancing_effect`, `commitment_date`, `transport_cost_eur_mwh`, `capacity_cost_eur_mwh`, `match_mwh`, `match_eur`, `corr1/2/3_mwh` |
| `bioticket_deliveries` | `uk_bioticket_instance (contract_id, contract_instance_id, book_type)` | `contract_month`, `volume_nominated_ton`, `volume_realised_ton`, `volume_ton`, `cost_eur_ton`, `revenue_eur`, `vat_eur`, `invoice_amount_eur`, `product` (default `'Tickets'`) |

`tax_tariffs` holds per-contract, per-period SEK-denominated tariffs (`tax_local_cur_mwh`, `tso_local_cur_mwh`, `dso_local_cur_mwh`, `dso_tariff_local_cur_day`, `adm_fee_local_cur_mwh`, `bal_fee_local_cur_mwh`, `currency CHAR(3) DEFAULT 'SEK'`) with `uk_tax_period (contract_id, period_start, period_end)`. `hedges` records `hedge_amount_mwh` / `hedge_price_eur_mwh` per `(contract_id, month)`.

### 3.6 Market Prices & Joined Materialized Views (`005_market_prices.sql`)

`market_prices` is a plain PostgreSQL table (D3): one wide end-of-day row per day, ~11k rows over thirty years. No partitioning, no aggregate infrastructure — a plain-SQL monthly averages VIEW covers reporting.

```sql
CREATE TABLE market_prices (
    price_date DATE PRIMARY KEY,
    ttf_eur_mwh NUMERIC(12,6),
    egsi_etf_eur_mwh NUMERIC(12,6),
    the_eur_mwh NUMERIC(12,6),
    bgo_eur_mwh NUMERIC(12,6),
    pgo_eur_mwh NUMERIC(12,6),
    eua_eur_mwh NUMERIC(12,6),
    within_day_mkt_eur_mwh NUMERIC(12,6),
    eur_sek NUMERIC(12,6),
    eur_chf NUMERIC(12,6),
    eur_gbp NUMERIC(12,6),
    eur_usd NUMERIC(12,6),
    eur_dkk NUMERIC(12,6),
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

-- Monthly index averages (plain SQL view — drives TTF/EGSI/THE/FX benchmarks)
CREATE VIEW market_prices_monthly AS
SELECT
    date_trunc('month', price_date)::date AS month,
    AVG(ttf_eur_mwh) AS avg_ttf_eur_mwh,
    AVG(egsi_etf_eur_mwh) AS avg_egsi_etf_eur_mwh,
    AVG(the_eur_mwh) AS avg_the_eur_mwh,
    AVG(eur_sek) AS avg_eur_sek,
    AVG(eur_chf) AS avg_eur_chf,
    AVG(eur_dkk) AS avg_eur_dkk
FROM market_prices
GROUP BY date_trunc('month', price_date);
```

`capacity_price_indexes` stores period-based capacity prices (mechanism per `capacity_price_mech_enum`):

```sql
CREATE TABLE capacity_price_indexes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    mechanism capacity_price_mech_enum NOT NULL,
    price_eur_mwh NUMERIC(12,6) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX idx_capacity_price_indexes ON capacity_price_indexes(mechanism, period_start, period_end);
```

**Joined Reporting Materialized View** — bridges delivery books to contracts/counterparties for monthly P&L (this is the join view the analytics layer and reports workbook consume):

```sql
CREATE MATERIALIZED VIEW delivery_monthly_summary AS
SELECT
    pd.supply_month,
    c.contract_name,
    c.product_type,
    c.action,
    cp.name AS counterparty_name,
    cp.segment AS counterparty_segment,
    pd.book_type,
    pd.status,
    SUM(COALESCE(pd.volume_mwh, 0)) AS volume_mwh,
    SUM(COALESCE(pd.revenue_eur, 0)) AS revenue_eur,
    SUM(COALESCE(pd.tax_eur, 0)) AS tax_eur,
    SUM(COALESCE(pd.tso_tariff_eur, 0)) AS tso_tariff_eur,
    SUM(COALESCE(pd.dso_tariff_eur, 0)) AS dso_tariff_eur,
    SUM(COALESCE(pd.vat_eur, 0)) AS vat_eur,
    SUM(COALESCE(pd.invoice_amount_eur, 0)) AS invoice_amount_eur
FROM physical_deliveries pd
JOIN contracts c ON c.id = pd.contract_id
LEFT JOIN counterparties cp ON cp.id = c.counterparty_id
GROUP BY pd.supply_month, c.contract_name, c.product_type, c.action,
         cp.name, cp.segment, pd.book_type, pd.status;

CREATE UNIQUE INDEX uk_delivery_monthly_summary
    ON delivery_monthly_summary (supply_month, contract_name, product_type, action, book_type, status);
```

### 3.7 Financial & Registry Entities (`006_financial_registry.sql`)

Implemented verbatim from blueprint §3 §5: `goo_certificate_transactions` (registry transfers with producer/customer contract FKs and `transaction_status_enum`), `invoice_line_items` (financial lines with polymorphic FKs to `physical_deliveries` / `capacity_bookings` / `transfers` / `bioticket_deliveries`, `status report_status_enum`, `sf_invoice_ref`), and `external_cogs` (monthly `cogs_eur` from sales↔purchase contract pairing).

### 3.8 Bi-Temporal Audit Log & Transactional Outbox (`007_bitemporal_audit_and_outbox.sql`)

```sql
CREATE TABLE audit_log (
    audit_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_name VARCHAR(128) NOT NULL,
    entity_id VARCHAR(128) NOT NULL,
    actor_id UUID NOT NULL,
    operation VARCHAR(16) NOT NULL CHECK (operation IN ('INSERT', 'UPDATE', 'DELETE', 'REVERT', 'MERGE')),
    system_time TSTZRANGE NOT NULL DEFAULT tstzrange(clock_timestamp(), NULL, '[)'),
    valid_time TSTZRANGE NOT NULL,
    pre_state JSONB,
    post_state JSONB,
    diff_patch JSONB NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    vector_timestamp JSONB NOT NULL DEFAULT '{}'::jsonb,
    commit_hash VARCHAR(64) NOT NULL,
    parent_commit_hash VARCHAR(64),
    EXCLUDE USING gist (
        entity_name WITH =,
        entity_id WITH =,
        system_time WITH &&,
        valid_time WITH &&
    )
);

CREATE INDEX idx_audit_composite ON audit_log (entity_name, entity_id, lower(system_time) DESC);
CREATE INDEX idx_audit_system_time_gist ON audit_log USING gist (system_time);
CREATE INDEX idx_audit_valid_time_gist ON audit_log USING gist (valid_time);
CREATE INDEX idx_audit_commit_hash ON audit_log (commit_hash);

CREATE TABLE outbox_events (
    event_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- Monotonic dispatch/catch-up cursor (Task 03). UUIDs are not orderable; this is.
    sequence_id BIGSERIAL NOT NULL UNIQUE,
    aggregate_type VARCHAR(128) NOT NULL,
    aggregate_id VARCHAR(128) NOT NULL,
    event_type VARCHAR(128) NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    processed_at TIMESTAMPTZ
);
CREATE INDEX idx_outbox_unprocessed ON outbox_events(created_at) WHERE processed_at IS NULL;

-- Wake Task 03's in-process dispatcher without polling. Task 01 OWNS this DDL (task-03 §1).
CREATE OR REPLACE FUNCTION notify_outbox_new_event() RETURNS trigger AS $$
BEGIN
  PERFORM pg_notify('outbox_new_event', NEW.event_id::text);
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_outbox_notify
AFTER INSERT ON outbox_events
FOR EACH ROW EXECUTE FUNCTION notify_outbox_new_event();
```

> **`aggregate_type` contract**: values are **PascalCase entity names** — `PhysicalDelivery`, `Contract`, `CapacityBooking`, `GooCertificateTransaction`, `Hedge`, `MarketPrice` — exactly matching Task 03's SignalR group whitelist. Task 02's mutation endpoints write these exact strings.

**Generic Bi-Temporal Audit Trigger** (single-tenant, entity-id based; actor comes from the `app.actor_id` session setting):

```sql
-- Migration File: src/Database/Functions/fn_bi_temporal_audit_trigger.sql
CREATE OR REPLACE FUNCTION fn_bi_temporal_audit_trigger()
RETURNS TRIGGER AS $$
DECLARE
    v_entity_id VARCHAR(128);
    v_actor_id UUID;
    v_op VARCHAR(16);
    v_pre_state JSONB := NULL;
    v_post_state JSONB := NULL;
    v_diff_patch JSONB := '[]'::jsonb;
    v_valid_time TSTZRANGE;
    v_commit_hash VARCHAR(64);
BEGIN
    v_entity_id := COALESCE(NEW.id::text, OLD.id::text);
    v_actor_id := COALESCE(
        NULLIF(current_setting('app.actor_id', true), '')::UUID,
        '00000000-0000-0000-0000-000000000000'::UUID
    );

    IF (TG_OP = 'INSERT') THEN
        v_op := 'INSERT';
        v_post_state := to_jsonb(NEW);
    ELSIF (TG_OP = 'UPDATE') THEN
        v_op := 'UPDATE';
        v_pre_state := to_jsonb(OLD);
        v_post_state := to_jsonb(NEW);
        SELECT jsonb_agg(jsonb_build_object('op','replace','path','/'||key,'value',value))
        INTO v_diff_patch
        FROM jsonb_each(v_post_state)
        WHERE v_pre_state->key IS DISTINCT FROM value;
        IF v_diff_patch IS NULL THEN v_diff_patch := '[]'::jsonb; END IF;
        UPDATE audit_log
        SET system_time = tstzrange(lower(system_time), clock_timestamp(), '[)')
        WHERE entity_name = TG_TABLE_NAME AND entity_id = v_entity_id AND upper_inf(system_time);
    ELSE
        v_op := 'DELETE';
        v_pre_state := to_jsonb(OLD);
        UPDATE audit_log
        SET system_time = tstzrange(lower(system_time), clock_timestamp(), '[)')
        WHERE entity_name = TG_TABLE_NAME AND entity_id = v_entity_id AND upper_inf(system_time);
    END IF;

    -- Valid time defaults to the business period of the row when present
    v_valid_time := tstzrange(COALESCE(
        (COALESCE(v_post_state, v_pre_state)->>'supply_month')::TIMESTAMPTZ,
        (COALESCE(v_post_state, v_pre_state)->>'contract_month')::TIMESTAMPTZ,
        (COALESCE(v_post_state, v_pre_state)->>'price_date')::TIMESTAMPTZ,
        (COALESCE(v_post_state, v_pre_state)->>'month')::TIMESTAMPTZ,
        clock_timestamp()
    ), NULL, '[)');

    -- Built-in sha256(bytea) (PG11+) — avoids a pgcrypto dependency; btree_gist is the only extension (D3)
    v_commit_hash := encode(sha256(convert_to(
        TG_TABLE_NAME || v_entity_id || clock_timestamp()::text || COALESCE(v_post_state::text, v_pre_state::text),
        'UTF8')), 'hex');

    INSERT INTO audit_log (entity_name, entity_id, actor_id, operation, valid_time, system_time,
                           pre_state, post_state, diff_patch, commit_hash, parent_commit_hash)
    VALUES (TG_TABLE_NAME, v_entity_id, v_actor_id, v_op, v_valid_time,
            tstzrange(clock_timestamp(), NULL, '[)'),
            v_pre_state, v_post_state, v_diff_patch, v_commit_hash, NULL);

    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp;

-- Attach to every audited entity table (contracts, counterparties, physical_deliveries,
-- capacity_bookings, transfers, bioticket_deliveries, goo_certificate_transactions, ...)
DROP TRIGGER IF EXISTS trg_physical_deliveries_audit ON physical_deliveries;
CREATE TRIGGER trg_physical_deliveries_audit
AFTER INSERT OR UPDATE OR DELETE ON physical_deliveries
FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
```

**Outbox rows are written by the application, not a generic table trigger**: Task 02's mutation repositories `INSERT INTO outbox_events` inside the same transaction as the entity change, using the PascalCase `aggregate_type` values above. A generic table-trigger enqueue (writing `TG_TABLE_NAME`) would emit snake_case aggregate types that Task 03's group whitelist rejects, and would double-enqueue alongside the application inserts — so no such trigger exists. The only trigger on `outbox_events` is `trg_outbox_notify` (NOTIFY wake-up).

### 3.9 Custom Fields & Semantic Models (`008_custom_fields_semantic_models.sql`)

`custom_field_definitions` (`target_entity` defaults to `'CONTRACT'`; supports `STRING`/`NUMBER`/`BOOLEAN`/`DATE`/`ENUM`) and `semantic_models` (`specification_yaml` + compiled `specification_json`, `uk_model_version`) — implemented verbatim from blueprint §3 §7. This replaces the old `TRADE`-targeted generic model with the real target entities (`CONTRACT`, `PHYSICAL_DELIVERY`, `CAPACITY_BOOKING`, `TRANSFER`, `BIOTICKET_DELIVERY`, `COUNTERPARTY`, `COMPANY`, `MARKET_PRICE`).

### 3.10 Time-Travel Recovery Function (`009_time_travel_recovery.sql`)

```sql
CREATE OR REPLACE FUNCTION get_entity_state_as_of(
    p_entity_name VARCHAR,
    p_entity_id VARCHAR,
    p_system_time TIMESTAMPTZ,
    p_valid_time TIMESTAMPTZ
)
RETURNS JSONB AS $$
DECLARE
    v_state JSONB;
BEGIN
    SELECT post_state INTO v_state
    FROM audit_log
    WHERE entity_name = p_entity_name
      AND entity_id = p_entity_id
      AND system_time @> p_system_time
      AND valid_time @> p_valid_time
    ORDER BY lower(system_time) DESC, lower(valid_time) DESC
    LIMIT 1;
    RETURN v_state;
END;
$$ LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp;
```

### 3.11 Seed & Excel Master-Data Migration (`010_seed_and_excel_import.sql`)

**Objectives**: (1) idempotently seed reference data; (2) import live workbook data through staging tables; (3) handle VAT/SEK/currency correctly.

- **Companies seed**: the internal BioGem trading centers (shorthand, country dial code, `vat_rate`, `default_currency`) from `Tradebook_Masterdata.xlsx` counterparty/company lists.
- **Counterparties seed**: name, shorthand, `segment`, `country_code`, `country_dial_code`, `vat_applicable`, `salesforce_account_id` — segment values normalized to `segment_enum`.
- **Contracts seed**: full rows via contract-name parsing (see entity-model.md §5), deriving `counterparty_id`, `sourcing_center`/`sales_center` company refs, `product_type`/`action`/quality codes from the name plus workbook columns.
- **VAT/SEK/Currency handling**:
  - `vat_pct` on books is resolved from the counterparty's country `vat_rate` when the workbook does not carry it.
  - SEK-denominated tariffs land in `tax_tariffs` (`currency='SEK'`); EUR conversion uses `market_prices.eur_sek` for the period (mid-rate), never a hardcoded rate.
  - All booked amounts are stored in EUR; local-currency amounts are preserved only in the dedicated `*_local_cur_*` columns of `tax_tariffs`.
- **Import staging** (idempotent): `CREATE TEMP TABLE`/`UNLOGGED` staging per workbook, `ON CONFLICT` upserts keyed on the natural unique keys (`contract_name`, instance `(contract_id, contract_instance_id, book_type)`, `(contract_id, period_start, period_end)`, `(contract_id, month)`).

> Source workbook→table mapping is defined in `architecture/entity-model.md` §8 (Workbook → Entity Traceability Matrix). The Python import runner (`scripts/excel-import/import_tradebook.py`, uses `openpyxl` read-only mode) maps sheets: `Physical` Hedge/Tax/Capacity Booking/Transfer/Totals/Shippingfix → delivery books; `Bioticket`/`Certificates_v2` → `bioticket_deliveries` / `goo_certificate_transactions` / `certificate_contracts`; `Masterdata` → `companies`/`counterparties`/`contracts`; `Reports_V2_SF_integration` → `invoice_line_items`/`external_cogs`.

---

## 4. Bi-Temporal Engine & Time-Travel Functions

### 4.1 Bi-Temporal Architecture Specification
Bi-temporal data modeling manages two orthogonal axes of time:
1. **Valid Time ($V_t$)**: real-world period during which a business fact is true (defaulted to the delivery/contract/price period of the row).
2. **System Time ($S_t$)**: physical database timeline during which a row was stored as active truth (`system_time TSTZRANGE`).

When a historical record is corrected retroactively (e.g. a delivery's `volume_realised_mwh` for a past supply month), the system time of the existing record is closed (`st_to = clock_timestamp()`) and a new audit record is inserted with the updated payload and a fresh open `system_time`, while the original `valid_time` (the supply month) is preserved.

### 4.2 Trigger Mechanics
`fn_bi_temporal_audit_trigger` (see §3.8) attaches to every audited entity table. The actor is resolved from `SET LOCAL app.actor_id = '<uuid>'` set by the API before each transaction, falling back to the zero UUID for system processes. The RFC 6902 `diff_patch` records only changed key paths on UPDATE.

---

## 5. Time-Series Approach

Tradebook is a monthly/daily energy-index business, not an intraday tick exchange. `market_prices` receives ~1 row per day (one wide end-of-day row of daily TTF/EGSI ETF/THE/BGO/PGO/EUA/within-day + EUR cross-FX indexes) — roughly 11k rows over thirty years — so it is a plain PostgreSQL table with `PRIMARY KEY (price_date)` and a plain-SQL monthly averages VIEW (§3.6). If intraday tick data ever arrives, the first scaling step is native declarative partitioning (D3). `capacity_price_indexes` remains a plain indexed table. The joined reporting materialized view `delivery_monthly_summary` (§3.6) is refreshed on demand by Task 04 (semantic layer) or via `REFRESH MATERIALIZED VIEW CONCURRENTLY` in the nightly batch.

---

## 6. Implementation Guide & Project File Structure

### 6.1 Project Layout Targets

```
c:\Users\LaxmananKrishnapilla\tradebook\
├── src/
│   └── Database/
│       ├── Migrations/
│       │   ├── 001_extensions_and_enums.sql
│       │   ├── 002_master_data.sql
│       │   ├── 003_contracts.sql
│       │   ├── 004_delivery_books.sql
│       │   ├── 005_market_prices.sql
│       │   ├── 006_financial_registry.sql
│       │   ├── 007_bitemporal_audit_and_outbox.sql
│       │   ├── 008_custom_fields_semantic_models.sql
│       │   ├── 009_time_travel_recovery.sql
│       │   └── 010_seed_and_excel_import.sql
│       ├── Functions/
│       │   ├── fn_bi_temporal_audit_trigger.sql
│       │   ├── fn_notify_outbox_new_event.sql
│       │   ├── fn_generate_contract_instance.sql
│       │   └── fn_get_entity_state_as_of.sql
│       ├── Triggers/
│       │   └── trg_entity_audit.sql
│       └── Runner/
│           ├── DatabaseMigrator.cs
│           └── TradebookDbContext.cs
├── scripts/
│   ├── db-init.sh
│   ├── db-migrate.ps1
│   └── excel-import/
│       ├── import_tradebook.py
│       └── mapping.yaml
└── tests/
    └── Database.Tests/
        ├── BiTemporalAuditTests.cs
        ├── ContractInstanceTests.cs
        ├── MarketPriceTests.cs
        └── OutboxTriggerTests.cs
```

### 6.2 C# Database Runner Code Snippets

```csharp
// File: src/Database/Runner/DatabaseMigrator.cs
namespace Tradebook.Database.Runner;

using System;
using System.IO;
using System.Threading.Tasks;
using Npgsql;

public class DatabaseMigrator
{
    private readonly string _connectionString;

    public DatabaseMigrator(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task MigrateAsync(string migrationsDirectory)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var migrationFiles = Directory.GetFiles(migrationsDirectory, "*.sql");
        Array.Sort(migrationFiles);

        foreach (var file in migrationFiles)
        {
            Console.WriteLine($"[Migrator] Executing migration: {Path.GetFileName(file)}");
            var sql = await File.ReadAllTextAsync(file);

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.CommandTimeout = 300;
                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
                Console.WriteLine($"[Migrator] Successfully applied: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[Migrator] ERROR applying {Path.GetFileName(file)}: {ex.Message}");
                throw;
            }
        }
    }
}
```

---

## 7. Comprehensive Test Plan

### 7.1 Test Suites Overview

| Test Suite | Purpose | Target Technology | Success Criteria |
| :--- | :--- | :--- | :--- |
| **TS-01: Schema Compilation** | Validate SQL DDL syntax and extension enablement | `psql` / `Npgsql` | 0 syntax errors; all 10 migrations execute cleanly on fresh container |
| **TS-02: Bi-Temporal Mutation** | Verify INSERT/UPDATE/DELETE audit snapshots and $V_t/S_t$ bounds on a delivery row | C# / xUnit + `Testcontainers` | Correct `pre_state`, `post_state`, and RFC 6902 `diff_patch` recorded |
| **TS-03: Exclusion Constraint** | Assert DB kernel blocks overlapping system/valid time ranges | `Npgsql` Exception Handling | PostgreSQL throws `23P01` (exclusion_violation) on range collision |
| **TS-04: Time-Travel Query** | Verify `get_entity_state_as_of` returns historical state | SQL Assertion | Reconstructed JSON matches exact state at historical $T$ |
| **TS-05: Contract Instance Generation** | Verify `fn_generate_contract_instance` + unique instance constraints | SQL Assertion | `BFEX45.BT.2301.CO2E-9-2023` generated; duplicate instance rejected |
| **TS-06: Market-Price Monthly View** | Seed 90 days of index rows & verify `market_prices_monthly` | SQL Assertion | View returns the exact monthly AVG computed from the seeded rows |
| **TS-07: Outbox Atomicity** | Verify outbox events enqueued atomically with entity mutations | C# Integration | Outbox event present in `outbox_events` matching domain transaction |
| **TS-08: Master-Data Migration** | Seed companies/counterparties/contracts and import a sample delivery month | Python + Npgsql | Row counts match source workbook; `vat_pct` resolved from country VAT; SEK→EUR uses `market_prices.eur_sek` |

### 7.2 Integration Test Implementation (`BiTemporalAuditTests.cs`)

```csharp
// File: tests/Database.Tests/BiTemporalAuditTests.cs
namespace Tradebook.Database.Tests;

using System;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public class BiTemporalAuditTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("tradebook_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgresContainer.StartAsync();
    public async Task DisposeAsync() => await _postgresContainer.DisposeAsync();

    [Fact]
    public async Task AuditTrigger_ShouldCapture_Insert_Update_And_TimeTravel()
    {
        var connStr = _postgresContainer.GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // 1. Run Migrations 001-010
        // 2. Seed a counterparty + contract
        var counterpartyId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();

        await using (var cmd = new NpgsqlCommand(@"
            INSERT INTO counterparties (id, name, shorthand, segment, country_code, country_dial_code, vat_applicable)
            VALUES (@cId, 'Arla Foods', 'ARLA', 'Industry', 'DK', 45, true);
            INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action, goo_quality, subsidy_status, contract_type)
            VALUES (@kId, 'ARLA45.SC.2601.ETSS', @cId, 'GoO', 'Sell', 'ETS', 'SUB', 'External');", conn))
        {
            cmd.Parameters.AddWithValue("cId", counterpartyId);
            cmd.Parameters.AddWithValue("kId", contractId);
            await cmd.ExecuteNonQueryAsync();
        }

        // 3. Insert a physical delivery (T1) — set actor session setting
        await using (var cmd = new NpgsqlCommand(@"
            SELECT set_config('app.actor_id', @actor, false);
            INSERT INTO physical_deliveries (id, contract_id, contract_instance_id, book_type, supply_month, volume_mwh, invoice_amount_eur, status)
            VALUES (@dId, @kId, fn_generate_contract_instance('ARLA45.SC.2601.ETSS', '2026-01-01'), 'Sourcing', '2026-01-01', 1000.0, 45000.00, 'Pending - No Invoice');", conn))
        {
            cmd.Parameters.AddWithValue("dId", deliveryId);
            cmd.Parameters.AddWithValue("kId", contractId);
            cmd.Parameters.AddWithValue("actor", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        // Assert T1 audit entry
        await using (var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM audit_log WHERE entity_id = @dId AND operation = 'INSERT';", conn))
        {
            cmd.Parameters.AddWithValue("dId", deliveryId.ToString());
            Assert.Equal(1, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
        }

        // 4. Correct realised volume (T2)
        await Task.Delay(100);
        await using (var cmd = new NpgsqlCommand(@"
            UPDATE physical_deliveries SET volume_realised_mwh = 950.0, updated_at = clock_timestamp()
            WHERE id = @dId;", conn))
        {
            cmd.Parameters.AddWithValue("dId", deliveryId);
            await cmd.ExecuteNonQueryAsync();
        }

        // 5. Time-travel reconstruction reflects the correction
        await using (var cmd = new NpgsqlCommand(@"
            SELECT get_entity_state_as_of('physical_deliveries', @dId, clock_timestamp(), '2026-01-01'::timestamptz);", conn))
        {
            cmd.Parameters.AddWithValue("dId", deliveryId.ToString());
            var jsonState = (string)await cmd.ExecuteScalarAsync();
            Assert.Contains("950.0", jsonState);
        }
    }
}
```

---

## 8. Agent Verification Steps & Automated Audit Protocol

### Step 1: Bootstrapping Test Container
```powershell
docker run -d --name tradebook-db-verify -p 5433:5432 -e POSTGRES_PASSWORD=secret postgres:17
```

### Step 2: Running Migration Suite
```powershell
$conn = "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=secret"
Get-ChildItem src/Database/Migrations/*.sql | Sort-Object Name | ForEach-Object {
    psql $conn -f $_.FullName
}
```

### Step 3: Verifying Schema Catalogs
```sql
-- Verify entity tables exist
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN ('contracts','physical_deliveries','capacity_bookings','transfers',
                     'bioticket_deliveries','tax_tariffs','hedges','market_prices',
                     'goo_certificate_transactions','invoice_line_items','external_cogs');

-- Verify the monthly averages view exists
SELECT table_name FROM information_schema.views WHERE table_name = 'market_prices_monthly';
-- Expected: 1 row ('market_prices_monthly')
```

### Step 4: Master-Data & Integrity Spot Checks
```sql
-- Contract instance generation
SELECT fn_generate_contract_instance('BFEX45.BT.2301.CO2E', '2023-09-01');
-- Expected: 'BFEX45.BT.2301.CO2E-9-2023'

-- Unique instance enforcement
INSERT INTO physical_deliveries (contract_id, contract_instance_id, book_type, supply_month, status)
VALUES (<contractId>, 'BFEX45.BT.2301.CO2E-9-2023', 'Sales', '2023-09-01', 'Pending - No Invoice');
-- Second identical insert MUST throw unique_violation (23505)
```

### Step 5: Verification Failure Invalidation Conditions
- Any `ERR` or syntax error during migration execution.
- Failure of `EXCLUDE USING gist` to prevent overlapping `TSTZRANGE` bounds.
- Absence of RFC 6902 `diff_patch` generation in `audit_log`.
- `get_entity_state_as_of` returning `NULL` for valid historic timestamps.
- Any hardcoded FX/VAT rate in migrations or import scripts (SEK→EUR MUST use `market_prices.eur_sek`).
- Any table/column/enum not present in `architecture/entity-model.md` v2.0.

---
*End of Task 01 Implementation Specification.*
