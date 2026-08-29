--liquibase formatted sql

--changeset root:3-create-role-permissions-table
CREATE TABLE IF NOT EXISTS role_permissions
(
    role_id       INTEGER NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
    permission_id INTEGER NOT NULL REFERENCES permissions (id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

--rollback DROP TABLE role_permissions;
