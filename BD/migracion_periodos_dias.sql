-- ============================================================
-- MIGRACIÓN: Cambiar cálculo de Vencimiento de MESES a DÍAS
-- ============================================================
-- IMPORTANTE: Ejecutar este script EN MySQL Workbench (o CLI)
--             conectado a la base de datos 'gym'.
-- 
-- Qué hace:
--   1. Actualiza la vista vwsociomembresias para calcular
--      el Vencimiento con INTERVAL meses DAY (en lugar de MONTH).
--   2. Actualiza los datos existentes en la tabla Membresia
--      convirtiendo los valores de meses a días equivalentes.
-- ============================================================

USE gym;

-- ------------------------------------------------------------
-- PASO 1: Actualizar datos existentes en la tabla Membresia
-- Convierte los valores actuales (que eran en meses) a días.
-- Revisa y ajusta según tus datos reales antes de ejecutar.
-- ------------------------------------------------------------

-- Ejemplo de conversión estándar (1 mes antiguo = 30 días):
-- UPDATE gym.Membresia SET meses = meses * 30;
--
-- Si prefieres actualizar manualmente registro por registro
-- (más seguro para preservar el significado original):

-- Membresías típicas — ajusta los idMembresia según tu BD:
-- UPDATE gym.Membresia SET meses = 1   WHERE Nombre = 'Visita';
-- UPDATE gym.Membresia SET meses = 7   WHERE Nombre = 'Semana';
-- UPDATE gym.Membresia SET meses = 15  WHERE Nombre = 'Quincena';
-- UPDATE gym.Membresia SET meses = 30  WHERE Nombre LIKE '%1 Mes%';
-- UPDATE gym.Membresia SET meses = 365 WHERE Nombre LIKE '%Anual%';

-- Opción automática (descomenta si quieres migración masiva):
-- UPDATE gym.Membresia SET meses = meses * 30;

-- ------------------------------------------------------------
-- PASO 2: Actualizar la vista vwsociomembresias
--         Cambia "interval meses MONTH" → "interval meses DAY"
-- ------------------------------------------------------------

CREATE OR REPLACE VIEW `gym`.`vwsociomembresias` AS
SELECT
  `sociomembresia`.`idSocioMembresia`        AS `idSocioMembresia`,
  `sociomembresia`.`idEstado`                AS `idEstado`,
  `sociomembresia`.`fechaCreacion`           AS `fechaCreacion`,
  `sociomembresia`.`idUsuarioCreo`           AS `idUsuarioCreo`,
  `sociomembresia`.`idSocio`                 AS `idSocio`,
  `sociomembresia`.`idMembresia`             AS `idMembresia`,
  `sociomembresia`.`Precio`                  AS `Precio`,
  `sociomembresia`.`fechaInicioMembresia`    AS `fechaInicioMembresia`,
  `estado`.`Estado`                          AS `Estado`,
  `membresia`.`Nombre`                       AS `NombreMembresia`,
  `membresia`.`meses`                        AS `meses`,
  `socio`.`Nombre`                           AS `NombreSocio`,
  `socio`.`Paterno`                          AS `Paterno`,
  `socio`.`Materno`                          AS `Materno`,
  `socio`.`Telefono`                         AS `Telefono`,
  `socio`.`Observaciones`                    AS `Observaciones`,
  -- *** CAMBIO CLAVE: MONTH → DAY ***
  (`sociomembresia`.`fechaInicioMembresia` + INTERVAL `membresia`.`meses` DAY) AS `Vencimiento`,
  `socio`.`foto`                             AS `foto`,
  `membresia`.`horaInicio`                   AS `horaInicio`,
  `membresia`.`horaFinal`                    AS `horaFinal`
FROM
  (((`sociomembresia`
    JOIN `estado`    ON (`sociomembresia`.`idEstado`    = `estado`.`idEstados`))
    LEFT JOIN `membresia` ON (`sociomembresia`.`idMembresia` = `membresia`.`idMembresia`))
    LEFT JOIN `socio`     ON (`sociomembresia`.`idSocio`     = `socio`.`idSocio`));

-- vwultimamembresiadetallada y rptmembresias heredan Vencimiento
-- de vwsociomembresias, por lo que se actualizan automáticamente.

-- ------------------------------------------------------------
-- Verificación: consulta un socio con membresía para confirmar
-- ------------------------------------------------------------
-- SELECT NombreSocio, Paterno, NombreMembresia, meses,
--        fechaInicioMembresia, Vencimiento
-- FROM vwsociomembresias LIMIT 10;
