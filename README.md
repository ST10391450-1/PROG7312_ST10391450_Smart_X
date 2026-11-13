# Smart X

**PROG7312 – Part 1**

**Student:** Thomas Dennis  
**Student Number:** ST10391450

---

## Overview

Smart X is a sensor management and network monitoring system.

The project provides a central REST API for managing sensors, collecting telemetry, processing device commands, and exposing network information. An Avalonia-based desktop application provides an interface for interacting with the system, monitoring connected devices, visualising physical and virtual network topologies, and performing network analysis. A Python-based simulator generates sensor activity and simulates network nodes, connections, and changing device statuses.

The application demonstrates the use of C#, ASP.NET Core, Avalonia UI, Python, REST APIs, Docker, object-oriented programming, file handling, input validation, client-server communication, device command handling, telemetry processing, network graph visualisation, and graph-based algorithms such as Minimum Spanning Tree and shortest-path analysis.

By combining sensor management, simulated telemetry, and network topology monitoring, Smart X demonstrates how software components can work together to manage connected devices, represent network relationships, monitor connectivity, and analyse network structures.

---

# Technology Stack

| Component | Technology |
|---|---|
| Backend Development | C# |
| Backend Framework | ASP.NET Core |
| Desktop Application | Avalonia UI |
| UI Markup | XAML |
| Sensor Simulator | Python |
| API Communication | HTTP / REST |
| Data Exchange Format | JSON |
| Network Visualisation | Avalonia Drawing / Canvas |
| Network Analysis | Graph Algorithms (MST and Shortest Path) |
| Asynchronous Processing | .NET Background Services / Queues |
| Containerisation | Docker |
| Version Control | Git / GitHub |
| API Testing | cURL / HTTP Requests |
| Development Environment | Visual Studio / Visual Studio Code |
| Configuration and Data Storage | JSON Files |

---

# Project Breakdown

## Smart X API

The **Smart X API** is the backend of the application.

It handles:

- Sensor registration
- Sensor retrieval
- Sensor deletion
- Sensor validation
- Duplicate sensor detection
- Sensor attachment management
- File uploads
- File downloads
- File deletion
- Telemetry submission and processing
- Device command requests
- Command history and command results
- Device insights, alerts, and disconnected-device information
- Priority alert queue operations
- Physical network topology retrieval
- Virtual network simulation
- Network node and connection information
- Minimum Spanning Tree calculations
- Shortest-path analysis
- HTTP responses
- Request validation

The API exposes the following network-related endpoints:

| Method | Endpoint | Functionality |
|---|---|---|
| `GET` | `/api/NetworkTopology` | Retrieves physical network topology information |
| `GET` | `/api/NetworkSimulation` | Retrieves virtual network simulation data |
| `POST` | `/api/Telemetry/measurement` | Submits sensor measurement data |
| `POST` | `/api/Telemetry/switch` | Submits device switch or relay commands |

The API is hosted locally on:

```text
http://localhost:8080
```

The actual base URL must match the address configured in the running API and the UI's API client.

---

## Smart X UI

The **Smart X UI** is the desktop client for the system.

It is built using **C# and Avalonia UI**, providing a graphical interface for managing sensors, monitoring device activity, interacting with the API, and analysing network information.

The interface allows users to:

- Register sensors
- View registered sensors
- Select sensors
- View sensor information
- Simulate sensor payloads
- Manage sensor attachments
- Interact with the Smart X API
- Monitor device state and operational insights
- Search and filter devices
- Issue supported manual device commands
- Review and filter command history
- Export command history to CSV
- Check whether a command undo operation is available
- Process the next alert from the priority alert queue
- View recently processed alerts
- Monitor physical network topology
- View registered, connected, and disconnected network nodes
- Inspect physical network connections
- View virtual network simulation statistics
- Visualise virtual network nodes and connections
- Monitor simulated online and offline device states
- Calculate and display a Minimum Spanning Tree (MST)
- View selected MST connections and total network cost
- Identify network components
- Calculate shortest paths between selected network nodes

The UI communicates with the backend using HTTP requests. The **Real-Time Command Stream** page periodically refreshes sensor, command, recommendation, alert, and disconnected-device information.

The **Network Topology** page provides a graphical representation of physical and simulated network structures, allowing users to inspect node relationships and evaluate network connectivity through graph-based analysis.

---

## Python Sensor Simulator

The project includes a Python sensor simulator that represents smart sensors without requiring physical hardware.

The simulator communicates with the Smart X API and can:

- Discover registered sensors
- Generate simulated sensor activity
- Send requests to the API
- Simulate telemetry
- Provide repeatable testing of the API

The simulator communicates with:

```text
http://localhost:8080
```

---

## Docker Deployment

The Smart X API can be containerised using Docker, allowing it to run in an isolated environment with a consistent runtime configuration.

Build the Docker image from the API project directory:

```bash
docker build -t smart-x-api:latest .
```

Run the container:

```bash
docker run -d --name smart-x-api -p 8080:8080 smart-x-api:latest
```

The API is then accessible through:

```text
http://localhost:8080
```

The port mapping must match the port configured by the ASP.NET Core application inside the container.

---

# System Architecture

Smart X uses a client-server architecture consisting of an ASP.NET Core REST API, an Avalonia desktop application, and a Python-based sensor simulator.

The API acts as the central communication point for sensor management, telemetry, device commands, attachments, operational insights, and network information. The desktop application communicates with the API to provide users with management and monitoring functionality, while the Python simulator generates simulated sensor readings and network activity.

```text
                              Smart X
                                 |
             +-------------------+-------------------+
             |                   |                   |
             v                   v                   v
        Avalonia UI       Python Simulator          Docker
             |                   |             (API Container)
             |                   |                   |
             +--------- HTTP / REST ----------------+
                                 |
                                 v
                         ASP.NET Core API
                                 |
              +------------------+------------------+
              |                  |                  |
              v                  v                  v
       Sensor Management   Attachment Management   Commands
              |                  |                  |
              v                  v                  v
         Telemetry          File Handling       Device Insights
                                                     |
                                                     v
                                              Priority Alert Queue
                                                     |
                                                     v
                                           Operational Information

                         Network Functionality
                                 |
              +------------------+------------------+
              |                                     |
              v                                     v
       Physical Topology                    Virtual Simulation
              |                                     |
              +------------------+------------------+
                                 |
                                 v
                       Network Graph Analysis
                                 |
                    +------------+------------+
                    |                         |
                    v                         v
             Minimum Spanning Tree      Shortest Path
```

---

# Project Structure

The project is organised into three primary components: the ASP.NET Core API, Avalonia desktop application, and Python sensor simulator.

```text
Smart_X/
│
├── Smart_X_API/
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Program.cs
│   ├── Dockerfile
│   └── ...
│
├── Smart_X_UI/
│   ├── Models/
│   ├── Pages/
│   ├── Services/
│   ├── Views/
│   └── ...
│
├── SensorSimulator/
│   ├── sensor_simulator.py
│   ├── sensors.json
│   ├── network_snapshot.json
│   └── ...
│
├── RUN.cs
│
└── README.md
```

---

# Sensor Management

Sensors represent the devices registered and managed by the Smart X system.

Each sensor contains identifying and configuration information, including:

- MAC address / unique hardware identifier
- Deployment location
- Node ID
- Sensor category
- Enabled or disabled state

The supported sensor categories are:

```text
Environmental
Power Consumption
Actuator
```

The API validates sensor registrations before adding them to the system.

Validation includes:

- Required registration information
- Valid sensor identifiers
- Duplicate Node ID detection
- Duplicate MAC address detection
- Sensor category validation

Duplicate Node IDs and duplicate MAC addresses are rejected to prevent conflicting device registrations.

---

# Payload Simulation

The Smart X UI provides a sensor payload management interface for generating and monitoring simulated sensor activity.

This functionality allows the system to be tested without requiring physical sensor hardware.

The general workflow is:

1. Start the Smart X API.
2. Start the Smart X desktop application.
3. Register a sensor through the UI.
4. Select the registered sensor.
5. Configure and start the Python sensor simulator.
6. The simulator generates readings for enabled sensors.
7. The Python simulator submits telemetry to the API using HTTP requests.
8. The API receives and processes the submitted measurements.
9. The desktop application retrieves and displays the available sensor information.

The simulator supports multiple measurement categories, including temperature, humidity, power consumption, air quality, pressure, light, motion, and relay activity.

This provides a controlled environment for testing sensor communication, telemetry handling, and device behaviour.

---

# Real-Time Command Stream

The **Real-Time Command Stream** page provides a dashboard for monitoring sensors and interacting with supported device commands.

The page includes:

- Dashboard metrics and device insights
- Device search and filtering
- Filtering by operational category and device or alert state
- Device monitoring and manual control actions
- Command history with filtering
- CSV export of command history
- Periodic API refresh
- An Undo Command panel
- A Priority Alert Queue panel

The page retrieves sensor and command information from the API, alongside recommendations, active alerts, and disconnected-device information.

## Device Commands

Command names supported by the backend depend on the configured command validation. Command examples used during development include:

```text
RelayOn
RelayOff
PumpStart
PumpStop
ManualOverride
EnableSensor
DisableSensor
```

The API must validate every requested command and confirm that the target device supports the operation. The UI should only present actions that are supported by the running backend.

## Command History

The command history section allows users to review previous command requests and filter the displayed records. The UI also provides a CSV export option for the displayed command history.

## Undo Command

The dashboard includes an Undo Command panel. It checks whether an undo operation is available and lets the user request the latest supported undo operation through the API.

Undo currently applies to the API's recorded state. It should not be considered confirmation that a physical actuator has returned to its previous state.

## Priority Alert Queue

The dashboard includes a Priority Alert Queue panel that:

- Displays the pending alert count
- Requests the next alert for processing
- Displays recently processed alerts

The queue service uses a priority queue for alerts and a queue for telemetry items. Alert priority ordering is configured so that higher numeric priority values are intended to be processed first.

The current queue implementation stores its data in memory. Pending queue data may be lost when the API process restarts.

The `GET /api/AlertQueue/next` endpoint dequeues an alert. It therefore processes and removes the returned alert rather than simply listing pending alerts.

---

# REST API

## Base URL

```text
http://localhost:8080
```

The base URL shown here is the local development address. Use the base URL configured for the API and UI when running the application in another environment.

## Sensor Route

```text
/api/Sensors
```

### Sensor Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/Sensors` | Retrieves all registered sensors |
| `GET` | `/api/Sensors/{nodeId}` | Retrieves a specific sensor |
| `POST` | `/api/Sensors` | Registers a new sensor |
| `DELETE` | `/api/Sensors/{nodeId}` | Deletes a sensor |

---

## Attachment Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/Sensors/{nodeId}/attachments` | Retrieves attachments for a sensor |
| `POST` | `/api/Sensors/{nodeId}/attachments` | Uploads an attachment |
| `GET` | `/api/Sensors/{nodeId}/attachments/{attachmentId}` | Downloads an attachment |
| `DELETE` | `/api/Sensors/{nodeId}/attachments/{attachmentId}` | Deletes an attachment |

`attachmentId` is a GUID identifying the attachment.

---

## Command and Insight Endpoints

The Real-Time Command Stream frontend calls the following routes in the current implementation:

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/Commands` | Retrieves command history |
| `GET` | `/api/Insights/recommendations` | Retrieves device recommendations |
| `GET` | `/api/Insights/alerts` | Retrieves active alerts |
| `GET` | `/api/Insights/disconnected` | Retrieves disconnected-device information |
| `GET` | `/api/Commands/undo/available` | Checks whether an undo operation is available |
| `POST` | `/api/Commands/undo` | Requests an undo of the latest supported command |

---

## Priority Alert Queue Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/AlertQueue` | Adds an alert to the queue |
| `GET` | `/api/AlertQueue/next` | Removes and returns the next alert, or returns `204 No Content` when no alert is available |
| `GET` | `/api/AlertQueue/status` | Returns pending alert and telemetry counts |

These command, insight, undo, and alert-queue endpoints must exist in the running API for the corresponding UI features to work. Check the API controller routes if the deployed implementation differs.

---

# Example API Requests

## Register a Sensor

```http
POST /api/Sensors
Content-Type: application/json
```

Example request body:

```json
{
  "macAddress": "AA:BB:CC:DD:EE:FF",
  "location": "Room 101",
  "nodeId": "NODE-001",
  "category": "Environmental"
}
```

---

## Retrieve All Sensors

```bash
curl http://localhost:8080/api/Sensors
```

---

## Retrieve a Specific Sensor

```bash
curl http://localhost:8080/api/Sensors/NODE-001
```

---

## Delete a Sensor

```bash
curl -X DELETE http://localhost:8080/api/Sensors/NODE-001
```

---

## Check Alert Queue Status

```bash
curl http://localhost:8080/api/AlertQueue/status
```

---

## Process the Next Alert

```bash
curl http://localhost:8080/api/AlertQueue/next
```

If the queue is empty, the endpoint returns `204 No Content`. Calling this endpoint removes the next alert from the queue.

---

## Check Undo Availability

```bash
curl http://localhost:8080/api/Commands/undo/available
```

---

## Request Command Undo

```bash
curl -X POST http://localhost:8080/api/Commands/undo
```

The actual response depends on the API implementation and whether an undo operation is available.

---

# Validation

The API performs server-side validation when sensors are registered.

The following information is required:

- MAC address / unique identifier
- Location
- Node ID
- Category

The API also checks for duplicate:

- Node IDs
- MAC addresses / unique identifiers

Invalid categories are rejected.

Valid categories are:

```text
Environmental
Power Consumption
Actuator
```

Command requests must also be validated by the API. The target sensor or device must exist, and the requested command must be supported for that device.

---

# HTTP Status Codes

| Status Code | Description |
|---|---|
| `200 OK` | Request completed successfully |
| `201 Created` | Resource was successfully created |
| `204 No Content` | Request completed without a response body, or a resource was successfully deleted |
| `400 Bad Request` | Request failed validation |
| `404 Not Found` | Requested resource does not exist |
| `409 Conflict` | Resource conflicts with an existing resource |

Specific endpoints may return additional status codes depending on their implementation.

---

# File Validation

Uploaded files are validated before being stored.

The API checks:

- Whether a file was supplied
- Whether the file is empty
- File size
- File extension
- Whether the associated sensor exists

The maximum permitted file size is:

```text
50 MB
```

Filenames are also sanitised before being stored.

---

# Object-Oriented Design

The C# implementation uses object-oriented programming principles.

The application separates responsibilities between different components, including:

- Models
- Controllers
- Services
- UI components
- API communication

Models represent application data, controllers handle HTTP requests, services provide supporting functionality, and the Avalonia UI provides the client-side interface.

This separation helps keep the application organised and maintainable.

---

# How to Run

Smart X includes a root `RUN.cs` launcher.

The complete application is started from the terminal using:

```bash
dotnet RUN.cs
```

This is the primary method for running the project.

## Requirements

Before running Smart X, ensure the following are installed:

- .NET SDK
- Python 3
- Docker Desktop
- Git

The Python environment must also have the required Python dependencies installed.

For the sensor simulator, install `requests` with:

```bash
pip install requests
```

---

## Start Smart X

Open a terminal in the root Smart X project directory.

Run:

```bash
dotnet RUN.cs
```

The `RUN.cs` launcher starts the required project components.

Follow the instructions displayed in the terminal.

When prompted to start the Python sensor simulator, press:

```text
ENTER
```

The simulator will then begin communicating with the Smart X API.

---

# Application Startup Flow

The complete application is started from the project root:

```bash
dotnet RUN.cs
```

The startup process is:

```text
RUN.cs
  |
  +-- Smart X API
  |
  +-- Docker
  |
  +-- Smart X UI
  |
  +-- Python Sensor Simulator
```

The API is available on:

```text
http://localhost:8080
```

---

# Docker API

The API can also be built manually using Docker.

Build the image:

```bash
docker build -t smart-x-api:latest .
```

Start the container:

```bash
docker run -d --name smart-x-api -p 8080:8080 smart-x-api:latest
```

Check that the container is running:

```bash
docker ps
```

View the container output:

```bash
docker logs smart-x-api
```

Stop the container:

```bash
docker stop smart-x-api
```

Remove the container:

```bash
docker rm smart-x-api
```

The normal project startup method remains:

```bash
dotnet RUN.cs
```

---

# Testing

The system can be tested through several components.

## API Testing

The REST API can be tested using:

- cURL
- Postman
- Python `requests`
- Smart X UI

Example:

```bash
curl http://localhost:8080/api/Sensors
```

---

## Sensor Testing

A typical test consists of:

1. Starting the application using `RUN.cs`.
2. Registering a sensor.
3. Confirming that the sensor appears in the UI.
4. Starting the Python simulator.
5. Confirming that the simulator can communicate with the API.
6. Testing sensor retrieval.
7. Testing attachment management.
8. Testing sensor deletion.

---

## Command and Alert Testing

A typical command and alert test consists of:

1. Starting the API and UI.
2. Confirming that registered devices appear in the Real-Time Command Stream.
3. Issuing a supported manual command.
4. Checking that the command appears in command history.
5. Checking undo availability and testing the undo action when available.
6. Enqueuing an alert using `POST /api/AlertQueue`.
7. Checking the pending counts using `GET /api/AlertQueue/status`.
8. Processing an alert using `GET /api/AlertQueue/next`.
9. Confirming that the processed alert appears in the UI.

---

# Example Workflow

```text
Start Application
       |
       v
dotnet RUN.cs
       |
       v
Register Sensor
       |
       v
Sensor Added to API
       |
       v
Sensor Displayed in UI
       |
       v
Start Python Simulator
       |
       v
Simulator Discovers Sensor
       |
       v
Simulated Sensor Activity
       |
       v
Monitor Devices and Insights
       |
       v
Issue Manual Command
       |
       v
Review Command History
       |
       v
Process Priority Alert
```

---

# Current Limitations

- Command undo depends on the prior state recorded by the API and does not guarantee reversal of a physical hardware action.
- In-memory command, undo, telemetry, and alert-queue state may be lost when the API process restarts.
- The alert queue UI displays the pending count and recently processed alerts. Listing all pending alerts without removing them would require a separate read-only endpoint.
- A successful HTTP response does not by itself prove that a physical sensor or actuator completed an operation. Real hardware integration and device acknowledgement must be verified separately.
- The API base URL and routes used by the UI must match the running backend configuration.

---

# Technologies Demonstrated

This project demonstrates practical use of:

- C#
- ASP.NET Core
- Avalonia UI
- XAML
- Python
- REST APIs
- HTTP communication
- JSON
- Docker
- File handling
- Input validation
- Object-oriented programming
- Client-server architecture
- Device command handling
- Priority queues and telemetry queues
- Git and GitHub

---

*README generated with assistance from ChatGPT. GitHub repository link provided to assist with README creation.*
