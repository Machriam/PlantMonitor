CREATE TABLE plantmonitor.automatic_photo_tour
(
    id bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY,
    device_id uuid NOT NULL,
    name text NOT NULL,
    comment text NOT NULL,
    intervall_in_minutes real NOT NULL,
    finished boolean NOT NULL,
    PRIMARY KEY (id)
);
ALTER TABLE IF EXISTS plantmonitor.automatic_photo_tour OWNER to postgres;

CREATE TYPE plantmonitor.photo_tour_event_type AS ENUM ('debug', 'information', 'warning', 'error');
ALTER TYPE plantmonitor.photo_tour_event_type OWNER TO postgres;

CREATE TABLE plantmonitor.photo_tour_trip
(
    id bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY,
    photo_tour_fk bigint NOT NULL,
    ir_data_folder text NOT NULL,
    vis_data_folder text NOT NULL,
    "timestamp" timestamp with time zone NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    FOREIGN KEY (photo_tour_fk)
        REFERENCES plantmonitor.automatic_photo_tour (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);
ALTER TABLE IF EXISTS plantmonitor.photo_tour_trip OWNER to postgres;

ALTER TABLE IF EXISTS plantmonitor.temperature_measurement ADD COLUMN photo_tour_fk bigint;
ALTER TABLE IF EXISTS plantmonitor.temperature_measurement
    ADD FOREIGN KEY (photo_tour_fk)
    REFERENCES plantmonitor.automatic_photo_tour (id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE NO ACTION
    NOT VALID;

CREATE TABLE plantmonitor.photo_tour_event
(
    id bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY,
    photo_tour_fk bigint NOT NULL,
    event_class text NOT NULL,
    message text NOT NULL,
    "timestamp" timestamp with time zone NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    FOREIGN KEY (photo_tour_fk)
        REFERENCES plantmonitor.automatic_photo_tour (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);
ALTER TABLE IF EXISTS plantmonitor.photo_tour_event OWNER to postgres;

ALTER TABLE IF EXISTS plantmonitor.photo_tour_event DROP COLUMN IF EXISTS event_class;
ALTER TABLE IF EXISTS plantmonitor.photo_tour_event ADD COLUMN type plantmonitor.photo_tour_event_type NOT NULL;
ALTER TABLE IF EXISTS plantmonitor.photo_tour_event ADD COLUMN references_event bigint;
ALTER TABLE IF EXISTS plantmonitor.photo_tour_event
    ADD FOREIGN KEY (references_event)
    REFERENCES plantmonitor.photo_tour_event (id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE NO ACTION
    NOT VALID;

ALTER TABLE IF EXISTS plantmonitor.temperature_measurement RENAME device_id TO sensor_id;
ALTER TABLE IF EXISTS plantmonitor.temperature_measurement ADD COLUMN device_id uuid;
update plantmonitor.temperature_measurement set device_id='00000000-0000-0000-0000-000000000000'::UUID;
ALTER TABLE IF EXISTS plantmonitor.temperature_measurement ALTER COLUMN device_id SET NOT NULL;

ALTER TABLE IF EXISTS plantmonitor.temperature_measurement ADD COLUMN finished boolean NOT NULL DEFAULT False;
update plantmonitor.temperature_measurement set finished=True WHERE 1=1;


update plantmonitor.configuration_data set value='5' where key='PatchNumber';