-- Existing history and invoice snapshots remain unchanged.
ALTER TABLE camper_historie ADD COLUMN IF NOT EXISTS beschreibung VARCHAR(1000) NULL;
