-- ═══════════════════════════════════════════════════════════════
--  Migración: Añadir campo template a socio_huella
--  Permite almacenar templates biométricos SourceAFIS portables
--  que funcionan con CUALQUIER lector de huellas.
-- ═══════════════════════════════════════════════════════════════

-- Verificar si la columna ya existe antes de añadirla
SET @dbname = DATABASE();
SET @tablename = 'socio_huella';
SET @columnname = 'template';

SET @preparedStatement = (
    SELECT IF(
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
         WHERE TABLE_SCHEMA = @dbname
           AND TABLE_NAME = @tablename
           AND COLUMN_NAME = @columnname) > 0,
        'SELECT ''La columna template ya existe.'' AS resultado;',
        'ALTER TABLE socio_huella ADD COLUMN template LONGBLOB NULL COMMENT ''Template biométrico SourceAFIS (portable, independiente del lector)'' AFTER pin;'
    )
);

PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
