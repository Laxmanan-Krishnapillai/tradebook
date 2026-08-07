-- Task 01 completion: enforce the workbook-derived business keys, make audit
-- system-time transitions gapless, and add the joined monthly reporting view.

CREATE OR REPLACE FUNCTION fn_contract_subsidy_consistent(
    p_contract_name TEXT,
    p_subsidy_status subsidy_status_enum
) RETURNS BOOLEAN
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
AS $$
    SELECT CASE
        -- NULL means the source workbook did not state a subsidy status.
        WHEN p_subsidy_status IS NULL THEN TRUE
        WHEN p_contract_name ~ '(RED|ETS|OZD|NMS|EWG|ISCC|NOQ|GEG|RTFO|BHG)S(-[[:alnum:]_-]+)?$'
            THEN p_subsidy_status = 'SUB'
        WHEN p_contract_name ~ '(RED|ETS|OZD|NMS|EWG|ISCC|NOQ|GEG|RTFO|BHG)U(-[[:alnum:]_-]+)?$'
            THEN p_subsidy_status = 'UNS'
        -- Legacy names and ticket names do not carry an unambiguous S/U suffix.
        ELSE TRUE
    END;
$$;

ALTER TABLE contracts
    ADD CONSTRAINT ck_contracts_subsidy_suffix
    CHECK (fn_contract_subsidy_consistent(contract_name, subsidy_status));

CREATE OR REPLACE FUNCTION fn_enforce_delivery_instance()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_contract_name contracts.contract_name%TYPE;
    v_delivery_month DATE;
    v_expected_instance VARCHAR(120);
BEGIN
    -- A polymorphic trigger record cannot reference a field that is absent from
    -- one of its target tables, even from an unselected CASE branch. Read the
    -- table-specific month field through JSON while retaining typed assignment.
    v_delivery_month := NULLIF(
        to_jsonb(NEW)->>(CASE TG_TABLE_NAME
            WHEN 'bioticket_deliveries' THEN 'contract_month'
            ELSE 'supply_month'
        END),
        '')::DATE;

    IF v_delivery_month IS NULL
       OR v_delivery_month <> date_trunc('month', v_delivery_month)::DATE THEN
        RAISE EXCEPTION '% must use the first day of its delivery month', TG_TABLE_NAME
            USING ERRCODE = '23514', CONSTRAINT = 'ck_delivery_month_first_day';
    END IF;

    SELECT contract_name
      INTO v_contract_name
      FROM contracts
     WHERE id = NEW.contract_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'contract % does not exist', NEW.contract_id
            USING ERRCODE = '23503';
    END IF;

    v_expected_instance := fn_generate_contract_instance(v_contract_name, v_delivery_month);
    IF NEW.contract_instance_id IS NOT NULL
       AND btrim(NEW.contract_instance_id) <> ''
       AND NEW.contract_instance_id <> v_expected_instance THEN
        RAISE EXCEPTION 'contract_instance_id must be %, received %',
            v_expected_instance, NEW.contract_instance_id
            USING ERRCODE = '23514', CONSTRAINT = 'ck_contract_instance_matches_month';
    END IF;

    NEW.contract_instance_id := v_expected_instance;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_physical_deliveries_instance
BEFORE INSERT OR UPDATE ON physical_deliveries
FOR EACH ROW EXECUTE FUNCTION fn_enforce_delivery_instance();

CREATE TRIGGER trg_capacity_bookings_instance
BEFORE INSERT OR UPDATE ON capacity_bookings
FOR EACH ROW EXECUTE FUNCTION fn_enforce_delivery_instance();

CREATE TRIGGER trg_transfers_instance
BEFORE INSERT OR UPDATE ON transfers
FOR EACH ROW EXECUTE FUNCTION fn_enforce_delivery_instance();

CREATE TRIGGER trg_bioticket_deliveries_instance
BEFORE INSERT OR UPDATE ON bioticket_deliveries
FOR EACH ROW EXECUTE FUNCTION fn_enforce_delivery_instance();

ALTER TABLE physical_deliveries
    ADD CONSTRAINT ck_physical_supply_month_first_day
        CHECK (supply_month = date_trunc('month', supply_month)::DATE),
    ADD CONSTRAINT ck_physical_delivery_dates
        CHECK (start_day IS NULL OR end_day IS NULL OR start_day <= end_day),
    ADD CONSTRAINT uk_physical_delivery_month
        UNIQUE (contract_id, supply_month, book_type);

ALTER TABLE capacity_bookings
    ADD CONSTRAINT ck_capacity_supply_month_first_day
        CHECK (supply_month = date_trunc('month', supply_month)::DATE),
    ADD CONSTRAINT ck_capacity_delivery_dates
        CHECK (start_day IS NULL OR end_day IS NULL OR start_day <= end_day),
    ADD CONSTRAINT uk_capacity_booking_month
        UNIQUE (contract_id, supply_month);

ALTER TABLE transfers
    ADD CONSTRAINT ck_transfer_supply_month_first_day
        CHECK (supply_month = date_trunc('month', supply_month)::DATE),
    ADD CONSTRAINT ck_transfer_delivery_dates
        CHECK (start_day IS NULL OR end_day IS NULL OR start_day <= end_day),
    ADD CONSTRAINT uk_transfer_month
        UNIQUE (contract_id, supply_month);

ALTER TABLE bioticket_deliveries
    ADD CONSTRAINT ck_bioticket_contract_month_first_day
        CHECK (contract_month = date_trunc('month', contract_month)::DATE),
    ADD CONSTRAINT ck_bioticket_delivery_dates
        CHECK (start_day IS NULL OR end_day IS NULL OR start_day <= end_day),
    ADD CONSTRAINT ck_bioticket_book_type
        CHECK (book_type IN ('Sourcing', 'Sales')),
    ADD CONSTRAINT ck_bioticket_year
        CHECK (year IS NULL OR year = EXTRACT(YEAR FROM contract_month)),
    ADD CONSTRAINT uk_bioticket_delivery_month
        UNIQUE (contract_id, contract_month, book_type);

ALTER TABLE hedges
    ADD CONSTRAINT ck_hedge_month_first_day
        CHECK (month = date_trunc('month', month)::DATE);

ALTER TABLE tax_tariffs
    ADD CONSTRAINT ck_tax_tariff_period
        CHECK (period_end >= period_start);

ALTER TABLE capacity_price_indexes
    ADD CONSTRAINT ck_capacity_price_period
        CHECK (period_end >= period_start);

CREATE OR REPLACE FUNCTION fn_bi_temporal_audit_trigger()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_change_time TIMESTAMPTZ := clock_timestamp();
    v_pre_state JSONB := CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE to_jsonb(OLD) END;
    v_post_state JSONB := CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE to_jsonb(NEW) END;
    v_entity_id VARCHAR(128);
    v_actor_id UUID;
    v_diff_patch JSONB := '[]'::jsonb;
    v_valid_start TIMESTAMPTZ;
    v_valid_time TSTZRANGE;
    v_commit_hash VARCHAR(64);
    v_parent_commit_hash VARCHAR(64);
BEGIN
    v_entity_id := COALESCE(
        v_post_state->>'id', v_pre_state->>'id',
        v_post_state->>'price_date', v_pre_state->>'price_date');
    v_actor_id := COALESCE(
        NULLIF(current_setting('app.actor_id', true), '')::UUID,
        '00000000-0000-0000-0000-000000000000'::UUID);

    IF TG_OP = 'UPDATE' THEN
        SELECT COALESCE(
            jsonb_agg(jsonb_build_object(
                'op', 'replace', 'path', '/' || key, 'value', value)),
            '[]'::jsonb)
        INTO v_diff_patch
        FROM jsonb_each(v_post_state)
        WHERE v_pre_state->key IS DISTINCT FROM value;
    END IF;

    IF TG_OP IN ('UPDATE', 'DELETE') THEN
        SELECT commit_hash
          INTO v_parent_commit_hash
          FROM audit_log
         WHERE entity_name = TG_TABLE_NAME
           AND entity_id = v_entity_id
           AND upper_inf(system_time)
         ORDER BY lower(system_time) DESC
         LIMIT 1
         FOR UPDATE;

        UPDATE audit_log
           SET system_time = tstzrange(lower(system_time), v_change_time, '[)')
         WHERE entity_name = TG_TABLE_NAME
           AND entity_id = v_entity_id
           AND upper_inf(system_time);
    END IF;

    v_valid_start := COALESCE(
        NULLIF(COALESCE(v_post_state, v_pre_state)->>'supply_month', '')::TIMESTAMPTZ,
        NULLIF(COALESCE(v_post_state, v_pre_state)->>'contract_month', '')::TIMESTAMPTZ,
        NULLIF(COALESCE(v_post_state, v_pre_state)->>'price_date', '')::TIMESTAMPTZ,
        NULLIF(COALESCE(v_post_state, v_pre_state)->>'month', '')::TIMESTAMPTZ,
        NULLIF(COALESCE(v_post_state, v_pre_state)->>'period_start', '')::TIMESTAMPTZ,
        NULLIF(COALESCE(v_post_state, v_pre_state)->>'transaction_start_date', '')::TIMESTAMPTZ,
        v_change_time);
    v_valid_time := tstzrange(v_valid_start, NULL, '[)');
    v_commit_hash := encode(sha256(convert_to(
        TG_TABLE_NAME || v_entity_id || v_change_time::TEXT ||
        COALESCE(v_post_state::TEXT, v_pre_state::TEXT), 'UTF8')), 'hex');

    INSERT INTO audit_log (
        entity_name, entity_id, actor_id, operation, system_time, valid_time,
        pre_state, post_state, diff_patch, commit_hash, parent_commit_hash)
    VALUES (
        TG_TABLE_NAME, v_entity_id, v_actor_id, TG_OP,
        tstzrange(v_change_time, NULL, '[)'), v_valid_time,
        v_pre_state, v_post_state, v_diff_patch, v_commit_hash, v_parent_commit_hash);

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

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
JOIN counterparties cp ON cp.id = c.counterparty_id
GROUP BY
    pd.supply_month, c.contract_name, c.product_type, c.action,
    cp.name, cp.segment, pd.book_type, pd.status;

CREATE UNIQUE INDEX uk_delivery_monthly_summary
    ON delivery_monthly_summary
       (supply_month, contract_name, book_type, status);
