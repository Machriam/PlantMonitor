CREATE VIEW plantmonitor.virtual_image_summary_by_photo_tour_id AS
SELECT id, coalesce(image_descriptors_json->>'PhotoTourId','0')::bigint photo_tour_id FROM plantmonitor.virtual_image_summary;

ALTER TABLE plantmonitor.virtual_image_summary_by_photo_tour_id OWNER TO postgres;
update plantmonitor.configuration_data set value='10' where key='PatchNumber';