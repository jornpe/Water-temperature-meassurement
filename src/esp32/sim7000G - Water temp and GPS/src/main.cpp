#include <Arduino.h>
#include <ArduinoHttpClient.h>
#include <ArduinoJson.h>
#include <DallasTemperature.h>
#include <OneWire.h>
#include <Preferences.h>
#include <TinyGsmClient.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <esp_sleep.h>

// ============================================================================
// LILYGO T-SIM7000G hardware
// ============================================================================

constexpr int MODEM_RX_PIN = 26;
constexpr int MODEM_TX_PIN = 27;
constexpr int MODEM_PWRKEY_PIN = 4;
constexpr int MODEM_DTR_PIN = 25;
constexpr int BATTERY_ADC_PIN = 35;
constexpr int TEMPERATURE_DATA_PIN = 21;

constexpr uint32_t MODEM_BAUD_RATE = 115200;
constexpr uint8_t SIM7000_NETWORK_MODE_LTE_ONLY = 38;
constexpr uint8_t SIM7000_PREFERRED_MODE_CAT_M = 1;
constexpr uint32_t DEFAULT_REPORT_INTERVAL_SECONDS = 30;
constexpr uint32_t DISCOVERY_INTERVAL_MS = 10000;
constexpr uint32_t POST_DISCOVERY_SLEEP_SECONDS = 10;
constexpr uint8_t MAX_CONFIGURATION_SYNC_ATTEMPTS = 3;
constexpr float BATTERY_EMPTY_VOLTAGE = 2.5687F;
constexpr float BATTERY_FULL_VOLTAGE = 3.5627F;
constexpr size_t MAX_LOGS_PER_UPDATE = 1000;
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
        true,
        "internet",
        "",
        "",
        "5174",
        120000,
    },
    {
        "watersensors.jphomelab.no",
        443,
        "",
        60000,
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
TinyGsmClientSecure cellularClient(modem);
WiFiClientSecure wifiClient;
Preferences preferences;
OneWire temperatureOneWire(TEMPERATURE_DATA_PIN);
DallasTemperature temperatureSensors(&temperatureOneWire);

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
bool simUnlockAttempted = false;

uint32_t lastNetworkAttemptMs = 0;
uint32_t lastGpsInitAttemptMs = 0;
uint32_t lastGpsPollMs = 0;
uint32_t lastNoFixLogMs = 0;
uint32_t lastDiscoveryAttemptMs = 0;
uint32_t lastUpdateAttemptMs = 0;

struct PendingDeviceLogEntry
{
    uint32_t timestampMs = 0;
    String level;
    String message;
};

PendingDeviceLogEntry pendingLogEntries[MAX_LOGS_PER_UPDATE];
size_t pendingLogEntryCount = 0;
size_t pendingUploadLogEntryCount = 0;

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

struct TemperatureStatus
{
    bool valid = false;
    float celsius = 0;
};

TemperatureStatus latestTemperatureStatus;

struct HttpResponse
{
    int statusCode = -1;
    String body;
    bool transportOk = false;
};

bool updateConfig();

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

String truncateLogMessage(const String& message)
{
    if (message.length() <= MAX_LOG_MESSAGE_LENGTH)
    {
        return message;
    }

    return message.substring(0, MAX_LOG_MESSAGE_LENGTH - 3) + "...";
}

void appendPendingDeviceLog(const String& level, const String& message)
{
    if (message.isEmpty())
    {
        return;
    }

    if (pendingLogEntryCount == MAX_LOGS_PER_UPDATE)
    {
        for (size_t index = 1; index < pendingLogEntryCount; ++index)
        {
            pendingLogEntries[index - 1] = pendingLogEntries[index];
        }

        pendingLogEntryCount -= 1;
        Serial.println("Device log queue full; dropped its oldest entry.");
    }

    PendingDeviceLogEntry& entry = pendingLogEntries[pendingLogEntryCount++];
    entry.timestampMs = millis();
    entry.level = level;
    entry.message = truncateLogMessage(message);
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

void clearUploadedLogs()
{
    if (pendingUploadLogEntryCount == 0)
    {
        return;
    }

    const size_t remainingEntryCount = pendingLogEntryCount > pendingUploadLogEntryCount
        ? pendingLogEntryCount - pendingUploadLogEntryCount
        : 0;

    for (size_t index = 0; index < remainingEntryCount; ++index)
    {
        pendingLogEntries[index] = pendingLogEntries[index + pendingUploadLogEntryCount];
    }

    pendingLogEntryCount = remainingEntryCount;
    pendingUploadLogEntryCount = 0;
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

    logInfo("Loaded persisted device state: reportIntervalSeconds="
        + String(reportIntervalSeconds)
        + ", appliedConfigurationVersion="
        + String(appliedConfigurationVersion)
        + ", apiKey="
        + (deviceApiKey.isEmpty() ? "<empty>" : "<present>"));

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

bool readPersistedConfiguration(uint32_t& configurationVersion, uint32_t& intervalSeconds)
{
    if (!preferences.begin("device", true))
    {
        logError("Could not open Preferences to verify the stored configuration.");
        return false;
    }

    configurationVersion = preferences.getUInt("cfgVer", 0);
    intervalSeconds = preferences.getUInt("reportSec", 0);
    preferences.end();

    return intervalSeconds > 0;
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

    logInfo("Modem: " + modem.getModemInfo());

    const SimStatus initialSimStatus = modem.getSimStatus();

    if (initialSimStatus == SIM_LOCKED)
    {
        if (config.cellular.simPin.isEmpty())
        {
            logError("The SIM card requires a PIN, but no SIM PIN is configured.");
            return false;
        }

        if (simUnlockAttempted)
        {
            logError("The SIM is still locked. PIN unlock will not be retried until reboot.");
            return false;
        }

        simUnlockAttempted = true;
        logInfo("Unlocking SIM card...");

        if (!modem.simUnlock(config.cellular.simPin.c_str()))
        {
            logError("SIM unlock failed. Reboot before trying another PIN to avoid blocking the SIM.");
            return false;
        }

        delay(1000);

        if (modem.getSimStatus() != SIM_READY)
        {
            logError("The SIM did not become ready after PIN unlock.");
            return false;
        }

        logInfo("SIM card unlocked.");
    }
    else if (initialSimStatus != SIM_READY)
    {
        logError("SIM card is not inserted or not ready.");
        return false;
    }
    else
    {
        logInfo("SIM card is ready.");
    }

    logInfo("Selecting LTE Cat-M network mode...");

    if (!modem.setNetworkMode(SIM7000_NETWORK_MODE_LTE_ONLY))
    {
        logWarning("Could not force LTE-only mode; continuing with the modem's current mode.");
    }

    if (!modem.setPreferredMode(SIM7000_PREFERRED_MODE_CAT_M))
    {
        logWarning("Could not prefer LTE Cat-M; continuing with the modem's current preference.");
    }

    logInfo("Waiting for cellular network...");

    if (!modem.waitForNetwork(config.cellular.networkTimeoutMs))
    {
        logError(
            "Cellular network registration failed. Signal quality: "
            + String(modem.getSignalQuality()));
        return false;
    }

    logInfo("Registered on operator: " + modem.getOperator());
    logInfo("Cellular signal quality: " + String(modem.getSignalQuality()));

    if (!modem.isGprsConnected())
    {
        logInfo("Connecting cellular data using APN: " + config.cellular.apn);

        const bool connected = config.cellular.username.isEmpty()
                && config.cellular.password.isEmpty()
            ? modem.gprsConnect(config.cellular.apn.c_str())
            : modem.gprsConnect(
                config.cellular.apn.c_str(),
                config.cellular.username.c_str(),
                config.cellular.password.c_str());

        if (!connected || !modem.isGprsConnected())
        {
            logError("Cellular data connection failed.");
            return false;
        }
    }

    logInfo("Cellular data connected.");
    logInfo("Cellular IP address: " + modem.getLocalIP());

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

    const bool connected = connectWifi()
        || (config.cellular.enabled && connectCellular());

    if (!connected)
    {
        activeTransport = NetworkTransport::None;
        logWarning("No network connection is currently available.");
        return false;
    }

    // Synchronize and confirm configuration before sending telemetry.
    updateConfig();

    return true;
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
    pendingUploadLogEntryCount = pendingLogEntryCount;

    JsonDocument document;
    document["firmwareVersion"] = config.tracker.firmwareVersion;
    document["deviceUptimeMs"] = now;

    if (latestTemperatureStatus.valid)
    {
        document["temperature"] = latestTemperatureStatus.celsius;
    }

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

    if (pendingUploadLogEntryCount > 0)
    {
        JsonArray logs = document["logs"].to<JsonArray>();

        for (size_t index = 0; index < pendingUploadLogEntryCount; ++index)
        {
            const PendingDeviceLogEntry& entry = pendingLogEntries[index];
            JsonObject log = logs.add<JsonObject>();
            log["timestampMs"] = entry.timestampMs;
            log["message"] = entry.message;
            log["level"] = entry.level;
        }
    }

    String payload;
    serializeJson(document, payload);
    return payload;
}

String buildConfigurationSyncPayload(
    const uint32_t configurationVersion,
    const uint32_t intervalSeconds,
    const uint8_t syncAttempt,
    const bool isConfirmation,
    const bool storageVerified)
{
    JsonDocument document;
    document["appliedConfigurationVersion"] = configurationVersion;
    document["appliedReportIntervalSeconds"] = intervalSeconds;
    document["syncAttempt"] = syncAttempt;
    document["isConfirmation"] = isConfirmation;
    document["storageVerified"] = storageVerified;

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

    Client* networkClient = nullptr;

    switch (activeTransport)
    {
        case NetworkTransport::Wifi:
            networkClient = &wifiClient;
            break;
        case NetworkTransport::Cellular:
            networkClient = &cellularClient;
            break;
        default:
            return response;
    }

    // Discard any stale socket state from the previous request.
    networkClient->stop();

    HttpClient client(*networkClient, config.backend.host.c_str(), config.backend.port);
    client.setHttpResponseTimeout(config.backend.requestTimeoutMs);

    Serial.println(
        "POST https://"
        + config.backend.host
        + ":"
        + String(config.backend.port)
        + path);

    client.beginRequest();

    const int requestResult = client.post(path.c_str());

    if (requestResult != 0)
    {
        logError(
            "HTTP connection/request initialization failed over "
            + String(activeTransportName())
            + ". Error: "
            + String(requestResult));

        client.stop();
        networkClient->stop();
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
            "HTTP body write incomplete over "
            + String(activeTransportName())
            + ". Wrote "
            + String(bytesWritten)
            + " of "
            + String(payload.length())
            + " bytes.");

        client.stop();
        networkClient->stop();
        return response;
    }

    response.statusCode = client.responseStatusCode();

    if (response.statusCode < 0)
    {
        logError(
            "No valid HTTP response received over "
            + String(activeTransportName())
            + ". Error: "
            + String(response.statusCode));

        client.stop();
        networkClient->stop();
        return response;
    }

    response.body = client.responseBody();
    response.transportOk = true;

    client.stop();
    networkClient->stop();
    return response;
}

bool updateConfig()
{
    if (deviceMode != DeviceMode::Operational || deviceApiKey.isEmpty())
    {
        logInfo("Skipping configuration check until the device has an API key.");
        return false;
    }

    if (!networkIsConnected())
    {
        logWarning("Skipping configuration check because no network is connected.");
        return false;
    }

    const String endpoint = buildApiPath(String("/api/devices/") + deviceId + "/configuration");

    auto synchronizeWithBackend = [&endpoint](
        const uint32_t storedConfigurationVersion,
        const uint32_t storedIntervalSeconds,
        const uint8_t syncAttempt,
        const bool isConfirmation,
        const bool storageVerified,
        uint32_t& desiredConfigurationVersion,
        uint32_t& desiredReportIntervalSeconds,
        bool& backendConfirmed) -> bool
    {
        const HttpResponse response = sendJsonPost(
            endpoint,
            buildConfigurationSyncPayload(
                storedConfigurationVersion,
                storedIntervalSeconds,
                syncAttempt,
                isConfirmation,
                storageVerified),
            deviceApiKey);

        if (!response.transportOk)
        {
            logWarning("Configuration sync failed without a valid HTTP response.");
            return false;
        }

        logInfo("Configuration sync status: " + String(response.statusCode));

        if (response.statusCode == 401)
        {
            logWarning("Device credential was rejected. Clearing API key and returning to discovery mode.");
            clearStoredApiKey();
            return false;
        }

        if (response.statusCode != 200)
        {
            logWarning("Configuration sync did not return a usable response.");
            return false;
        }

        JsonDocument document;
        const auto error = deserializeJson(document, response.body);

        if (error)
        {
            logError("Configuration JSON parse failed: " + String(error.c_str()));
            return false;
        }

        const String responseDeviceId = document["deviceId"] | "";
        if (responseDeviceId != deviceId)
        {
            logError("Configuration response device ID does not match this device.");
            return false;
        }

        const JsonVariantConst configuration = document["configuration"];
        if (!configuration.is<JsonObjectConst>())
        {
            logError("Configuration response does not contain a configuration object.");
            return false;
        }

        const JsonVariantConst versionValue = configuration["desiredConfigurationVersion"];
        const JsonVariantConst intervalValue = configuration["reportIntervalSeconds"];
        const JsonVariantConst confirmedValue = document["isSynchronized"];
        const JsonVariantConst maximumAttemptsValue = document["maximumAttempts"];

        if (!versionValue.is<uint32_t>()
            || !intervalValue.is<uint32_t>()
            || !confirmedValue.is<bool>()
            || !maximumAttemptsValue.is<uint8_t>())
        {
            logError("Configuration response contains invalid synchronization values.");
            return false;
        }

        desiredConfigurationVersion = versionValue.as<uint32_t>();
        desiredReportIntervalSeconds = intervalValue.as<uint32_t>();
        backendConfirmed = confirmedValue.as<bool>();

        if (desiredConfigurationVersion == 0 || desiredReportIntervalSeconds == 0)
        {
            logError("Configuration version and report interval must be greater than zero.");
            return false;
        }

        if (maximumAttemptsValue.as<uint8_t>() != MAX_CONFIGURATION_SYNC_ATTEMPTS)
        {
            logError("Backend and firmware configuration retry limits do not match.");
            return false;
        }

        const bool expectedBackendConfirmation = storageVerified
            && storedConfigurationVersion == desiredConfigurationVersion
            && storedIntervalSeconds == desiredReportIntervalSeconds;

        if (backendConfirmed != expectedBackendConfirmation)
        {
            logError("Backend configuration evaluation does not match the reported stored values.");
            return false;
        }

        return true;
    };

    for (uint8_t attempt = 1; attempt <= MAX_CONFIGURATION_SYNC_ATTEMPTS; ++attempt)
    {
        logInfo(
            "Configuration application attempt "
            + String(attempt)
            + " of "
            + String(MAX_CONFIGURATION_SYNC_ATTEMPTS)
            + ".");

        uint32_t storedConfigurationVersion = appliedConfigurationVersion;
        uint32_t storedReportIntervalSeconds = reportIntervalSeconds;
        const bool storedConfigurationRead = readPersistedConfiguration(
            storedConfigurationVersion,
            storedReportIntervalSeconds);
        const bool currentStorageVerified = storedConfigurationRead
            && storedConfigurationVersion == appliedConfigurationVersion
            && storedReportIntervalSeconds == reportIntervalSeconds;

        if (!currentStorageVerified)
        {
            logWarning("Stored configuration does not match the in-memory configuration.");
        }

        uint32_t desiredConfigurationVersion = appliedConfigurationVersion;
        uint32_t desiredReportIntervalSeconds = reportIntervalSeconds;
        bool backendConfirmed = false;

        if (!synchronizeWithBackend(
                storedConfigurationVersion,
                storedReportIntervalSeconds,
                attempt,
                false,
                currentStorageVerified,
                desiredConfigurationVersion,
                desiredReportIntervalSeconds,
                backendConfirmed))
        {
            return false;
        }

        if (backendConfirmed)
        {
            logInfo("Stored configuration is verified and confirmed by the backend.");
            return true;
        }

        const uint32_t previousConfigurationVersion = appliedConfigurationVersion;
        const uint32_t previousReportIntervalSeconds = reportIntervalSeconds;

        appliedConfigurationVersion = desiredConfigurationVersion;
        reportIntervalSeconds = desiredReportIntervalSeconds;

        const bool configurationStored = savePersistedDeviceState();
        uint32_t confirmedStoredVersion = 0;
        uint32_t confirmedStoredIntervalSeconds = 0;
        const bool confirmationRead = readPersistedConfiguration(
            confirmedStoredVersion,
            confirmedStoredIntervalSeconds);
        bool localConfigurationVerified = configurationStored
            && confirmationRead
            && confirmedStoredVersion == appliedConfigurationVersion
            && confirmedStoredIntervalSeconds == reportIntervalSeconds
            && appliedConfigurationVersion == desiredConfigurationVersion
            && reportIntervalSeconds == desiredReportIntervalSeconds;

        if (!localConfigurationVerified)
        {
            appliedConfigurationVersion = previousConfigurationVersion;
            reportIntervalSeconds = previousReportIntervalSeconds;

            const bool previousConfigurationRestored = savePersistedDeviceState();
            const bool restoredConfigurationRead = readPersistedConfiguration(
                confirmedStoredVersion,
                confirmedStoredIntervalSeconds);

            localConfigurationVerified = previousConfigurationRestored
                && restoredConfigurationRead
                && confirmedStoredVersion == appliedConfigurationVersion
                && confirmedStoredIntervalSeconds == reportIntervalSeconds;

            if (!localConfigurationVerified)
            {
                logError("Could not restore the previous persisted configuration.");
            }

            logError("Desired configuration could not be stored and verified.");
        }

        uint32_t confirmationDesiredVersion = desiredConfigurationVersion;
        uint32_t confirmationDesiredIntervalSeconds = desiredReportIntervalSeconds;

        if (!synchronizeWithBackend(
                confirmedStoredVersion,
                confirmedStoredIntervalSeconds,
                attempt,
                true,
                localConfigurationVerified,
                confirmationDesiredVersion,
                confirmationDesiredIntervalSeconds,
                backendConfirmed))
        {
            return false;
        }

        const bool confirmedValuesMatchMemory = localConfigurationVerified
            && confirmedStoredVersion == appliedConfigurationVersion
            && confirmedStoredIntervalSeconds == reportIntervalSeconds;

        if (backendConfirmed && confirmedValuesMatchMemory)
        {
            logInfo(
                "Configuration version "
                + String(appliedConfigurationVersion)
                + " with report interval "
                + String(reportIntervalSeconds)
                + " seconds is stored, verified, and confirmed by the backend.");
            return true;
        }

        logWarning(
            "Configuration attempt "
            + String(attempt)
            + " was not confirmed; retrying if attempts remain.");
    }

    logError("Configuration sync failed after three attempts; the backend will show the failure.");
    return false;
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

    const String apiKey = document["apiKey"] | "";
    if (!apiKey.isEmpty())
    {
        logInfo("Received device API key from discovery response.");
        storeApiKey(apiKey);
        updateConfig();
    }
}

void handleUpdateResponse(const HttpResponse& response)
{
    if (!response.transportOk)
    {
        return;
    }

    if (response.statusCode == 200)
    {
        clearUploadedLogs();
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

int8_t calculateBatteryPercentage(const float voltage)
{
    if (voltage <= BATTERY_EMPTY_VOLTAGE)
    {
        return 0;
    }

    if (voltage >= BATTERY_FULL_VOLTAGE)
    {
        return 100;
    }

    const float percentage =
        ((voltage - BATTERY_EMPTY_VOLTAGE)
            / (BATTERY_FULL_VOLTAGE - BATTERY_EMPTY_VOLTAGE))
        * 100.0F;

    return static_cast<int8_t>(roundf(percentage));
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

    status.percentage = calculateBatteryPercentage(status.adcVoltage);

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

void initializeTemperatureSensor()
{
    temperatureSensors.begin();

    const uint8_t sensorCount = temperatureSensors.getDeviceCount();

    if (sensorCount == 0)
    {
        logError(
            "No DS18B20 temperature sensor found on GPIO "
            + String(TEMPERATURE_DATA_PIN)
            + ". Check the wiring and 4.7 kOhm DATA-to-3V3 pull-up resistor.");
        return;
    }

    logInfo(
        "DS18B20 temperature sensor initialized on GPIO "
        + String(TEMPERATURE_DATA_PIN)
        + ". Sensors found: "
        + String(sensorCount));
}

void readTemperature()
{
    TemperatureStatus status;
    DeviceAddress sensorAddress;

    if (!temperatureSensors.getAddress(sensorAddress, 0))
    {
        temperatureSensors.begin();

        if (!temperatureSensors.getAddress(sensorAddress, 0))
        {
            logError(
                "Cannot read temperature: no DS18B20 found on GPIO "
                + String(TEMPERATURE_DATA_PIN)
                + ".");
            latestTemperatureStatus = status;
            return;
        }
    }

    if (!temperatureSensors.isConnected(sensorAddress))
    {
        logError("Cannot read temperature: the DS18B20 is not responding.");
        temperatureSensors.begin();
        latestTemperatureStatus = status;
        return;
    }

    temperatureSensors.requestTemperatures();

    const float temperatureCelsius = temperatureSensors.getTempC(sensorAddress);

    if (temperatureCelsius == DEVICE_DISCONNECTED_C)
    {
        logError("Cannot read temperature: the DS18B20 disconnected during the reading.");
        latestTemperatureStatus = status;
        return;
    }

    if (isnan(temperatureCelsius) || temperatureCelsius < -55.0F || temperatureCelsius > 125.0F)
    {
        logError(
            "Cannot read temperature: invalid DS18B20 value "
            + String(temperatureCelsius, 2)
            + " C.");
        latestTemperatureStatus = status;
        return;
    }

    status.valid = true;
    status.celsius = temperatureCelsius;
    latestTemperatureStatus = status;

    logInfo("Temperature: " + String(temperatureCelsius, 2) + " C");
}

[[noreturn]] void enterDeepSleep(uint32_t sleepSeconds)
{
    if (sleepSeconds == 0)
    {
        sleepSeconds = DEFAULT_REPORT_INTERVAL_SECONDS;
    }

    logInfo("Entering deep sleep for " + String(sleepSeconds) + " seconds.");

    cellularClient.stop();
    wifiClient.stop();

    if (gpsReady)
    {
        modem.disableGPS();
        gpsReady = false;
    }

    if (activeTransport == NetworkTransport::Cellular && modem.isGprsConnected())
    {
        modem.gprsDisconnect();
    }

    if (modemReady)
    {
        modem.poweroff();
        modemReady = false;
    }

    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);

    esp_sleep_enable_timer_wakeup(
        static_cast<uint64_t>(sleepSeconds) * 1000000ULL);

    Serial.flush();
    esp_deep_sleep_start();
}

void setup()
{
    Serial.begin(115200);

    // Read the battery before initializing or powering up other peripherals.
    readBatteryStatus();
    delay(1000);

    Serial.println();
    // TODO: Install the backend CA certificate for production verification.
    wifiClient.setInsecure();

    logInfo("LILYGO T-SIM7000G Device Onboarding Client");
    logInfo("==========================================");

    deviceId = createDeviceId();

    logInfo("Device ID: " + deviceId);

    loadPersistedDeviceState();

    logInfo("Loaded device mode: " + String(deviceModeName()));
    logInfo("Report interval: " + String(reportIntervalSeconds) + " seconds");
    logInfo("Applied configuration version: " + String(appliedConfigurationVersion));

    initializeTemperatureSensor();
    readTemperature();

    connectNetwork();

    ensureGpsReady();
    pollGps();

    if (deviceMode == DeviceMode::Discovery)
    {
        logInfo("Running in discovery mode.");
        return;
    }

    logInfo("Running in operational mode.");
    runOperationalMode(millis());

    if (deviceMode == DeviceMode::Operational)
    {
        enterDeepSleep(reportIntervalSeconds);
    }

    logInfo("Returning to discovery mode.");
}

void loop()
{
    const uint32_t now = millis();

    if (deviceMode != DeviceMode::Discovery)
    {
        enterDeepSleep(reportIntervalSeconds);
    }

    if (!intervalElapsed(now, lastDiscoveryAttemptMs, DISCOVERY_INTERVAL_MS))
    {
        delay(10);
        return;
    }

    lastDiscoveryAttemptMs = now;
    ensureNetworkConnection();

    if (!networkIsConnected())
    {
        return;
    }

    logInfo("Requesting an API key in discovery mode.");
    runDiscoveryMode(millis());

    if (deviceMode != DeviceMode::Operational)
    {
        return;
    }

    logInfo("API key received; sending the latest readings and logs.");
    runOperationalMode(millis());

    if (deviceMode == DeviceMode::Operational)
    {
        enterDeepSleep(POST_DISCOVERY_SLEEP_SECONDS);
    }
}
