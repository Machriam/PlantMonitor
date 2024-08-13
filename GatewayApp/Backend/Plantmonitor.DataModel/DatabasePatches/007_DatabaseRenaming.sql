ALTER TABLE IF EXISTS plantmonitor.photo_tour_event DROP COLUMN IF EXISTS references_event;
ALTER TABLE IF EXISTS plantmonitor.photo_tour_plant RENAME qr_code TO "position";

update plantmonitor.configuration_data set value='7' where key='PatchNumber';