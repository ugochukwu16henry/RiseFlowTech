using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitActivationAndMonthlyBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CacNumber",
                table: "Schools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolType",
                table: "Schools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActivationAmountDue",
                table: "BillingRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyAmountDue",
                table: "BillingRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PlatformComplianceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataProtectionOfficerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DataProtectionOfficerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DpiaDocumentUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformComplianceSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformComplianceSettings");

            migrationBuilder.DropColumn(
                name: "CacNumber",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "SchoolType",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "ActivationAmountDue",
                table: "BillingRecords");

            migrationBuilder.DropColumn(
                name: "MonthlyAmountDue",
                table: "BillingRecords");
        }
    }
}
