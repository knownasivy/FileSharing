alter table Uploads
    add constraint filecount_nonnegative check (FilesCount >= 0);