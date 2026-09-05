# ms-authz — repo conventions

Authorization component over OpenFGA. Standalone repo, own git history, part of the `Estudio-Martinez`
workspace but consumed by any number of unrelated systems (MS-AUTHZ-SPEC.md §2 — nothing is shared
between systems except this repo's code, the `Authz.Client` package, and the OpenFGA model file).

**Read `docs/MS-AUTHZ-SPEC.md` first**. It is the contract —
identifiers, the catalog/materialization model, the API shape, the security boundary. This file only
covers house style for writing code here.

## ⚠️ Security boundary (see README.md for the full explanation)

ms-authz does not validate JWTs and does not verify the tenant/subject a caller passes in — it trusts
the caller, gated only by API key. **Never expose this service publicly**, in any environment. Any
change to `ApiKeyAuthenticationHandler` or the authorization filter setup in `Program.cs` needs to be
weighed against this — the API-key check is the *entire* security model.

## Structure — deliberately no Domain project

Three server-side projects, not the four-layer Clean Architecture shape `estudio-contable-backend`
uses:

- `MsAuthz.Api` — controllers, API-key auth, Program.cs, Dockerfile.
- `MsAuthz.Application` — interfaces + services. **No infrastructure dependency** — no
  OpenFga.Sdk reference, no file I/O. Settings classes (`CatalogSettings`, `OpenFgaSettings`) live in
  Infrastructure instead of a Domain project, because there is no Domain project: they're consumed
  nowhere else.
- `MsAuthz.Infrastructure` — `FileCatalogRepository` (loads/serves the catalog from a JSON file, see
  "Catalog is a file, ms-authz is stateless" below) and `OpenFgaGateway`, the only class talking to
  OpenFGA. Everything else in the codebase reaches OpenFGA through `IOpenFgaGateway`.
- `Authz.Client` — the NuGet SDK. Separate solution folder, its own README, packable independently.

## No MediatR / CQRS on the server side — this is a deliberate deviation from `estudio-contable-backend`

`MsAuthz.Api`/`Application`/`Infrastructure` are controllers + services, not commands/queries/handlers.
This service is small and thin by design (MS-AUTHZ-SPEC.md §1: "no reimplementa evaluación de
permisos, eso lo hace OpenFGA"); CQRS ceremony would outweigh what it buys here.

**`Authz.Client` is the one exception** — it registers a MediatR `IPipelineBehavior<,>` because that's
what its *consumers* use (estudio-contable-backend and, eventually, other systems). MediatR is a
dependency of the client SDK, not of ms-authz itself.

## Identifier construction is centralized — never concatenate by hand

`MsAuthz.Application/Common/Identifiers/OpenFgaIdentifiers.cs` is the only place that builds
`user:`/`role:`/`permission:` strings. If you find yourself writing `$"role:{tenant}|{code}"`
somewhere else, that's a bug — route it through `OpenFgaIdentifiers` instead, both for consistency and
because it's the one place validating that a tenant/role/permission code doesn't itself contain `:`,
`#`, `|` or whitespace (which would produce a malformed or ambiguously-parsed OpenFGA object).

The tenant separator is `|`. **Never `#`** — that's OpenFGA's reserved userset separator. This bit a
previous version of the spec (see MS-AUTHZ-SPEC.md §4's warning) — don't reintroduce it.

## `ListObjects` / partial `Read` are cross-tenant by construction — always filter

OpenFGA has no notion of "tenant"; `ListObjects` and a partial `Read` (user+relation, no object) both
return matches across every tenant, not just the one you're asking about. Every call site that uses
either MUST filter the result through `OpenFgaIdentifiers.TryStripTenantPrefix` /
`TryStripTenantRolePrefix` before doing anything with it. `EffectivePermissionsService` and
`UserRoleService` are the two existing examples — follow their shape for anything new.

## Idempotency is achieved via OpenFGA's conflict options, not read-before-write — except where the operation is genuinely a replace

`OpenFgaGateway.WriteTuplesAsync`/`DeleteTuplesAsync` pass `OnDuplicateWrites.Ignore` /
`OnMissingDeletes.Ignore` to the SDK's `ClientWriteOptions.Conflict`. This is what makes an add-only
write safe to re-run without reading current state first — don't reach for read-then-diff just to get
idempotency; it already exists at the OpenFGA call itself.

Two call sites do read-then-diff anyway, both for the same reason: the operation is a *replace*, not a
plain add, so the service genuinely needs to know what's currently there in order to know what to
remove.
- `UserRoleService.SetUserRolesAsync` — `PUT /users/{id}/roles` replaces a user's whole role set.
- `TenantProvisioningService.ProvisionTenantAsync` — a role's permission set in the catalog is the
  source of truth, so re-syncing a tenant via `POST /catalog/sync` must revoke a
  permission the catalog no longer grants a role, not just add newly-granted ones. It diffs per role
  via `IOpenFgaGateway.ReadObjectsForUserAsync` against the role's assignee userset.

Both delete before add, so a failure partway through leaves the tenant/user under-granted rather than
over-granted — that's the direction to fail in if you add a third case.

## Catalog is a file, ms-authz is stateless

The catalog (roles, permissions, role→permission, names, descriptions — MS-AUTHZ-SPEC.md §5) loads
once at startup from `Catalog:Path`, a JSON file validated and expanded in memory by
`CatalogFileLoader`. It fails fast: an invalid or missing catalog stops the process at startup
(`Program.cs` forces the load before serving any request), it never surfaces as a runtime 500 later.
Changing the catalog is: new file + redeploy + `POST /catalog/sync` with the tenants that need
re-expanding. ms-authz does not know what tenants exist — that's the consuming system's job, not a
list ms-authz keeps for itself (see `POST /catalog/sync`'s body in README.md).

## Logging pattern

Same shape as `estudio-contable-backend`'s mandatory logging pattern, adapted to services instead of
handlers: start with `{@Request}` (or a small anonymous object carrying the request's shape, when the
method doesn't take a single request object), log success with the counts/ids that matter, log
`Warning` before throwing a validation/not-found exception. There is no blanket `try/catch` — an
unexpected exception is left to reach `GlobalExceptionHandler`.

## Testing without infrastructure

`dotnet build MsAuthz.slnx && dotnet test MsAuthz.slnx` must always succeed with **no** Postgres, no
OpenFGA, no Docker. `tests/MsAuthz.UnitTests/TestDoubles/` holds in-memory fakes of `IOpenFgaGateway`
and `ICatalogRepository` — extend those, don't reach for a real OpenFGA or a database for anything
covered by this repo's unit tests. `CatalogFileLoader` is the one piece of Infrastructure worth
testing directly (it has real parsing/validation logic, not just a thin OpenFGA/HTTP wrapper) —
`tests/MsAuthz.UnitTests/Catalog/` exercises it against real temp files. If integration tests against
a real OpenFGA are added later, they need their own opt-in project/trait so the default `dotnet test`
run stays infrastructure-free.

## Business codes: there isn't one

Unlike `estudio-contable-backend`'s `BusinessCodes.cs` (mirrored by hand to the frontend), ms-authz has
no such catalog. Its callers are backends, not end users — `GlobalExceptionHandler` maps the small,
closed exception set in `MsAuthz.Application/Common/Exceptions/` to a `Title` + HTTP status, nothing
more granular. Don't add a business-code system here unless a real consumer need shows up.

## Commit messages

English, Angular / Conventional Commits: `<type>(<scope>): <subject>`. Types: `feat`, `fix`, `docs`,
`style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`. Subject up to 72 chars, lowercase,
imperative mood, no trailing period. The body explains **why** — the diff already shows what.

Note this differs from `estudio-contable`, whose repos use Spanish subjects by team convention. This
repo is a standalone reusable component, not part of that workspace, so it follows the default
English convention instead.

**No AI attribution.** Never add `Co-Authored-By`, "Generated with", or any other assistant
attribution line to commit messages or PR descriptions. The author is the person committing.
