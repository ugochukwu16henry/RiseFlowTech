using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateMarketingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AffiliateId",
                table: "Schools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AffiliateReferralCodeUsed",
                table: "Schools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AffiliateLeadRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InviteSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateLeadRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Affiliates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UniqueCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HeadshotPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    BankName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PaystackRecipientCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Affiliates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Affiliates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateTrainingVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    YoutubeUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateTrainingVideos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateLeadRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InviteToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateInvites_AffiliateLeadRequests_AffiliateLeadRequest~",
                        column: x => x.AffiliateLeadRequestId,
                        principalTable: "AffiliateLeadRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateNotifications_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliatePayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PayoutType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PaystackTransferReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliatePayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliatePayouts_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCommissionLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffiliatePayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentCount = table.Column<int>(type: "integer", nullable: false),
                    BillableStudentCount = table.Column<int>(type: "integer", nullable: false),
                    ActivationCommissionAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    MonthlyCommissionAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCommissionAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CommissionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCommissionLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissionLedgers_AffiliatePayouts_AffiliatePayout~",
                        column: x => x.AffiliatePayoutId,
                        principalTable: "AffiliatePayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissionLedgers_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissionLedgers_BillingRecords_BillingRecordId",
                        column: x => x.BillingRecordId,
                        principalTable: "BillingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissionLedgers_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schools_AffiliateId",
                table: "Schools",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionLedgers_AffiliateId",
                table: "AffiliateCommissionLedgers",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionLedgers_AffiliatePayoutId",
                table: "AffiliateCommissionLedgers",
                column: "AffiliatePayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionLedgers_BillingRecordId",
                table: "AffiliateCommissionLedgers",
                column: "BillingRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionLedgers_SchoolId",
                table: "AffiliateCommissionLedgers",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateInvites_AffiliateLeadRequestId",
                table: "AffiliateInvites",
                column: "AffiliateLeadRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateInvites_InviteToken",
                table: "AffiliateInvites",
                column: "InviteToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLeadRequests_Email",
                table: "AffiliateLeadRequests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateNotifications_AffiliateId",
                table: "AffiliateNotifications",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliatePayouts_AffiliateId",
                table: "AffiliatePayouts",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_UniqueCode",
                table: "Affiliates",
                column: "UniqueCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_UserId",
                table: "Affiliates",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_Affiliates_AffiliateId",
                table: "Schools",
                column: "AffiliateId",
                principalTable: "Affiliates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schools_Affiliates_AffiliateId",
                table: "Schools");

            migrationBuilder.DropTable(
                name: "AffiliateCommissionLedgers");

            migrationBuilder.DropTable(
                name: "AffiliateInvites");

            migrationBuilder.DropTable(
                name: "AffiliateNotifications");

            migrationBuilder.DropTable(
                name: "AffiliateTrainingVideos");

            migrationBuilder.DropTable(
                name: "AffiliatePayouts");

            migrationBuilder.DropTable(
                name: "AffiliateLeadRequests");

            migrationBuilder.DropTable(
                name: "Affiliates");

            migrationBuilder.DropIndex(
                name: "IX_Schools_AffiliateId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "AffiliateId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "AffiliateReferralCodeUsed",
                table: "Schools");
        }
    }
}
