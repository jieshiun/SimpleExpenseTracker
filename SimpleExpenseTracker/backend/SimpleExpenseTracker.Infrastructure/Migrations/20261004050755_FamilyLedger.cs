using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpleExpenseTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FamilyLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedById",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreationHash",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedById",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OwnerMemberId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Ownership",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedById",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Members",
                columns: new[] { "Id", "Name", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,] {
                    { 1, "成員一", true, new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "成員二", true, new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ClientRequestId",
                table: "Transactions",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreatedById",
                table: "Transactions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_DeletedById",
                table: "Transactions",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OwnerMemberId",
                table: "Transactions",
                column: "OwnerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UpdatedById",
                table: "Transactions",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Members_CreatedById",
                table: "Transactions",
                column: "CreatedById",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Members_DeletedById",
                table: "Transactions",
                column: "DeletedById",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Members_OwnerMemberId",
                table: "Transactions",
                column: "OwnerMemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Members_UpdatedById",
                table: "Transactions",
                column: "UpdatedById",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Members_CreatedById",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Members_DeletedById",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Members_OwnerMemberId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Members_UpdatedById",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ClientRequestId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CreatedById",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_DeletedById",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_OwnerMemberId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UpdatedById",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreationHash",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "OwnerMemberId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Ownership",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Transactions");
        }
    }
}
