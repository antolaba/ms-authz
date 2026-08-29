--liquibase formatted sql

-- =====================================================================
-- CATALOGO DE PERMISOS Y ROLES — estudio-contable
-- =====================================================================
-- Taxonomia: espeja `menu_modules` del schema system de estudio-contable
-- (34 items en 6 grupos). Esa tabla ya es la division de modulos
-- consensuada del producto, asi que el catalogo de permisos y el sidebar
-- hablan de las mismas cosas: la pantalla de administracion puede agrupar
-- los permisos igual que el menu, y cada feature lib lazy del frontend
-- mapea a un par Read/Write.
--
-- Convencion del codigo: `<Entidad>.<Accion>`, en ingles PascalCase, dos
-- niveles — identificadores en ingles, textos de usuario en espanol, como
-- manda el CLAUDE.md raiz. La columna `module` guarda el grupo de menu (en
-- espanol) y es solo para agrupar en la UI de administracion.
--
-- Granularidad: Read/Write por item de menu, MAS codigos propios para las
-- operaciones sensibles. La razon: en un ERP contable hay acciones que no
-- deberian quedar cubiertas por un Write generico. Que "puede escribir en
-- Ventas" implique "puede anular un comprobante con CAE ya emitido" es que
-- el permiso no dice lo que la gente cree que dice. Las 12 acciones
-- sensibles estan marcadas abajo con [SENSIBLE].
--
-- Totales: 78 permisos (36 de lectura, 30 de escritura, 12 sensibles),
-- 5 roles, 222 asignaciones. Verificado aplicando este changeset contra
-- un Postgres real.
--
-- La jerarquia de roles del modelo OpenFGA (§3) existe pero NO se usa aca:
-- a `gerente` se le asignan sus permisos directamente. Asi el catalogo
-- sigue siendo la unica fuente de verdad de "que otorga este rol", legible
-- en un solo lugar. La jerarquia queda disponible para cuando haga falta,
-- que es justamente por que se incluyo en el modelo desde el dia uno.
-- =====================================================================

--changeset root:5-seed-catalog

-- ---------------------------------------------------------------------
-- PERMISOS
-- ---------------------------------------------------------------------

INSERT INTO permissions (code, module, description) VALUES
-- === Operacion diaria ===
    ('Dashboard.Read',                 'Operación diaria',     'Ver el tablero de inicio'),
    ('CashSession.Read',               'Operación diaria',     'Ver sesiones de caja'),
    ('CashSession.Write',              'Operación diaria',     'Abrir sesion de caja y registrar movimientos'),
    ('CashSession.Close',              'Operación diaria',     '[SENSIBLE] Cerrar una sesion de caja'),
    ('CashSession.Adjust',             'Operación diaria',     '[SENSIBLE] Ajustar el saldo de una caja'),
    ('Pos.Read',                       'Operación diaria',     'Ver la pantalla de venta rapida'),
    ('Pos.Write',                      'Operación diaria',     'Registrar ventas desde el punto de venta'),

-- === Ventas ===
    ('Customers.Read',                 'Ventas',               'Ver clientes'),
    ('Customers.Write',                'Ventas',               'Crear y editar clientes'),
    ('Customers.BulkValidateAfip',     'Ventas',               '[SENSIBLE] Validar clientes contra AFIP de forma masiva'),
    ('Quotes.Read',                    'Ventas',               'Ver presupuestos'),
    ('Quotes.Write',                   'Ventas',               'Crear y editar presupuestos'),
    ('Sales.Read',                     'Ventas',               'Ver ventas y comprobantes emitidos'),
    ('Sales.Write',                    'Ventas',               'Crear y confirmar ventas'),
    ('Sales.Void',                     'Ventas',               '[SENSIBLE] Anular un comprobante ya emitido'),
    ('Collections.Read',               'Ventas',               'Ver cobranzas y cuenta corriente de clientes'),
    ('Collections.Write',              'Ventas',               'Registrar cobranzas'),
    ('Collections.Delete',             'Ventas',               '[SENSIBLE] Eliminar una cobranza registrada'),

-- === Compras e Inventario ===
    ('Suppliers.Read',                 'Compras e Inventario', 'Ver proveedores'),
    ('Suppliers.Write',                'Compras e Inventario', 'Crear y editar proveedores'),
    ('SupplierPriceImport.Read',       'Compras e Inventario', 'Ver importaciones de listas de precios'),
    ('SupplierPriceImport.Write',      'Compras e Inventario', 'Preparar y editar importaciones de precios'),
    ('SupplierPriceImport.Execute',    'Compras e Inventario', '[SENSIBLE] Aplicar una importacion masiva de precios'),
    ('ProductCosts.Read',              'Compras e Inventario', 'Ver costos de producto'),
    ('ProductCosts.Write',             'Compras e Inventario', 'Editar costos de producto'),
    ('Stock.Read',                     'Compras e Inventario', 'Ver stock y movimientos'),
    ('Stock.Write',                    'Compras e Inventario', 'Registrar movimientos de stock'),
    ('Stock.Adjust',                   'Compras e Inventario', '[SENSIBLE] Ajustar existencias sin documento respaldatorio'),
    ('StockTransformations.Read',      'Compras e Inventario', 'Ver transformaciones de stock'),
    ('StockTransformations.Write',     'Compras e Inventario', 'Ejecutar transformaciones de stock'),
    ('GoodsReceipts.Read',             'Compras e Inventario', 'Ver recepciones de mercaderia'),
    ('GoodsReceipts.Write',            'Compras e Inventario', 'Registrar recepciones de mercaderia'),
    ('SupplierInvoices.Read',          'Compras e Inventario', 'Ver facturas de compra'),
    ('SupplierInvoices.Write',         'Compras e Inventario', 'Cargar y editar facturas de compra'),
    ('SupplierPayments.Read',          'Compras e Inventario', 'Ver pagos a proveedores'),
    ('SupplierPayments.Write',         'Compras e Inventario', 'Registrar pagos a proveedores'),
    ('SupplierPayments.Delete',        'Compras e Inventario', '[SENSIBLE] Eliminar un pago a proveedor'),
    ('CostVariances.Read',             'Compras e Inventario', 'Ver diferencias de costo'),

-- === Catalogo ===
    ('Products.Read',                  'Catálogo',             'Ver productos'),
    ('Products.Write',                 'Catálogo',             'Crear y editar productos'),
    ('ProductPresentations.Read',      'Catálogo',             'Ver presentaciones de producto'),
    ('ProductPresentations.Write',     'Catálogo',             'Crear y editar presentaciones de producto'),
    ('ProductFamilies.Read',           'Catálogo',             'Ver familias de productos'),
    ('ProductFamilies.Write',          'Catálogo',             'Crear y editar familias de productos'),
    ('ProductTypes.Read',              'Catálogo',             'Ver tipos de productos'),
    ('ProductTypes.Write',             'Catálogo',             'Crear y editar tipos de productos'),
    ('ProductCategory.Read',           'Catálogo',             'Ver categorias de producto'),
    ('ProductCategory.Write',          'Catálogo',             'Crear y editar categorias de producto'),
    ('UnitMeasures.Read',              'Catálogo',             'Ver unidades de medida'),
    ('UnitMeasures.Write',             'Catálogo',             'Crear y editar unidades de medida'),
    ('Services.Read',                  'Catálogo',             'Ver servicios'),
    ('Services.Write',                 'Catálogo',             'Crear y editar servicios'),
    ('ServiceCategories.Read',         'Catálogo',             'Ver categorias de servicios'),
    ('ServiceCategories.Write',        'Catálogo',             'Crear y editar categorias de servicios'),

-- === Precios ===
    ('PriceLists.Read',                'Precios',              'Ver listas de precios'),
    ('PriceLists.Write',               'Precios',              'Crear y editar listas de precios'),
    ('ProductPrices.Read',             'Precios',              'Ver precios de productos'),
    ('ProductPrices.Write',            'Precios',              'Editar precios de productos'),
    ('ProductPrices.BulkUpdate',       'Precios',              '[SENSIBLE] Actualizar precios de forma masiva'),
    ('MarginRules.Read',               'Precios',              'Ver reglas de margen'),
    ('MarginRules.Write',              'Precios',              '[SENSIBLE] Editar reglas de margen (afecta precios sugeridos de todo el catalogo)'),
    ('StalePrices.Read',               'Precios',              'Ver el reporte de precios desactualizados'),

-- === Configuracion ===
    ('TenantConfiguration.Read',       'Configuración',        'Ver la configuracion general de la empresa'),
    ('TenantConfiguration.Write',      'Configuración',        '[SENSIBLE] Editar la configuracion general de la empresa'),
    ('PointsOfSale.Read',              'Configuración',        'Ver puntos de venta'),
    ('PointsOfSale.Write',             'Configuración',        'Crear y editar puntos de venta'),
    ('BillingNumberingBooks.Read',     'Configuración',        'Ver talonarios'),
    ('BillingNumberingBooks.Write',    'Configuración',        'Crear y editar talonarios'),
    ('Locations.Read',                 'Configuración',        'Ver ubicaciones'),
    ('Locations.Write',                'Configuración',        'Crear y editar ubicaciones'),
    ('CashRegisters.Read',             'Configuración',        'Ver la configuracion de cajas'),
    ('CashRegisters.Write',            'Configuración',        'Crear y editar cajas'),
    ('PaymentMethods.Read',            'Configuración',        'Ver medios de pago'),
    ('PaymentMethods.Write',           'Configuración',        'Crear y editar medios de pago'),
    ('PerceptionRules.Read',           'Configuración',        'Ver reglas de percepcion'),
    ('PerceptionRules.Write',          'Configuración',        'Crear y editar reglas de percepcion'),
    ('Iam.Read',                       'Configuración',        'Ver usuarios y sus roles asignados'),
    ('Iam.ManageRoles',                'Configuración',        '[SENSIBLE] Asignar y quitar roles a los usuarios')
ON CONFLICT (code) DO NOTHING;

-- ---------------------------------------------------------------------
-- ROLES
-- ---------------------------------------------------------------------

INSERT INTO roles (code, name, description) VALUES
    ('admin',    'Administrador', 'Acceso total, incluida la administracion de roles.'),
    ('gerente',  'Gerente',       'Operacion completa mas las acciones sensibles. No administra roles.'),
    ('vendedor', 'Vendedor',      'Mostrador y caja: ventas, presupuestos, clientes y cobranzas. No anula comprobantes ni cierra caja.'),
    ('stock',    'Stock',         'Deposito: existencias, recepciones, transformaciones y costos.'),
    ('compras',  'Compras',       'Circuito de compras: proveedores, recepciones, facturas, pagos e importacion de precios.')
ON CONFLICT (code) DO NOTHING;

-- ---------------------------------------------------------------------
-- ROL -> PERMISOS
-- ---------------------------------------------------------------------
-- Se asignan por patron donde el patron dice algo real ("gerente es todo
-- menos administrar roles"), y por lista explicita donde no. Las listas
-- explicitas son deliberadas: este archivo tiene que poder auditarse
-- leyendolo, sin ejecutar nada.
--
-- NO hay rol de contador. El estudio no ingresa a este ERP: lee la
-- informacion de sus clientes desde Atlas, la app de consulta
-- multi-empresa (ATLAS-SPEC.md). Atlas es otro sistema, asi que bajo el
-- §2 de MS-AUTHZ-SPEC.md tiene su propia instancia de ms-authz y su
-- propio catalogo — nada de eso se modela aca.

-- admin: todo, incluido lo que se agregue en changesets posteriores.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.code = 'admin'
ON CONFLICT DO NOTHING;

-- vendedor: escribe su circuito de mostrador; lee catalogo y precios para
-- poder vender. NO anula comprobantes, NO cierra ni ajusta caja, NO borra
-- cobranzas — esas tres son las sensibles del circuito de ventas.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.code = 'vendedor' AND p.code IN (
    'Dashboard.Read',
    'CashSession.Read', 'CashSession.Write',
    'Pos.Read', 'Pos.Write',
    'Customers.Read', 'Customers.Write',
    'Quotes.Read', 'Quotes.Write',
    'Sales.Read', 'Sales.Write',
    'Collections.Read', 'Collections.Write',
    'Products.Read', 'ProductPresentations.Read', 'ProductFamilies.Read',
    'ProductTypes.Read', 'ProductCategory.Read', 'UnitMeasures.Read',
    'Services.Read', 'ServiceCategories.Read',
    'PriceLists.Read', 'ProductPrices.Read',
    'Stock.Read'
)
ON CONFLICT DO NOTHING;

-- stock: deposito. Stock.Adjust va aca porque ajustar existencias sin
-- documento es parte del trabajo de inventario, no una excepcion.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.code = 'stock' AND p.code IN (
    'Dashboard.Read',
    'Stock.Read', 'Stock.Write', 'Stock.Adjust',
    'StockTransformations.Read', 'StockTransformations.Write',
    'GoodsReceipts.Read', 'GoodsReceipts.Write',
    'ProductCosts.Read', 'ProductCosts.Write',
    'CostVariances.Read',
    'Products.Read', 'ProductPresentations.Read', 'ProductFamilies.Read',
    'ProductTypes.Read', 'ProductCategory.Read', 'UnitMeasures.Read',
    'Suppliers.Read', 'Locations.Read'
)
ON CONFLICT DO NOTHING;

-- compras: circuito completo de compras. SupplierPriceImport.Execute va
-- aca porque aplicar la lista del proveedor es la tarea, no un privilegio
-- aparte. SupplierPayments.Delete NO: borrar un pago ya registrado queda
-- para gerente.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.code = 'compras' AND p.code IN (
    'Dashboard.Read',
    'Suppliers.Read', 'Suppliers.Write',
    'SupplierPriceImport.Read', 'SupplierPriceImport.Write', 'SupplierPriceImport.Execute',
    'SupplierInvoices.Read', 'SupplierInvoices.Write',
    'SupplierPayments.Read', 'SupplierPayments.Write',
    'GoodsReceipts.Read', 'GoodsReceipts.Write',
    'ProductCosts.Read', 'ProductCosts.Write',
    'CostVariances.Read',
    'Stock.Read',
    'Products.Read', 'ProductPresentations.Read', 'ProductFamilies.Read',
    'ProductTypes.Read', 'ProductCategory.Read', 'UnitMeasures.Read',
    'PriceLists.Read', 'ProductPrices.Read'
)
ON CONFLICT DO NOTHING;

-- gerente: todo menos la administracion de roles. Es el unico rol
-- operativo que junta las acciones sensibles de los tres circuitos.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.code = 'gerente' AND p.code <> 'Iam.ManageRoles'
ON CONFLICT DO NOTHING;

--rollback DELETE FROM role_permissions;
--rollback DELETE FROM roles WHERE code IN ('admin','gerente','vendedor','stock','compras');
--rollback DELETE FROM permissions;
