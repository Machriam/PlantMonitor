ALTER TABLE IF EXISTS plantmonitor.automatic_photo_tour ADD COLUMN pixel_size_in_mm real NOT NULL DEFAULT 0.2;

update plantmonitor.configuration_data set value='9' where key='PatchNumber';