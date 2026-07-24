using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMqttPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordProtected",
                table: "HomeAssistantIntegrationSettings");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "HomeAssistantIntegrationSettings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Password",
                table: "HomeAssistantIntegrationSettings");

            migrationBuilder.AddColumn<string>(
                name: "PasswordProtected",
                table: "HomeAssistantIntegrationSettings",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);
        }
    }
}
