create table AudioMetadata
(
    FileId      uuid not null
        constraint AudioMetadata_uploads_id_fk
            references uploadfiles(id)
            on delete cascade,
    Title       text not null,
    Album       text not null,
    Artist      text not null,
    AttachedPic boolean not null
);

create table ZipMetadata
(
    FileId   uuid not null
        constraint ZipMetadata_uploads_id_fk
            references uploadfiles(id)
            on delete cascade,
    Files    jsonb not null,
    Password boolean not null
);