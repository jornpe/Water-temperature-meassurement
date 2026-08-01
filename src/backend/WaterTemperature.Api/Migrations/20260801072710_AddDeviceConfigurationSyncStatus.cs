using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceConfigurationSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfigurationSyncAttemptCount",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConfigurationSyncError",
                table: "Devices",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigurationSyncStatus",
                table: "Devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfigurationSyncStatusUpdatedAtUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Devices"
                SET "ConfigurationSyncStatus" = CASE
                        WHEN "ReportedConfigurationVersion" = "DesiredConfigurationVersion"
                            AND "ReportedReportIntervalSeconds" = "ReportIntervalSeconds"
                        THEN 'Synchronized'
                        ELSE 'Pending'
                    END,
                    "ConfigurationSyncStatusUpdatedAtUtc" = "RuntimeConfigurationReportedAtUtc";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfigurationSyncAttemptCount",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ConfigurationSyncError",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ConfigurationSyncStatus",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ConfigurationSyncStatusUpdatedAtUtc",
                table: "Devices");
        }
    }
}
