using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionTransitionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "AcademicSystemProfileId" uuid NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "OwnerName" character varying(128) NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "PromotionTransitionOverrideJson" character varying(12000) NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "RegistrationDocumentPath" character varying(512) NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "SchoolAdminName" character varying(128) NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "WhatsAppNumber" character varying(512) NULL;
ALTER TABLE IF EXISTS "FileAssets" ADD COLUMN IF NOT EXISTS "FileBytes" bytea NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "HeadshotBytes" bytea NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "HeadshotContentType" character varying(128) NULL;

CREATE TABLE IF NOT EXISTS "AcademicSystemProfiles" (
    "Id" uuid NOT NULL,
    "Code" character varying(32) NOT NULL,
    "Name" character varying(128) NOT NULL,
    "Description" character varying(512),
    "SuggestedTermsPerYear" integer,
    "GradeTemplatesJson" character varying(12000) NOT NULL,
    "StageOrderJson" character varying(12000),
    "PromotionTransitionJson" character varying(12000),
    "DefaultGradingScaleCode" character varying(64),
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_AcademicSystemProfiles" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AcademicSystemProfiles_Code" ON "AcademicSystemProfiles" ("Code");

DO $EF$
BEGIN
    IF to_regclass('"Affiliates"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: Affiliates';
    END IF;
    IF to_regclass('"AffiliateTrainingVideos"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: AffiliateTrainingVideos';
    END IF;
END $EF$;

CREATE TABLE IF NOT EXISTS "AffiliateTrainingCompletions" (
    "Id" uuid NOT NULL,
    "AffiliateId" uuid NOT NULL,
    "TrainingVideoId" uuid NOT NULL,
    "IsCompleted" boolean NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_AffiliateTrainingCompletions" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AffiliateTrainingCompletions_AffiliateId_TrainingVideoId"
    ON "AffiliateTrainingCompletions" ("AffiliateId", "TrainingVideoId");
CREATE INDEX IF NOT EXISTS "IX_AffiliateTrainingCompletions_TrainingVideoId"
    ON "AffiliateTrainingCompletions" ("TrainingVideoId");

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_AffiliateTrainingCompletions_Affiliates_AffiliateId'
          AND conrelid = '"AffiliateTrainingCompletions"'::regclass
    ) THEN
        ALTER TABLE "AffiliateTrainingCompletions"
        ADD CONSTRAINT "FK_AffiliateTrainingCompletions_Affiliates_AffiliateId"
        FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_AffiliateTrainingCompletions_AffiliateTrainingVideos_TrainingVideoId'
          AND conrelid = '"AffiliateTrainingCompletions"'::regclass
    ) THEN
        ALTER TABLE "AffiliateTrainingCompletions"
        ADD CONSTRAINT "FK_AffiliateTrainingCompletions_AffiliateTrainingVideos_TrainingVideoId"
        FOREIGN KEY ("TrainingVideoId") REFERENCES "AffiliateTrainingVideos" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF to_regclass('"AcademicTerms"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: AcademicTerms';
    END IF;
    IF to_regclass('"Classes"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: Classes';
    END IF;
    IF to_regclass('"Schools"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: Schools';
    END IF;
    IF to_regclass('"Teachers"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: Teachers';
    END IF;
END $EF$;

CREATE TABLE IF NOT EXISTS "ClassPromotionRequests" (
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "TeacherId" uuid NOT NULL,
    "FromClassId" uuid NOT NULL,
    "ToClassId" uuid NOT NULL,
    "FromTermId" uuid,
    "PromotionSessionLabel" character varying(64),
    "Notes" character varying(512),
    "Status" character varying(16) NOT NULL,
    "RequestedAtUtc" timestamp with time zone NOT NULL,
    "ReviewedAtUtc" timestamp with time zone,
    "ReviewedByUserId" uuid,
    CONSTRAINT "PK_ClassPromotionRequests" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_ClassPromotionRequests_FromClassId" ON "ClassPromotionRequests" ("FromClassId");
CREATE INDEX IF NOT EXISTS "IX_ClassPromotionRequests_FromTermId" ON "ClassPromotionRequests" ("FromTermId");
CREATE INDEX IF NOT EXISTS "IX_ClassPromotionRequests_SchoolId_Status_RequestedAtUtc" ON "ClassPromotionRequests" ("SchoolId", "Status", "RequestedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_ClassPromotionRequests_TeacherId" ON "ClassPromotionRequests" ("TeacherId");
CREATE INDEX IF NOT EXISTS "IX_ClassPromotionRequests_ToClassId" ON "ClassPromotionRequests" ("ToClassId");

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_ClassPromotionRequests_AcademicTerms_FromTermId'
          AND conrelid = '"ClassPromotionRequests"'::regclass
    ) THEN
        ALTER TABLE "ClassPromotionRequests"
        ADD CONSTRAINT "FK_ClassPromotionRequests_AcademicTerms_FromTermId"
        FOREIGN KEY ("FromTermId") REFERENCES "AcademicTerms" ("Id") ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_ClassPromotionRequests_Classes_FromClassId'
          AND conrelid = '"ClassPromotionRequests"'::regclass
    ) THEN
        ALTER TABLE "ClassPromotionRequests"
        ADD CONSTRAINT "FK_ClassPromotionRequests_Classes_FromClassId"
        FOREIGN KEY ("FromClassId") REFERENCES "Classes" ("Id") ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_ClassPromotionRequests_Classes_ToClassId'
          AND conrelid = '"ClassPromotionRequests"'::regclass
    ) THEN
        ALTER TABLE "ClassPromotionRequests"
        ADD CONSTRAINT "FK_ClassPromotionRequests_Classes_ToClassId"
        FOREIGN KEY ("ToClassId") REFERENCES "Classes" ("Id") ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_ClassPromotionRequests_Schools_SchoolId'
          AND conrelid = '"ClassPromotionRequests"'::regclass
    ) THEN
        ALTER TABLE "ClassPromotionRequests"
        ADD CONSTRAINT "FK_ClassPromotionRequests_Schools_SchoolId"
        FOREIGN KEY ("SchoolId") REFERENCES "Schools" ("Id") ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_ClassPromotionRequests_Teachers_TeacherId'
          AND conrelid = '"ClassPromotionRequests"'::regclass
    ) THEN
        ALTER TABLE "ClassPromotionRequests"
        ADD CONSTRAINT "FK_ClassPromotionRequests_Teachers_TeacherId"
        FOREIGN KEY ("TeacherId") REFERENCES "Teachers" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF to_regclass('"ClassPromotionRequests"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: ClassPromotionRequests';
    END IF;
    IF to_regclass('"Students"') IS NULL THEN
        RAISE EXCEPTION 'Missing prerequisite table: Students';
    END IF;
END $EF$;

CREATE TABLE IF NOT EXISTS "ClassPromotionRequestItems" (
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    CONSTRAINT "PK_ClassPromotionRequestItems" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ClassPromotionRequestItems_RequestId_StudentId"
    ON "ClassPromotionRequestItems" ("RequestId", "StudentId");
CREATE INDEX IF NOT EXISTS "IX_ClassPromotionRequestItems_SchoolId_StudentId"
    ON "ClassPromotionRequestItems" ("SchoolId", "StudentId");
CREATE INDEX IF NOT EXISTS "IX_ClassPromotionRequestItems_StudentId"
    ON "ClassPromotionRequestItems" ("StudentId");

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_ClassPromotionRequestItems_ClassPromotionRequests_RequestId'
          AND conrelid = '"ClassPromotionRequestItems"'::regclass
    ) THEN
        ALTER TABLE "ClassPromotionRequestItems"
        ADD CONSTRAINT "FK_ClassPromotionRequestItems_ClassPromotionRequests_RequestId"
        FOREIGN KEY ("RequestId") REFERENCES "ClassPromotionRequests" ("Id") ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_ClassPromotionRequestItems_Students_StudentId'
          AND conrelid = '"ClassPromotionRequestItems"'::regclass
    ) THEN
        ALTER TABLE "ClassPromotionRequestItems"
        ADD CONSTRAINT "FK_ClassPromotionRequestItems_Students_StudentId"
        FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

CREATE INDEX IF NOT EXISTS "IX_Schools_AcademicSystemProfileId" ON "Schools" ("AcademicSystemProfileId");

DO $EF$
DECLARE
    orphan_count integer;
BEGIN
    SELECT COUNT(*) INTO orphan_count
    FROM "Schools" s
    WHERE s."AcademicSystemProfileId" IS NOT NULL
      AND NOT EXISTS (
            SELECT 1 FROM "AcademicSystemProfiles" a
            WHERE a."Id" = s."AcademicSystemProfileId"
      );

    IF orphan_count > 0 THEN
        RAISE EXCEPTION 'Cannot add FK_Schools_AcademicSystemProfiles_AcademicSystemProfileId: % orphan rows in Schools.AcademicSystemProfileId', orphan_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_Schools_AcademicSystemProfiles_AcademicSystemProfileId'
          AND conrelid = '"Schools"'::regclass
    ) THEN
        ALTER TABLE "Schools"
        ADD CONSTRAINT "FK_Schools_AcademicSystemProfiles_AcademicSystemProfileId"
        FOREIGN KEY ("AcademicSystemProfileId") REFERENCES "AcademicSystemProfiles" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;
""");

                return;
            }

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicSystemProfileId",
                table: "Schools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Schools",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionTransitionOverrideJson",
                table: "Schools",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationDocumentPath",
                table: "Schools",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolAdminName",
                table: "Schools",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "Schools",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FileBytes",
                table: "FileAssets",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "HeadshotBytes",
                table: "Affiliates",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadshotContentType",
                table: "Affiliates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcademicSystemProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SuggestedTermsPerYear = table.Column<int>(type: "integer", nullable: true),
                    GradeTemplatesJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    StageOrderJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                    PromotionTransitionJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                    DefaultGradingScaleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicSystemProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateTrainingCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateTrainingCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateTrainingCompletions_AffiliateTrainingVideos_Traini~",
                        column: x => x.TrainingVideoId,
                        principalTable: "AffiliateTrainingVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffiliateTrainingCompletions_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassPromotionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTermId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionSessionLabel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassPromotionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_AcademicTerms_FromTermId",
                        column: x => x.FromTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Classes_FromClassId",
                        column: x => x.FromClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Classes_ToClassId",
                        column: x => x.ToClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassPromotionRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassPromotionRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequestItems_ClassPromotionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ClassPromotionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequestItems_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schools_AcademicSystemProfileId",
                table: "Schools",
                column: "AcademicSystemProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSystemProfiles_Code",
                table: "AcademicSystemProfiles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateTrainingCompletions_AffiliateId_TrainingVideoId",
                table: "AffiliateTrainingCompletions",
                columns: new[] { "AffiliateId", "TrainingVideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateTrainingCompletions_TrainingVideoId",
                table: "AffiliateTrainingCompletions",
                column: "TrainingVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequestItems_RequestId_StudentId",
                table: "ClassPromotionRequestItems",
                columns: new[] { "RequestId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequestItems_SchoolId_StudentId",
                table: "ClassPromotionRequestItems",
                columns: new[] { "SchoolId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequestItems_StudentId",
                table: "ClassPromotionRequestItems",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_FromClassId",
                table: "ClassPromotionRequests",
                column: "FromClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_FromTermId",
                table: "ClassPromotionRequests",
                column: "FromTermId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_SchoolId_Status_RequestedAtUtc",
                table: "ClassPromotionRequests",
                columns: new[] { "SchoolId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_TeacherId",
                table: "ClassPromotionRequests",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_ToClassId",
                table: "ClassPromotionRequests",
                column: "ToClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_AcademicSystemProfiles_AcademicSystemProfileId",
                table: "Schools",
                column: "AcademicSystemProfileId",
                principalTable: "AcademicSystemProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schools_AcademicSystemProfiles_AcademicSystemProfileId",
                table: "Schools");

            migrationBuilder.DropTable(
                name: "AcademicSystemProfiles");

            migrationBuilder.DropTable(
                name: "AffiliateTrainingCompletions");

            migrationBuilder.DropTable(
                name: "ClassPromotionRequestItems");

            migrationBuilder.DropTable(
                name: "ClassPromotionRequests");

            migrationBuilder.DropIndex(
                name: "IX_Schools_AcademicSystemProfileId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "AcademicSystemProfileId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "PromotionTransitionOverrideJson",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "RegistrationDocumentPath",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "SchoolAdminName",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "FileBytes",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "HeadshotBytes",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "HeadshotContentType",
                table: "Affiliates");
        }
    }
}
