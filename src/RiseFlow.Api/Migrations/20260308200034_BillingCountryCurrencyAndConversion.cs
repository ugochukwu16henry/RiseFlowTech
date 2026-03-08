using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class BillingCountryCurrencyAndConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AmountPaidNaira",
                table: "BillingRecords",
                newName: "AmountPaid");

            migrationBuilder.RenameColumn(
                name: "AmountDueNaira",
                table: "BillingRecords",
                newName: "AmountDue");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Schools",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Schools",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "NGN");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "BillingRecords",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "NGN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "BillingRecords");

            migrationBuilder.RenameColumn(
                name: "AmountPaid",
                table: "BillingRecords",
                newName: "AmountPaidNaira");

            migrationBuilder.RenameColumn(
                name: "AmountDue",
                table: "BillingRecords",
                newName: "AmountDueNaira");
        }
    }
}
