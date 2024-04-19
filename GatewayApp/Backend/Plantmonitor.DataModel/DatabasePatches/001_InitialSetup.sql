CREATE SCHEMA plantmonitor AUTHORIZATION postgres;
CREATE TABLE plantmonitor.configuration_data
(
    id bigserial,
    key text NOT NULL,
    value text NOT NULL,
    PRIMARY KEY (id),
    UNIQUE (key)
);

ALTER TABLE IF EXISTS plantmonitor.configuration_data OWNER to postgres;
INSERT INTO plantmonitor.configuration_data (key,value) VALUES ('PatchNumber','1');

CREATE TABLE plantmonitor.device_movement
(
    id bigserial,
    device_id uuid NOT NULL,
    movement_plan jsonb NOT NULL,
    name text NOT NULL,
    PRIMARY KEY (id),
    UNIQUE (device_id, name)
);

ALTER TABLE IF EXISTS plantmonitor.device_movement OWNER to postgres;