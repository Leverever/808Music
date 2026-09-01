SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName sysname = N'$(DatabaseName)';
DECLARE @BackupPath nvarchar(4000) = N'$(BackupPath)';

IF DB_ID(@DatabaseName) IS NOT NULL
BEGIN
    PRINT N'Database ' + QUOTENAME(@DatabaseName) + N' already exists; keeping the volume data.';
    RETURN;
END;

PRINT N'Restoring ' + QUOTENAME(@DatabaseName) + N' from ' + @BackupPath + N'.';

CREATE TABLE #BackupFiles
(
    LogicalName nvarchar(128),
    PhysicalName nvarchar(260),
    [Type] char(1),
    FileGroupName nvarchar(128) NULL,
    Size numeric(20, 0),
    MaxSize numeric(20, 0),
    FileId bigint,
    CreateLSN numeric(25, 0),
    DropLSN numeric(25, 0) NULL,
    UniqueId uniqueidentifier,
    ReadOnlyLSN numeric(25, 0) NULL,
    ReadWriteLSN numeric(25, 0) NULL,
    BackupSizeInBytes bigint,
    SourceBlockSize int,
    FileGroupId int,
    LogGroupGUID uniqueidentifier NULL,
    DifferentialBaseLSN numeric(25, 0) NULL,
    DifferentialBaseGUID uniqueidentifier NULL,
    IsReadOnly bit,
    IsPresent bit,
    TDEThumbprint varbinary(32) NULL,
    SnapshotURL nvarchar(360) NULL
);

DECLARE @FileListSql nvarchar(max) =
    N'RESTORE FILELISTONLY FROM DISK = N''' +
    REPLACE(@BackupPath, '''', '''''') + N'''';

INSERT INTO #BackupFiles
EXEC sys.sp_executesql @FileListSql;

DECLARE @LogicalName sysname;
DECLARE @FileType char(1);
DECLARE @FileId bigint;
DECLARE @MoveClauses nvarchar(max) = N'';

DECLARE backup_files CURSOR LOCAL FAST_FORWARD FOR
    SELECT LogicalName, [Type], FileId
    FROM #BackupFiles
    ORDER BY FileId;

OPEN backup_files;
FETCH NEXT FROM backup_files INTO @LogicalName, @FileType, @FileId;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @MoveClauses +=
        N', MOVE N''' + REPLACE(@LogicalName, '''', '''''') +
        N''' TO N''/var/opt/mssql/data/' + @DatabaseName + N'_' +
        CONVERT(nvarchar(20), @FileId) +
        CASE WHEN @FileType = 'L' THEN N'.ldf' ELSE N'.mdf' END + N'''';

    FETCH NEXT FROM backup_files INTO @LogicalName, @FileType, @FileId;
END;

CLOSE backup_files;
DEALLOCATE backup_files;

IF @MoveClauses = N''
    THROW 50001, 'The backup contains no restorable database files.', 1;

DECLARE @RestoreSql nvarchar(max) =
    N'RESTORE DATABASE ' + QUOTENAME(@DatabaseName) +
    N' FROM DISK = N''' + REPLACE(@BackupPath, '''', '''''') +
    N''' WITH RECOVERY' + @MoveClauses + N';';

EXEC sys.sp_executesql @RestoreSql;
PRINT N'Database ' + QUOTENAME(@DatabaseName) + N' restored successfully.';
