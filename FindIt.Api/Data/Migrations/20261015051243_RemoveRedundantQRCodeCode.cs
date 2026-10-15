using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FindIt.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantQRCodeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_qr_code_Code",
                table: "qr_code");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "qr_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "qr_code",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE `qr_code` SET `Code` = REPLACE(`Guid`, '-', '')");

            migrationBuilder.CreateIndex(
                name: "IX_qr_code_Code",
                table: "qr_code",
                column: "Code",
                unique: true);
        }
    }
}
