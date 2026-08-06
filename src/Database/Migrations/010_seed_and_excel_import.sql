-- Source workbooks and the import contract are not present in this repository.
-- See docs/architecture/spec-issues.md before adding seed data or an importer.

ALTER TABLE companies ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE counterparties ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE trading_points ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE contracts ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE certificate_contracts ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE physical_deliveries ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE capacity_bookings ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE transfers ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE bioticket_deliveries ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE tax_tariffs ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE hedges ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE market_prices ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE capacity_price_indexes ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE goo_certificate_transactions ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE invoice_line_items ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE external_cogs ADD COLUMN version BIGINT NOT NULL DEFAULT 1;
