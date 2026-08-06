-- 011: Application users for JWT authentication (Task 02 §3.8, D11).
-- Auth infrastructure, not a trading entity: no bi-temporal audit trigger, no outbox,
-- no version column. Password hashes are salted PBKDF2-SHA256 (>=210k iterations);
-- the encoded format is "pbkdf2-sha256.{iterations}.{salt-b64}.{hash-b64}".

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(100) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    roles TEXT[] NOT NULL DEFAULT '{}'::TEXT[]
        CHECK (roles <@ ARRAY['Trader', 'BackOffice', 'Admin']),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_users_username_active ON users (username) WHERE is_active;
