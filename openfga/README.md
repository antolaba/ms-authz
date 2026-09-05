# Modelo de autorización OpenFGA

El modelo es uno de los tres artefactos que se comparten entre sistemas, junto con el código de este
repo y el paquete `Authz.Client`. Es el mismo para todos y no cambia por feature: roles y permisos
concretos son tuplas, no tipos del modelo.

## Archivos

- `model.fga` — el DSL canónico (schema 1.1). Tres tipos, dos relaciones.
- `model.json` — el mismo modelo en el JSON que acepta la API de OpenFGA. **Generado** desde
  `model.fga`; `MsAuthz.Infrastructure` lo embebe en el assembly y lo escribe en el store al arrancar
  si el store no tiene modelo. Si cambia `model.fga`, regenerarlo en el mismo commit:
  ```bash
  fga model transform --file model.fga > model.json
  ```
- `model.fga.yaml` — suite de `fga model test`. Es la única prueba ejecutable de la semántica del
  modelo: dirección de la jerarquía de roles, aislamiento entre tenants, y por qué `ms-authz` filtra
  por prefijo de tenant además de scopear el user.

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
Tests 8/8 passing
Checks 12/12 passing
ListObjects 3/3 passing
```

## Qué cubre cada caso

| # | Caso | Resultado |
|---|---|---|
| 1 | Permiso directo por rol | `true` |
| 2 | Permiso que el usuario no tiene | `false` |
| 3 | Unión de dos roles del mismo tenant | `true` en ambos permisos |
| 4 | Jerarquía de roles (gerente hereda de vendedor, no al revés) | `true` para lo heredado, `false` en la dirección inversa |
| 5 | Aislamiento entre tenants, contra permisos que sí están concedidos en el otro tenant | `false` |
| 6 | Usuario sin roles | `false` |
| 7 | `ListObjects(user:<tenant>\|x, granted, permission)` queda acotado al tenant que lleva el user | Sólo los permisos de ese tenant |
| 8 | Asignación cruzada malformada (`user:jurol\|x` sobre `role:otraempresa\|admin`) | `ListObjects` **sí** la devuelve: por eso `ms-authz` mantiene el filtro por prefijo como defensa |
