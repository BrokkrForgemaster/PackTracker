using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    public partial class RepairShowcaseColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The two prior migrations may be recorded in __EFMigrationsHistory without their
            // DDL having actually executed (phantom entries from a failed dotnet-ef run).
            // Remove the phantom entries so that EF will re-run them on the next startup.
            migrationBuilder.Sql(@"
                DELETE FROM ""__EFMigrationsHistory""
                WHERE ""MigrationId"" IN (
                    '20260429234500_AddMedalsIntegration',
                    '20260430000500_AddProfileShowcaseFields'
                )
                AND NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Profiles' AND column_name = 'ShowcaseBio'
                );
            ");

            // Idempotent column additions — safe whether or not the prior migrations ran.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Profiles""
                    ADD COLUMN IF NOT EXISTS ""ShowcaseBio""       character varying(5000),
                    ADD COLUMN IF NOT EXISTS ""ShowcaseEyebrow""   character varying(100),
                    ADD COLUMN IF NOT EXISTS ""ShowcaseImageUrl""  character varying(1024),
                    ADD COLUMN IF NOT EXISTS ""ShowcaseTagline""   character varying(200);
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""MedalDefinitions"" (
                    ""Id""            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    ""Name""          character varying(200)   NOT NULL,
                    ""Description""   character varying(4000)  NOT NULL,
                    ""ImagePath""     character varying(512),
                    ""SourceSystem""  character varying(100)   NOT NULL,
                    ""DisplayOrder""  integer                  NOT NULL,
                    ""CreatedAt""     timestamp with time zone NOT NULL DEFAULT now(),
                    ""UpdatedAt""     timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_MedalDefinitions"" PRIMARY KEY (""Id"")
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MedalDefinitions_Name""
                ON ""MedalDefinitions"" (""Name"");
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""MedalAwards"" (
                    ""Id""                  uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    ""MedalDefinitionId""   uuid                     NOT NULL,
                    ""ProfileId""           uuid,
                    ""RecipientName""       character varying(200)   NOT NULL,
                    ""AwardedAt""           timestamp with time zone,
                    ""ImportedAt""          timestamp with time zone NOT NULL DEFAULT now(),
                    ""SourceSystem""        character varying(100)   NOT NULL,
                    ""Citation""            character varying(4000),
                    ""AwardedBy""           character varying(200),
                    CONSTRAINT ""PK_MedalAwards"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_MedalAwards_MedalDefinitions_MedalDefinitionId""
                        FOREIGN KEY (""MedalDefinitionId"") REFERENCES ""MedalDefinitions"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_MedalAwards_Profiles_ProfileId""
                        FOREIGN KEY (""ProfileId"") REFERENCES ""Profiles"" (""Id"") ON DELETE SET NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MedalAwards_MedalDefinitionId_RecipientName""
                ON ""MedalAwards"" (""MedalDefinitionId"", ""RecipientName"");

                CREATE INDEX IF NOT EXISTS ""IX_MedalAwards_ProfileId""
                ON ""MedalAwards"" (""ProfileId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MedalAwards"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MedalDefinitions"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Profiles""
                    DROP COLUMN IF EXISTS ""ShowcaseBio"",
                    DROP COLUMN IF EXISTS ""ShowcaseEyebrow"",
                    DROP COLUMN IF EXISTS ""ShowcaseImageUrl"",
                    DROP COLUMN IF EXISTS ""ShowcaseTagline"";
            ");
        }
    }
}
