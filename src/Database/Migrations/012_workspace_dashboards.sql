-- Task 01 platform entity for Task 06 persisted dashboards.
-- actor_id is set by application code from the JWT sub claim only.
CREATE TABLE workspace_dashboards (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_id UUID NOT NULL,
    layout_json JSONB NOT NULL,
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX idx_workspace_dashboards_actor_updated_at
    ON workspace_dashboards(actor_id, updated_at DESC);

CREATE TRIGGER trg_workspace_dashboards_audit
AFTER INSERT OR UPDATE OR DELETE ON workspace_dashboards
FOR EACH ROW EXECUTE FUNCTION fn_bi_temporal_audit_trigger();
