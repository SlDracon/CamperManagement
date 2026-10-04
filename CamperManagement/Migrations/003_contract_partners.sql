ALTER TABLE camper ADD COLUMN IF NOT EXISTS gemeinsame_adresse TINYINT(1) NOT NULL DEFAULT 1;
ALTER TABLE camper_personen ADD COLUMN IF NOT EXISTS vertragsnehmer_nr TINYINT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_camper_vertragsnehmer ON camper_personen(camper_id,vertragsnehmer_nr);
-- Preserve the previously selected billing person as the first contract holder.
-- Never infer a second contract holder from an additional contact or a combined name.
UPDATE camper_personen cp
JOIN (SELECT camper_id,MIN(id) id FROM camper_personen WHERE rechnungsadresse=1 GROUP BY camper_id) chosen ON chosen.id=cp.id
LEFT JOIN camper_personen existing ON existing.camper_id=cp.camper_id AND existing.vertragsnehmer_nr=1
SET cp.vertragsnehmer_nr=1 WHERE existing.id IS NULL AND cp.vertragsnehmer_nr IS NULL;
ALTER TABLE rechnung_empfaenger
 ADD COLUMN IF NOT EXISTS zweite_anrede VARCHAR(255) NOT NULL DEFAULT '',
 ADD COLUMN IF NOT EXISTS zweiter_vorname VARCHAR(255) NOT NULL DEFAULT '',
 ADD COLUMN IF NOT EXISTS zweiter_nachname VARCHAR(255) NOT NULL DEFAULT '';
-- Existing invoice snapshots deliberately keep their original recipient details.
