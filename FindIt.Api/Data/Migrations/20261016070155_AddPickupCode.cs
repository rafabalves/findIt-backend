using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FindIt.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PickupCodeEncrypted",
                table: "recovery_request",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickupCodeFailedAttempts",
                table: "recovery_request",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupCodeLockedUntil",
                table: "recovery_request",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupCodeEncrypted",
                table: "recovery_request");

            migrationBuilder.DropColumn(
                name: "PickupCodeFailedAttempts",
                table: "recovery_request");

            migrationBuilder.DropColumn(
                name: "PickupCodeLockedUntil",
                table: "recovery_request");
        }
    }
}
