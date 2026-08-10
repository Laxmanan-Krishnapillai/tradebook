-- Freeze legacy writers for this transactional migration so no row can arrive
-- during the history snapshot and shared-sequence handoff.
LOCK TABLE public.outbox_events IN ACCESS EXCLUSIVE MODE;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM public.outbox_events
        WHERE aggregate_type = 'WorkspaceDashboard'
          AND pg_input_is_valid(payload ->> 'actorId', 'uuid') IS NOT TRUE)
    THEN
        RAISE EXCEPTION USING
            ERRCODE = '22023',
            MESSAGE = 'WorkspaceDashboard outbox history contains a missing or invalid actorId';
    END IF;
END;
$$;

CREATE TABLE public.realtime_event_log (
    event_id UUID PRIMARY KEY,
    group_name TEXT NOT NULL,
    aggregate_type VARCHAR(128) NOT NULL,
    aggregate_id VARCHAR(128) NOT NULL,
    event_type VARCHAR(128) NOT NULL,
    sequence_id BIGSERIAL NOT NULL UNIQUE,
    payload JSONB NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    UNIQUE (group_name, sequence_id)
);

CREATE INDEX idx_realtime_event_log_occurred_at
    ON public.realtime_event_log (occurred_at);

INSERT INTO public.realtime_event_log (
    event_id,
    group_name,
    aggregate_type,
    aggregate_id,
    event_type,
    sequence_id,
    payload,
    occurred_at)
SELECT
    event_id,
    CASE
        WHEN aggregate_type = 'WorkspaceDashboard'
            THEN 'dashboard:' || ((payload ->> 'actorId')::uuid)::text
        ELSE 'entity:' || aggregate_type
    END AS group_name,
    aggregate_type,
    aggregate_id,
    event_type,
    sequence_id,
    payload,
    created_at
FROM public.outbox_events
ORDER BY sequence_id;

-- Copy both processed history and pending rows. Pending legacy events remain
-- catch-up-visible if deployment stops the previous dispatcher before its next pass.

SELECT setval(
    pg_get_serial_sequence('public.realtime_event_log', 'sequence_id')::regclass,
    COALESCE(MAX(sequence_id), 1),
    COUNT(*) > 0)
FROM public.realtime_event_log;

-- Migrations run before the new API is deployed and the previous image must remain
-- rollback-compatible. During this expand phase both writers share one sequence and
-- mirror into both stores. A later contract migration can remove these legacy objects
-- only after the rollback window closes and processed_at IS NULL is empty.
ALTER TABLE public.outbox_events
    ALTER COLUMN sequence_id SET DEFAULT nextval(
        pg_get_serial_sequence('public.realtime_event_log', 'sequence_id')::regclass);

CREATE OR REPLACE FUNCTION public.notify_outbox_new_event() RETURNS trigger AS $$
BEGIN
    INSERT INTO public.realtime_event_log (
        event_id, group_name, aggregate_type, aggregate_id, event_type,
        sequence_id, payload, occurred_at)
    VALUES (
        NEW.event_id,
        CASE
            WHEN NEW.aggregate_type = 'WorkspaceDashboard'
                THEN 'dashboard:' || ((NEW.payload ->> 'actorId')::uuid)::text
            ELSE 'entity:' || NEW.aggregate_type
        END,
        NEW.aggregate_type, NEW.aggregate_id, NEW.event_type,
        NEW.sequence_id, NEW.payload, NEW.created_at)
    ON CONFLICT (event_id) DO NOTHING;

    PERFORM pg_notify('outbox_new_event', NEW.event_id::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE FUNCTION public.mirror_realtime_event_to_legacy_outbox() RETURNS trigger AS $$
BEGIN
    INSERT INTO public.outbox_events (
        event_id, sequence_id, aggregate_type, aggregate_id, event_type,
        payload, created_at, processed_at)
    VALUES (
        NEW.event_id, NEW.sequence_id, NEW.aggregate_type, NEW.aggregate_id,
        NEW.event_type, NEW.payload, NEW.occurred_at, NEW.occurred_at)
    ON CONFLICT (event_id) DO NOTHING;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_realtime_legacy_compatibility
AFTER INSERT ON public.realtime_event_log
FOR EACH ROW EXECUTE FUNCTION public.mirror_realtime_event_to_legacy_outbox();
