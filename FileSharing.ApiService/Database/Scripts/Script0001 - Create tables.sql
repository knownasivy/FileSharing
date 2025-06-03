create table Uploads
(
    Id        uuid not null
        constraint Uploads_pk
            primary key,
    Status    integer not null,
    CreatedAt timestamp not null,
    IPAddress text not null
);

create table UploadFiles
(
    Id        uuid not null
        constraint Files_pk
            primary key,
    UploadId  uuid not null
        constraint Files_uploads_id_fk
            references uploads(id)
            on delete cascade,
    Name      text not null,
    Size      integer not null,
    Type      integer not null,
    Status    integer not null,
    CreatedAt timestamp not null,
    Hash      bytea not null,
    FakeFile  boolean not null,
    IPAddress text not null
);