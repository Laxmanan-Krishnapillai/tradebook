CREATE TABLE companies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    shorthand VARCHAR(10) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    country_code CHAR(2),
    country_dial_code SMALLINT,
    vat_rate NUMERIC(5,4),
    default_currency CHAR(3),
    is_active BOOLEAN NOT NULL DEFAULT true,
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
    end_area VARCHAR(20)
);

