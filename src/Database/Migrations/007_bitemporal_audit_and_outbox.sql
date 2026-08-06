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
    EXCLUDE USING gist (entity_name WITH =, entity_id WITH =, system_time WITH &&, valid_time WITH &&)
);

CREATE INDEX idx_audit_composite ON audit_log (entity_name, entity_id, lower(system_time) DESC);
CREATE INDEX idx_audit_system_time_gist ON audit_log USING gist (system_time);
CREATE INDEX idx_audit_valid_time_gist ON audit_log USING gist (valid_time);
CREATE INDEX idx_audit_commit_hash ON audit_log (commit_hash);

CREATE TABLE outbox_events (
    event_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sequence_id BIGSERIAL NOT NULL UNIQUE,
    aggregate_type VARCHAR(128) NOT NULL,
    aggregate_id VARCHAR(128) NOT NULL,
    event_type VARCHAR(128) NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    processed_at TIMESTAMPTZ
);

CREATE INDEX idx_outbox_unprocessed ON outbox_events(created_at) WHERE processed_at IS NULL;

CREATE OR REPLACE FUNCTION notify_outbox_new_event() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('outbox_new_event', NEW.event_id::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_outbox_notify AFTER INSERT ON outbox_events
FOR EACH ROW EXECUTE FUNCTION notify_outbox_new_event();

CREATE OR REPLACE FUNCTION fn_bi_temporal_audit_trigger()
RETURNS TRIGGER AS $$
DECLARE
    v_pre_state JSONB := CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE to_jsonb(OLD) END;
    v_post_state JSONB := CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE to_jsonb(NEW) END;
    v_entity_id VARCHAR(128);
    v_actor_id UUID;
    v_diff_patch JSONB := '[]'::jsonb;
    v_valid_time TSTZRANGE;
    v_commit_hash VARCHAR(64);
BEGIN
    v_entity_id := COALESCE(v_post_state->>'id', v_pre_state->>'id', v_post_state->>'price_date', v_pre_state->>'price_date');
    v_actor_id := COALESCE(NULLIF(current_setting('app.actor_id', true), '')::UUID, '00000000-0000-0000-0000-000000000000'::UUID);

    IF TG_OP = 'UPDATE' THEN
        SELECT COALESCE(jsonb_agg(jsonb_build_object('op', 'replace', 'path', '/' || key, 'value', value)), '[]'::jsonb)
        INTO v_diff_patch
        FROM jsonb_each(v_post_state)
        WHERE v_pre_state->key IS DISTINCT FROM value;
        UPDATE audit_log SET system_time = tstzrange(lower(system_time), clock_timestamp(), '[)')
        WHERE entity_name = TG_TABLE_NAME AND entity_id = v_entity_id AND upper_inf(system_time);
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE audit_log SET system_time = tstzrange(lower(system_time), clock_timestamp(), '[)')
        WHERE entity_name = TG_TABLE_NAME AND entity_id = v_entity_id AND upper_inf(system_time);
    END IF;

    v_valid_time := tstzrange(COALESCE(
        (COALESCE(v_post_state, v_pre_state)->>'supply_month')::TIMESTAMPTZ,
        (COALESCE(v_post_state, v_pre_state)->>'contract_month')::TIMESTAMPTZ,
        (COALESCE(v_post_state, v_pre_state)->>'price_date')::TIMESTAMPTZ,
        (COALESCE(v_post_state, v_pre_state)->>'month')::TIMESTAMPTZ,
        clock_timestamp()), NULL, '[)');
    v_commit_hash := encode(sha256(convert_to(TG_TABLE_NAME || v_entity_id || clock_timestamp()::text || COALESCE(v_post_state::text, v_pre_state::text), 'UTF8')), 'hex');

    INSERT INTO audit_log (entity_name, entity_id, actor_id, operation, valid_time, pre_state, post_state, diff_patch, commit_hash)
    VALUES (TG_TABLE_NAME, v_entity_id, v_actor_id, TG_OP, v_valid_time, v_pre_state, v_post_state, v_diff_patch, v_commit_hash);
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp;

CREATE TRIGGER trg_companies_audit AFTER INSERT OR UPDATE OR DELETE ON companies FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_counterparties_audit AFTER INSERT OR UPDATE OR DELETE ON counterparties FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_trading_points_audit AFTER INSERT OR UPDATE OR DELETE ON trading_points FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_contracts_audit AFTER INSERT OR UPDATE OR DELETE ON contracts FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_certificate_contracts_audit AFTER INSERT OR UPDATE OR DELETE ON certificate_contracts FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_physical_deliveries_audit AFTER INSERT OR UPDATE OR DELETE ON physical_deliveries FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_capacity_bookings_audit AFTER INSERT OR UPDATE OR DELETE ON capacity_bookings FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_transfers_audit AFTER INSERT OR UPDATE OR DELETE ON transfers FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_bioticket_deliveries_audit AFTER INSERT OR UPDATE OR DELETE ON bioticket_deliveries FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_tax_tariffs_audit AFTER INSERT OR UPDATE OR DELETE ON tax_tariffs FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_hedges_audit AFTER INSERT OR UPDATE OR DELETE ON hedges FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_market_prices_audit AFTER INSERT OR UPDATE OR DELETE ON market_prices FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_capacity_price_indexes_audit AFTER INSERT OR UPDATE OR DELETE ON capacity_price_indexes FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_goo_certificate_transactions_audit AFTER INSERT OR UPDATE OR DELETE ON goo_certificate_transactions FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_invoice_line_items_audit AFTER INSERT OR UPDATE OR DELETE ON invoice_line_items FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
CREATE TRIGGER trg_external_cogs_audit AFTER INSERT OR UPDATE OR DELETE ON external_cogs FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
