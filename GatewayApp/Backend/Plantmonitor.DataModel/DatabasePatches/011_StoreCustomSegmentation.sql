ALTER TABLE IF EXISTS plantmonitor.photo_tour_trip ADD COLUMN segmentation_template jsonb;
update plantmonitor.configuration_data set value='11' where key='PatchNumber';