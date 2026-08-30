--liquibase formatted sql

--changeset root:6-add-not-null-created-at
ALTER TABLE roles ALTER COLUMN created_at SET NOT NULL;
ALTER TABLE permissions ALTER COLUMN created_at SET NOT NULL;
ALTER TABLE tenants ALTER COLUMN created_at SET NOT NULL;

--rollback ALTER TABLE roles ALTER COLUMN created_at DROP NOT NULL;
--rollback ALTER TABLE permissions ALTER COLUMN created_at DROP NOT NULL;
--rollback ALTER TABLE tenants ALTER COLUMN created_at DROP NOT NULL;
