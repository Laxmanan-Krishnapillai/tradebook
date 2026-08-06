CREATE OR REPLACE FUNCTION get_entity_state_as_of(
    p_entity_name VARCHAR,
    p_entity_id VARCHAR,
    p_system_time TIMESTAMPTZ,
    p_valid_time TIMESTAMPTZ
) RETURNS JSONB AS $$
    SELECT post_state
    FROM audit_log
    WHERE entity_name = p_entity_name
      AND entity_id = p_entity_id
      AND system_time @> p_system_time
      AND valid_time @> p_valid_time
    ORDER BY lower(system_time) DESC, lower(valid_time) DESC
    LIMIT 1;
$$ LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public, pg_temp;
