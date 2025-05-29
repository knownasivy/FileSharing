create table Files
(
    Id        uuid not null
        constraint Files_pk
            primary key,
    Name      text not null,
    Size      integer not null,
    Type      integer not null,
    Status    integer not null,
    CreatedAt timestamp not null,
    Hash      bytea not null,
    FakeFile  boolean not null,
    IPAddress text not null
);

create table AudioMetadata
(
    FileId        uuid not null
        constraint AudioMetadata_pk
            primary key,
    Title       text not null,
    Album       text not null,
    Artist      text not null,
    AttachedPic boolean not null
);

create table ArchiveMetadata
(
    FileId        uuid not null
        constraint ArchiveMetadata_pk
            primary key,
    Password  boolean not null,
    Files jsonb not null
);

create table ImageMetadata
(
    FileId        uuid not null
        constraint ImageMetadata_pk
            primary key,
    Size integer not null
);