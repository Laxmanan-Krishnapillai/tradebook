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
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

-- Plain table, ~1 row/day (TimescaleDB removed per D3 — future tick path is
-- native declarative partitioning, see architecture/decision-log.md)

-- Monthly index averages as a plain SQL view (no continuous aggregate needed)
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
GROUP BY 1;

CREATE TABLE capacity_price_indexes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    mechanism capacity_price_mech_enum NOT NULL,
    price_eur_mwh NUMERIC(12,6) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX idx_capacity_price_indexes ON capacity_price_indexes(mechanism, period_start, period_end);
