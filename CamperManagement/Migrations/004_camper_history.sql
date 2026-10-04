CREATE TABLE IF NOT EXISTS camper_historie (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 camper_id INT NOT NULL,
 recorded_at DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
 ereignis VARCHAR(32) NOT NULL,
 schema_version INT NOT NULL DEFAULT 1,
 vorher JSON NULL,
 nachher JSON NOT NULL,
 INDEX ix_camper_historie(camper_id,id),
 FOREIGN KEY (camper_id) REFERENCES camper(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
