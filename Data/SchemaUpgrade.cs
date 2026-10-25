using Microsoft.EntityFrameworkCore;

namespace TimeTracker.Data;

public static class SchemaUpgrade
{
    private static readonly string[] OwnedTables = ["Customers", "Projects", "TimeEntries"];

    // EnsureCreated does not alter existing databases, so bring older ones up to date. Every step is idempotent.
    public static void Apply(TimeTrackerDbContext db)
    {
        // Remove the string-based user columns of an earlier iteration.
        db.Database.ExecuteSqlRaw("""
            IF EXISTS (SELECT 1 FROM sys.columns
                       WHERE object_id = OBJECT_ID('TimeEntries') AND name = 'UserId' AND system_type_id = 231)
            BEGIN
                DROP INDEX IF EXISTS IX_TimeEntries_UserId ON TimeEntries;
                ALTER TABLE TimeEntries DROP COLUMN UserId;
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('TimeEntries', 'UserName') IS NOT NULL
                ALTER TABLE TimeEntries DROP COLUMN UserName;
            """);

        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID('Users', 'U') IS NULL
                CREATE TABLE Users (
                    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                    Login nvarchar(320) NOT NULL);
            """);
        db.Database.ExecuteSqlRaw("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Login' AND object_id = OBJECT_ID('Users'))
                CREATE UNIQUE INDEX IX_Users_Login ON Users (Login);
            """);

        foreach (var table in OwnedTables)
        {
            var addColumn = "IF COL_LENGTH('" + table + "', 'UserId') IS NULL ALTER TABLE " + table + " ADD UserId int NULL;";
            var addIndex = "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_" + table + "_UserId' AND object_id = OBJECT_ID('" + table + "')) "
                + "CREATE INDEX IX_" + table + "_UserId ON " + table + " (UserId);";
            var addForeignKey = "IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_" + table + "_Users_UserId') "
                + "ALTER TABLE " + table + " ADD CONSTRAINT FK_" + table + "_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id);";

            db.Database.ExecuteSqlRaw(addColumn);
            db.Database.ExecuteSqlRaw(addIndex);
            db.Database.ExecuteSqlRaw(addForeignKey);
        }
    }
}
