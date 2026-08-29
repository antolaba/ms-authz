--liquibase formatted sql

--changeset root:2-create-permissions-table
CREATE TABLE IF NOT EXISTS permissions
(
    id          SERIAL PRIMARY KEY,
    code        VARCHAR(150) UNIQUE NOT NULL,
    module      VARCHAR(100)        NOT NULL,
    description VARCHAR(500),
    created_at  TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

--rollback DROP TABLE permissions;
