#include <Arduino.h>
#include <ArduinoHttpClient.h>
#include <ArduinoJson.h>
#include <Preferences.h>
#include <TinyGsmClient.h>
#include <WiFi.h>

// ============================================================================
// LILYGO T-SIM7000G hardware
// ============================================================================

constexpr int MODEM_RX_PIN = 26;
constexpr int MODEM_TX_PIN = 27;
constexpr int MODEM_PWRKEY_PIN = 4;
constexpr int MODEM_DTR_PIN = 25;
constexpr int BATTERY_ADC_PIN = 35;

constexpr uint32_t MODEM_BAUD_RATE = 115200;
constexpr uint32_t DEFAULT_REPORT_INTERVAL_SECONDS = 30;
constexpr uint32_t DISCOVERY_INTERVAL_MS = 30000;
constexpr size_t MAX_PENDING_LOG_ENTRIES = 120;
constexpr size_t MAX_LOGS_PER_UPDATE = 25;
constexpr size_t MAX_LOG_MESSAGE_LENGTH = 200;

struct WifiSettings
{
    String ssid;
    String password;
    uint32_t connectionTimeoutMs;
    uint32_t retryIntervalMs;
};

struct CellularSettings
{
    bool enabled;
    String apn;
    String username;
    String password;
    String simPin;
    uint32_t networkTimeoutMs;
};

struct BackendSettings
{
    String host;
    uint16_t port;
    String basePath;
    uint32_t requestTimeoutMs;
};

struct TrackerSettings
{
    String deviceName;
    String firmwareVersion;
    uint32_t gpsPollIntervalMs;
    uint32_t noFixLogIntervalMs;
};

struct RuntimeConfig
{
    WifiSettings wifi;
    CellularSettings cellular;
    BackendSettings backend;
    TrackerSettings tracker;
};

RuntimeConfig config = {
    {
        "Duni19",
        "Eddie2007",
        15000,
        15000,
    },
    {
        false,
        "",
        "",
        "",
        "",
        120000,
    },
    {
        "192.168.0.254",
        8080,
        "",
        15000,
    },
    {
        "LILYGO GPS Tracker",
        "0.1.0",
        5000,
        30000,
    },
};

HardwareSerial SerialAT(1);
TinyGsm modem(SerialAT);
TinyGsmClient cellularClient(modem);
WiFiClient wifiClient;
Preferences preferences;

enum class NetworkTransport
{
    None,
    Wifi,
    Cellular
};

enum class DeviceMode
{
    Discovery,
    Operational
};

NetworkTransport activeTransport = NetworkTransport::None;
DeviceMode deviceMode = DeviceMode::Discovery;

String deviceId;
String deviceApiKey;
uint32_t reportIntervalSeconds = DEFAULT_REPORT_INTERVAL_SECONDS;
uint32_t appliedConfigurationVersion = 0;

bool modemReady = false;
bool gpsReady = false;

uint32_t lastNetworkAttemptMs = 0;
uint32_t lastGpsInitAttemptMs = 0;
uint32_t lastGpsPollMs = 0;
uint32_t lastNoFixLogMs = 0;
uint32_t lastDiscoveryAttemptMs = 0;
uint32_t lastUpdateAttemptMs = 0;

struct PendingDeviceLogEntry
{
    uint32_t sequenceNumber = 0;
    String level;
    String message;
    uint32_t deviceUptimeMs = 0;
};

PendingDeviceLogEntry pendingLogEntries[MAX_PENDING_LOG_ENTRIES];
size_t pendingLogEntryCount = 0;
uint32_t nextPendingLogSequenceNumber = 1;
uint32_t droppedPendingLogCount = 0;

struct GpsFix
{
    bool valid = false;
    float latitude = 0;
    float longitude = 0;
    float altitudeMeters = 0;
    float speedKnots = 0;
    float hdop = 0;
    int satellitesVisible = 0;
    int satellitesUsed = 0;
    int year = 0;
    int month = 0;
    int day = 0;
    int hour = 0;
    int minute = 0;
    int second = 0;
    uint32_t receivedAtMs = 0;
};

GpsFix latestFix;

enum class BatteryState : int8_t
{
    Unknown = -1,
    NotCharging = 0,
    Charging = 1,
    Full = 2
};

struct BatteryStatus
{
    bool modemReadingValid;
    int8_t chargeState;
    BatteryState batteryState;
    int8_t percentage;
    int16_t modemMillivolts;
    float adcVoltage;
};

BatteryStatus latestBatteryStatus;

struct HttpResponse
{
    int statusCode = -1;
    String body;
    bool transportOk = false;
};

bool intervalElapsed(const uint32_t now, const uint32_t previous, const uint32_t interval)
{
    return previous == 0 || now - previous >= interval;
}

String createDeviceId()
{
    const uint64_t chipId = ESP.getEfuseMac();
    char result[40];

    snprintf(
        result,
        sizeof(result),
        "lilygo_sim7000g_%012llx",
        static_cast<unsigned long long>(chipId & 0xFFFFFFFFFFFFULL));

    return String(result);
}

const char* activeTransportName()
{
    switch (activeTransport)
    {
        case NetworkTransport::Wifi:
            return "wifi";
        case NetworkTransport::Cellular:
            return "cellular";
        default:
            return "none";
    }
}

const char* deviceModeName()
{
    switch (deviceMode)
    {
        case DeviceMode::Discovery:
            return "discovery";
        case DeviceMode::Operational:
            return "operational";
        default:
            return "unknown";
    }
}

String formatGpsUtcTime(const GpsFix& fix)
{
    if (fix.year <= 0)
    {
        return "";
    }

    char value[30];

    snprintf(
        value,
        sizeof(value),
        "%04d-%02d-%02dT%02d:%02d:%02dZ",
        fix.year,
        fix.month,
        fix.day,
        fix.hour,
        fix.minute,
        fix.second);

    return String(value);
}

String buildApiPath(const String& endpoint)
{
    String basePath = config.backend.basePath;

    if (basePath.isEmpty() || basePath == "/")
    {
        return endpoint;
    }

    if (!basePath.startsWith("/"))
    {
        basePath = "/" + basePath;
    }

    if (basePath.endsWith("/"))
    {
        basePath.remove(basePath.length() - 1);
    }

    return basePath + endpoint;
}

float buildDummyTemperatureCelsius(const uint32_t now)
{
    const float offset = static_cast<float>((now / 1000) % 20) / 10.0f;
    return 18.5f + offset;
}

String truncateLogMessage(const String& message)
{
    if (message.length() <= MAX_LOG_MESSAGE_LENGTH)
    {
        return message;
    }

    return message.substring(0, MAX_LOG_MESSAGE_LENGTH - 3) + "...";
}

void removeOldestPendingLogEntry()
{
    if (pendingLogEntryCount == 0)
    {
        return;
    }

    for (size_t index = 1; index < pendingLogEntryCount; ++index)
    {
        pendingLogEntries[index - 1] = pendingLogEntries[index];
    }

    pendingLogEntryCount -= 1;
}

void appendPendingLogEntryInternal(const String& level, const String& message, const uint32_t deviceUptimeMs)
{
    if (pendingLogEntryCount >= MAX_PENDING_LOG_ENTRIES)
    {
        return;
    }

    PendingDeviceLogEntry& entry = pendingLogEntries[pendingLogEntryCount++];
    entry.sequenceNumber = nextPendingLogSequenceNumber++;
    entry.level = level;
    entry.message = truncateLogMessage(message);
    entry.deviceUptimeMs = deviceUptimeMs;
}

void appendPendingOverflowWarningIfNeeded(const uint32_t deviceUptimeMs)
{
    if (droppedPendingLogCount == 0)
    {
        return;
    }

    while (pendingLogEntryCount >= MAX_PENDING_LOG_ENTRIES)
    {
        removeOldestPendingLogEntry();
    }

    const uint32_t droppedCount = droppedPendingLogCount;
    droppedPendingLogCount = 0;

    appendPendingLogEntryInternal(
        "warning",
        "Log buffer overflow: dropped " + String(droppedCount) + (droppedCount == 1 ? " unsent log entry." : " unsent log entries."),
        deviceUptimeMs);
}

void appendPendingDeviceLog(const String& level, const String& message)
{
    if (message.isEmpty())
    {
        return;
    }

    const uint32_t now = millis();

    if (pendingLogEntryCount >= MAX_PENDING_LOG_ENTRIES)
    {
        removeOldestPendingLogEntry();
        droppedPendingLogCount += 1;
    }

    appendPendingOverflowWarningIfNeeded(now);

    if (pendingLogEntryCount >= MAX_PENDING_LOG_ENTRIES)
    {
        removeOldestPendingLogEntry();
        droppedPendingLogCount += 1;
        appendPendingOverflowWarningIfNeeded(now);
    }

    appendPendingLogEntryInternal(level, message, now);
}

void logInfo(const String& message)
{
    Serial.println(message);
    appendPendingDeviceLog("info", message);
}

void logWarning(const String& message)
{
    Serial.println(message);
    appendPendingDeviceLog("warning", message);
}

void logError(const String& message)
{
    Serial.println(message);
    appendPendingDeviceLog("error", message);
}

void acknowledgePendingLogs(const uint32_t highestAcknowledgedSequenceNumber)
{
    while (pendingLogEntryCount > 0 && pendingLogEntries[0].sequenceNumber <= highestAcknowledgedSequenceNumber)
    {
        removeOldestPendingLogEntry();
    }
}

void loadPersistedDeviceState()
{
    if (!preferences.begin("device", true))
    {
        logError("Could not open Preferences in read mode.");
        return;
    }

    deviceApiKey = preferences.getString("apiKey", "");
    reportIntervalSeconds = preferences.getUInt("reportSec", DEFAULT_REPORT_INTERVAL_SECONDS);
    appliedConfigurationVersion = preferences.getUInt("cfgVer", 0);
    preferences.end();

    if (reportIntervalSeconds == 0)
    {
        reportIntervalSeconds = DEFAULT_REPORT_INTERVAL_SECONDS;
    }

    deviceMode = deviceApiKey.isEmpty() ? DeviceMode::Discovery : DeviceMode::Operational;
}

bool savePersistedDeviceState()
{
    if (!preferences.begin("device", false))
    {
        logError("Could not open Preferences in write mode.");
        return false;
    }

    const bool intervalSaved = preferences.putUInt("reportSec", reportIntervalSeconds) > 0;
    const bool configurationVersionSaved = preferences.putUInt("cfgVer", appliedConfigurationVersion) > 0;
    const bool keySaved = deviceApiKey.isEmpty()
        ? (!preferences.isKey("apiKey") || preferences.remove("apiKey"))
        : preferences.putString("apiKey", deviceApiKey) > 0;

    preferences.end();

    return intervalSaved && configurationVersionSaved && keySaved;
}

void persistReportInterval(const uint32_t intervalSeconds)
{
    if (intervalSeconds == 0)
    {
        return;
    }

    reportIntervalSeconds = intervalSeconds;
    savePersistedDeviceState();
}

void storeApiKey(const String& apiKey)
{
    deviceApiKey = apiKey;
    deviceMode = deviceApiKey.isEmpty() ? DeviceMode::Discovery : DeviceMode::Operational;
    savePersistedDeviceState();
}

void clearStoredApiKey()
{
    deviceApiKey = "";
    deviceMode = DeviceMode::Discovery;
    lastDiscoveryAttemptMs = 0;
    lastUpdateAttemptMs = 0;
    savePersistedDeviceState();
}

void applyServerConfiguration(JsonVariantConst configuration)
{
    if (configuration.isNull())
    {
        return;
    }

    const uint32_t desiredConfigurationVersion = configuration["desiredConfigurationVersion"] | appliedConfigurationVersion;
    const uint32_t intervalSeconds = configuration["reportIntervalSeconds"] | reportIntervalSeconds;
    bool changed = false;

    if (intervalSeconds > 0 && intervalSeconds != reportIntervalSeconds)
    {
        reportIntervalSeconds = intervalSeconds;
        changed = true;
        logInfo("Applying new report interval: " + String(intervalSeconds) + " seconds");
    }

    if (desiredConfigurationVersion > 0 && desiredConfigurationVersion != appliedConfigurationVersion)
    {
        appliedConfigurationVersion = desiredConfigurationVersion;
        changed = true;
    }

    if (changed)
    {
        savePersistedDeviceState();
    }
}

void pulseModemPowerKey()
{
    logInfo("Pulsing modem power key...");
    pinMode(MODEM_PWRKEY_PIN, OUTPUT);
    digitalWrite(MODEM_PWRKEY_PIN, LOW);
    delay(100);
    digitalWrite(MODEM_PWRKEY_PIN, HIGH);
    delay(1000);
    digitalWrite(MODEM_PWRKEY_PIN, LOW);
    delay(3000);
}

bool initializeModemHardware()
{
    if (modemReady)
    {
        return true;
    }

    logInfo("Initializing SIM7000G modem...");

    pinMode(MODEM_DTR_PIN, OUTPUT);
    digitalWrite(MODEM_DTR_PIN, LOW);

    SerialAT.begin(MODEM_BAUD_RATE, SERIAL_8N1, MODEM_RX_PIN, MODEM_TX_PIN);
    delay(500);

    if (!modem.testAT(1000))
    {
        pulseModemPowerKey();
    }

    const uint32_t startedAt = millis();

    while (!modem.testAT(1000))
    {
        Serial.print(".");

        if (millis() - startedAt >= 30000)
        {
            Serial.println();
            logError("The SIM7000G modem did not respond.");
            return false;
        }

        delay(500);
    }

    Serial.println();
    logInfo("SIM7000G is responding.");

    modem.sendAT("E0");
    modem.waitResponse();
    modemReady = true;

    return true;
}

bool sendGpsAntennaPowerCommand(const char* command)
{
    modem.sendAT(command);
    return modem.waitResponse(10000L) == 1;
}

bool enableGps()
{
    if (gpsReady)
    {
        return true;
    }

    if (!initializeModemHardware())
    {
        return false;
    }

    logInfo("Enabling active GPS antenna power...");

    bool antennaPowered = sendGpsAntennaPowerCommand("+SGPIO=0,48,1,1");

    if (!antennaPowered)
    {
        logWarning("SGPIO failed; trying CGPIO...");
        antennaPowered = sendGpsAntennaPowerCommand("+CGPIO=0,48,1,1");
    }

    if (!antennaPowered)
    {
        logError("Could not enable GPS antenna power.");
        return false;
    }

    logInfo("Starting GNSS receiver...");

    if (!modem.enableGPS())
    {
        logError("Could not start GNSS receiver.");
        return false;
    }

    gpsReady = true;
    logInfo("GNSS receiver started.");

    return true;
}

void ensureGpsReady()
{
    if (gpsReady)
    {
        return;
    }

    const uint32_t now = millis();

    lastGpsInitAttemptMs = now;
    enableGps();
}

bool connectWifi()
{
    if (WiFi.status() == WL_CONNECTED)
    {
        activeTransport = NetworkTransport::Wifi;
        return true;
    }

    logInfo("Connecting to Wi-Fi: " + config.wifi.ssid);

    WiFi.mode(WIFI_STA);
    WiFi.setHostname(deviceId.c_str());
    WiFi.begin(config.wifi.ssid.c_str(), config.wifi.password.c_str());

    const uint32_t startedAt = millis();

    while (WiFi.status() != WL_CONNECTED)
    {
        if (millis() - startedAt >= config.wifi.connectionTimeoutMs)
        {
            Serial.println();
            logWarning("Wi-Fi connection failed.");
            return false;
        }

        Serial.print(".");
        delay(500);
    }

    Serial.println();
    logInfo("Wi-Fi connected.");
    logInfo("IP address: " + WiFi.localIP().toString());
    logInfo("Wi-Fi RSSI: " + String(WiFi.RSSI()) + " dBm");

    activeTransport = NetworkTransport::Wifi;
    return true;
}

bool connectCellular()
{
    if (!config.cellular.enabled)
    {
        logWarning("Cellular connection is disabled.");
        return false;
    }

    if (!initializeModemHardware())
    {
        return false;
    }

    logInfo("Initializing modem for cellular access...");

    if (!modem.init())
    {
        logError("Cellular modem initialization failed.");
        return false;
    }

    if (!config.cellular.simPin.isEmpty())
    {
        logInfo("Unlocking SIM card...");

        if (!modem.simUnlock(config.cellular.simPin.c_str()))
        {
            logError("SIM unlock failed.");
            return false;
        }
    }

    logInfo("Waiting for cellular network...");

    if (!modem.waitForNetwork(config.cellular.networkTimeoutMs))
    {
        logError("Cellular network registration failed.");
        return false;
    }

    logInfo("Connecting cellular data...");

    if (!modem.gprsConnect(
            config.cellular.apn.c_str(),
            config.cellular.username.c_str(),
            config.cellular.password.c_str()))
    {
        logError("Cellular data connection failed.");
        return false;
    }

    logInfo("Cellular data connected.");
    logInfo("Cellular IP address: " + modem.localIP());

    activeTransport = NetworkTransport::Cellular;
    return true;
}

bool networkIsConnected()
{
    switch (activeTransport)
    {
        case NetworkTransport::Wifi:
            return WiFi.status() == WL_CONNECTED;
        case NetworkTransport::Cellular:
            return modem.isGprsConnected();
        default:
            return false;
    }
}

bool connectNetwork()
{
    lastNetworkAttemptMs = millis();

    if (connectWifi())
    {
        return true;
    }

    if (config.cellular.enabled && connectCellular())
    {
        return true;
    }

    activeTransport = NetworkTransport::None;
    logWarning("No network connection is currently available.");
    return false;
}

void ensureNetworkConnection()
{
    if (networkIsConnected())
    {
        return;
    }

    const uint32_t now = millis();

    connectNetwork();
}

void appendWifiDiagnostics(JsonObject diagnostics)
{
    diagnostics["localIp"] = WiFi.localIP().toString();
    diagnostics["wifiRssiDbm"] = WiFi.RSSI();
    diagnostics["ssid"] = WiFi.SSID();
    diagnostics["bssid"] = WiFi.BSSIDstr();
    diagnostics["channel"] = WiFi.channel();
    diagnostics["gatewayIp"] = WiFi.gatewayIP().toString();
    diagnostics["subnetMask"] = WiFi.subnetMask().toString();
    diagnostics["dnsIp"] = WiFi.dnsIP().toString();
    diagnostics["macAddress"] = WiFi.macAddress();
}

void appendCellularDiagnostics(JsonObject diagnostics)
{
    diagnostics["localIp"] = modem.localIP();
    diagnostics["simStatus"] = String(modem.getSimStatus());
    diagnostics["networkConnected"] = modem.isNetworkConnected();
    diagnostics["gprsConnected"] = modem.isGprsConnected();
    diagnostics["operator"] = modem.getOperator();
    diagnostics["signalQuality"] = modem.getSignalQuality();
}

String buildDiscoveryPayload()
{
    JsonDocument document;
    document["deviceId"] = deviceId;
    document["firmwareVersion"] = config.tracker.firmwareVersion;
    document["networkTransport"] = activeTransportName();

    String payload;
    serializeJson(document, payload);
    return payload;
}

String buildUpdatePayload(const uint32_t now)
{
    JsonDocument document;
    document["firmwareVersion"] = config.tracker.firmwareVersion;
    document["temperature"] = buildDummyTemperatureCelsius(now);

    JsonObject runtimeConfiguration = document["runtimeConfiguration"].to<JsonObject>();
    runtimeConfiguration["appliedConfigurationVersion"] = appliedConfigurationVersion;
    runtimeConfiguration["appliedReportIntervalSeconds"] = reportIntervalSeconds;

    if (latestFix.valid)
    {
        JsonObject position = document["position"].to<JsonObject>();
        position["latitude"] = latestFix.latitude;
        position["longitude"] = latestFix.longitude;
        position["altitudeMeters"] = latestFix.altitudeMeters;
        position["speedKnots"] = latestFix.speedKnots;
        position["hdop"] = latestFix.hdop;
        position["satellitesVisible"] = latestFix.satellitesVisible;
        position["satellitesUsed"] = latestFix.satellitesUsed;

        const String gpsTimeUtc = formatGpsUtcTime(latestFix);
        if (!gpsTimeUtc.isEmpty())
        {
            position["gpsTimeUtc"] = gpsTimeUtc;
        }
    }

    JsonObject battery = document["battery"].to<JsonObject>();
    battery["modemReadingValid"] = latestBatteryStatus.modemReadingValid;
    battery["chargeState"] = latestBatteryStatus.chargeState;
    battery["batteryState"] = static_cast<int8_t>(latestBatteryStatus.batteryState);
    battery["percentage"] = latestBatteryStatus.percentage;
    battery["modemMillivolts"] = latestBatteryStatus.modemMillivolts;
    battery["adcVoltage"] = latestBatteryStatus.adcVoltage;

    JsonObject network = document["network"].to<JsonObject>();
    network["transport"] = activeTransportName();

    if (activeTransport == NetworkTransport::Wifi)
    {
        appendWifiDiagnostics(network["wifi"].to<JsonObject>());
    }
    else if (activeTransport == NetworkTransport::Cellular)
    {
        appendCellularDiagnostics(network["cellular"].to<JsonObject>());
    }

    if (pendingLogEntryCount > 0)
    {
        JsonArray logs = document["logs"].to<JsonArray>();

        for (size_t index = 0; index < pendingLogEntryCount && index < MAX_LOGS_PER_UPDATE; ++index)
        {
            const PendingDeviceLogEntry& entry = pendingLogEntries[index];
            JsonObject log = logs.add<JsonObject>();
            log["sequenceNumber"] = entry.sequenceNumber;
            log["message"] = entry.message;
            log["level"] = entry.level;
            log["deviceUptimeMs"] = entry.deviceUptimeMs;
        }
    }

    String payload;
    serializeJson(document, payload);
    return payload;
}

HttpResponse sendJsonPost(const String& path, const String& payload, const String& apiKey = "")
{
    HttpResponse response;

    if (!networkIsConnected())
    {
        logWarning("Skipping HTTP request because no network is connected.");
        return response;
    }

    if (activeTransport == NetworkTransport::Wifi)
    {
        // Discard any stale socket state from the previous request.
        wifiClient.stop();

        HttpClient client(
            wifiClient,
            config.backend.host.c_str(),
            config.backend.port);

        client.setHttpResponseTimeout(config.backend.requestTimeoutMs);

        logInfo(
            "POST http://"
            + config.backend.host
            + ":"
            + String(config.backend.port)
            + path);

        client.beginRequest();

        const int requestResult = client.post(path.c_str());

        if (requestResult != 0)
        {
            logError(
                "HTTP connection/request initialization failed. Error: "
                + String(requestResult));

            client.stop();
            wifiClient.stop();
            return response;
        }

        client.sendHeader("Content-Type", "application/json");
        client.sendHeader("Content-Length", payload.length());
        client.sendHeader("Connection", "close");

        if (!apiKey.isEmpty())
        {
            client.sendHeader("X-Api-Key", apiKey);
        }

        client.beginBody();

        const size_t bytesWritten = client.print(payload);
        client.endRequest();

        if (bytesWritten != payload.length())
        {
            logError(
                "HTTP body write incomplete. Wrote "
                + String(bytesWritten)
                + " of "
                + String(payload.length())
                + " bytes.");

            client.stop();
            wifiClient.stop();
            return response;
        }

        response.statusCode = client.responseStatusCode();

        if (response.statusCode < 0)
        {
            logError(
                "No valid HTTP response received. Error: "
                + String(response.statusCode));

            client.stop();
            wifiClient.stop();
            return response;
        }

        response.body = client.responseBody();
        response.transportOk = true;

        client.stop();
        wifiClient.stop();
        return response;
    }

    if (activeTransport == NetworkTransport::Cellular)
    {
        HttpClient client(cellularClient, config.backend.host.c_str(), config.backend.port);
        client.beginRequest();
        client.post(path.c_str());
        client.sendHeader("Content-Type", "application/json");
        client.sendHeader("Content-Length", payload.length());
        client.sendHeader("Connection", "close");

        if (!apiKey.isEmpty())
        {
            client.sendHeader("X-Api-Key", apiKey);
        }

        client.beginBody();
        client.print(payload);
        client.endRequest();

        response.statusCode = client.responseStatusCode();
        response.body = client.responseBody();
        response.transportOk = true;
        client.stop();
        return response;
    }

    return response;
}

void handleDiscoveryResponse(const HttpResponse& response)
{
    if (!response.transportOk)
    {
        return;
    }

    logInfo("Discovery status: " + String(response.statusCode));

    if (response.statusCode != 200)
    {
        logWarning("Discovery did not return a usable response.");
        return;
    }

    JsonDocument document;
    const auto error = deserializeJson(document, response.body);

    if (error)
    {
        logError("Discovery JSON parse failed: " + String(error.c_str()));
        return;
    }

    applyServerConfiguration(document["configuration"]);

    const String apiKey = document["apiKey"] | "";
    if (!apiKey.isEmpty())
    {
        logInfo("Received device API key from discovery response.");
        storeApiKey(apiKey);
    }
}

void handleUpdateResponse(const HttpResponse& response)
{
    if (!response.transportOk)
    {
        return;
    }

    logInfo("Update status: " + String(response.statusCode));

    if (response.statusCode == 401)
    {
        logWarning("Device credential was rejected. Clearing API key and returning to discovery mode.");
        clearStoredApiKey();
        return;
    }

    if (response.statusCode != 200)
    {
        logWarning("Update request failed without changing provisioning state.");
        return;
    }

    JsonDocument document;
    const auto error = deserializeJson(document, response.body);

    if (error)
    {
        logError("Update JSON parse failed: " + String(error.c_str()));
        return;
    }

    applyServerConfiguration(document["configuration"]);

    if (!document["highestAcknowledgedLogSequenceNumber"].isNull())
    {
        acknowledgePendingLogs(document["highestAcknowledgedLogSequenceNumber"] | 0U);
    }
}

void runDiscoveryMode(const uint32_t now)
{
    lastDiscoveryAttemptMs = now;

    const HttpResponse response = sendJsonPost(
        buildApiPath("/api/devices/discover"),
        buildDiscoveryPayload());

    handleDiscoveryResponse(response);
}

void runOperationalMode(const uint32_t now)
{
    const uint32_t reportIntervalMs = reportIntervalSeconds * 1000UL;

    lastUpdateAttemptMs = now;

    const String endpoint = buildApiPath(String("/api/devices/") + deviceId + "/updates");
    const HttpResponse response = sendJsonPost(endpoint, buildUpdatePayload(now), deviceApiKey);

    handleUpdateResponse(response);
}

void pollGps()
{
    if (!gpsReady)
    {
        return;
    }

    const uint32_t now = millis();

    lastGpsPollMs = now;

    GpsFix newFix;

    newFix.valid = modem.getGPS(
        &newFix.latitude,
        &newFix.longitude,
        &newFix.speedKnots,
        &newFix.altitudeMeters,
        &newFix.satellitesVisible,
        &newFix.satellitesUsed,
        &newFix.hdop,
        &newFix.year,
        &newFix.month,
        &newFix.day,
        &newFix.hour,
        &newFix.minute,
        &newFix.second);

    if (!newFix.valid)
    {
        if (intervalElapsed(now, lastNoFixLogMs, config.tracker.noFixLogIntervalMs))
        {
            lastNoFixLogMs = now;
            logWarning("Waiting for a valid GPS fix. Test outdoors with a clear sky.");
        }

        return;
    }

    newFix.receivedAtMs = now;
    latestFix = newFix;

    logInfo(
        "GPS fix: "
        + String(latestFix.latitude, 6)
        + ", "
        + String(latestFix.longitude, 6)
        + " | satellites used: "
        + String(latestFix.satellitesUsed));
}

float readBatteryVoltage()
{
    constexpr int sampleCount = 20;

    uint32_t totalMillivolts = 0;

    analogSetPinAttenuation(BATTERY_ADC_PIN, ADC_11db);

    for (int i = 0; i < sampleCount; ++i) {
        totalMillivolts += analogReadMilliVolts(BATTERY_ADC_PIN);
        delay(5);
    }

    const float adcMillivolts =
        static_cast<float>(totalMillivolts) / sampleCount;

    // The board uses approximately a 1:1 voltage divider.
    return (adcMillivolts * 2.0F) / 1000.0F;
}

void readBatteryStatus()
{
    BatteryStatus status{
        .modemReadingValid = false,
        .chargeState = -1,
        .percentage = -1,
        .modemMillivolts = 0,
        .adcVoltage = readBatteryVoltage()
    };

    status.modemReadingValid = modem.getBattStats(
        status.chargeState,
        status.percentage,
        status.modemMillivolts
    );

    status.batteryState = static_cast<BatteryState>(status.chargeState);

    logInfo(
        "Battery status: "
        + String(status.adcVoltage, 2)
        + " V (ADC), "
        + String(status.modemMillivolts / 1000.0F, 2)
        + " V (modem), "
        + String(status.percentage)
        + "%, "
        + (status.batteryState == BatteryState::Charging ? "charging" :
            status.batteryState == BatteryState::Full ? "full" :
            status.batteryState == BatteryState::NotCharging ? "not charging" : "unknown"));

    latestBatteryStatus = status;
}

void setup()
{
    Serial.begin(115200);
    delay(1000);

    Serial.println();
    logInfo("LILYGO T-SIM7000G Device Onboarding Client");
    logInfo("==========================================");

    deviceId = createDeviceId();

    logInfo("Device ID: " + deviceId);

    loadPersistedDeviceState();

    logInfo("Loaded device mode: " + String(deviceModeName()));
    logInfo("Report interval: " + String(reportIntervalSeconds) + " seconds");
    logInfo("Applied configuration version: " + String(appliedConfigurationVersion));

    connectNetwork();
    ensureGpsReady();
}

void loop()
{
    readBatteryStatus();
    ensureNetworkConnection();
    ensureGpsReady();
    pollGps();

    const uint32_t now = millis();

    if (!networkIsConnected())
    {
        delay(10);
        return;
    }

    if (deviceMode == DeviceMode::Discovery)
    {
        runDiscoveryMode(now);
    }
    else
    {
        runOperationalMode(now);
    }

    const uint32_t reportIntervalMs = reportIntervalSeconds * 1000UL;

    delay(reportIntervalMs);
}