--liquibase formatted sql

--changeset root:1-create-roles-table
CREATE TABLE IF NOT EXISTS roles
(
    id          SERIAL PRIMARY KEY,
    code        VARCHAR(100) UNIQUE NOT NULL,
    name        VARCHAR(150)        NOT NULL,
    description VARCHAR(500),
    created_at  TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

--rollback DROP TABLE roles;
