# Modelo de autorización OpenFGA

El modelo es uno de los tres artefactos que se comparten entre sistemas, junto con el código de este
repo y el paquete `Authz.Client`. Es el mismo para todos y es mínimo a propósito: OpenFGA guarda sólo
asignaciones (`user:<tenant>|<subject> assignee role:<tenant>|<code>`). Qué permisos otorga cada rol
lo resuelve `ms-authz` desde el catálogo en memoria, así que el modelo no necesita saber de permisos.

## Archivos

- `model.fga` — el DSL canónico (schema 1.1). Tres tipos, dos relaciones.
- `model.json` — el mismo modelo en el JSON que acepta la API de OpenFGA. **Generado** desde
  `model.fga`; `MsAuthz.Infrastructure` lo embebe en el assembly y lo escribe en el store al arrancar
  si el store no tiene modelo. Si cambia `model.fga`, regenerarlo en el mismo commit:
  ```bash
  fga model transform --file model.fga > model.json
  ```
- `model.fga.yaml` — suite de `fga model test`: asignación directa, aislamiento entre tenants por
  el tenant dentro del user, y que el `Read` de roles de un user devuelve sólo los de su tenant.

## Correr la suite

Requiere la CLI `fga` (`brew install openfga/tap/fga`), sólo como herramienta de desarrollo. No hace
falta un servidor: evalúa el modelo y las tuplas en memoria.

```bash
cd openfga/
fga model test --tests model.fga.yaml
```

Salida esperada:

```
# Test Summary #
Tests 3/3 passing
Checks 4/4 passing
ListObjects 2/2 passing
```
