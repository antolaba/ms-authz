# ms-authz

Authorization component over [OpenFGA](https://openfga.dev/). Each system that needs authorization
runs its **own** instance of ms-authz + OpenFGA + two Postgres databases — nothing is shared between
systems except this repo's code, the `Authz.Client` NuGet package, and the OpenFGA model file. Full
design rationale lives in `MS-AUTHZ-SPEC.md` (in the workspace root, one level up from this repo).

## ⚠️ Security boundary — read this before deploying anything

**ms-authz does not authenticate end users and does not validate JWTs.** Authentication is Keycloak's
job, done entirely by the *consuming* system before it ever calls ms-authz. ms-authz trusts whoever
calls it, gated only by a shared **API key** (`X-Api-Key` header) — the same trust model as
`ms-filestore`.

Concretely: a caller passes a Keycloak user id and a tenant code directly in the request (e.g.
`GET /me/permissions?tenant=jurol&subject=<keycloak-user-id>`), and **ms-authz does not verify either
one against Keycloak.** It assumes the caller already validated the end user's JWT in its own
pipeline.

**Consequence: ms-authz must never be reachable from outside the private network of the system that
owns it.** No public port, no public DNS, ever — in every environment (dev, qa, staging, prod). If you
are wiring this into a new system's infrastructure, treat this exactly like `ms-filestore`: internal
compose network only.

## What it is

- **A readable catalog.** OpenFGA stores structure, not names or descriptions. `role:jurol|vendedor`
  has no display name in OpenFGA — that lives in ms-authz's own `authz` Postgres database
  (`roles`, `permissions`, `role_permissions`).
- **A translator from business operations to OpenFGA tuples.** "Assign the Vendedor role to this user
  in jurol" becomes a `Write` of tuples. "Provision tenant jurol" expands the whole catalog into
  tuples for that tenant. Consuming systems never write OpenFGA tuples by hand — **ms-authz is the
  only thing that talks to OpenFGA.**
- **The hot-path endpoint**: `GET /me/permissions`, which every business request ultimately depends
  on (through `Authz.Client`'s cache — see below).

## Identifiers (MS-AUTHZ-SPEC.md §4)

Centralized in `MsAuthz.Application/Common/Identifiers/OpenFgaIdentifiers.cs` — nothing else in this
repo concatenates these strings by hand.

| Object | Shape | Example |
|---|---|---|
| User | `user:<keycloak user id>` | `user:8f3c1a94-...` |
| Role | `role:<tenant>\|<role code>` | `role:jurol\|vendedor` |
| Permission | `permission:<tenant>\|<Modulo.Accion>` | `permission:jurol\|Sales.Write` |

The separator is `|`, **never `#`** — `#` is OpenFGA's reserved userset separator
(`object#relation`).

**Cross-tenant leak, verified empirically against a real OpenFGA server (MS-AUTHZ-SPEC.md §4):**
`ListObjects` returns permission objects across **every** tenant the subject has a role in, not just
the one being asked about. `EffectivePermissionsService` filters by the `permission:<tenant>|` prefix
before responding to `GET /me/permissions` — this is covered by
`tests/MsAuthz.UnitTests/Services/EffectivePermissionsServiceTests.cs`. Skipping that filter leaks
permissions between companies.

## Endpoints (all behind the API key — MS-AUTHZ-SPEC.md §7)

| Endpoint | For |
|---|---|
| `GET /me/permissions?tenant=&subject=` | Flat list of permission codes. The hot path. |
| `GET /roles` | Readable catalog (roles + the permission codes each carries). |
| `GET /users/{id}/roles?tenant=` | Roles currently assigned to a user in a tenant. |
| `PUT /users/{id}/roles?tenant=` | Replaces the user's role set in that tenant. Body: `{ "roleCodes": [...] }`. |
| `POST /tenants` | Provisioning: expands the whole catalog into tuples for a new tenant. Body: `{ "tenantCode": "jurol" }`. Idempotent. |
| `POST /catalog/sync` | Re-expands the catalog for every known tenant — run after a catalog change. Idempotent. |
| `GET /health` | Liveness, no API key required. |

## Running locally

No Docker, no real Postgres/OpenFGA is required to build or test this repo — `dotnet build` and
`dotnet test` both run standalone (see the Testing section). To actually run `MsAuthz.Api` against a
live stack:

1. A Postgres instance with an `authz` database and `authz_user` (see
   `estudio-contable-infra/postgres/init/04-create-authorization-resources.sql` for how that's
   provisioned in the `estudio-contable` system's compose).
2. Run this repo's Liquibase changelog against it:
   ```bash
   cd liquibase
   liquibase --defaults-file=liquibase.properties update
   ```
   (or via the `ms-authz-liquibase` service in `docker/docker-compose.fragment.yml`).
3. An OpenFGA instance + store, with the model loaded from this repo's `openfga/model.fga`
   (`./scripts/bootstrap-openfga.sh`, or `fga model write --store-id <id> --file openfga/model.fga`,
   or the OpenFGA Playground in dev). That file is the validated, canonical model
   (MS-AUTHZ-SPEC.md §3, §12 step 2). Any .NET system adopting ms-authz loads the same file into its
   own store, unmodified.
4. `appsettings.Development.json` in `src/MsAuthz.Api` already points at the conventional local
   ports (Postgres `5432`, OpenFGA `8082`) — fill in `OpenFga:StoreId` with your store's id.
5. `dotnet run --project src/MsAuthz.Api`

The service listens on **port 6010** (MS-AUTHZ-SPEC.md §6), matching the `ms-filestore` family
(6000/6001).

### Docker

`src/MsAuthz.Api/Dockerfile` builds the API image (build context must be the repo root — see the
comment at the top of the file). `docker/docker-compose.fragment.yml` documents the service block a
consuming system's infra repo would add — it is **not** wired into any other repo automatically (this
repo does not touch other repos).

## `Authz.Client` — consuming ms-authz from a .NET system

`src/Authz.Client` is the SDK every .NET system consumes (MS-AUTHZ-SPEC.md §8), same molde as
`FileStore.Client`. It never calls OpenFGA — only ms-authz's own `GET /me/permissions`, and only on a
cache miss.

### Registration

```csharp
using Authz.Client;

// appsettings.json: { "Authz": { "BaseUrl": "http://ms-authz:6010", "ApiKey": "...", "CacheTtl": "00:05:00" } }
builder.Services.AddAuthzClient(builder.Configuration);

// The host MUST also register its own IAuthzRequestContextAccessor — Authz.Client cannot provide
// one generically, since it doesn't know how the host resolves the current tenant/user. See below.
builder.Services.AddScoped<IAuthzRequestContextAccessor, MyAuthzRequestContextAccessor>();

// AddAuthzClient does NOT register the pipeline behavior: only the host knows the right slot.
// Add it in your own MediatR config, after whatever resolves tenant/user, before validation.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    // ... host behaviors that resolve tenant and user ...
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PermissionAuthorizationBehavior<,>));
    // ... validation, etc ...
});
```

### `IAuthzRequestContextAccessor`

`PermissionAuthorizationBehavior` needs to know the current request's tenant code and Keycloak
subject id, but `Authz.Client` cannot depend on any host system's own request-context type. Implement
this interface once per host, wrapping whatever it already resolves identity from:

```csharp
// estudio-contable-backend example — the behavior runs AFTER TenantUserAuthorizationBehavior,
// which is what guarantees these are populated (MS-AUTHZ-SPEC.md §9).
public class EstudioContableAuthzRequestContextAccessor(IRequestContext requestContext) : IAuthzRequestContextAccessor
{
    // RealmName, NOT SchemaName: the realm is the tenant's external identity (it's what the
    // frontend sends in X-Tenant); the schema is an internal storage detail. Tenant provisioning
    // must write tuples with this same convention or nothing will ever match.
    public string? TenantCode => requestContext.CurrentTenant?.RealmName;
    public string? SubjectId => requestContext.CurrentTokenUserInfo?.KeycloakUserId;
}
```

### Protecting a command/query

```csharp
[RequirePermission("Sales.Write")]
public class CreateInvoiceCommandRequest : IRequest<CreateInvoiceCommandResponse> { }
```

`PermissionAuthorizationBehavior<TRequest, TResponse>` is a MediatR pipeline behavior: a request
without the attribute passes straight through (this is what makes rollout incremental — see
MS-AUTHZ-SPEC.md §9), one with it is checked against the subject's cached effective permissions and
throws `Authz.Client.Exceptions.AuthzForbiddenAccessException` if any listed code is missing.
**`RequirePermission` with multiple codes is an AND**, not an OR — every code must be present.

Register the behavior in the host's own MediatR pipeline (see the snippet above), **after** whatever
behavior resolves tenant/user context and **before** validation — so an unauthorized caller never
receives a validation error that discloses details about a resource it cannot access. In
`estudio-contable-backend` that slot is between `TenantUserAuthorizationBehavior` and
`ValidationBehaviour`.

`AuthzForbiddenAccessException` is intentionally independent of any host's exception hierarchy — wire
it into your global exception handler next to your own forbidden-access exception so both map to the
same 403.

## Repository layout

```
src/
  MsAuthz.Api/              Controllers, API-key auth, Program.cs, Dockerfile
  MsAuthz.Application/      Interfaces + services. No infrastructure dependency.
  MsAuthz.Infrastructure/   EF Core/Npgsql (authz catalog db) + the OpenFGA client
  Authz.Client/             NuGet SDK for .NET consumers
tests/
  MsAuthz.UnitTests/        Identifiers, tenant filtering, idempotent materialization
  Authz.Client.UnitTests/   Attribute/extension + pipeline-behavior tests
liquibase/                  This repo's own migrations for the `authz` database
docker/                     Dockerfile lives in src/MsAuthz.Api; compose fragment lives here
```

No `MsAuthz.Domain` project — deliberately. See `CLAUDE.md` for that and other repo conventions
(no MediatR/CQRS on the server side, no EF migrations, logging pattern, etc).

## Testing

```bash
dotnet build MsAuthz.slnx
dotnet test MsAuthz.slnx
```

Both run with **no external dependencies** — no Postgres, no OpenFGA. `MsAuthz.Infrastructure`'s
`OpenFgaGateway` and EF repositories are never exercised directly in tests; instead, the Application
layer's services are tested against in-memory fakes of `IOpenFgaGateway`, `ICatalogRepository` and
`ITenantRegistry` (`tests/MsAuthz.UnitTests/TestDoubles/`). This is deliberate: the interfaces those
services depend on are the actual contract worth testing at this layer, and it keeps the whole suite
runnable offline.
