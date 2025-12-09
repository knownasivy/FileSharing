alter table Uploads 
    add FilesCount integer;

update Uploads u
set FilesCount = (
    select count(*) from UploadFiles uf where uf.UploadId = u.id
);

alter table Uploads
    alter column FilesCount set not null;