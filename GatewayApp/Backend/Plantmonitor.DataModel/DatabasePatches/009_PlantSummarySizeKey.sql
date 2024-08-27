ALTER TABLE IF EXISTS plantmonitor.automatic_photo_tour ADD COLUMN pixel_size_in_mm real NOT NULL DEFAULT 0.2;
ALTER TABLE IF EXISTS plantmonitor.virtual_image_summary RENAME data TO image_descriptors_json;

update plantmonitor.configuration_data set value='9' where key='PatchNumber';