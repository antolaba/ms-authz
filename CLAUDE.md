# ms-authz — repo conventions

Authorization component over OpenFGA. Standalone repo, own git history, part of the `Estudio-Martinez`
workspace but consumed by any number of unrelated systems (MS-AUTHZ-SPEC.md §2 — nothing is shared
between systems except this repo's code, the `Authz.Client` package, and the OpenFGA model file).

**Read `docs/MS-AUTHZ-SPEC.md` first**. It is the contract —
identifiers, the catalog, the API shape, the security boundary. This file only
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
  "Catalog is a file, ms-authz is stateless" below), `OpenFgaGateway` (the only class issuing tuple
  reads/writes to OpenFGA; everything else goes through `IOpenFgaGateway`) and
  `OpenFgaBootstrapHostedService` (finds or creates the store and writes the embedded model at
  startup, through `IOpenFgaAdminApi`). `openfga/model.json` is embedded into this assembly and is
  generated from `openfga/model.fga` — regenerate it in the same commit if the DSL ever changes.
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
`user:`/`role:` strings. Both carry the tenant, the user included (`user:<tenant>|<subject>`): a
subject only exists inside a tenant, and scoping the user object is what keeps a read of its roles
bounded to one tenant's tuples. If you find yourself writing `$"role:{tenant}|{code}"`
somewhere else, that's a bug — route it through `OpenFgaIdentifiers` instead, both for consistency and
because it's the one place validating that a tenant/role/permission code doesn't itself contain `:`,
`#`, `|` or whitespace (which would produce a malformed or ambiguously-parsed OpenFGA object).

The tenant separator is `|`. **Never `#`** — that's OpenFGA's reserved userset separator. This bit a
previous version of the spec (see MS-AUTHZ-SPEC.md §4's warning) — don't reintroduce it.

## OpenFGA holds assignments only; permissions are resolved from the catalog in memory

The store has one kind of tuple: `user:<t>|<subject> assignee role:<t>|<code>`. `GET /me/permissions`
does a paginated `Read` of the subject's roles and unions their permission codes from the catalog.
Nothing is materialised per tenant, OpenFGA evaluates nothing, and `ListObjects` (which silently
truncates at a server-side cap) is not used — don't reintroduce it. Every partial `Read` result is
still filtered through `OpenFgaIdentifiers.TryStripTenantRolePrefix` as defence in depth against a
malformed tuple; `EffectivePermissionsService` and `UserRoleService` are the two examples.

## Idempotency is achieved via OpenFGA's conflict options, not read-before-write — except where the operation is genuinely a replace

`OpenFgaGateway.WriteTuplesAsync`/`DeleteTuplesAsync` pass `OnDuplicateWrites.Ignore` /
`OnMissingDeletes.Ignore` to the SDK's `ClientWriteOptions.Conflict`. This is what makes an add-only
write safe to re-run without reading current state first — don't reach for read-then-diff just to get
idempotency; it already exists at the OpenFGA call itself.

One call site does read-then-diff anyway: `UserRoleService.SetUserRolesAsync`, because
`PUT /users/{id}/roles` is a *replace*, not a plain add, so it needs to know what's currently there
in order to know what to remove. It deletes before it adds, so a failure partway through leaves the
user under-granted rather than over-granted — that's the direction to fail in if you add another
case.

## Catalog is a file, ms-authz is stateless

The catalog (roles, permissions, role→permission, names, descriptions — MS-AUTHZ-SPEC.md §5) loads
once at startup from `Catalog:Path`, a JSON file validated and expanded in memory by
`CatalogFileLoader`. It fails fast: an invalid or missing catalog stops the process at startup
(`Program.cs` forces the load before serving any request), it never surfaces as a runtime 500 later.
A role's `PermissionCodes` in the snapshot is its effective set: `"*"` and `"includes"` are expanded
by the loader, so nothing downstream knows about wildcards or role inclusion. Changing the catalog is:
new file + redeploy, and it applies to every tenant immediately. ms-authz does not know what tenants
exist and never needs to.

## Logging pattern

Same shape as `estudio-contable-backend`'s mandatory logging pattern, adapted to services instead of
handlers: start with `{@Request}` (or a small anonymous object carrying the request's shape, when the
method doesn't take a single request object), log success with the counts/ids that matter, log
`Warning` before throwing a validation/not-found exception. There is no blanket `try/catch` — an
unexpected exception is left to reach `GlobalExceptionHandler`.

## Testing without infrastructure

`dotnet build MsAuthz.slnx && dotnet test MsAuthz.slnx` must always succeed with **no** Postgres, no
OpenFGA, no Docker. `tests/MsAuthz.UnitTests/TestDoubles/` holds in-memory fakes of `IOpenFgaGateway`, `IOpenFgaAdminApi`
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
