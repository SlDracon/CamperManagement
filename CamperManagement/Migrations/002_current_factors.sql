-- A single current tariff, independent of the invoice billing year.
-- Keep the old per-year table intact for rollback/audit; invoices are never updated.
CREATE TABLE IF NOT EXISTS standardfaktoren (
 id TINYINT UNSIGNED NOT NULL PRIMARY KEY CHECK (id=1),
 strom DECIMAL(18,6) NOT NULL, wasser DECIMAL(18,6) NOT NULL,
 version BIGINT NOT NULL DEFAULT 1
) ENGINE=InnoDB;
-- Prefer the current/most recent past year, falling back to an existing future
-- setting only if no past setting exists; otherwise retain the original defaults.
INSERT INTO standardfaktoren(id,strom,wasser,version)
SELECT 1,
 COALESCE((SELECT strom FROM jahresfaktoren ORDER BY (jahr<=YEAR(CURRENT_DATE)) DESC,jahr DESC LIMIT 1),0.5),
 COALESCE((SELECT wasser FROM jahresfaktoren ORDER BY (jahr<=YEAR(CURRENT_DATE)) DESC,jahr DESC LIMIT 1),8),1
ON DUPLICATE KEY UPDATE id=VALUES(id);
