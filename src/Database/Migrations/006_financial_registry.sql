CREATE TABLE goo_certificate_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sf_transaction_id VARCHAR(50) UNIQUE,
    transaction_name VARCHAR(100),
    batch_type VARCHAR(100),
    certificate_transaction_id VARCHAR(100),
    country_of_production CHAR(2),
    producer_contract_id UUID REFERENCES contracts(id),
    producer_company VARCHAR(200),
    producer_monthly_quantity_id VARCHAR(50),
    producer_register_account_id VARCHAR(50),
    producer_sf_account_id VARCHAR(50),
    producer_goo_price_eur_mwh NUMERIC(12,6),
    production_date DATE,
    customer_contract_id UUID REFERENCES contracts(id),
    customer_company VARCHAR(200),
    customer_monthly_quantity_id VARCHAR(50),
    customer_register_account_id VARCHAR(50),
    customer_sf_account_id VARCHAR(50),
    earmark VARCHAR(200),
    issue_date DATE,
    receiver_organization_name VARCHAR(200),
    register VARCHAR(100),
    sender_organization_name VARCHAR(200),
    status transaction_status_enum,
    transaction_start_date DATE,
    transaction_volume_mwh NUMERIC(15,6),
    type VARCHAR(100),
    volume_mwh NUMERIC(15,6),
    beneficiary_name VARCHAR(200),
    consumption_period_start DATE,
    consumption_period_end DATE,
    production_device_name VARCHAR(200),
    gsrn VARCHAR(64),
    energy_source VARCHAR(100),
    text TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX idx_goo_txn_producer ON goo_certificate_transactions(producer_contract_id);
CREATE INDEX idx_goo_txn_customer ON goo_certificate_transactions(customer_contract_id);
CREATE INDEX idx_goo_txn_start_date ON goo_certificate_transactions(transaction_start_date);

CREATE TABLE invoice_line_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contract_id UUID NOT NULL REFERENCES contracts(id),
    physical_delivery_id UUID REFERENCES physical_deliveries(id),
    capacity_booking_id UUID REFERENCES capacity_bookings(id),
    transfer_id UUID REFERENCES transfers(id),
    bioticket_delivery_id UUID REFERENCES bioticket_deliveries(id),
    supply_month DATE NOT NULL,
    invoice_date DATE,
    payment_due_date DATE,
    volume_mwh NUMERIC(15,6),
    price_eur_mwh NUMERIC(12,6),
    subtotal_eur NUMERIC(15,2),
    tax_eur NUMERIC(15,2),
    handling_fee_eur NUMERIC(15,2),
    tso_tariff_eur NUMERIC(15,2),
    dso_tariff_eur NUMERIC(15,2),
    total_eur NUMERIC(15,2),
    vat_pct NUMERIC(5,4),
    vat_eur NUMERIC(15,2),
    invoicing_amount_eur NUMERIC(15,2),
    status report_status_enum,
    sf_invoice_ref VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX idx_invoice_contract_month ON invoice_line_items(contract_id, supply_month);

CREATE TABLE external_cogs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    month DATE NOT NULL,
    sales_contract_id UUID NOT NULL REFERENCES contracts(id),
    purchase_contract_id UUID NOT NULL REFERENCES contracts(id),
    volume_mwh NUMERIC(15,6),
    cost_eur_mwh NUMERIC(12,6),
    cogs_eur NUMERIC(15,2),
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX idx_external_cogs_month ON external_cogs(month);
CREATE INDEX idx_external_cogs_sales ON external_cogs(sales_contract_id);
