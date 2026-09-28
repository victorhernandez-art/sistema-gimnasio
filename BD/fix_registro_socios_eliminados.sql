-- ============================================================
-- CORRECCIÓN: Registro no debe mostrar socios eliminados
-- ============================================================
-- PROBLEMA: Al buscar un socio en la pantalla de Registro,
--   aparecen datos aunque el socio haya sido eliminado/deshabilitado
--   en la lista de Socios.
--
-- CAUSA: La vista vwultimamembresia solo filtra por el estado de la
--   membresía (sociomembresia.idEstado = 1), pero no verifica si el
--   socio está activo (socio.idEstado = 1).
--
-- SOLUCIÓN: Agregar un JOIN con la tabla socio y filtrar por
--   socio.idEstado = 1 en la vista vwultimamembresia.
-- ============================================================

USE gym;

-- ------------------------------------------------------------
-- Corregir vista vwultimamembresia para excluir socios
-- que han sido deshabilitados o eliminados.
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW `gym`.`vwultimamembresia` AS
SELECT
    MAX(`sociomembresia`.`idSocioMembresia`) AS `idSocioMembresia`,
    `sociomembresia`.`idSocio`              AS `idSocio`
FROM `sociomembresia`
INNER JOIN `socio` ON `sociomembresia`.`idSocio` = `socio`.`idSocio`
WHERE `sociomembresia`.`idEstado` = 1
  AND `socio`.`idEstado` = 1
GROUP BY `sociomembresia`.`idSocio`;

-- ------------------------------------------------------------
-- Verificación: ejecuta esta consulta para confirmar que los
-- socios eliminados ya NO aparecen.
-- Debería devolver 0 filas para socios con idEstado != 1.
-- ------------------------------------------------------------
-- SELECT u.idSocio, s.Nombre, s.idEstado
-- FROM vwultimamembresia u
-- INNER JOIN socio s ON u.idSocio = s.idSocio
-- WHERE s.idEstado != 1;
