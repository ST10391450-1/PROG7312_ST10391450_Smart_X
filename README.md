# Smart X

Smart X is a small IoT-style demo system for registering sensors, sending telemetry, and monitoring the results from a desktop dashboard.

The project is made up of three main components:

- **`Smart_X_API`** — ASP.NET Core Web API for sensor registration, telemetry, attachments, and location validation.
- **`Smart_X_UI`** — Avalonia desktop application for managing sensors and displaying the monitoring dashboard.
- **`Smart_X_Simulator.py`** — Python script that simulates sensor telemetry and sends it to the API.

The API runs inside Docker while the desktop application and simulator run on the host machine.

## Features

- Register sensors using:
  - MAC address / unique identifier
  - Node ID
  - Sensor category
  - Deployment location
- Prevent duplicate node IDs and MAC addresses.
- Validate supported sensor categories.
- Support Environmental, Power Consumption, and Actuator sensors.
- Send temperature, power, and actuator telemetry.
- Support individual, batch, and jagged-array telemetry.
- Perform basic telemetry calculations such as sums, deltas, and threshold checks.
- Upload, list, download, and delete sensor attachments.
- Limit attachments to 50 MB and supported file extensions.
- Validate deployment locations using a building → floor → room structure.
- Display API connection status.
- Display registered, active, and inactive sensors.
- Display current temperature, power, and actuator information.
- Track recent sensor activity.
- Simulate telemetry from the desktop application and Python simulator.
- Run the API through Docker.
- Start the complete project using a C# launcher.

## Tech Stack

| Component | Technology |
|---|---|
| API | ASP.NET Core / .NET 10 |
| Desktop UI | Avalonia UI 12 / .NET 10 |
| Simulator | Python |
| Container | Docker |
| Launcher | C# / .NET 10 |
| Communication | HTTP / JSON |

The API currently uses in-memory storage. Sensors, telemetry, and attachments are cleared when the API restarts. This is intentional for the demo.

## Project Structure

```text
Smart_X/
├── Smart_X_API/
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Dockerfile
│   └── Program.cs
│
├── Smart_X_UI/
│   ├── Controls/
│   ├── Models/
│   ├── Pages/
│   ├── Services/
│   └── Program.cs
│
├── Smart_X_Simulator.py
├── Program.cs
└── README.md
```

## Architecture

```text
┌──────────────────┐
│   Avalonia UI    │
│   Smart_X_UI     │
└────────┬─────────┘
         │ HTTP
         ▼
┌──────────────────┐
│ Docker Container │
│   Smart_X_API    │
│   ASP.NET Core   │
└────────▲─────────┘
         │ HTTP
         │
┌────────┴─────────┐
│ Python Simulator │
│ Smart_X_Simulator│
└──────────────────┘
```

The API acts as the central point of communication. The UI and simulator communicate with the API rather than directly with each other.

## Sensor Types

### Environmental

Environmental sensors send temperature readings as floating-point values.

```json
{
  "deviceId": "ENV-001",
  "sensorCategory": "Environmental",
  "value": 22.5
}
```

### Power Consumption

Power sensors send power readings as integer values.

```json
{
  "deviceId": "PWR-001",
  "sensorCategory": "Power Consumption",
  "value": 145
}
```

### Actuator

Actuator sensors send an on/off state using a boolean value.

```json
{
  "deviceId": "ACT-001",
  "sensorCategory": "Actuator",
  "value": true
}
```

## API

The API runs inside Docker and is available at:

```text
http://localhost:8080
```

### Sensor Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/Sensors` | Get all registered sensors |
| GET | `/api/Sensors/{nodeId}` | Get a specific sensor |
| POST | `/api/Sensors` | Register a sensor |
| DELETE | `/api/Sensors/{nodeId}` | Delete a sensor |

### Telemetry Endpoints

```text
POST /api/Telemetry/temperature
POST /api/Telemetry/power
POST /api/Telemetry/actuator
```

The API performs server-side validation for sensor registrations, telemetry, locations, and attachments.

## Docker

Build the API image:

```bash
docker build -t smart-x-api:latest ./Smart_X_API
```

Run the API:

```bash
docker run -d --name smart-x-api -p 8080:8080 smart-x-api:latest
```

Remove an existing container:

```bash
docker rm -f smart-x-api
```

Only the API is containerized. The Avalonia application and Python simulator run on the host machine.

## Python Simulator

`Smart_X_Simulator.py` generates simulated sensor readings and sends them to the Docker-hosted API.

It can be used to test:

- Sensor telemetry
- Multiple sensor types
- Repeated readings
- API connectivity
- Dashboard updates

Run it with:

```bash
python Smart_X_Simulator.py
```

## Project Launcher

The root `Program.cs` provides a C# launcher for the complete project.

The launcher:

1. Builds the API.
2. Builds the Avalonia UI.
3. Builds the Docker image.
4. Removes an existing API container.
5. Starts the Docker API.
6. Starts the Avalonia application.
7. Waits for the user before starting the Python simulator.

This allows the project to be started without manually opening several terminals.

## Running the Project

### Requirements

- .NET 10 SDK
- Docker
- Python 3

Docker must be running before starting the project.

### Recommended

Run the launcher from the project root:

```bash
dotnet run
```

The launcher builds the projects, starts the API in Docker, starts the desktop application, and then asks whether the Python simulator should be started.

### Manual

Build the projects:

```bash
dotnet build Smart_X_API/Smart_X_API.csproj
dotnet build Smart_X_UI/Smart_X_UI.csproj
```

Build and start the API:

```bash
docker build -t smart-x-api:latest ./Smart_X_API
docker rm -f smart-x-api
docker run -d --name smart-x-api -p 8080:8080 smart-x-api:latest
```

Start the desktop application:

```bash
dotnet run --project Smart_X_UI
```

Start the simulator:

```bash
python Smart_X_Simulator.py
```

## Application Flow

```text
Start Launcher
      ↓
Build API and UI
      ↓
Build Docker Image
      ↓
Start API Container
      ↓
Start Avalonia UI
      ↓
Register Sensors
      ↓
Start Simulator
      ↓
Send Telemetry
      ↓
API Validates and Processes Data
      ↓
Dashboard Displays Results
```

## Error Handling

The API validates incoming requests and returns appropriate HTTP responses for invalid operations.

Examples include:

- `400 Bad Request` — Invalid input.
- `404 Not Found` — Sensor or resource does not exist.
- `409 Conflict` — Duplicate sensor registration.
- `413 Payload Too Large` — Attachment exceeds the size limit.
- `415 Unsupported Media Type` — Unsupported attachment type.

## Data Storage

Smart X currently uses in-memory collections instead of a database.

This means data is available only while the API is running. Restarting the API clears registered sensors, telemetry, and attachments.

This is intentional for the project and keeps the demo focused on API design, desktop integration, Docker deployment, and telemetry processing.

A production version could replace the in-memory storage with a persistent database.

## Purpose

Smart X demonstrates the integration of several technologies into a single application:

- C# and .NET
- ASP.NET Core REST APIs
- Avalonia desktop development
- Docker
- Python
- HTTP and JSON communication
- Object-oriented programming
- Input validation
- Telemetry processing
