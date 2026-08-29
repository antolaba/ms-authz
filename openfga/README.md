# Validación del modelo OpenFGA (§12, paso 2 de MS-AUTHZ-SPEC.md)

Este directorio contiene el DSL validado y la suite de tests para el modelo de autorización
descrito en el §3 de `MS-AUTHZ-SPEC.md`. Validado el 2026-08-27 contra un OpenFGA real
(`openfga/openfga:v1.19.0`), no solo revisado en papel.

## Archivos

- `model.fga` — el modelo DSL (schema 1.1), idéntico al del §3 de la spec.
- `model.fga.yaml` — suite de tests en el formato nativo de `fga model test` (store test):
  tuplas de ejemplo + 7 casos de test que cubren §12 punto 2.
- `tuples.json` — las mismas tuplas en formato `fga tuple write --file`, usadas para la
  validación manual contra un store HTTP real (evidencia adicional, no solo evaluación local).

## Cómo correr la validación

### Opción rápida: solo la suite de tests (evaluación local, sin store)

Requiere la CLI `fga` (`brew install openfga/tap/fga`). No necesita un servidor corriendo —
`fga model test` evalúa el modelo y las tuplas en memoria:

```bash
cd openfga/
fga model test --tests model.fga.yaml
```

Salida esperada:

```
# Test Summary #
Tests 7/7 passing
Checks 10/10 passing
ListObjects 1/1 passing
```

### Opción completa: contra un servidor OpenFGA real (efímero, aislado)

**No usar el Postgres del proyecto ni el docker-compose de infra.** Levantar un contenedor
efímero con storage en memoria, en un puerto que no choque con nada (infra usa 8082/8083 para
OpenFGA; acá se usó 8085):

```bash
docker run --rm -d -p 8085:8080 --name openfga-validate openfga/openfga:v1.19.0 run

export FGA_API_URL=http://localhost:8085
STORE_ID=$(fga store create --name ms-authz-validate | jq -r .store.id)

fga model write --store-id "$STORE_ID" --file model.fga
fga tuple write --store-id "$STORE_ID" --file tuples.json

# Checks puntuales, ejemplo:
fga query check --store-id "$STORE_ID" user:u-vendedor granted permission:jurol\|Sales.Write

# ListObjects (caso 7):
fga query list-objects --store-id "$STORE_ID" user:u-multitenant granted permission

# al terminar:
docker rm -f openfga-validate
```

Los 7 casos de `model.fga.yaml` fueron corridos ambas formas (local y contra el servidor real)
el 2026-08-27 y dieron el mismo resultado en las dos. Ver el reporte de la tarea de validación
para la salida completa de cada caso.

## Qué cubre cada caso (mapeo a §12.2 de la spec)

| # | Caso | Resultado real |
|---|---|---|
| 1 | Permiso directo por rol | `true` |
| 2 | Permiso que el usuario no tiene | `false` |
| 3 | Unión de dos roles del mismo tenant (vendedor + stock) | `true` en ambos permisos |
| 4 | Jerarquía de roles (gerente hereda de vendedor, no al revés) | `true` para lo heredado, `false` para la dirección inversa |
| 5 | Aislamiento entre tenants | `false` |
| 6 | Usuario sin roles | `false` |
| 7 | `ListObjects(user, "granted", "permission")` cruza tenants | **Confirmado**: devuelve permisos de todos los tenants donde el usuario tiene rol — `ms-authz` tiene que filtrar por prefijo `<tenant>\|` del lado del cliente, tal como dice el §4 |

El modelo del §3 queda validado tal cual está escrito — no hizo falta ningún cambio de DSL.
