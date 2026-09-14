# Smart X

Smart X is a small IoT-style demo system for registering sensors, sending telemetry, and monitoring the results from a desktop dashboard.

The project is made up of three main components:

- **`Smart_X_API`** — An ASP.NET Core Web API that handles sensor registration, telemetry, file attachments, and deployment-location validation.
- **`Smart_X_UI`** — An Avalonia desktop application that connects to the API, manages sensors and attachments, and provides the monitoring dashboard.
- **`Smart_X_Simulator.py`** — A Python script that simulates sensor telemetry and sends it to the API.

- Start Application by downloading, extracting then running 
# dotnet RUN.cs# 
in terminal under appropriate directory 

## Features

- Register sensors using:
  - MAC address
  - Node ID
  - Sensor category
  - Deployment location
- Reject duplicate node IDs and MAC addresses.
- Reject unsupported sensor categories.
- Upload, list, download, and delete file attachments for individual sensors.
- Limit attachments to **50 MB**.
- Restrict attachments to an allow-list of supported file extensions.
- Accept telemetry for:
  - Temperature (`float`)
  - Power draw (`int`)
  - Switch state (`bool`)
- Support individual telemetry submissions as well as batch and jagged-array ingestion.
- Calculate basic telemetry results:
  - Sum of readings
  - Delta between two readings
  - Threshold checks
- Validate deployment locations using a tree structure such as **building → floor → room**.
- Provide a desktop dashboard with live API connection status.
- Display registered, active, and inactive sensors.
- Display current temperature, power, and actuator information.
- Track recent sensor activity.
- Select a sensor and simulate sending telemetry from the desktop application.
- Run the API inside a Docker container.
- Start the complete project using a C# launcher.

## Tech Stack

| Component | Stack |
|---|---|
| API | ASP.NET Core (.NET 10), in-memory storage |
| Desktop UI | Avalonia UI 12 (.NET 10), Fluent theme |
| Simulator | Python |
| Container | Docker (API only) |
| Launcher | C# / .NET 10 |

> The API stores everything in memory — sensors, attachments, and telemetry all reset when the process restarts. This is intentional for a demo/dev setup, not a production data layer.

## Project Structure

```text
Smart_X/
├── Smart_X_API/                 # ASP.NET Core Web API
│   ├── Controllers/             # Locations, Sensors, Telemetry endpoints
│   ├── Models/                  # Request/response models
│   ├── Services/                # Telemetry processing and validation
│   ├── Dockerfile               # API Docker image
│   └── Program.cs
│
├── Smart_X_UI/                  # Avalonia desktop application
│   ├── Controls/                # Custom UI controls
│   ├── Pages/                   # Dashboard and management screens
│   ├── Models/                  # UI-side data models
│   ├── Services/                # API and UI services
│   └── Program.cs
│
├── Smart_X_Simulator.py         # Python sensor simulator
├── Program.cs                   # Project launcher
└── README.md
