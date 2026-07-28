# Plan 2: Device Details Page And Logging

## Objective

Replace the current modal-based device detail experience with a route-backed device page, add editable device configuration for registered devices, add runtime-configuration reporting and pending-sync visibility, and add end-to-end ESP32 log transport so each device can upload unsent serial log lines together with telemetry in a single authenticated update request.

At the end of this stage:

1. The device inventory page still shows device cards, with unregistered devices first.
2. Opening a device navigates to its own page instead of opening `DeviceDetailDialog`.
3. The device page has a back button and three tabs:
   - Sensor information
   - Network and configuration
   - Device logs
4. Registered devices can have name, place, and report interval updated after registration.
5. The backend stores both desired configuration and the runtime configuration reported by the device.
6. The UI can show whether configuration changes are still pending on the device.
7. The device update request carries telemetry, runtime configuration, and unsent log lines in one request.
8. The backend persists device logs and the frontend can view them.
9. The inventory page and device details page refresh automatically without manual reload.

## Depends On

- `docs/plans/plan-1-device-onboarding-foundation.md`

## Verified Repository Findings

- Plan 1 has already been implemented in the repository, not only documented:
  - discovery endpoint in `src/backend/WaterTemperature.Api/Controllers/DeviceDiscoveryController.cs`
  - authenticated update endpoint in `src/backend/WaterTemperature.Api/Controllers/DeviceUpdatesController.cs`
  - device administration endpoints in `src/backend/WaterTemperature.Api/Controllers/DevicesController.cs`
  - device persistence and history tables in `src/backend/WaterTemperature.Api/Data/Device.cs`, `src/backend/WaterTemperature.Api/Data/AppDbContext.cs`, and `src/backend/WaterTemperature.Api/Migrations/20260715201842_DeviceOnboardingFoundation.cs`
  - controller and service tests in `src/backend/WaterTemperature.Api.Tests/Controllers` and `src/backend/WaterTemperature.Api.Tests/Services`
- The current device details UX is still a modal dialog implemented in `src/frontend/app/src/components/devices/DeviceDetailDialog.tsx` and opened from `src/frontend/app/src/pages/Sensors.tsx`.
- Frontend routing currently exposes only `/`, `/login`, `/register`, and `/profile` in `src/frontend/app/src/main.tsx`.
- The current device page data model in `src/frontend/app/src/api.ts` does not include runtime configuration state, log entries, or editable update models for registered devices.
- The inventory cards are already real device cards, rendered from backend device summaries in `src/frontend/app/src/components/MainGrid.tsx`.
- There is no existing frontend tab pattern for device details, no route parameter usage, and no live-update mechanism in the frontend codebase.
- The backend has no current SignalR, SSE, WebSocket, hosted background worker, or notification infrastructure in `src/backend/WaterTemperature.Api`.
- The device update contract currently accepts firmware version, temperature, position, and network diagnostics only through `DeviceUpdateRequest` in `src/backend/WaterTemperature.Api/Models/Devices/DeviceModels.cs`.
- The update response currently returns only `reportIntervalSeconds` through `DeviceConfigurationResponse` in `src/backend/WaterTemperature.Api/Models/Devices/DeviceModels.cs`.
- The ESP32 firmware already persists the API key and report interval in Preferences and switches between discovery and operational mode in `src/esp32/sim7000G - Water temp and GPS/src/main.cpp`.
- The ESP32 firmware currently writes logs directly to `Serial` and does not buffer or upload them.
- The ESP32 firmware is still largely implemented in one file, `src/esp32/sim7000G - Water temp and GPS/src/main.cpp`, so the next stage should prefer small local abstractions over a broad firmware restructure.
- The frontend has a build pipeline but no established automated test runner in `src/frontend/app/package.json`.

## Current-Stage Scope

In scope:

- Route-backed device details page.
- Removal of the modal detail flow.
- Three-tab device details layout.
- Editing name, place, and report interval for registered devices.
- Runtime configuration reporting from device to backend.
- Pending-configuration detection in backend and frontend.
- Device log batching, upload, persistence, and viewing.
- Automatic refresh of the device list and device detail page.
- Backward-compatible extension of the existing device update contract.

Out of scope:

- Home Assistant MQTT integration.
- Real temperature sensor hardware integration.
- Replacing the current telemetry model with chart-heavy historical views.
- Broad firmware file splitting unless the implementation becomes unmanageable without it.
- Background processing infrastructure for push notifications.

## Recommendation Requiring Decision

### Live refresh transport

There is no existing push infrastructure in either the backend or frontend. For this stage, the lowest-risk option is visibility-aware HTTP polling:

- device inventory page polling `GET /api/devices`
- device details page polling `GET /api/devices/{id}` and `GET /api/devices/{id}/logs`

Recommendation:

- Use polling in this stage.
- Keep the polling intervals conservative, for example 10 seconds on the inventory page and 5 seconds on the active device page.
- Pause or slow polling when the tab is hidden.

If immediate push delivery is a hard requirement, choose SSE or SignalR before implementation. That would materially change the backend scope and should replace the polling tasks below rather than be added on top of them.

This plan assumes the polling approach.

## Architectural Direction

### 1. Move device details from modal state to route state

The current details flow is owned by `Sensors.tsx` through `selectedDeviceId`, `selectedDevice`, and `DeviceDetailDialog`. That structure prevents deep linking, makes refresh awkward, and couples list and detail concerns.

Recommended direction:

- Keep `/` as the inventory page.
- Add a dedicated device details route such as `/devices/:id`.
- Move data fetching for the device details page into a dedicated page component rather than keeping it in `Sensors.tsx`.
- Replace dialog open-close state with route navigation.
- Retire `DeviceDetailDialog.tsx` once the new page is in place.

This is the smallest change that satisfies the user requirement without disturbing the existing inventory card layout.

### 2. Separate desired configuration from reported runtime configuration

The current device model stores only the desired report interval and the latest telemetry snapshot. The user now wants to see:

- what configuration the backend wants the device to use
- what configuration the device says it is currently using
- whether there are pending configuration updates

Recommended direction:

- Keep `Device.ReportIntervalSeconds` as the desired report interval managed by the user.
- Add reported runtime configuration fields on the device record for the values the firmware says it is actually using.
- Add a configuration version or revision field that increments whenever device-consumable settings change.
- Have the firmware persist and report the last applied configuration version.
- Mark configuration as pending when the desired version is newer than the reported applied version.

Why versioning is preferable here:

- it supports more runtime configuration fields later without rewriting pending-state logic
- it avoids brittle field-by-field comparisons
- it allows the UI to show sync status explicitly

For this stage, the only device-consumable configuration that must be editable is report interval. Name and place remain admin metadata and should not participate in configuration sync status.

### 3. Extend the device update contract instead of creating a second telemetry endpoint

The current update endpoint already returns configuration and is the right place to add runtime config and logs.

Recommended direction:

- Keep `POST /api/devices/{deviceId}/updates` as the single authenticated device write endpoint.
- Extend `DeviceUpdateRequest` with:
  - runtime configuration snapshot
  - device log batch
- Extend `DeviceUpdateResponse` with:
  - desired configuration version
  - log acknowledgment marker
  - existing configuration payload

Backward compatibility requirement:

- new request fields must be optional so already-flashed stage-1 firmware can keep working until the new firmware is deployed
- old clients that ignore new response fields must still work

### 4. Make device log ingestion idempotent

The user requirement says the ESP32 must send log lines that have not yet been sent. Because requests can fail after partial processing, the backend must tolerate retries without storing duplicates.

Recommended direction:

- Give each log line a monotonically increasing device-local sequence number.
- Send log lines as a batch on each update until the backend acknowledges them.
- Persist logs in an append-only `DeviceLogEntry` table with a uniqueness constraint on `(DeviceId, SequenceNumber)`.
- Return the highest accepted sequence number in the update response.
- Delete or advance the device-side unsent buffer only after the backend acknowledges receipt.

This gives safe retry behavior and avoids duplicate log rows when the same update is retried.

### 5. Keep full server log history and persist the device-side unsent queue

The frontend requirement is to show the full log for a device. The backend can satisfy that with append-only persistence and paginated reads.

The original RAM ring-buffer design was superseded after testing exposed restart sequence reuse and offline overflow as loss paths.

Implemented direction:

- Backend stores log entries indefinitely and serves indexed cursor pages with server-side filters.
- Frontend reads the newest page, polls forward from the latest database ID, and requests older cursor pages on demand.
- Firmware stores unsent entries in segmented LittleFS files instead of a fixed-size RAM ring.
- Each segment is removed only after the matching backend acknowledgement.
- Sequence ranges are reserved in Preferences so rebooting cannot reuse an already-stored log sequence.

The persistent queue is limited only by the board's physical LittleFS capacity. A full or unavailable filesystem is reported on Serial; the firmware does not evict an older unacknowledged entry to make room.

## Repository Areas Likely Affected

Backend:

- `src/backend/WaterTemperature.Api/Data/Device.cs`
- `src/backend/WaterTemperature.Api/Data/AppDbContext.cs`
- `src/backend/WaterTemperature.Api/Models/Devices/DeviceModels.cs`
- `src/backend/WaterTemperature.Api/Controllers/DevicesController.cs`
- `src/backend/WaterTemperature.Api/Controllers/DeviceUpdatesController.cs`
- `src/backend/WaterTemperature.Api/Program.cs`
- `src/backend/WaterTemperature.Api/Migrations/*`
- likely new files:
  - `src/backend/WaterTemperature.Api/Data/DeviceLogEntry.cs`
  - `src/backend/WaterTemperature.Api/Services/Devices/*` or equivalent if controller logic needs extraction

Backend tests:

- `src/backend/WaterTemperature.Api.Tests/Controllers/DeviceUpdatesControllerTests.cs`
- `src/backend/WaterTemperature.Api.Tests/Controllers/DevicesControllerTests.cs`
- likely new tests for log retrieval and configuration sync behavior

Frontend:

- `src/frontend/app/src/main.tsx`
- `src/frontend/app/src/api.ts`
- `src/frontend/app/src/pages/Sensors.tsx`
- `src/frontend/app/src/components/MainGrid.tsx`
- `src/frontend/app/src/components/SensorsContent.tsx`
- `src/frontend/app/src/components/devices/DeviceDetailDialog.tsx`
- likely new files:
  - `src/frontend/app/src/pages/DeviceDetails.tsx`
  - `src/frontend/app/src/components/devices/DeviceDetailsTabs.tsx`
  - `src/frontend/app/src/components/devices/DeviceLogsTab.tsx`
  - `src/frontend/app/src/components/devices/DeviceConfigurationForm.tsx`

ESP32 firmware:

- `src/esp32/sim7000G - Water temp and GPS/src/main.cpp`

Documentation:

- `README.md`
- `docs/plans/plan-2-device-details-and-logging.md`

## Ordered Implementation Tasks

### Task 1: Extend backend persistence for runtime configuration and logs

Add the minimum database support needed for configuration sync state and log storage.

Implementation notes:

- Extend `Device` with fields for desired configuration version and reported applied configuration version.
- Add explicit runtime configuration snapshot fields for what the device last reported.
- Keep the schema small in this stage. Do not build a generic key-value configuration store.
- Add a `DeviceLogEntry` entity with:
  - `Id`
  - `DeviceId`
  - `SequenceNumber`
  - `Level` if the firmware can provide it, otherwise keep it nullable
  - `Message`
  - device-side timestamp or uptime if available
  - backend received timestamp
- Add an index and uniqueness constraint on `(DeviceId, SequenceNumber)`.
- Generate an additive EF Core migration.

Why first:

- All API and UI work in this stage depends on these persisted shapes.

### Task 2: Extend the backend device models and update contract

Update the request and response models in `DeviceModels.cs` to carry runtime configuration, log batches, sync state, and log query responses.

Recommended model additions:

- runtime configuration request model, for example:
  - applied configuration version
  - applied report interval seconds
- log entry request model, for example:
  - sequence number
  - message
  - optional level
  - optional device timestamp or uptime
- update response additions:
  - desired configuration version
  - highest accepted log sequence number
- device details response additions:
  - desired configuration summary
  - reported runtime configuration summary
  - pending configuration boolean
  - last configuration sync timestamps
- device update model for admin edits, separate from registration
- paginated device logs response model

Compatibility requirement:

- all new device-update request fields must remain nullable or optional

### Task 3: Add backend endpoints for editing configuration and reading logs

The current controller set supports registration, key regeneration, delete, and history clear, but not post-registration metadata/config updates or log retrieval.

Recommended endpoint additions:

- `PUT /api/devices/{id}` for admin edits to:
  - name
  - place
  - report interval
- `GET /api/devices/{id}/logs` for paginated device log retrieval

Recommended controller behavior:

- Updating name or place should not affect configuration sync state.
- Updating report interval should:
  - update the desired report interval
  - increment the desired configuration version
  - leave the reported applied version unchanged until the device reports it back
- `GET /api/devices/{id}` should include enough data for the details page to render all three tabs without multiple unrelated admin calls.
- `GET /api/devices/{id}/logs` should be sorted deterministically, preferably newest first for the UI while still preserving sequence numbers.

### Task 4: Extend the backend update pipeline to store runtime config and logs

Update `DeviceUpdatesController` so a single device update request can store telemetry, runtime configuration, and logs while returning the desired configuration and log acknowledgment.

Implementation notes:

- Preserve current authentication behavior with `X-Api-Key`.
- Keep telemetry handling intact for temperature, position, and network data.
- When runtime configuration is included:
  - update the reported runtime configuration fields on the `Device`
  - update the reported applied configuration version
  - recompute pending configuration state if it is stored, or let it be derived in query models
- When logs are included:
  - insert only logs that are not already stored for the device
  - do not fail the whole update because of duplicates from retried requests
- Return the current desired configuration and highest accepted log sequence number.

Test expectation:

- existing update tests should continue to pass after minimal request-model adjustments

### Task 5: Add backend query shaping for the route-backed details page

The current `GetDevice` projection is enough for a modal snapshot, but not for a full page with configuration sync and logs.

Recommended query updates:

- Include desired configuration data.
- Include reported runtime configuration data.
- Include a derived `hasPendingConfiguration` flag.
- Include timestamps that help explain the sync state, for example:
  - desired configuration last changed at
  - device last reported at
  - runtime configuration last reported at
- Keep log retrieval on its own endpoint so detail fetches stay lightweight.

### Task 6: Replace the modal flow with a route-backed device details page

Refactor the frontend routing and page structure.

Implementation notes:

- Add a route such as `/devices/:id` in `src/frontend/app/src/main.tsx`.
- Change inventory card click handling in `MainGrid.tsx` and `Sensors.tsx` from “select and open dialog” to navigation.
- Create a dedicated `DeviceDetails` page component that owns:
  - detail fetch
  - configuration save action
  - key regeneration action
  - log polling
  - back navigation
- Remove the dependency on `DeviceDetailDialog.tsx` once the new page is stable.

UI structure:

- top-level back button returning to `/`
- page header with device name or device ID
- visible status chip for registered or unregistered
- three tabs:
  - Sensor information
  - Network and configuration
  - Device logs

### Task 7: Implement the tab contents

Map the user requirements into the new page layout.

Sensor information tab:

- latest temperature
- last update and last seen times
- position snapshot
- registration state and discovery timestamps
- registration form when the device is still unregistered

Network and configuration tab:

- editable name
- editable place
- editable report interval
- desired configuration summary
- reported runtime configuration summary
- explicit pending-sync indicator
- API key regeneration action with confirmation
- existing network diagnostics

Device logs tab:

- chronological log list with timestamps and sequence numbers
- auto-refresh while the page is active
- empty state when no logs are stored yet
- loading and failure states that do not break the rest of the device page

The styling should follow the existing MUI theme and layout rather than the current full-width gray dialog treatment.

### Task 8: Add frontend polling for live refresh

Because the repository has no push infrastructure, add focused client polling.

Recommended behavior:

- Inventory page:
  - refresh device summaries on an interval
  - preserve stable ordering with unregistered devices first
- Device details page:
  - refresh device details on an interval
  - refresh logs on an interval
  - avoid overlapping requests
  - pause or slow polling when the page is hidden

Implementation note:

- Keep polling local to the pages that need it instead of introducing app-wide global polling state.

### Task 9: Extend ESP32 firmware to report runtime config and upload logs

Enhance the existing firmware loop in `main.cpp` without broad structural churn.

Implementation notes:

- Keep the current discovery and operational mode split.
- Extend the persisted device state to include the applied configuration version if versioning is introduced.
- Add a small logging abstraction so firmware log statements can be mirrored into an unsent ring buffer instead of going to `Serial` only.
- Give each buffered log line a monotonically increasing sequence number.
- Include unsent logs in the operational update payload.
- Include reported runtime configuration in the update payload.
- On successful update response:
  - apply returned configuration
  - persist the applied configuration state
  - drop only the log lines acknowledged by the backend
- On failed or unauthorized responses:
  - preserve unsent logs unless the API key is explicitly invalidated

Important compatibility rule:

- Keep the existing dummy temperature generation in place for now; this stage is about transport and visibility, not real sensor integration.

### Task 10: Update tests, builds, and documentation

Backend:

- Extend xUnit coverage for:
  - updating registered device metadata and report interval
  - incrementing desired configuration version
  - reporting pending configuration status correctly
  - storing log entries idempotently across duplicate updates
  - returning log acknowledgments
  - fetching paginated logs

Frontend:

- No existing frontend test runner is established in the repository.
- At minimum, validate with `npm run build`.
- If frontend component tests are introduced, keep them scoped to the new device-details surface rather than broad app coverage.

Firmware:

- Validate with `platformio run --environment esp32dev`.
- Verify the JSON payload size remains within safe limits for the chosen batching strategy.

Documentation:

- Update `README.md` with the device-details flow, configuration-sync behavior, and log transport summary.

## Testing And Validation Requirements

Required executable validation for implementation work:

- backend tests: `dotnet test src/backend/WaterTemperature.Api.Tests/WaterTemperature.Api.Tests.csproj -c Release --nologo`
- backend build: `dotnet build src/backend/WaterTemperature.Api/WaterTemperature.Api.csproj -c Release`
- frontend build: `cd src/frontend/app && npm install && npm run build`
- firmware build: `platformio run --environment esp32dev`

Required manual validation:

1. Discover an unregistered device and confirm it still appears first on the inventory page.
2. Open a device card and confirm navigation goes to its own page instead of opening a dialog.
3. Register the device from the page and confirm the page remains usable after registration.
4. Edit name, place, and report interval for a registered device and confirm the new values persist.
5. Confirm the page shows configuration as pending until the device posts an update with the applied runtime configuration.
6. Confirm the device receives the new report interval and reports it back.
7. Confirm new log lines appear on the log tab after device updates.
8. Confirm duplicate update retries do not create duplicate log entries.
9. Confirm the inventory page and detail page refresh without manual browser reload.
10. Confirm a stage-1 firmware build that does not send logs or runtime configuration can still post updates successfully.

## Rollout Considerations

Recommended rollout order:

1. Deploy the additive backend schema and backward-compatible API changes first.
2. Deploy the frontend route-based device details page second.
3. Deploy the updated firmware last.

Why this order is safest:

- the new backend can still accept stage-1 device updates
- the new frontend can tolerate missing logs until new firmware starts sending them
- the firmware depends on the backend understanding the extended payload and acknowledgment fields

## Rollback Considerations

- Frontend rollback is low risk if backend endpoints remain additive.
- Firmware rollback should still work if the backend keeps the extended request fields optional.
- Backend rollback is the riskiest step once the migration is applied. Keep the migration additive and avoid destructive schema changes in this stage.

## Risks And Mitigations

- Log payloads can grow too large for a single update request.
  - Mitigation: cap batch size by number of lines and total serialized bytes; rely on sequence-based acknowledgments.
- Device-side flash can fill during very long offline periods.
  - Mitigation: use a persistent segmented queue, never evict unacknowledged segments, and report append failures on Serial.
- Polling can create unnecessary load if intervals are too aggressive.
  - Mitigation: page-local polling, no overlap, and visibility-aware throttling.
- Configuration sync state can become misleading if name and place changes are treated like device runtime config.
  - Mitigation: limit sync state to device-consumable settings only.
- The single-file firmware can become harder to maintain.
  - Mitigation: add small helper functions and structs locally first; defer larger file splitting unless needed.

## Assumptions Used In This Plan

- The current stage-1 onboarding implementation is the correct baseline and should be extended rather than reworked.
- The device details page applies to both unregistered and registered devices.
- Name and place are admin-managed metadata only and do not need to be sent back to the device as runtime configuration.
- Showing the full device log means full backend history with paginated retrieval, not loading every stored line into the browser at once.
- Polling is acceptable for this stage unless a stricter real-time requirement is confirmed.

## Completion Criteria

This stage is complete when:

1. Device cards navigate to a dedicated device page with a back button.
2. The modal device detail flow is removed from normal usage.
3. The device page exposes the required three tabs.
4. Registered device name, place, and report interval can be edited after registration.
5. The backend stores desired and reported runtime configuration and can indicate pending config changes.
6. The ESP32 sends unsent log lines together with telemetry in the update request.
7. The backend stores device logs idempotently and exposes them to the frontend.
8. The frontend shows device logs and refreshes them automatically.
9. The inventory page and detail page update automatically without manual browser refresh.
10. Existing stage-1 devices remain functional during rollout.
