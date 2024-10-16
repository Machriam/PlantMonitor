ALTER TYPE plantmonitor.photo_tour_event_type ADD VALUE 'critical' AFTER 'error';
update plantmonitor.configuration_data set value='12' where key='PatchNumber';