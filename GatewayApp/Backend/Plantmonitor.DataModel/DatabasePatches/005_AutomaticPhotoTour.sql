CREATE TABLE plantmonitor.automatic_photo_tour
(
    id bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY,
    device_id uuid NOT NULL,
    name text NOT NULL,
    comment text NOT NULL,
    PRIMARY KEY (id)
);

ALTER TABLE IF EXISTS plantmonitor.automatic_photo_tour OWNER to postgres;

CREATE TABLE plantmonitor.photo_tour_journey
(
    id bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY,
    photo_tour_fk bigint NOT NULL,
    ir_data_folder text NOT NULL,
    vis_data_folder text NOT NULL,
    PRIMARY KEY (id),
    FOREIGN KEY (photo_tour_fk)
        REFERENCES plantmonitor.automatic_photo_tour (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

ALTER TABLE IF EXISTS plantmonitor.photo_tour_journey OWNER to postgres;

ALTER TABLE IF EXISTS plantmonitor.temperature_measurement ADD COLUMN photo_tour_fk bigint;
ALTER TABLE IF EXISTS plantmonitor.temperature_measurement
    ADD FOREIGN KEY (photo_tour_fk)
    REFERENCES plantmonitor.automatic_photo_tour (id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE NO ACTION
    NOT VALID;

update plantmonitor.configuration_data set value='5' where key='PatchNumber';