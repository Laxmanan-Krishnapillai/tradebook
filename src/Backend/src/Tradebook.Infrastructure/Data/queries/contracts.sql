-- name: GetContractPersistenceProbe :one
SELECT id, fixed_price_gas_eur_mwh AS price
FROM contracts
WHERE id = sqlc.arg(id);
