--liquibase formatted sql

--changeset root:4-create-tenants-table
-- Not part of MS-AUTHZ-SPEC.md §5's catalog schema (roles/permissions/role_permissions) — this is
-- ms-authz's own record of which tenant codes it has provisioned, so that POST /catalog/sync has
-- something to iterate over. See MsAuthz.Application/Interfaces/ITenantRegistry.cs for the full
-- rationale. It is NOT a copy of the consuming system's tenant metadata (name, realm, schema, ...) —
-- just the code, which is all ms-authz's own tuple materialization needs.
CREATE TABLE IF NOT EXISTS tenants
(
    id         SERIAL PRIMARY KEY,
    code       VARCHAR(50) UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

--rollback DROP TABLE tenants;
