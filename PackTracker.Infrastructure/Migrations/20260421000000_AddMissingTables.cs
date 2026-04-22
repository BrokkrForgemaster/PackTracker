using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PackTracker.Infrastructure.Persistence;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260421000000_AddMissingTables")]
    public partial class AddMissingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF NOT EXISTS so this migration is idempotent even when the
            // defensive startup SQL (or a previous run) already created these tables.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""LoginStates"" (
                    ""Id"" uuid NOT NULL,
                    ""ClientState"" character varying(256) NOT NULL,
                    ""AccessToken"" text NOT NULL DEFAULT '',
                    ""RefreshToken"" text NOT NULL DEFAULT '',
                    ""ExpiresIn"" integer NOT NULL DEFAULT 0,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""ExpiresAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_LoginStates"" PRIMARY KEY (""Id"")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LoginStates_ClientState""
                    ON ""LoginStates""(""ClientState"");

                CREATE TABLE IF NOT EXISTS ""SyncMetadatas"" (
                    ""Id"" uuid NOT NULL,
                    ""TaskName"" character varying(128) NOT NULL,
                    ""LastStartedAt"" timestamp with time zone,
                    ""LastCompletedAt"" timestamp with time zone,
                    ""IsSuccess"" boolean NOT NULL DEFAULT false,
                    ""LastErrorMessage"" text,
                    ""ItemsProcessed"" integer NOT NULL DEFAULT 0,
                    CONSTRAINT ""PK_SyncMetadatas"" PRIMARY KEY (""Id"")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SyncMetadatas_TaskName""
                    ON ""SyncMetadatas""(""TaskName"");

                CREATE TABLE IF NOT EXISTS ""DistributedLocks"" (
                    ""LockKey"" character varying(128) NOT NULL,
                    ""LockedBy"" character varying(128) NOT NULL,
                    ""LockedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""ExpiresAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_DistributedLocks"" PRIMARY KEY (""LockKey"")
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""LoginStates"";
                DROP TABLE IF EXISTS ""SyncMetadatas"";
                DROP TABLE IF EXISTS ""DistributedLocks"";
            ");
        }
    }
}
