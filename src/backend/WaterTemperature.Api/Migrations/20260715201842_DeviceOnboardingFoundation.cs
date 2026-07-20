using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class DeviceOnboardingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Place = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReportIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastDiscoveryTransport = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ApiKeyHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PendingApiKeyProtected = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDiscoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdateReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApiKeyCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApiKeyIssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApiKeyLastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestTemperatureCelsius = table.Column<decimal>(type: "numeric", nullable: true),
                    LatestTemperatureAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestLatitude = table.Column<double>(type: "double precision", nullable: true),
                    LatestLongitude = table.Column<double>(type: "double precision", nullable: true),
                    LatestAltitudeMeters = table.Column<double>(type: "double precision", nullable: true),
                    LatestGpsTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestSpeedKnots = table.Column<double>(type: "double precision", nullable: true),
                    LatestHdop = table.Column<double>(type: "double precision", nullable: true),
                    LatestSatellitesVisible = table.Column<int>(type: "integer", nullable: true),
                    LatestSatellitesUsed = table.Column<int>(type: "integer", nullable: true),
                    LatestPositionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestNetworkTransport = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LatestWifiLocalIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LatestWifiRssiDbm = table.Column<int>(type: "integer", nullable: true),
                    LatestWifiSsid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LatestWifiBssid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LatestWifiChannel = table.Column<int>(type: "integer", nullable: true),
                    LatestWifiGatewayIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LatestWifiSubnetMask = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LatestWifiDnsIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LatestWifiMacAddress = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LatestCellularLocalIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LatestCellularSimStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LatestCellularNetworkConnected = table.Column<bool>(type: "boolean", nullable: true),
                    LatestCellularGprsConnected = table.Column<bool>(type: "boolean", nullable: true),
                    LatestCellularOperator = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LatestCellularSignalQuality = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DevicePositionHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AltitudeMeters = table.Column<double>(type: "double precision", nullable: true),
                    GpsTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SpeedKnots = table.Column<double>(type: "double precision", nullable: true),
                    Hdop = table.Column<double>(type: "double precision", nullable: true),
                    SatellitesVisible = table.Column<int>(type: "integer", nullable: true),
                    SatellitesUsed = table.Column<int>(type: "integer", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePositionHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevicePositionHistory_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTemperatureHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    TemperatureCelsius = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTemperatureHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTemperatureHistory_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePositionHistory_DeviceId_RecordedAtUtc",
                table: "DevicePositionHistory",
                columns: new[] { "DeviceId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTemperatureHistory_DeviceId_RecordedAtUtc",
                table: "DeviceTemperatureHistory",
                columns: new[] { "DeviceId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceIdentifier",
                table: "Devices",
                column: "DeviceIdentifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevicePositionHistory");

            migrationBuilder.DropTable(
                name: "DeviceTemperatureHistory");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
