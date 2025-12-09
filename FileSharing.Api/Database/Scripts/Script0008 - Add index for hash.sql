create index concurrently if not exists idx_uploadfiles_hash_fakefile
    on UploadFiles(Hash, FakeFile);

create index concurrently if not exists idx_uploadfiles_createdat
    on UploadFiles(CreatedAt);