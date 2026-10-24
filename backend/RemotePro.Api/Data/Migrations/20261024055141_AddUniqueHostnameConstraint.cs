using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemotePro.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueHostnameConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_Hostname",
                table: "devices");

            migrationBuilder.CreateIndex(
                name: "IX_devices_Hostname",
                table: "devices",
                column: "Hostname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_Hostname",
                table: "devices");

            migrationBuilder.CreateIndex(
                name: "IX_devices_Hostname",
                table: "devices",
                column: "Hostname");
        }
    }
}
