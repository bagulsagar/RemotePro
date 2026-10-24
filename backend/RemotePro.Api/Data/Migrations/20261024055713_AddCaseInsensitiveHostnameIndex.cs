using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemotePro.Api.Data.Migrations
{
    public partial class AddCaseInsensitiveHostnameIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_Hostname",
                table: "devices");

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_devices_Hostname_CaseInsensitive"
                ON public.devices (LOWER(TRIM("Hostname")));
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS public."IX_devices_Hostname_CaseInsensitive";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_devices_Hostname",
                table: "devices",
                column: "Hostname",
                unique: true);
        }
    }
}