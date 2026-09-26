CREATE TABLE IF NOT EXISTS jahresfaktoren (
 jahr INT NOT NULL PRIMARY KEY, strom DECIMAL(18,6) NOT NULL, wasser DECIMAL(18,6) NOT NULL, version BIGINT NOT NULL DEFAULT 1
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS rechnung_empfaenger (
 rechnung_id INT NOT NULL PRIMARY KEY, camper_id INT NOT NULL,
 anrede VARCHAR(255) NOT NULL, vorname VARCHAR(255) NOT NULL, nachname VARCHAR(255) NOT NULL,
 strasse VARCHAR(255) NOT NULL, plz VARCHAR(32) NOT NULL, ort VARCHAR(255) NOT NULL,
 vertragskosten DECIMAL(18,2) NOT NULL DEFAULT 0,
 FOREIGN KEY (rechnung_id) REFERENCES rechnungen(id), FOREIGN KEY (camper_id) REFERENCES camper(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
ALTER TABLE rechnungen MODIFY alt DECIMAL(18,6) NOT NULL, MODIFY neu DECIMAL(18,6) NOT NULL,
 MODIFY verbrauch DECIMAL(18,6) NOT NULL, MODIFY faktor DECIMAL(18,6) NOT NULL,
 MODIFY betrag DECIMAL(18,2) NOT NULL;
-- Preserve immutable recipient details. Equivalent duplicate occupation records are
-- canonicalized by latest creation/id only when every billing field is identical.
-- Different recipients remain unresolved rather than being guessed.
INSERT INTO rechnung_empfaenger(rechnung_id,camper_id,anrede,vorname,nachname,strasse,plz,ort,vertragskosten)
SELECT r.id,c.id,p.anrede,p.vorname,p.nachname,p.strasse,LPAD(CAST(p.plz AS CHAR),5,'0'),p.ort,c.Vertragskosten
FROM rechnungen r JOIN camper c ON c.platz_id=r.platz_id
JOIN camper_personen cp ON cp.camper_id=c.id AND cp.rechnungsadresse=1
JOIN personen p ON p.id=cp.personen_id
WHERE c.created<=r.created AND (c.deactivated='0000-00-00 00:00:00' OR c.deactivated>r.created)
AND NOT EXISTS(SELECT 1 FROM rechnung_empfaenger s WHERE s.rechnung_id=r.id)
AND cp.id=(SELECT MIN(cp3.id) FROM camper_personen cp3 WHERE cp3.camper_id=c.id AND cp3.rechnungsadresse=1)
AND c.id=(SELECT c3.id FROM camper c3 WHERE c3.platz_id=r.platz_id AND c3.created<=r.created
 AND (c3.deactivated='0000-00-00 00:00:00' OR c3.deactivated>r.created)
 AND EXISTS(SELECT 1 FROM camper_personen cp3 WHERE cp3.camper_id=c3.id AND cp3.rechnungsadresse=1)
 ORDER BY c3.created DESC,c3.id DESC LIMIT 1)
AND 1=(SELECT COUNT(DISTINCT BINARY JSON_ARRAY(p2.anrede,p2.vorname,p2.nachname,p2.strasse,p2.plz,p2.ort,c2.Vertragskosten))
 FROM camper c2 JOIN camper_personen cp2 ON cp2.camper_id=c2.id AND cp2.rechnungsadresse=1
 JOIN personen p2 ON p2.id=cp2.personen_id
 WHERE c2.platz_id=r.platz_id AND c2.created<=r.created AND (c2.deactivated='0000-00-00 00:00:00' OR c2.deactivated>r.created));
