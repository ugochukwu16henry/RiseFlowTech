using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowancesNote",
                table: "Teachers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseSalaryAmount",
                table: "Teachers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseSalaryCurrency",
                table: "Teachers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateEmployed",
                table: "Teachers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Teachers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Teachers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "Teachers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FieldOfStudy",
                table: "Teachers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Teachers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HighestQualification",
                table: "Teachers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LGA",
                table: "Teachers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NIN",
                table: "Teachers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdNumber",
                table: "Teachers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdType",
                table: "Teachers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "Teachers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchools",
                table: "Teachers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalBodies",
                table: "Teachers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoFileName",
                table: "Teachers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionHistory",
                table: "Teachers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recognitions",
                table: "Teachers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialAddress",
                table: "Teachers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoleTitle",
                table: "Teachers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateOfOrigin",
                table: "Teachers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrcnNumber",
                table: "Teachers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "Teachers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowancesNote",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "BaseSalaryAmount",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "BaseSalaryCurrency",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "DateEmployed",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "FieldOfStudy",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "HighestQualification",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "LGA",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "NIN",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "NationalIdNumber",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "NationalIdType",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "PreviousSchools",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ProfessionalBodies",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoFileName",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "PromotionHistory",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Recognitions",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ResidentialAddress",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "RoleTitle",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "StateOfOrigin",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "TrcnNumber",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "Teachers");
        }
    }
}
