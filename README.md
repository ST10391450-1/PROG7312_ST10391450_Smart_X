# Smart X

Smart X is a small IoT-style demo system for registering sensors, sending telemetry, and monitoring the results from a desktop dashboard.

The project is made up of two applications:

- **`Smart_X_API`** — An ASP.NET Core Web API that handles sensor registration, telemetry, file attachments, and deployment-location validation.
- **`Smart_X_UI`** — An Avalonia desktop application that connects to the API, manages sensors and attachments, and allows telemetry payloads to be simulated.

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
- Select a sensor and simulate sending telemetry from the desktop application.

## Tech Stack

| Component | Stack |
|---|---|
| API | ASP.NET Core (.NET 10), in-memory storage |
| Desktop UI | Avalonia UI 12 (.NET 10), Fluent theme |
| Container | Docker (API only) |

> The API stores everything in memory — sensors, attachments, and telemetry all reset when the process restarts. This is intentional for a demo/dev setup, not a production data layer.

## Project Structure

```text
Smart_X/
├── Smart_X_API/                 # ASP.NET Core Web API
│   ├── Controllers/             # Locations, Sensors, Telemetry endpoints
│   ├── Models/                  # Request/response DTOs
│   ├── Services/                # Telemetry processing, location validation
│   ├── Dockerfile
│   └── Program.cs
└── Smart_X_UI/                  # Avalonia desktop app
    ├── Controls/                # Custom title bar
    ├── Pages/                   # Sensor & payload management screen
    ├── Models/                  # UI-side data models
    └── Program.cs
