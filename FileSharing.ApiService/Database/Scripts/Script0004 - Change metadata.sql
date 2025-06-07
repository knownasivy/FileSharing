alter table AudioMetadata add column FileHash bytea;
update AudioMetadata am
set FileHash = uf.Hash
from UploadFiles uf
where am.FileId = uf.Id;
alter table AudioMetadata alter column FileHash set not null;
alter table AudioMetadata drop constraint if exists AudioMetadata_uploads_id_fk;
alter table AudioMetadata drop column FileId;
alter table AudioMetadata add primary key (FileHash);

alter table ZipMetadata add column FileHash bytea;
update ZipMetadata zm
set FileHash = uf.Hash
from UploadFiles uf
where zm.FileId = uf.Id;
alter table ZipMetadata alter column FileHash set not null;
alter table ZipMetadata drop constraint if exists ZipMetadata_uploads_id_fk;
alter table ZipMetadata drop column FileId;
alter table ZipMetadata add primary key (FileHash);
