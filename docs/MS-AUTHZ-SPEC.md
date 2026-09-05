# ms-authz — autorización como componente reusable, sobre OpenFGA

**Estado:** Para revisión
**Verificado contra:** el código al 2026-08-27 — `estudio-contable-backend` (pipeline MediatR,
`RequestContext`, `UserTenant`), `estudio-contable-frontend` (lib `libs/services/authorization`),
`estudio-contable-infra` (`docker-compose.yml`, `postgres/init/`, Liquibase `system`/`tenant`).
**Alcance:** el componente de autorización y su primer consumidor (`estudio-contable`). El catálogo
final de permisos por módulo se arma incremental (§7), no se cierra acá.

Este documento es autocontenido. Reemplaza y absorbe a `RBAC-SPEC.md` (motor propio con tablas
`role`/`permission` evaluadas a mano) y a `MS-ACCESOS-SPEC.md` (mismo motor que acá, pero como
servicio central compartido entre sistemas — premisa descartada, ver §2). Ambos fueron borrados.

---

## Punto de partida: Keycloak es AuthN, la autorización es nuestra

Keycloak se usa para **autenticación** (¿quién sos?) y queda así. La **autorización de negocio**
(¿qué podés hacer en este tenant?) se modela y se evalúa fuera de Keycloak.

**Por qué los permisos de negocio no son roles de Keycloak:** cada tenant es un *realm* separado (ver
el contrato de identidad en el `CLAUDE.md` raíz). Modelarlos como roles de Keycloak significaría
administrarlos y mantenerlos sincronizados **realm por realm** vía la Admin API, sin versionado, sin
Liquibase, y desacoplado del resto del sistema — que sí versiona todo lo que cambia seguido, hasta los
`BusinessCodes` que se mirrorean a mano entre repos. Los permisos de negocio cambian con cada feature;
tienen que vivir donde vive el resto del dominio.

**Un usuario puede tener varios roles por tenant**, y el permiso efectivo es la **unión** de los
permisos de todos ellos — patrón estándar de ERP (Odoo `res.groups`, Dynamics security roles). No hay
selector de "rol activo" tipo NetSuite: los roles no son mutuamente excluyentes, solo suman acceso.
Tampoco hay acceso cross-tenant especial ni super-admin que bypasee el chequeo de tenant: cada usuario
tiene su fila `UserTenant` por empresa, como ya existe hoy.

**El terreno está limpio.** Nada de lo que funciona hoy depende de roles de Keycloak:

- El pipeline de MediatR ya separa AuthN de AuthZ en dos behaviors distintos.
  `HybridKeycloakAuthenticationBehavior` valida el JWT (offline o introspección) y llena
  `RequestContext.CurrentTokenUserInfo` — puro AuthN. `TenantUserAuthorizationBehavior` valida que
  exista un `UserTenant` activo para `(KeycloakUserId, TenantId)` — AuthZ rudimentario, ya resuelto
  contra Postgres y no contra Keycloak. Es el patrón exacto que se extiende (§9).
- `TokenUserInfo.Roles` (los roles que trae el token) **no se usa en ningún lado** para decidir
  autorización. Campo muerto: no hay nada que migrar, es una hoja en blanco.
- El lib `authorization` del frontend existe pero está inerte (§10).

---

## 1. Qué es

Un **componente reusable** que cada sistema instancia por su cuenta, igual que `ms-filestore`:
un repo, una imagen, un paquete NuGet y un archivo de modelo. No es un servicio corporativo al que
todos se conectan.

Resuelve dos cosas que OpenFGA por sí solo no da:

- **Catálogo legible.** OpenFGA guarda estructura, no metadata humana. La tupla
  `role:jurol|vendedor` no tiene nombre para mostrar, ni descripción, ni agrupación por módulo.
  Eso vive en el Postgres de `ms-authz`.
- **Traducción de operaciones de negocio a tuplas.** "Asignar el rol Vendedor a este usuario en
  jurol" se convierte en `Write` de tuplas; "dar de alta el tenant jurol" expande el catálogo entero.
  Los sistemas consumidores nunca escriben tuplas a mano.

Y expone `GET /me/permissions`, que es lo único que toca el hot path.

**`ms-authz` es el único que le habla a OpenFGA.** Los sistemas consumidores no conocen su API. Si
mañana se cambia de motor, los consumidores no se enteran.

## 2. Principio rector: nada se comparte entre sistemas

Cada sistema levanta su propia instancia completa. Lo único que viaja entre sistemas es **código**.

```
estudio-contable                          sistema-2
├── Postgres (su instancia)               ├── Postgres (su instancia)
│   ├── keycloak                          │   ├── ...
│   ├── estudio-contable                  │   ├── openfga
│   ├── openfga     ← tuplas              │   └── authz
│   └── authz       ← catálogo            ├── OpenFGA (container)
├── OpenFGA (container)                   ├── ms-authz (container)
├── ms-authz (container)                  └── backend → Authz.Client
└── backend → Authz.Client (NuGet)
```

Compartido entre sistemas: el repo `ms-authz`, el paquete `Authz.Client`, y el archivo del modelo DSL
(§3). Nada más — ni base, ni motor, ni servicio.

**Consecuencia que hay que aceptar de entrada:** no existe un lugar donde preguntar "qué puede hacer
esta persona en todos mis sistemas". Cada instancia sabe lo suyo. Keycloak sigue siendo la identidad
compartida, pero la autorización queda fragmentada por diseño. Un panel único de administración de
accesos, si algún día se quiere, hay que construirlo aparte consultando N instancias.

**Esto reemplaza el §3 del spec anterior** ("un store por `application`, todos dentro de un OpenFGA").
Con instancia por sistema, cada OpenFGA tiene **exactamente un store** y el aislamiento entre
aplicaciones lo da la separación de deployments, no el store. También desaparece el concepto de
`application` en el catálogo: `ms-authz` sirve a un solo sistema y no necesita saber a cuál.

## 3. Modelo de autorización

**El mismo archivo DSL en todos los sistemas.** Es lo que hace que esto sea un estándar y no un
patrón que cada uno reimplementa.

```
model
  schema 1.1

type user

type role
  relations
    define assignee: [user, role#assignee]

type permission
  relations
    define granted: [role#assignee]
```

Tres tipos, dos relaciones. Los roles y permisos concretos **no viven en el modelo** — viven en
tuplas y en el catálogo. Por eso el modelo no cambia nunca: agregar `Purchasing.ApproveOrder` es una
tupla, no una versión nueva del modelo replicada en N sistemas desplegados.

### Por qué `permission` es un tipo de objeto y no una relación

La alternativa descartada era declarar una relación por acción sobre un tipo `module`
(`define reader`, `define writer`, `define void_invoice`, …). Se descartó por tres razones:

1. **Rompe el estándar.** Los permisos serían parte del DSL, así que el modelo de cada sistema
   divergiría — el de estudio-contable tendría `void_invoice` y el del sistema siguiente no. No
   quedaría un modelo común que compartir.
2. **Cada feature versionaría el modelo de autorización.** Un permiso nuevo sería un cambio de
   esquema, no un dato.
3. **`/me` costaría N llamadas.** Con `permission` como tipo, un solo
   `ListObjects(user, "granted", "permission")` devuelve la lista plana completa. Con una relación
   por acción hace falta un `ListObjects` por relación del modelo.

Lo que se resigna: los permisos quedan planos dentro de OpenFGA, sin aprovechar la composición del
motor para el RBAC de módulo. Para RBAC puro da igual, y es exactamente el caso de v1.

También se evaluó un híbrido (`module` con `reader`/`writer`/`admin` genéricos para lo grueso +
`permission` para lo fino). Se sostiene, pero deja dos formas de expresar lo mismo y obliga a `/me` a
mergear dos `ListObjects`. No se paga sola.

### Jerarquía de roles: va desde el día uno

El `role#assignee` dentro de `define assignee: [user, role#assignee]` permite que un rol incluya a
otro ("Gerente incluye Vendedor") declarativamente, en un solo lugar, en vez de depender de que
alguien se acuerde de asignar los dos roles a cada usuario.

**Se incluye ahora aunque v1 no lo use.** El sentido de este modelo es que quede fijo; agregarlo
después sí sería una versión nueva del modelo, a propagar en todos los sistemas ya desplegados. El
costo hoy es un token en el DSL.

### Camino a ReBAC

Cuando aparezca la primera regla del tipo "este contador ve solo estos clientes" — bastante probable
en un ERP contable — se agrega un tipo nuevo que compone con lo anterior:

```
type customer
  relations
    define assigned_accountant: [user]
    define viewer: [role#assignee] or assigned_accountant
```

Es una extensión del modelo, no un sistema nuevo, y no toca nada de lo de arriba.

## 4. Identificadores

⚠️ **El DSL del spec anterior era inválido.** Usaba `#` dentro de los ids (`module:jurol#sales`,
`role:jurol#vendedor`), y en OpenFGA `#` es el separador reservado para referencias a userset
(`objeto#relación`): un object válido tiene exactamente un `:` y ningún `#` ni espacios. Las tuplas
de ejemplo de aquel documento (`role:jurol#vendedor#assignee`) tenían doble `#` y no cargan.

**El separador de tenant es `|`.**

| Objeto | Forma | Ejemplo |
|---|---|---|
| Usuario | `user:<keycloak user id>` | `user:8f3c1a94-...` |
| Rol | `role:<tenant>\|<role code>` | `role:jurol\|vendedor` |
| Permiso | `permission:<tenant>\|<permission code>` | `permission:jurol\|Sales.Write` |

El código de permiso es `Modulo.Accion`: string legible, identificador
interno — no un código corto tipo `BusinessCodes`, que son otra cosa (mensajes de error al usuario).

### Tuplas

Asignación de usuario a rol, por tenant:

| User | Relation | Object |
|---|---|---|
| `user:8f3c1a94-...` | `assignee` | `role:jurol\|vendedor` |

Catálogo materializado (rol → permiso), por tenant:

| User | Relation | Object |
|---|---|---|
| `role:jurol\|vendedor#assignee` | `granted` | `permission:jurol\|Sales.Read` |
| `role:jurol\|vendedor#assignee` | `granted` | `permission:jurol\|Sales.Write` |

Jerarquía de roles (Gerente incluye Vendedor), por tenant:

| User | Relation | Object |
|---|---|---|
| `role:jurol\|gerente#assignee` | `assignee` | `role:jurol\|vendedor` |

### Consultas

- `Check(user:8f3c…, granted, permission:jurol|Sales.Write)` → chequeo puntual.
- `ListObjects(user:8f3c…, granted, permission)` → la lista para `/me/permissions`.

**Un usuario con varios roles en el mismo tenant queda resuelto gratis:** cada rol aporta sus tuplas
y OpenFGA evalúa "es `granted` si **algún** camino lo conecta" — unión de permisos, sin código propio
para calcularla. Es la unión de permisos del "Punto de partida", resuelta por el motor.

**Detalle a implementar, no sale solo — verificado empíricamente:** `ListObjects` devuelve los
permisos del usuario en **todos** los tenants donde tenga roles. Con un usuario con rol en `jurol` y
en `otraempresa`, el servidor devolvió:

```json
{"objects":["permission:jurol|Sales.Read","permission:jurol|Sales.Write","permission:otraempresa|Purchasing.ApproveOrder"]}
```

El motor no filtra por tenant — no tiene forma de saber que el prefijo significa algo. `ms-authz`
filtra por `<tenant>|` del lado del cliente antes de responder `/me/permissions`. Con uno o dos
tenants por usuario es irrelevante en costo, pero es lógica que hay que escribir y testear.

## 5. Catálogo y materialización

**v1: catálogo de roles fijo por sistema.** Los roles y qué permisos trae cada uno se definen por
seed/deploy, no desde la UI. No hay pantalla de "armar un rol" — solo de "asignar rol a usuario".

El catálogo vive en el Postgres de `ms-authz`, migrado con Liquibase como el resto del sistema. Es la
fuente de verdad legible: nombres, descripciones, agrupación por módulo.

```
roles              (id, code, name, description)
permissions        (id, code, module, description)
role_permissions   (role_id, permission_id)
tenants            (id, code)                      -- ver nota
```

*(→ ver §15: este schema quedó reemplazado por un archivo JSON — ms-authz ya no tiene base propia.)*

**Nota sobre `tenants`:** no estaba en el diseño original y se agregó al implementar. `POST /catalog/sync`
tiene que iterar sobre todos los tenants para re-expandir el catálogo, y esa lista tiene que vivir en
algún lado durable — OpenFGA no la puede enumerar, porque el tenant es solo un prefijo de string dentro
de los ids, no una entidad. Es la tabla mínima que hace posible el §5, no un registro de tenants
paralelo al de `public.tenants` del sistema consumidor.

### Las tuplas rol→permiso se materializan por tenant

El catálogo es global al sistema, pero las asignaciones **tienen** que llevar tenant: el mismo usuario
puede ser Vendedor en jurol y no tener nada en otra empresa. Así que el `role` de la tupla lleva
tenant, y en consecuencia el vínculo rol→permiso también se materializa por tenant.

La alternativa evaluada era un rol global del que heredan los roles de tenant
(`role:vendedor#assignee @ role:jurol|vendedor#assignee`), definiendo rol→permiso una sola vez. Se
descartó por dos razones:

1. **No sobrevive a roles por empresa.** Con las tuplas ya scopeadas por tenant, el día que una
   empresa quiera su propio rol se tocan las tuplas de esa empresa y nada más. Con la variante global
   habría que expandir todo por tenant, en producción, en cada sistema desplegado.
2. **Complica `/me`, que es el hot path.** Con materialización por tenant, `ListObjects` devuelve
   permisos ya etiquetados y el filtro es un prefijo. Con herencia global habría que resolver de qué
   tenant vino cada permiso por otra vía.

**Lo que cuesta:** dar de alta un tenant expande rol × permiso en tuplas, y agregar un permiso a un
rol del catálogo obliga a un job de re-expansión sobre todos los tenants. Es idempotente y es
exactamente la forma operativa que el equipo ya vive con `migrate-all-tenant.sh` — aplicar un cambio a
todos los tenants, uno por uno. No es un patrón nuevo.

**El volumen no es problema:** 30 tenants × 8 roles × 40 permisos ≈ 10k tuplas. Para OpenFGA es nada.

## 6. Despliegue

Dentro de un sistema, el Postgres **sí** se comparte a nivel instancia — una base por servicio, con su
propio usuario, siguiendo el patrón que la instancia ya tiene con Keycloak
(`postgres/init/01-create-keycloack-resources.sql`).

```
Postgres :5432  (una instancia, un backup, un compose)
├── keycloak          keycloak_user      ← migra Keycloak          (ya existe)
├── estudio-contable  api/migration_user ← migra Liquibase         (ya existe)
├── openfga           openfga_user       ← migra `openfga migrate`     NUEVO
└── authz             authz_user         ← migra Liquibase             NUEVO (→ ver §15: eliminada)
```

**OpenFGA necesita base propia, no es preferencia:** migra su esquema con su propia herramienta
(`openfga migrate`), no con Liquibase, así que no puede convivir dentro de la base que Liquibase
gestiona. Y la metadata de `ms-authz` tampoco va dentro de `estudio-contable`: acoplar el componente
de autorización a la base de uno de sus consumidores rompe el estándar que se busca.

### Puertos (dev local)

Los defaults de OpenFGA (8080 HTTP, 8081 gRPC) chocan con Keycloak, que ya tiene el 8080. Se remapean:

| Servicio | Puerto host | Nota |
|---|---|---|
| OpenFGA HTTP | 8082 | container 8080 |
| OpenFGA gRPC | 8083 | container 8081 |
| OpenFGA Playground | 3000 | **solo dev** — UI sin autenticación sobre el store |
| ms-authz | 6010 | familia de `ms-filestore` (6000/6001) |

`OPENFGA_PLAYGROUND_ENABLED` debe ser `false` en qa/staging/prod.

### Servicios en el compose de infra

`openfga-migrate` con `profiles: [migration]`, siguiendo el patrón del servicio `liquibase` que ya
existe — se corre una vez antes de levantar, y de nuevo en cada bump de imagen.

Imagen: `openfga/openfga:v1.19.0` (última al 2026-08-27), pineada — no `latest`.

Los scripts de `postgres/init/` solo se ejecutan cuando el data dir está vacío. En un entorno nuevo
—que es el caso hoy: `docker/postgres-data` está vacío— corren solos y crean `openfga` y `authz` sin
intervención. Si el data dir ya estuviera poblado no se re-ejecutan, y el SQL equivalente hay que
aplicarlo a mano (documentado en el README de infra).

## 7. `ms-authz` — responsabilidades y API

Repo nuevo. Chico: no reimplementa evaluación de permisos, eso lo hace OpenFGA.

| Endpoint | Para qué |
|---|---|
| `GET /me/permissions` | Lista plana de códigos para el tenant y usuario dados. Lo consume el SDK. |
| `GET /roles` | Catálogo legible, para pintar la pantalla de administración. |
| `GET /users/{id}/roles` · `PUT` | Asignación de roles a un usuario en un tenant. |
| `POST /tenants` | Provisioning: expande el catálogo a tuplas para el tenant nuevo. → ver §15: absorbido por `POST /catalog/sync`. |
| `POST /catalog/sync` | Job idempotente de re-expansión tras un cambio de catálogo. |

### Frontera de seguridad

`ms-authz` **no valida JWTs** — no hace AuthN, eso es Keycloak ("Punto de partida"). Confía en quien lo
llama, autenticado por API key, igual que `ms-filestore`. **Por lo tanto no puede exponerse
públicamente**: va en la red interna del compose, nunca con puerto publicado en qa/staging/prod.

Quien llama le pasa el keycloak user id y el código de tenant; `ms-authz` no los verifica contra
Keycloak. La cadena de confianza es: el backend consumidor ya validó el JWT en su propio pipeline
antes de preguntar.

## 8. SDK cliente (`Authz.Client`, NuGet)

Mismo molde que `FileStore.Client`, que ya se consume del feed privado de GitHub.

- `IAuthzClient.GetEffectivePermissionsAsync(tenantCode, subjectId, ct)` — patrón *pull*, cacheado en
  `IMemoryCache` con TTL corto (5 min). **No se llama a OpenFGA por request de negocio.**
- `RequirePermissionAttribute` + `PermissionAuthorizationBehavior` (MediatR) empaquetados en el SDK,
  para que cada sistema .NET obtenga el enforcement sin reimplementarlo. Esto es lo que hace real el
  "estandarizar".
- **Varios códigos en un `[RequirePermission]` son AND**, no OR: se exigen todos. La razón es la
  dirección de la falla — si alguien asume OR y recibe AND, el efecto es denegar de más (molesto,
  seguro); al revés sería un bypass de autorización. Además la jerarquía de roles (§3) ya resuelve la
  subsunción, que es el caso que normalmente motiva un OR. Un command que toca dos módulos
  (una venta que descuenta stock) lista los dos: `[RequirePermission("Sales.Write", "Stock.Write")]`.
- `IAuthzRequestContextAccessor`: interfaz chica que implementa cada host para decirle al behavior de
  dónde sacar tenant y subject, así el SDK no depende del tipo de contexto de request de ningún
  sistema en particular. **El código de tenant es el `RealmName`**, no el `SchemaName`: el realm es la
  identidad externa del tenant (es lo que el frontend manda en `X-Tenant`), el schema es un detalle de
  almacenamiento. El provisioning (§11) tiene que escribir las tuplas con la misma convención — si se
  desalinean, no matchea nada y el síntoma es un 403 sin causa visible.
- **`AddAuthzClient()` NO registra el pipeline behavior**, a propósito. El orden en el pipeline lo sabe
  solo el host, y MediatR agrupa todos los `cfg.AddBehavior` en un bloque contiguo: un registro hecho
  fuera de ese bloque cae entero antes o entero después de los del host, nunca intercalado. Si el SDK
  lo registrara, cada consumidor tendría que sacar y volver a agregar el descriptor a mano para
  ubicarlo bien — un hack replicado en cada sistema, justo lo que el SDK viene a evitar. El host lo
  agrega en su propio `AddMediatR`, en el slot que necesita.

Un cambio de rol tarda hasta el TTL en reflejarse — aceptable para un ERP sin requisito de revocación
instantánea. Si hiciera falta invalidación inmediata, se agrega un `permissions_stamp` por usuario que
entre en la clave de cache.

## 9. Integración con `estudio-contable-backend`

El terreno está limpio: `TokenUserInfo.Roles` (los roles del token de Keycloak) no se usa en ningún
lado para decidir autorización. No hay nada que migrar.

El pipeline MediatR (`EstudioContable.Application/DependencyInjection.cs`) registra hoy cinco
behaviors en orden: `UnhandledException` → `Performance` → `HybridKeycloakAuthentication` →
`TenantUserAuthorization` → `Validation`. **El nuevo va después de `TenantUserAuthorization`**, que es
quien deja resuelto `RequestContext.CurrentUserTenantInfo`.

El atributo sigue exactamente la forma de los dos que ya existen en
`Application/Common/Attributes/` (`SkipAuthenticationAttribute`, `RequireIntrospectionAttribute`),
leídos por extension methods sobre `Type`:

```csharp
[RequirePermission("Sales.Write")]
public class CreateInvoiceCommandRequest : IRequest<IResult<CreateInvoiceCommandResponse>> { }
```

Sin atributo, el request se comporta como hoy: solo pasa el chequeo de membresía de
`TenantUserAuthorizationBehavior`. **La migración es incremental** — no hace falta anotar los 200+
commands/queries de una vez.

Falla con el mismo `ForbiddenAccessException` que ya usa `TenantUserAuthorizationBehavior` → mismo 403
RFC 7807 → **cero cambios en el contrato de errores del frontend**.

## 10. Frontend

`libs/services/authorization` ya existe con la forma correcta (`AuthorizationService`, `role.guard.ts`,
`has-role.directive.ts`, `isAdmin()`) pero está **inerte**: lee `UserInfo.roles`, que nunca se
popula en `auth.service.ts`. Hoy `isAdmin()` devuelve `false` siempre. Es scaffolding sin cablear, no
código en producción que haya que migrar con cuidado.

- `UserInfo.roles` → `permissions: string[]`, poblado desde `GET /api/v1/me` (no desde el token).
- `HasRoleDirective` → `HasPermissionDirective` (`*hasPermission="'Sales.Write'"`), `roleGuard` →
  `permissionGuard`. Mismo mecanismo interno.
- Esto es **solo UX** — ocultar botones y menús. La autorización real la hace siempre el backend.

## 11. Provisioning de tenants

Crear un tenant hoy son tres pasos (realm Keycloak + fila en `public.tenants` + schema Postgres). Se
suma un cuarto: registrarlo en `ms-authz`, que expande el catálogo a tuplas. Toca `create-tenant.sh`.

Un tenant creado sin ese paso autentica bien pero no tiene ningún permiso — falla en todo command
anotado. Es el mismo modo de falla que ya tiene el `tenant-schema-mapper` faltante (403 en todo
endpoint de negocio), así que conviene un script de reparación equivalente a
`add-tenant-schema-mapper.sh`.

## 12. Orden de implementación

1. ✅ **OpenFGA en el compose de infra** + init script de las bases `openfga` y `authz`. Hecho en
   `feat/authz` (sin commitear). Las bases `openfga` y `authz` las crea el init script en el primer
   arranque, porque el data dir está vacío (§6).
2. ✅ **Modelo validado contra un OpenFGA real** (artefactos en `ms-authz/openfga/` — el modelo DSL
   es uno de los tres artefactos compartidos del §2, así que vive con el componente, no en el repo de
   infra de un consumidor) (`v1.19.0`, store en memoria). El DSL del §3 quedó
   **sin cambios**: cargó tal cual, y el separador `|` no dio fricción. Artefactos: `model.fga`, `model.fga.yaml` (suite nativa de `fga model test`,
   7 casos / 12 checks, en verde) y `tuples.json`. Cubre permiso directo, permiso ausente, unión de dos
   roles, jerarquía en ambas direcciones, aislamiento entre tenants contra permisos que **sí** están
   concedidos en el otro tenant, usuario sin roles, y el `ListObjects` cross-tenant del §4.
3. ✅ **`ms-authz`**: repo creado en `~/Code/ms-authz` (sin commitear) — **fuera del workspace de
   estudio-contable a propósito**: es el componente reusable del §1/§2, no una pieza de este
   sistema. Su único vínculo con estudio-contable es el paquete `Authz.Client`, consumido por un
   feed en disco hasta que se publique. Tres proyectos
   + tests, Liquibase propio para el catálogo, `OpenFgaGateway` como única clase que toca OpenFGA,
   y los cinco endpoints del §7. Build limpio, 47 tests en verde sin infraestructura levantada.
4. ✅ **`Authz.Client`**: pull + cache + `RequirePermissionAttribute` + `PermissionAuthorizationBehavior`,
   como cuarto proyecto del mismo repo.
5. ✅ **`estudio-contable-backend`**: SDK referenciado (`PackageReference` contra un feed local en
   disco, temporal hasta publicar al feed de GitHub — nunca un `ProjectReference` cruzado entre
   repos, que ataría el build a rutas relativas). Behavior registrado entre
   `TenantUserAuthorization` y `Validation`. `ProductCategory` anotado entero (10 requests) con
   `ProductCategory.Read` / `ProductCategory.Write`. Build sin warnings nuevos, 1038 tests en verde.
   **Falta confirmar el 403 real de punta a punta** — requiere el stack levantado.
6. ✅ **`GET /api/v1/me`** — ⚠️ **ya existía**: `Api/Controllers/v1/MeController.cs` +
   `Application/Features/Me/Queries/GetMe/`. No hay que crearlo. `GetMeQueryResponse` ya tiene un
   campo `Roles` (`string[]`), que es el campo muerto del "Punto de partida". El trabajo real es
   agregarle `Permissions` delegando al SDK, y retirar `Roles`.
7. ✅ **Provisioning** (§11): `create-tenant.sh` pasó a 5 pasos, con el registro en `ms-authz`
   idempotente y de falla ruidosa (un tenant a medio provisionar es peor que uno que falló entero).
   `register-tenant-authz.sh` nuevo para reparar tenants que ya existen. Documentado el modo de falla
   en `DEV-TENANT.md` y `README.md`.
8. ✅ **Pantalla de administración de roles**. Backend: `IamController` con los tres endpoints,
   usuarios desde `UserTenant` y roles desde un `IAuthzAdminClient` nuevo en el SDK —
   deliberadamente **sin caché**, porque cachear administración daría lecturas rancias justo después
   de asignar un rol. Frontend: feature lib `role-assignments`, gateada con `Iam.Read` y el guardar
   con `Iam.ManageRoles`. Más el changeset `system/032` que registra la entrada en `menu_modules`,
   sin la cual la pantalla existe pero no aparece en el sidebar (que es backend-driven).
9. ✅ **Frontend**: lib `authorization` migrado a permisos contra `/api/v1/me`, `*hasPermission` y
   `permissionGuard` en su lugar, `UserInfo.roles` eliminado. Fail-closed si el campo no viene
   (que es hoy). 24 tests en verde. Hecho antes de tiempo porque no dependía del backend.
10. **Rollout incremental**: anotar el resto de commands/queries módulo por módulo, agregando
    `*hasPermission` en la UI del mismo commit (criterio de "feature de dos repos" que el equipo ya sigue).
11. **Segundo sistema .NET**: su propia instancia + el mismo modelo y SDK — valida que el estándar es
    real y no solo en el papel.

Los pasos 1-2 son la base de todo y se pueden hacer y verificar hoy, sin comprometer nada del resto.

### Prueba de integración corrida (2026-08-28)

El circuito `ms-authz` + OpenFGA + Postgres se ejercitó contra servicios reales, aislados del entorno
del proyecto: store creado, modelo cargado, catálogo migrado, dos tenants dados de alta, roles
asignados. Resultados: API key rechaza sin credencial (401); alta de tenant materializa 222 tuplas y
es idempotente al repetirse; unión de dos roles da 35 permisos efectivos; el `PUT` de roles reemplaza
el set en vez de acumular; y **el aislamiento entre tenants se sostiene** — el mismo usuario como
`vendedor` en jurol (24 permisos, ninguno sensible) y `admin` en otraempresa (78) devuelve exactamente
lo de cada tenant, sin contaminación cruzada y con el prefijo cortado.

Aparecieron dos bugs que los tests unitarios no podían ver, porque mockeaban el gateway de OpenFGA:

- **Límite de 100 escrituras por transacción.** Materializar el catálogo son 222 tuplas en un solo
  `Write` → error 500. Corregido con batches de 50 en `OpenFgaGateway`. El batching es seguro
  justamente porque las escrituras ya eran idempotentes.
- **`Read` parcial sin tipo de objeto.** Leer los roles de un usuario mandaba user + relation sin
  object; OpenFGA lo rechaza. Corregido pasando el tipo (`role:`), que es la convención para "todos
  los objetos de este tipo".

También quedó cubierto el hueco de arranque: el store **no se crea solo** y la config viene con
`StoreId = "REPLACE_WITH_DEV_STORE_ID"`, así que sin ese paso el servicio levanta pero toda llamada a
OpenFGA falla. `ms-authz/scripts/bootstrap-openfga.sh` lo resuelve y está probado.

## 13. Riesgos

| Riesgo | Mitigación |
|---|---|
| Dos piezas más para operar **por sistema** | Es el precio explícito de no compartir nada. Mismo esfuerzo que ya asumen con Keycloak: Docker self-hosted, misma infra, mismo backup. |
| Curva del modelo de tuplas | Paso 2 antes de escribir código de producción. Experimentar es gratis. |
| Latencia en el hot path | No se llama a OpenFGA por request de negocio — el SDK cachea el set de permisos (patrón *pull*, §8). |
| Fan-out al cambiar el catálogo | Job idempotente, misma forma que `migrate-all-tenant.sh`. |
| `ms-authz` confía en quien lo llama | Nunca expuesto públicamente (§7). Es la misma frontera que `ms-filestore`. |
| Upgrade de OpenFGA N veces | Real, es la contra de instancia por sistema. Se compensa con que un upgrade fallido afecta a un solo sistema. |
| Backup/DR de las tuplas | Las bases `openfga` y `authz` entran al backup de la instancia que ya existe. No son un caso especial. |

## 14. Estado de las decisiones

**Cerrado en esta vuelta:**

- Nada se comparte entre sistemas; cada uno levanta `ms-authz` + OpenFGA + sus dos bases (§2).
- Se cae el "store por aplicación" del spec anterior — un store por deployment (§2).
- Modelo B: `permission` como tipo de objeto, con jerarquía de roles desde el día uno (§3).
- Separador `|`; el DSL del spec anterior era inválido (§4).
- Catálogo de roles fijo por sistema en v1, materializado en tuplas por tenant (§5).
- Postgres compartido a nivel instancia dentro de un sistema, una base por servicio (§6).
- Nombre: `ms-authz` / `Authz.Client` — inglés como `ms-filestore`, y `authz` marca la diferencia con
  `authn` (el riesgo de confusión número uno acá es "¿eso no lo hace Keycloak?").

**Abierto:**

- ~~Catálogo concreto de permisos y roles iniciales~~ — **cerrado**. Vive en
  `ms-authz/liquibase/changelog/005-seed-catalog.sql`: 78 permisos (36 lectura, 30 escritura, 12
  sensibles) y 5 roles con 222 asignaciones, verificado aplicando el changeset contra un Postgres
  real. La taxonomía espeja `menu_modules` de estudio-contable, así el catálogo y el sidebar hablan
  de las mismas cosas y los nombres legibles ya estaban escritos. **No hay rol de contador**: el
  estudio no ingresa a este ERP, lee a sus clientes desde Atlas (`ATLAS-SPEC.md`), que es otro
  sistema y por lo tanto tiene su propia instancia de `ms-authz` (§2). Granularidad: Read/Write por ítem
  de menú, más código propio para cada operación sensible — que "puede escribir en Ventas" no
  implique "puede anular un comprobante con CAE". Falta el rollout: anotar el resto de los módulos
  (paso 10).
- Si esto va a `/gsd-plan-phase`, decidir si se planifica como una fase cross-repo o se parte en
  fases encadenadas (infra → ms-authz → backend → frontend).
- Quién tiene acceso directo a la API/CLI de OpenFGA para debugging, vs. todo pasa siempre por
  `ms-authz` en operación normal. Recomendado: acceso directo solo para ops/debug.
- Disciplina de migración de modelos cuando aparezca ReBAC (§3). No bloqueante para v1.

## 15. Revisión 2026-09-04: ms-authz sin persistencia propia

**Qué cambió.** El catálogo (roles, permisos, rol→permiso, nombres, descripciones — §5) ya no vive en
un Postgres propio migrado con Liquibase: se lee de un archivo JSON (`Catalog:Path`) una sola vez al
arrancar, validado y expandido en memoria. `ms-authz` ya no tiene base `authz` ni base propia de
ningún tipo — OpenFGA es la única persistencia que le queda. La tabla `tenants` (§5, nota) también
desaparece: `ms-authz` no guarda una lista de tenants provisionados. `POST /catalog/sync` recibe la
lista de tenants a re-expandir directamente en el body (`{ "tenantCodes": [...] }`) en vez de
iterarla desde su propio registro.

**Por qué.** El tenant es del sistema consumidor, no de `ms-authz` — mantener una segunda lista acá
(la tabla `tenants`) corre el riesgo de desalinearse con la fuente real (`public.tenants` del
consumidor, o el listado de realms de Keycloak), sin aportar nada que el consumidor no supiera ya.
Un contenedor sin base propia es más fácil de reusar en un sistema nuevo: no hay Postgres que
aprovisionar ni Liquibase que correr antes del primer request, solo un archivo. Y el catálogo nunca
necesitó ser una base relacional: es "fijo por deploy" (§5, v1) — la razón original para tener
`roles`/`permissions`/`role_permissions` en Postgres era que OpenFGA no admite metadata (nombres,
descripciones, agrupación) en sus propios objetos, no que hiciera falta una base transaccional. Un
archivo cumple exactamente ese rol.

**Qué queda igual.** El modelo DSL (§3), los identificadores y el separador `|` (§4), la
materialización por tenant (§5), el filtro obligatorio contra el cruce entre tenants de `ListObjects`
y `Read` parcial (§4), y el SDK `Authz.Client` (§8) — que ahora además expone `SyncCatalogAsync` en
`IAuthzAdminClient`, ya que sin `ITenantRegistry` es el consumidor quien decide qué tenants sincronizar.

**Un solo endpoint de materialización.** `POST /tenants` (§7) desaparece: dar de alta un tenant y
re-expandir el catálogo eran la misma operación con dos puertas. Queda sólo `POST /catalog/sync` con la
lista de tenants; el alta de un tenant es un sync de uno.
