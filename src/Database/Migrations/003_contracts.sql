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
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);


CREATE OR REPLACE FUNCTION fn_generate_contract_instance(p_contract_name VARCHAR, p_supply_month DATE) RETURNS VARCHAR AS $$ SELECT p_contract_name || '-' || to_char(p_supply_month, 'FMMM') || '-' || to_char(p_supply_month, 'YYYY'); $$ LANGUAGE sql IMMUTABLE;
