using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class CalculateBatteryPercentageFromAdc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestBatteryPercentage",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Percentage",
                table: "DeviceBatteryHistory");

            migrationBuilder.AddColumn<float>(
                name: "BatteryEmptyAdcVoltage",
                table: "Devices",
                type: "real",
                nullable: false,
                defaultValue: 2.5f);

            migrationBuilder.AddColumn<float>(
                name: "BatteryFullAdcVoltage",
                table: "Devices",
                type: "real",
                nullable: false,
                defaultValue: 4.2f);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Devices_BatteryAdcVoltageRange",
                table: "Devices",
                sql: "\"BatteryEmptyAdcVoltage\" >= 0 AND \"BatteryFullAdcVoltage\" > \"BatteryEmptyAdcVoltage\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Devices_BatteryAdcVoltageRange",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BatteryEmptyAdcVoltage",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BatteryFullAdcVoltage",
                table: "Devices");

            migrationBuilder.AddColumn<int>(
                name: "LatestBatteryPercentage",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Percentage",
                table: "DeviceBatteryHistory",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
