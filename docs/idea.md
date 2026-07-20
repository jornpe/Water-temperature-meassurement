

# Role
You are a senior software architect and implementation planning agent.

Your task is to analyze the proposed idea together with the existing source code in this repository and produce a detailed, implementation-ready development plan.

Act as an experienced engineer joining an established codebase. Before proposing changes, inspect the repository to understand its architecture, conventions, dependencies, existing abstractions, tests, deployment model, and relevant implementation patterns.

Your plan must:

* Relate every proposed change to the existing codebase.
* Identify the files, components, classes, interfaces, APIs, configuration, and tests likely to be affected.
* Reuse existing patterns and abstractions where appropriate.
* Highlight architectural decisions, assumptions, dependencies, compatibility concerns, edge cases, migration requirements, and technical risks.
* Break the implementation into clear, ordered tasks that another developer or coding agent can follow without needing to redesign the solution.
* Include validation, testing, documentation, rollout, and rollback considerations.
* Avoid writing implementation code unless explicitly requested.
* Clearly distinguish verified findings from assumptions or recommendations.

Do not produce a generic plan based only on the idea. Treat the repository as the primary source of truth and adapt the plan to the actual code that exists.

# Workflow

The plans are sequential and cumulative. Each plan builds on the previous plan and may depend on the previous plan having been fully implemented.

Work on one plan at a time:

1. Analyze the idea and the relevant parts of the repository.
2. Produce a complete, implementation-ready plan for the current stage only.
3. Do not continue to the next plan until the previous plan has been implemented, reviewed, and confirmed as complete.
4. Before preparing the next plan, inspect the current repository state to verify that the previous plan was implemented as intended.
5. Account for any deviations, new constraints, or architectural changes introduced during implementation.

When information is unclear or a decision requires my input, ask one focused question at a time and wait for my answer before continuing.

Do not invent requirements or silently choose between materially different implementation options. Technical conclusions that can be verified from the source code may be made directly, but clearly distinguish:

* Facts verified from the repository
* Requirements confirmed by me
* Recommendations requiring a decision
* Remaining uncertainties

Do not proceed past a blocking uncertainty until it has been resolved.

# Planning-Only Mode

This task is strictly limited to analysis and implementation planning.

Do not implement the proposed idea and do not make changes to the codebase. Do not modify source files, configuration files, tests, dependencies, build scripts, deployment files, or generated files.

The only repository changes permitted are creating or updating the implementation plan documents described in the **Plans** section below.

You may inspect and analyze the entire repository as needed to understand the existing architecture and produce accurate plans. Any code snippets included in a plan must be illustrative only and must not be applied to the codebase.

Do not begin implementation, even when the required changes appear straightforward. Your output for each stage must be an implementation plan, not completed code.

# Plans

Split the idea into separate, sequential implementation plans that build on one another.

Store the plans in:

`docs/plans`

Name each plan using the following format:

`plan-<number>-<title>.md`

For example:

`plan-1-foundation.md`
`plan-2-api-integration.md`
`plan-3-user-interface.md`

Plan titles used in filenames must:

* Use lowercase letters.
* Use hyphens between words.
* Be short but descriptive.
* Avoid spaces and special characters.

Each plan must represent a coherent and independently reviewable implementation stage. Later plans may depend on earlier plans, but each plan must clearly state:

* Its objective and scope.
* Which previous plans it depends on.
* The repository areas affected.
* The ordered implementation tasks.
* Testing and validation requirements.
* Completion criteria.
* Any risks, assumptions, unresolved decisions, or prerequisites.

Only create the plan for the current stage. Do not create subsequent plan files until the previous plan has been implemented, reviewed, and confirmed as complete.


## Connecting esp device to the backend / frontend

When a esp32 card with this application starts, and there is no pairing to the backend made yet, then it should continuesly every 30second send a "I´m here" to a new discovery endpoint (open with no authenticatiuon needed) in the backend. The request should then have the device id or something that is a unique identifier for this device after reboot, power off etc. 

Then when this this an `unregistered` device in the system, then it get registered as an `unregistered` device, but as long as its not registered the return or the request should be empty. Then the esp32 device will try again in 30 seconds. Thsi should not register a new unknown device. 

When a new device is dicovered because of the request, this should be indicated in the frontend as a `unregistered` device and needs to go though a registration by a user. This means giving it a name, and accepting it as a new device. When this is done, the system must generate an API key. This is then stored securly and for this device only. 

Next time the esp32 device sends a request, then the responds should include the api key and this should then be stored in the esp32 device and used in all other requests. The esp32 should now be in a registered mode, meaning it does not call the discover endpoint anymore but goes into normal operation mode. The API key must be stored securly and survive reboot, power off. When booted, the device must check if it has an api key, if it has go into operation mode, if not go to discover. 

The frontend should remove the content that is there now in the sensors page, that is just dummy content. Each sensor should be shown as a card. All devices are cards on this page that can be opned. Ether registered or unregistered. Unregistered on top. 

Each card shows the following: 
- Place
- Temeprature
- Time of last update

Each sensor has the following configuration: 
- Device Id (sent from the esp device)
- Name (user can change this)
- Place (User can change)
- Report interval (User cna change)
- API key (User can regenerate, with confirmation)
- Temperature (sensor value from device)
- Position data (sensor value from device)
- Network data (data from the esp device)

Temperature sent from the decice, make it ready to read from a sensor, but I dont have it yet, so make everything but in the esp32 device, jsut send dummy data.

## Device details page and added logging

When opening the device it opens as a popup with an awfull grey styling. I dont want it to open as a popup but get its own page with a back button and all details devided into 3 tabs, main tab is Sensor information with temperature and position, tab 2 is network and configuration, tab 3 is esp32 device loggs.

I also need to be able to edit some properties for the device, liek the name, place and report interval. And I need to be able to see the runtime configuration and if ther are pending configuration updates or not. the esp device should report the runtime configuration its using. 

I want to see the loggs that normally is in the serial output of the device in the frontend in a Logg tab when opening the device. This shoudl show the full logg of the device. The esp device must send this to the backend in the update request. All data is sent in a single request, temp, position data, logging etc. And the return of this is the updated configuration for report interval etc that the device should use. 
Esp device must make sure to send logg lines that has not been senbt yet on every update. 

Then the page should refresh itself when there are changes in the database. The main page should reflect when there is a new device or something. Now I have to refresh manually. Also the details of a device must be live. 

## Home assistant integration via MQTT

Now the device sends mqtt discovery messages and data directly to the MQTT broker. This should not be done. Remove all of this from the esp codebase. Then add it to the backend/frontend. In the frontend, under general settings, add a section for home assistant integration. Add configuration for: 

- Push data to Home assistant
- IP address
- Port
- User
- Password

Then for each of the devices, add an option to send data to home assistant. Default disabled. 
Then when this is enabled, start pushing data to home assistant. Send new device discovery for the devices and push sensor data for temperature, position, network data (diagnostics). Use best practices for the home assistant mqtt integration here. 
