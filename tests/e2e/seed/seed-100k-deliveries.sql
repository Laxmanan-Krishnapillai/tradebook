\set ON_ERROR_STOP on
BEGIN;
SELECT set_config('app.current_actor_id', '09000000-0000-4000-8000-000000000001', true);
INSERT INTO users (id, username, password_hash, roles) VALUES
  ('09000000-0000-4000-8000-000000000001', 'e2e-trader', 'pbkdf2-sha256.210000.dHJhZGVib29rLWUyZS0wOQ==.fdaDGUidx0MJwwuNzw1Olrjdqp+iwRrXtRKiXUL2CLY=', ARRAY['Trader','BackOffice','Admin'])
ON CONFLICT (username) DO UPDATE SET password_hash = EXCLUDED.password_hash, roles = EXCLUDED.roles, is_active = true;
INSERT INTO counterparties (id, name, shorthand) VALUES ('09000000-0000-4000-8000-000000000002', 'Task 09 E2E Counterparty', 'T09E2E') ON CONFLICT (id) DO NOTHING;
INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action) VALUES ('09000000-0000-4000-8000-000000000003', 'TASK09-PRIMARY', '09000000-0000-4000-8000-000000000002', 'Gas', 'Buy') ON CONFLICT (contract_name) DO NOTHING;
INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action)
SELECT (substr(md5('task09-contract-' || n),1,8)||'-'||substr(md5('task09-contract-' || n),9,4)||'-4'||substr(md5('task09-contract-' || n),14,3)||'-8'||substr(md5('task09-contract-' || n),18,3)||'-'||substr(md5('task09-contract-' || n),21,12))::uuid,
       'TASK09-' || n, '09000000-0000-4000-8000-000000000002', 'Gas', 'Buy'
FROM generate_series(1, 100000) AS series(n) ON CONFLICT (contract_name) DO NOTHING;
INSERT INTO physical_deliveries (contract_id, contract_instance_id, book_type, supply_month, volume_nominated_mwh, volume_realised_mwh, volume_mwh, price_mechanism, start_day, end_day)
SELECT id, contract_name || '.Sourcing.2026-03', 'Sourcing', DATE '2026-03-01', 12000, 11840, 11840, 'Fixed', DATE '2026-03-01', DATE '2026-03-31'
FROM contracts WHERE contract_name LIKE 'TASK09-%' ON CONFLICT (contract_id, supply_month, book_type) DO NOTHING;
COMMIT;
\echo E2E_CONTRACT_ID=09000000-0000-4000-8000-000000000003
