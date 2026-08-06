-- ============================================================================
-- TRADEBOOK MASTER PRODUCTION DDL SCHEMA (POSTGRESQL 17)
-- Domain source of truth: architecture/entity-model.md (v2.0, Excel-verified)
-- ============================================================================

-- Enable required PostgreSQL extensions
-- uuid-ossp not needed: gen_random_uuid() is built into PG 13+ (D3 cleanup)
CREATE EXTENSION IF NOT EXISTS "btree_gist";
-- timescaledb removed per D3: all tables are plain PostgreSQL

-- ============================================================================
-- 0. Enum Types (mirrors architecture/entity-model.md §4)
-- ============================================================================

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

-- ============================================================================
