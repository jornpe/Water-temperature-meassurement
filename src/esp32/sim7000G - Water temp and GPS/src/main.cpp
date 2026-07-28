#include <Arduino.h>
#include <ArduinoHttpClient.h>
#include <ArduinoJson.h>
#include <LittleFS.h>
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
constexpr uint8_t SIM7000_NETWORK_MODE_LTE_ONLY = 38;
constexpr uint8_t SIM7000_PREFERRED_MODE_CAT_M = 1;
constexpr uint32_t DEFAULT_REPORT_INTERVAL_SECONDS = 30;
constexpr uint32_t DISCOVERY_INTERVAL_MS = 30000;
constexpr size_t MAX_LOGS_PER_UPDATE = 25;
constexpr size_t MAX_LOG_MESSAGE_LENGTH = 200;
constexpr uint32_t FIRST_PERSISTENT_LOG_SEQUENCE_NUMBER = 1000000000UL;
constexpr uint32_t LOG_SEQUENCE_RESERVATION_SIZE = 1024;
constexpr char LOG_QUEUE_DIRECTORY[] = "/logqueue";

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
bool simUnlockAttempted = false;

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

PendingDeviceLogEntry pendingUploadLogEntries[MAX_LOGS_PER_UPDATE];
size_t pendingUploadLogEntryCount = 0;
String pendingUploadSegmentPath;
uint32_t pendingUploadLastSequenceNumber = 0;
String activeLogSegmentPath;
size_t activeLogSegmentEntryCount = 0;
uint32_t nextPendingLogSequenceNumber = FIRST_PERSISTENT_LOG_SEQUENCE_NUMBER;
uint32_t reservedLogSequenceUpperBound = FIRST_PERSISTENT_LOG_SEQUENCE_NUMBER;
bool logQueueReady = false;

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

uint32_t getLogSegmentFirstSequenceNumber(const String& path)
{
    if (!path.endsWith(".jsonl"))
    {
        return 0;
    }

    const int slashIndex = path.lastIndexOf('/');
    const int extensionIndex = path.lastIndexOf(".jsonl");

    if (extensionIndex <= slashIndex + 1)
    {
        return 0;
    }

    const String sequenceText = path.substring(slashIndex + 1, extensionIndex);

    for (size_t index = 0; index < sequenceText.length(); ++index)
    {
        if (!isDigit(sequenceText[index]))
        {
            return 0;
        }
    }

    return static_cast<uint32_t>(strtoul(sequenceText.c_str(), nullptr, 10));
}

uint32_t findHighestQueuedLogSequenceNumber()
{
    uint32_t highestSequenceNumber = 0;
    File directory = LittleFS.open(LOG_QUEUE_DIRECTORY);

    if (!directory || !directory.isDirectory())
    {
        return highestSequenceNumber;
    }

    File file = directory.openNextFile();

    while (file)
    {
        if (!file.isDirectory())
        {
            const uint32_t firstSequenceNumber = getLogSegmentFirstSequenceNumber(String(file.path()));

            if (firstSequenceNumber > 0)
            {
                const uint32_t estimatedLastSequenceNumber =
                    firstSequenceNumber <= UINT32_MAX - MAX_LOGS_PER_UPDATE
                    ? firstSequenceNumber + MAX_LOGS_PER_UPDATE
                    : UINT32_MAX;
                highestSequenceNumber = max(highestSequenceNumber, estimatedLastSequenceNumber);
            }
        }

        file = directory.openNextFile();
    }

    return highestSequenceNumber;
}

bool reserveLogSequenceRange(const uint32_t firstSequenceNumber)
{
    if (!preferences.begin("logqueue", false))
    {
        Serial.println("Could not open log queue Preferences.");
        return false;
    }

    const uint32_t upperBound = firstSequenceNumber <= UINT32_MAX - LOG_SEQUENCE_RESERVATION_SIZE
        ? firstSequenceNumber + LOG_SEQUENCE_RESERVATION_SIZE
        : UINT32_MAX;
    const bool saved = preferences.putUInt("seqUpper", upperBound) > 0;
    preferences.end();

    if (!saved)
    {
        Serial.println("Could not reserve persistent device log sequence numbers.");
        return false;
    }

    nextPendingLogSequenceNumber = firstSequenceNumber;
    reservedLogSequenceUpperBound = upperBound;
    return true;
}

bool initializePersistentLogQueue()
{
    bool fileSystemWasInitialized = false;

    if (preferences.begin("logqueue", true))
    {
        fileSystemWasInitialized = preferences.getBool("fsInit", false);
        preferences.end();
    }

    if (!LittleFS.begin(false))
    {
        if (fileSystemWasInitialized)
        {
            Serial.println(
                "Could not mount the existing LittleFS log queue. It was not formatted so queued logs can be recovered.");
            return false;
        }

        if (!LittleFS.begin(true))
        {
            Serial.println("Could not initialize LittleFS; device logs will only be written to Serial.");
            return false;
        }

        if (preferences.begin("logqueue", false))
        {
            preferences.putBool("fsInit", true);
            preferences.end();
        }
    }
    else if (!fileSystemWasInitialized && preferences.begin("logqueue", false))
    {
        preferences.putBool("fsInit", true);
        preferences.end();
    }

    if (!LittleFS.exists(LOG_QUEUE_DIRECTORY) && !LittleFS.mkdir(LOG_QUEUE_DIRECTORY))
    {
        Serial.println("Could not create the persistent device log queue directory.");
        return false;
    }

    uint32_t storedUpperBound = FIRST_PERSISTENT_LOG_SEQUENCE_NUMBER;

    if (preferences.begin("logqueue", true))
    {
        storedUpperBound = preferences.getUInt("seqUpper", FIRST_PERSISTENT_LOG_SEQUENCE_NUMBER);
        preferences.end();
    }

    const uint32_t highestQueuedSequenceNumber = findHighestQueuedLogSequenceNumber();
    uint32_t firstSequenceNumber = max(storedUpperBound, FIRST_PERSISTENT_LOG_SEQUENCE_NUMBER);

    if (highestQueuedSequenceNumber >= firstSequenceNumber && highestQueuedSequenceNumber < UINT32_MAX)
    {
        firstSequenceNumber = highestQueuedSequenceNumber + 1;
    }

    logQueueReady = reserveLogSequenceRange(firstSequenceNumber);
    return logQueueReady;
}

bool ensureLogSequenceNumberAvailable()
{
    if (nextPendingLogSequenceNumber < reservedLogSequenceUpperBound)
    {
        return true;
    }

    if (nextPendingLogSequenceNumber == UINT32_MAX)
    {
        Serial.println("Persistent device log sequence numbers are exhausted.");
        return false;
    }

    return reserveLogSequenceRange(nextPendingLogSequenceNumber);
}

void appendPendingDeviceLog(const String& level, const String& message)
{
    if (message.isEmpty())
    {
        return;
    }

    if (!logQueueReady || !ensureLogSequenceNumberAvailable())
    {
        return;
    }

    const uint32_t sequenceNumber = nextPendingLogSequenceNumber++;

    if (activeLogSegmentPath.isEmpty())
    {
        activeLogSegmentPath =
            String(LOG_QUEUE_DIRECTORY)
            + "/"
            + String(sequenceNumber)
            + ".jsonl";
        activeLogSegmentEntryCount = 0;
    }

    File file = LittleFS.open(activeLogSegmentPath, FILE_APPEND);

    if (!file)
    {
        Serial.println("Could not append a device log entry to the persistent queue.");
        activeLogSegmentPath = "";
        activeLogSegmentEntryCount = 0;
        return;
    }

    JsonDocument document;
    document["sequenceNumber"] = sequenceNumber;
    document["message"] = truncateLogMessage(message);
    document["level"] = level;
    document["deviceUptimeMs"] = millis();

    const bool written = serializeJson(document, file) > 0 && file.println() > 0;
    file.close();

    if (!written)
    {
        Serial.println("A device log entry could not be fully written to the persistent queue.");
        activeLogSegmentPath = "";
        activeLogSegmentEntryCount = 0;
        return;
    }

    activeLogSegmentEntryCount += 1;

    if (activeLogSegmentEntryCount >= MAX_LOGS_PER_UPDATE)
    {
        activeLogSegmentPath = "";
        activeLogSegmentEntryCount = 0;
    }
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

String findOldestLogSegmentPath()
{
    String oldestPath;
    uint32_t oldestSequenceNumber = UINT32_MAX;
    File directory = LittleFS.open(LOG_QUEUE_DIRECTORY);

    if (!directory || !directory.isDirectory())
    {
        return oldestPath;
    }

    File file = directory.openNextFile();

    while (file)
    {
        if (!file.isDirectory())
        {
            const String path = String(file.path());
            const uint32_t firstSequenceNumber = getLogSegmentFirstSequenceNumber(path);

            if (firstSequenceNumber > 0 && firstSequenceNumber < oldestSequenceNumber)
            {
                oldestSequenceNumber = firstSequenceNumber;
                oldestPath = path;
            }
        }

        file = directory.openNextFile();
    }

    return oldestPath;
}

void loadPendingLogUploadBatch()
{
    pendingUploadLogEntryCount = 0;
    pendingUploadSegmentPath = "";
    pendingUploadLastSequenceNumber = 0;

    if (!logQueueReady)
    {
        return;
    }

    // Close the current segment logically before sending it. Logs produced by
    // the HTTP request are appended to a new segment and cannot be removed by
    // the acknowledgement for this request.
    activeLogSegmentPath = "";
    activeLogSegmentEntryCount = 0;

    const String segmentPath = findOldestLogSegmentPath();

    if (segmentPath.isEmpty())
    {
        return;
    }

    File file = LittleFS.open(segmentPath, FILE_READ);

    if (!file)
    {
        Serial.println("Could not read the oldest persistent device log segment.");
        return;
    }

    while (file.available() && pendingUploadLogEntryCount < MAX_LOGS_PER_UPDATE)
    {
        JsonDocument document;
        const DeserializationError error = deserializeJson(document, file);

        if (error)
        {
            Serial.println("A persistent device log segment is corrupt and could not be uploaded.");
            break;
        }

        PendingDeviceLogEntry& entry = pendingUploadLogEntries[pendingUploadLogEntryCount];
        entry.sequenceNumber = document["sequenceNumber"] | 0U;
        entry.message = document["message"] | "";
        entry.level = document["level"] | "";
        entry.deviceUptimeMs = document["deviceUptimeMs"] | 0U;

        if (entry.sequenceNumber == 0 || entry.message.isEmpty())
        {
            Serial.println("A persistent device log record is invalid and could not be uploaded.");
            break;
        }

        pendingUploadLastSequenceNumber = entry.sequenceNumber;
        pendingUploadLogEntryCount += 1;
    }

    file.close();

    if (pendingUploadLogEntryCount > 0)
    {
        pendingUploadSegmentPath = segmentPath;
    }
    else
    {
        const String corruptPath = segmentPath + ".corrupt";

        if (LittleFS.rename(segmentPath, corruptPath))
        {
            Serial.println("An unreadable device log segment was preserved with a .corrupt suffix.");
        }
    }
}

void acknowledgePendingLogs(const uint32_t highestAcknowledgedSequenceNumber)
{
    if (pendingUploadSegmentPath.isEmpty()
        || pendingUploadLastSequenceNumber == 0
        || highestAcknowledgedSequenceNumber < pendingUploadLastSequenceNumber)
    {
        return;
    }

    if (!LittleFS.remove(pendingUploadSegmentPath))
    {
        Serial.println("The acknowledged persistent device log segment could not be removed.");
    }

    pendingUploadLogEntryCount = 0;
    pendingUploadSegmentPath = "";
    pendingUploadLastSequenceNumber = 0;
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

    if (config.cellular.enabled && connectCellular())
    {
        return true;
    }

    if (connectWifi())
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
    loadPendingLogUploadBatch();

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

    if (pendingUploadLogEntryCount > 0)
    {
        JsonArray logs = document["logs"].to<JsonArray>();

        for (size_t index = 0; index < pendingUploadLogEntryCount; ++index)
        {
            const PendingDeviceLogEntry& entry = pendingUploadLogEntries[index];
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
        // Discard any stale socket state from the previous request.
        cellularClient.stop();

        HttpClient client(cellularClient, config.backend.host.c_str(), config.backend.port);
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
                "Cellular HTTP connection/request initialization failed. Error: "
                + String(requestResult));

            client.stop();
            cellularClient.stop();
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
                "Cellular HTTP body write incomplete. Wrote "
                + String(bytesWritten)
                + " of "
                + String(payload.length())
                + " bytes.");

            client.stop();
            cellularClient.stop();
            return response;
        }

        response.statusCode = client.responseStatusCode();

        if (response.statusCode < 0)
        {
            logError(
                "No valid cellular HTTP response received. Error: "
                + String(response.statusCode));

            client.stop();
            cellularClient.stop();
            return response;
        }

        response.body = client.responseBody();
        response.transportOk = true;

        client.stop();
        cellularClient.stop();
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
    initializePersistentLogQueue();
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
