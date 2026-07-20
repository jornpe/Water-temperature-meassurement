# Plan 1: Device Onboarding Foundation

## Objective

Establish the first end-to-end device management slice across the ESP32 firmware, ASP.NET API, and React frontend so a physical device can:

1. Identify itself with a stable hardware-derived device ID.
2. Discover the backend without prior credentials.
3. Appear in the frontend as an unregistered device.
4. Be accepted and named by an authenticated user.
5. Receive a backend-generated API key on a subsequent discovery request.
6. Switch into authenticated operational mode and send dummy telemetry through the backend instead of publishing directly to MQTT.

This stage should stop at the point where registered devices are visible and manageable in the existing Sensors area, with placeholder telemetry flowing through the backend, historical temperature and position data retained for later trend views, latest network diagnostics retained on the device record, and admin cleanup operations in place. Full device log transport and Home Assistant MQTT publishing are intentionally deferred to later plans.

## Depends On

None.

## Verified Repository Findings

- Backend runtime is controller-based ASP.NET with JWT auth, EF Core, and PostgreSQL wiring in `src/backend/WaterTemperature.Api/Program.cs`.
- Database startup and migrations are handled centrally by `src/backend/WaterTemperature.Api/Data/DatabaseStartupExtensions.cs`.
- The current database model contains only `User` via `src/backend/WaterTemperature.Api/Data/AppDbContext.cs` and `src/backend/WaterTemperature.Api/Data/User.cs`.
- The current temperature API is demo-only and returns random data from `src/backend/WaterTemperature.Api/Controllers/TemperaturesController.cs`.
- Backend tests exist but are minimal and mostly non-integration xUnit tests in `src/backend/WaterTemperature.Api.Tests`.
- Frontend routing currently exposes only Sensors and Profile screens through `src/frontend/app/src/main.tsx`.
- The Sensors page currently fetches demo temperatures from `src/frontend/app/src/api.ts` and renders placeholder dashboard content via `src/frontend/app/src/pages/Sensors.tsx`, `src/frontend/app/src/components/SensorsContent.tsx`, and `src/frontend/app/src/components/MainGrid.tsx`.
- The ESP32 firmware is currently implemented in one file, `src/esp32/sim7000G - Water temp and GPS/src/main.cpp`.
- The ESP32 already has a stable, reboot-safe hardware ID source through `createDeviceId()` based on `ESP.getEfuseMac()` in `src/esp32/sim7000G - Water temp and GPS/src/main.cpp`.
- The ESP32 currently publishes GPS and Home Assistant MQTT discovery directly to an MQTT broker from `src/esp32/sim7000G - Water temp and GPS/src/main.cpp`.
- The ESP32 code comments already anticipate future local persistence via ESP32 Preferences/NVS, but that persistence is not implemented yet.
- Current frontend component patterns already use authenticated fetches, protected routes, MUI dialogs, and drawer-based layout.

## Current-Stage Scope

In scope for Plan 1:

- Backend device domain model and persistence.
- Unauthenticated discovery endpoint for unpaired devices.
- Authenticated admin endpoints for listing, viewing, registering, and regenerating device credentials.
- Backend-issued API key flow for devices.
- Authenticated device update endpoint for dummy telemetry and server-driven configuration return.
- Historical storage of temperature and position data for later trend views, while showing only the latest values in the current UI.
- Latest-only storage of network diagnostics on the device record, shown in the device details view when opening a card.
- Authenticated admin endpoints and UI actions for deleting a device and clearing stored temperature or position history.
- Frontend replacement of the dummy Sensors page with device inventory cards and basic device details/registration UX.
- ESP32 boot-mode split between discovery mode and operational mode.
- ESP32 local persistence of the device API key and backend-driven report interval.
- ESP32 fallback from operational mode to discovery mode when the backend returns unauthorized for a device update.
- Removal of direct MQTT publishing from the operational path, replaced by backend HTTP calls.

Out of scope for Plan 1:

- Full device log upload, storage, and frontend log viewer.
- Backend-to-Home-Assistant MQTT publishing.
- HA integration settings UI.
- Real temperature sensor hardware integration.
- Cellular data transport activation.
- Multi-user administration model changes.

## Architectural Direction

### 1. Introduce a real device aggregate in the backend

Add persistent device records instead of treating sensor data as anonymous temperature readings.

Recommended model split:

- `Device` entity: stable device identity and user-managed configuration.
- `DeviceApiKey` or equivalent hashed credential fields: device authentication material.
- `DeviceTelemetrySnapshot` fields on `Device` for the latest known values needed by the Sensors screen.
- Separate append-only history tables for the categories that must be trended and cleared independently:
  - temperature readings
  - position snapshots

Minimum fields needed in this stage:

- Stable device identifier from firmware.
- Registration status: unregistered vs registered.
- User-managed name.
- User-managed place.
- Report interval.
- API key hash and metadata such as creation/regeneration timestamps.
- Last seen/discovered timestamps.
- Latest dummy temperature.
- Latest position summary fields needed for the details view.
- Latest network diagnostic summary fields needed for the details view.
- Timestamps and values on historical temperature and position rows so later UI stages can trend data over time.

Telemetry contract for this stage:

- Temperature payload:
  - `temperature`
- Position payload:
  - `latitude`
  - `longitude`
  - `altitude`
  - `gps_time_utc`
  - `speed_knots`
  - `hdop`
  - `satellites_visible`
  - `satellites_used`
- Network diagnostics payload:
  - Wi-Fi path, based on the ESP32 Wi-Fi library currently in use: `local_ip`, `wifi_rssi_dbm`, `ssid`, `bssid`, `channel`, `gateway_ip`, `subnet_mask`, `dns_ip`, `mac_address`
  - Cellular path, based on the TinyGSM library currently in use: `local_ip`, `sim_status`, `network_connected`, `gprs_connected`, `operator`, `signal_quality`

Do not add generic sensor bags or extra temperature-adjacent fields in this stage. Temperature remains a single value, position uses the explicit GPS fields above, and network data is limited to diagnostics the current Wi-Fi and TinyGSM stack can provide.

Prefer a `Device` aggregate root plus focused history tables only for trendable telemetry. That keeps category-specific clearing straightforward and supports later trend visualizations without changing the main device model.

### 2. Separate unauthenticated discovery from authenticated operations

Discovery must remain open to allow first contact, but all post-pairing data submission must require the device API key.

Recommended endpoint split:

- `POST /api/devices/discover`: no auth.
- `POST /api/device-updates` or `POST /api/devices/{deviceId}/updates`: device API key auth.
- Admin CRUD endpoints under authenticated `/api/devices/*`.

This keeps the public surface narrow and makes later hardening easier.

### 3. Return the API key only after an admin has approved the device

The discovery endpoint should behave differently by device state:

- Unknown device ID: create one unregistered device record, return an empty or no-op response.
- Known unregistered device: update last-seen metadata, return an empty or no-op response.
- Registered device without confirmed firmware credential: return the generated API key and operational configuration.
- Registered device with confirmed credential already handed out: stop using discovery and require operational updates.

To avoid accidental duplicate registrations, device identity should be keyed by the unique firmware device ID with a unique index in the database.

### 4. Persist credentials in firmware using ESP32 Preferences/NVS

The firmware already derives a stable identity, but it does not persist pairing state. This stage should add a small persistence layer for:

- Device API key.
- Report interval.
- Optional backend base URL if that is intended to be configurable later.

On boot:

- If no API key is present, enter discovery mode.
- If an API key is present, enter operational mode.

### 5. Store full telemetry history but surface only the newest snapshot

The current UI only needs the latest values, but the backend must already persist historical temperature and position telemetry so later plans can add trend views without changing the device protocol.

Recommended storage behavior:

- Each authenticated device update writes append-only history rows for temperature and position data included in the request.
- The `Device` record is updated in the same flow with the newest snapshot values used by the current Sensors UI.
- Network diagnostics are stored only as the latest snapshot on the `Device` record and are replaced on each update instead of being historized.
- Category-specific clear operations delete only the selected historical data set and then reset or recalculate the corresponding latest snapshot fields on the device.
- Device deletion removes the device and all dependent historical data.

### 6. Make backend state the source of truth for device cards

The frontend should stop adapting temperature readings into pseudo-device cards. Instead, it should fetch a list of backend device summaries and render:

- Unregistered devices first.
- Registered devices after that.
- Per-card fields: place, latest temperature, last update time.

Card expansion or detail dialog should expose the stage-1 editable configuration and read-only telemetry snapshot.

## Repository Areas Likely Affected

Backend:

- `src/backend/WaterTemperature.Api/Program.cs`
- `src/backend/WaterTemperature.Api/Data/AppDbContext.cs`
- `src/backend/WaterTemperature.Api/Data/DatabaseStartupExtensions.cs`
- `src/backend/WaterTemperature.Api/Controllers/TemperaturesController.cs`
- `src/backend/WaterTemperature.Api/Controllers/ApiControllerBase.cs`
- `src/backend/WaterTemperature.Api/Configuration/AppSettings.cs`
- `src/backend/WaterTemperature.Api/Migrations/*`
- New backend areas likely required:
  - `src/backend/WaterTemperature.Api/Data/Device*.cs`
  - `src/backend/WaterTemperature.Api/Controllers/DevicesController.cs`
  - `src/backend/WaterTemperature.Api/Controllers/DeviceDiscoveryController.cs`
  - `src/backend/WaterTemperature.Api/Controllers/DeviceUpdatesController.cs`
  - `src/backend/WaterTemperature.Api/Models/Devices/*`
  - `src/backend/WaterTemperature.Api/Services/Devices/*` or equivalent local service files if logic should not stay in controllers

Backend tests:

- `src/backend/WaterTemperature.Api.Tests/Controllers/*`
- `src/backend/WaterTemperature.Api.Tests/Services/*`

Frontend:

- `src/frontend/app/src/api.ts`
- `src/frontend/app/src/main.tsx`
- `src/frontend/app/src/pages/Sensors.tsx`
- `src/frontend/app/src/components/SensorsContent.tsx`
- `src/frontend/app/src/components/MainGrid.tsx`
- `src/frontend/app/src/components/MenuContent.tsx`
- New frontend areas likely required:
  - `src/frontend/app/src/components/devices/*`
  - `src/frontend/app/src/types/devices.ts` or local types near `api.ts`

ESP32:

- `src/esp32/sim7000G - Water temp and GPS/src/main.cpp`
- `src/esp32/sim7000G - Water temp and GPS/platformio.ini` only if new library flags or build definitions become necessary

Documentation:

- `README.md`
- `docs/plans/plan-1-device-onboarding-foundation.md`

## Ordered Implementation Tasks

### Task 1: Define backend device persistence and migration

Add the backend data model needed for discovery, registration, latest-state telemetry, and historical retention for trendable telemetry.

Implementation notes:

- Extend `AppDbContext` with a device set.
- Add a `Device` entity with a unique constraint on the firmware device ID.
- Store API keys as hashes, not plaintext.
- Add timestamps for created, registered, last discovered, and last updated moments.
- Add nullable latest telemetry fields needed by the stage-1 UI: temperature, latitude, longitude, network summary, and last update time.
- Add focused history tables for temperature readings and position snapshots so telemetry can be trended later and cleared by category now.
- Keep network diagnostics as latest-value fields on the `Device` entity rather than a historical table.
- Add a status field or enum that cleanly distinguishes unregistered and registered states.
- Generate and add an EF Core migration with EF Core tools, using the repository projects rather than ad hoc SQL.
- Do not run `dotnet ef database update` or any other manual migration command against a database. Database migration remains a runtime concern handled by backend startup code.

Why first:

- Every later slice depends on a durable device record and identity constraint.

### Task 2: Add backend API key generation and device-auth validation

Introduce a dedicated service responsible for generating high-entropy API keys, hashing them for storage, and validating device requests.

Implementation notes:

- Follow the existing backend pattern of registering services in `Program.cs`.
- Keep device auth separate from JWT auth; do not overload user JWTs for firmware.
- Prefer a custom header such as `X-Api-Key` together with the device ID in body or route.
- Include regeneration semantics now, because the admin UI requires it in this stage.

Validation requirements:

- Unit-test key generation and verification behavior.
- Unit-test that regenerated keys invalidate the old credential.

### Task 3: Replace the demo temperature API with device-oriented backend endpoints

Add the stage-1 device endpoints and remove the Sensors page dependency on random temperature responses.

Recommended endpoint set:

- `POST /api/devices/discover`
  - Anonymous.
  - Accepts stable device ID and lightweight discovery metadata such as firmware version and current network transport.
  - Creates the device record if absent, but never creates duplicates.
  - Returns either no pairing material or the issued API key plus current configuration.

- `GET /api/devices`
  - JWT-protected.
  - Returns card-friendly summaries ordered with unregistered devices first.

- `GET /api/devices/{id}`
  - JWT-protected.
  - Returns full device detail for the drawer/dialog view, including latest temperature, latest position snapshot, latest network diagnostics, and useful historical counts for temperature and position.

- `POST /api/devices/{id}/register`
  - JWT-protected.
  - Accepts name, place, and report interval.
  - Generates a new API key and transitions the device to registered.

- `POST /api/devices/{id}/regenerate-key`
  - JWT-protected.
  - Forces credential rotation with explicit confirmation at the UI layer.

- `POST /api/devices/{deviceId}/updates`
  - Device-authenticated.
  - Accepts dummy telemetry payload with one `temperature` field, the explicit position fields defined in this plan, and the supported network diagnostics for the active transport; appends historical temperature and position records, updates the latest device snapshot including latest network diagnostics, and returns effective configuration, including report interval.

- `DELETE /api/devices/{id}`
  - JWT-protected.
  - Removes the device, credential material, and dependent historical data.

- `DELETE /api/devices/{id}/telemetry/{category}`
  - JWT-protected.
  - Clears a selected historical telemetry category such as `temperature` or `position` without deleting the whole device.

Important behavior decision:

- For stage 1, discovery should return the API key only while the device is still polling discovery and the backend knows it has been approved but not yet switched to operational mode.
- After the firmware starts using the API key successfully, discovery responses for that device should no longer be the normal path.

### Task 4: Define DTOs that match the intended frontend and firmware contracts

Introduce explicit request/response models under `Models` rather than passing entity types through controllers.

Minimum DTO groups needed:

- Discovery request/response.
- Device summary response for card list.
- Device detail response for the modal/drawer.
- Device registration request.
- API key regeneration response.
- Device update request/response.
- Telemetry-clear response and any confirmation metadata needed by the UI.
- Device delete response if the API does not use an empty success result.

Design recommendation:

- Include a compact `configuration` object in device update responses, even if it initially only contains `reportIntervalSeconds`. That gives Plan 2 a clean place to add more server-driven settings.

### Task 5: Replace frontend dummy sensor data with device inventory data

Refactor the Sensors screen into a device inventory view backed by the new device endpoints.

Implementation notes:

- Remove the temperature-reading adaptation currently in `src/frontend/app/src/pages/Sensors.tsx`.
- Add typed API functions in `src/frontend/app/src/api.ts` for device summaries, device details, registration, and key regeneration.
- Add typed API functions for deleting a device and clearing selected temperature or position history.
- Replace `MainGrid` placeholder metrics and recent readings list with device cards.
- Keep the current route at `/` unless there is a strong reason to rename it; the sidebar currently points there.
- Sort unregistered devices first in the frontend only if the backend does not already guarantee ordering. Prefer backend ordering to keep behavior consistent.
- Keep the list and cards driven by the latest device snapshot only; trend UI for the stored historical data is intentionally deferred.

Per-card requirements in this stage:

- Place.
- Latest temperature.
- Last update time.
- Visual distinction for unregistered devices.

### Task 6: Add frontend registration and device detail UX

Implement a device details surface using existing MUI patterns already present in the repo.

Recommended approach:

- Reuse the repo’s dialog pattern from `src/frontend/app/src/pages/Profile.tsx` for confirmation and editing interactions.
- Use either a full-width dialog or a right-side drawer for device details; either matches the current MUI layout conventions.

Minimum detail fields to expose:

- Device ID.
- Name.
- Place.
- Report interval.
- API key controls with confirmation for regeneration.
- Latest temperature.
- Latest position data using the explicit GPS field set from this plan.
- Latest network diagnostic data from the active transport.
- Historical data counts or last-recorded timestamps for temperature and position where useful to support delete and clear confirmations.

Registration flow in this stage:

- Unregistered device opens into a registration form.
- User submits name, place, and report interval.
- UI updates the card state to registered after success.
- UI does not need to reveal the raw API key if the backend only intends the device to fetch it via discovery.

Destructive admin actions in this stage:

- Allow deleting a device with explicit confirmation.
- Allow clearing stored temperature or position history with explicit confirmation.
- When opening a device card, show latest position data and latest network diagnostics in the detail view.
- After a clear action, refresh the latest snapshot shown in the detail view and cards.

### Task 7: Add ESP32 discovery mode and operational mode

Refactor `main.cpp` behavior so the firmware no longer depends on MQTT connectivity for its primary workflow.

Implementation notes:

- Keep `createDeviceId()` as the canonical identity source.
- Add Preferences/NVS persistence for API key and report interval.
- On boot, load persisted credentials and decide mode.
- Discovery mode should send an unauthenticated backend request every 30 seconds until a key is returned.
- Operational mode should send authenticated updates on the configured interval.
- If an authenticated update returns unauthorized, clear the stored API key, switch back to discovery mode, and continue pairing retries every 30 seconds.
- Do not fall back to discovery on transient transport failures or ordinary server errors; only do it when the backend explicitly rejects the credential.
- While real temperature hardware is unavailable, include a single dummy `temperature` value in the update payload and keep sending the defined position and supported network diagnostic fields that the firmware can already collect.

Available network diagnostics verified from the current firmware stack:

- ESP32 Wi-Fi library surface available in the installed Arduino core: `WiFi.localIP()`, `WiFi.RSSI()`, `WiFi.SSID()`, `WiFi.BSSIDstr()`, `WiFi.channel()`, `WiFi.gatewayIP()`, `WiFi.subnetMask()`, `WiFi.dnsIP()`, `WiFi.macAddress()`.
- TinyGSM surface available in the installed SIM7000-capable library: `modem.localIP()`, `modem.getSimStatus()`, `modem.isNetworkConnected()`, `modem.isGprsConnected()`, `modem.getOperator()`, `modem.getSignalQuality()`.
- Do not plan around `getProvider()` for this modem path in stage 1; the installed TinyGSM code marks that method as not implemented in the shared GPRS layer.

Recommended firmware restructuring:

- Split responsibilities into local helper functions even if they remain in `main.cpp` initially:
  - persistence
  - discovery request
  - operational update request
  - configuration application
  - telemetry snapshot assembly

That keeps Plan 2 from adding log transport into an already tangled control loop.

### Task 8: Remove direct MQTT publishing from the firmware operational path

The current firmware publishes directly to MQTT and Home Assistant discovery topics. For this stage, that path should be removed or fully disabled in favor of backend HTTP communication.

Implementation notes:

- Remove MQTT connect, publish, and Home Assistant discovery calls from the normal loop.
- Do not partially maintain parallel backend and direct MQTT pipelines; that would create duplicate sources of truth ahead of the HA integration stage.
- If temporary compile-safe stubbing is needed while moving behavior, isolate it clearly and remove it before Plan 1 is considered complete.

### Task 9: Update backend and frontend documentation

Document the new pairing flow and device lifecycle.

Minimum docs to update:

- `README.md` backend/frontend summary should stop describing the Sensors screen as temperature-demo driven.
- Add a short device lifecycle explanation: unregistered discovery, admin registration, API key handoff, operational updates.
- Document the credential-loss recovery path: device receives unauthorized in operational mode, clears its key, and returns to discovery mode.
- Document the migration workflow: create EF Core migration files with tooling, but do not manually run them against any database.
- Call out that Home Assistant publishing is not device-direct anymore and is planned to be backend-managed.

## Testing And Validation Requirements

Backend:

- Add xUnit coverage for device discovery behavior:
  - unknown device creates one unregistered record
  - repeated discovery does not create duplicates
  - registered device receives pairing payload when appropriate
- Add xUnit coverage for registration and API key regeneration.
- Add xUnit coverage for device-authenticated update endpoint success and rejection paths, including unauthorized credential handling.
- Add xUnit coverage that authenticated updates create historical temperature and position records while updating the latest snapshot fields, including latest network diagnostics.
- Add xUnit coverage for device deletion and category-specific telemetry clearing.
- Run the existing backend test project: `dotnet test src/backend/WaterTemperature.Api.Tests/WaterTemperature.Api.Tests.csproj -c Release --nologo`.
- Build the backend: `dotnet build src/backend/WaterTemperature.Api/WaterTemperature.Api.csproj -c Release`.
- Create the schema migration with EF Core tools and verify the generated migration files are correct, but do not manually apply that migration to a database.

Frontend:

- Build the frontend: `cd src/frontend/app && npm install && npm run build`.
- Manually verify:
  - unregistered devices appear first
  - registration updates the card state
  - regeneration requires confirmation
  - details view shows latest backend data

ESP32:

- Rebuild the PlatformIO project for the existing environment: `platformio run --environment esp32dev`.
- Manually verify on hardware or serial monitor:
  - first boot without stored key enters discovery mode
  - device retries discovery every 30 seconds
  - after backend approval, device stores the API key and switches to operational mode after restart or next successful handshake
  - stored key survives reboot and power cycle
  - unauthorized from the update endpoint clears the stored key and returns the device to discovery mode

End-to-end:

- Verify one complete onboarding cycle against the local backend/frontend stack.
- Confirm that a single device ID results in one backend record even after repeated discovery requests.
- Confirm that each device update stores historical temperature and position data while the Sensors page still shows only the newest backend snapshot.
- Confirm that opening a device card shows the latest position data and latest network diagnostics.
- Confirm that device delete removes the device and its dependent data, and category-specific clear operations remove only the selected stored temperature or position history.
- Confirm that the Sensors page is driven entirely by backend device data, not demo random temperatures.

## Completion Criteria

Plan 1 is complete when all of the following are true:

- A new ESP32 device without a stored key repeatedly discovers the backend every 30 seconds.
- The backend creates exactly one unregistered record for a new device ID and updates its last-seen metadata on subsequent discovery calls.
- The frontend Sensors view shows that unregistered device as a card.
- An authenticated user can register the device with name, place, and report interval.
- The backend issues and stores a hashed device API key.
- The device receives the API key from the backend, stores it durably, and switches to authenticated operational updates.
- If the backend later rejects the stored device credential as unauthorized, the device clears it and falls back to discovery mode.
- Authenticated device updates persist historical temperature and position data while updating the latest snapshot used by the current UI, including latest network diagnostics.
- Registered device cards show place, latest temperature, and last update time from backend state.
- An authenticated user can delete a device or clear selected telemetry categories with confirmation.
- Direct MQTT publishing is removed from the firmware operational flow.
- Backend, frontend, and firmware builds/tests required above pass.

## Risks And Compatibility Concerns

- The existing firmware is monolithic; adding HTTP discovery, persistence, and update flows without local decomposition will make later log-shipping work harder.
- API key storage must be hashed server-side; storing plaintext in PostgreSQL would create an avoidable security regression.
- Returning the API key through discovery introduces a one-time bootstrap path; the implementation must define clearly when that response is allowed so devices do not repeatedly reacquire new credentials.
- Firmware fallback from operational mode to discovery must trigger only on explicit unauthorized responses, not on transient network failures, or devices will churn unnecessarily.
- The current backend test suite is too thin to safely support these new flows without adding targeted controller/service tests.
- Removing the demo temperature endpoint may break the existing frontend immediately; backend and frontend changes should land together or behind a temporary compatibility layer during development.
- Historical temperature and position storage introduce table growth immediately; if retention limits are needed later, they should be added explicitly rather than assumed.
- Some network diagnostics are transport-specific. The backend contract and frontend display logic must treat Wi-Fi and cellular fields as optional rather than assuming both are always present.
- Because database updates are applied on application startup, schema issues will surface when the backend boots; manual database migration should not be added as a separate operator workflow in this plan.

## Assumptions

- The existing single-admin user model remains acceptable for device approval in this stage.
- The backend base URL for the ESP32 is already known at firmware build or deployment time.
- Report interval can be represented as a single server-controlled numeric value for now.
- The first frontend device inventory screen still only needs the latest telemetry snapshot even though the backend stores history.
- Historical temperature and position data are retained until a user deletes the device or explicitly clears a telemetry category; no automatic pruning policy is introduced in this stage.
- The current firmware stage uses only one temperature value rather than a broader sensor payload.
- EF Core migration files are created with tooling and committed to source control, but database application remains handled by backend startup.
- Dummy temperature generation on the ESP32 is acceptable until the real sensor is integrated.

## Remaining Uncertainties To Revisit In Plan 2

- Exact retention and presentation strategy for full device logs.
- Whether device details should grow into a separate route instead of a dialog/drawer.
- Whether device API key exchange should later move to a more formal claim-token or challenge-based enrollment flow.
- Whether automatic telemetry retention limits or pruning jobs will be needed once historical volume is understood.

## Rollout And Rollback Notes

Rollout:

- Generate and commit the new EF Core migration files before testing device onboarding against persistent environments.
- Do not manually run database migration commands against any environment. Start the backend and allow its existing startup path to apply migrations.
- Deploy backend support before flashing firmware that expects discovery and update endpoints.
- Deploy frontend changes after backend endpoints are available.

Rollback:

- Backend rollback requires reverting the migration and controller/service changes together.
- Firmware rollback should only be attempted if direct MQTT behavior remains available in the older firmware image; otherwise, devices may lose their reporting path.
- If rollback is required after key issuance, clear affected device credentials server-side to avoid mismatched pairing state on the next rollout.