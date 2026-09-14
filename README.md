# Smart X

**PROG7312 – Part 1**

**Student:** Thomas Dennis
**Student Number:** ST10391450

---

## Overview

Smart X is a sensor management system developed for the PROG7312 module.

The project provides a central REST API for managing sensors, a desktop user interface for interacting with the system, and a Python-based sensor simulator for generating simulated sensor activity.

The application demonstrates the use of C#, ASP.NET Core, Avalonia UI, Python, REST APIs, Docker, object-oriented programming, file handling, validation, and client-server communication.

---

# Technology Stack

| Component           | Technology           |
| ------------------- | -------------------- |
| Backend             | C#                   |
| REST API            | ASP.NET Core         |
| Desktop Application | Avalonia UI          |
| UI Markup           | XAML                 |
| Sensor Simulator    | Python               |
| API Communication   | HTTP / REST          |
| Data Format         | JSON                 |
| Containerisation    | Docker               |
| Version Control     | Git / GitHub         |
| API Testing         | cURL / HTTP Requests |

---

# Project Breakdown

## Smart X API

The **Smart X API** is the backend of the application.

It provides the REST endpoints used by the desktop application and Python sensor simulator.

The API handles:

* Sensor registration
* Sensor retrieval
* Sensor deletion
* Sensor validation
* Duplicate sensor detection
* Sensor attachment management
* File uploads
* File downloads
* File deletion
* HTTP responses
* Request validation

The API is hosted locally on:

```text
http://localhost:8080
```

---

## Smart X UI

The **Smart X UI** is the desktop client for the system.

It is built using **C# and Avalonia UI**.

The interface allows users to:

* Register sensors
* View registered sensors
* Select sensors
* View sensor information
* Simulate sensor payloads
* Manage sensor attachments
* Interact with the Smart X API

The UI communicates with the backend using HTTP requests.

---

## Python Sensor Simulator

The project includes a Python sensor simulator that represents smart sensors without requiring physical hardware.

The simulator communicates with the Smart X API and can:

* Discover registered sensors
* Generate simulated sensor activity
* Send requests to the API
* Simulate telemetry
* Provide repeatable testing of the API

The simulator communicates with:

```text
http://localhost:8080
```

---

## Docker

Docker is used to containerise the ASP.NET Core API.

The API can therefore be run inside a Docker container while exposing port `8080` to the host machine.

Build the Docker image:

```bash
docker build -t smart-x-api:latest .
```

Run the container:

```bash
docker run -d --name smart-x-api -p 8080:8080 smart-x-api:latest
```

---

# System Architecture

```text
                         Smart X
                            │
          ┌─────────────────┼─────────────────┐
          │                 │                 │
          ▼                 ▼                 ▼
     Avalonia UI      Python Simulator      Docker
          │                 │                 │
          │                 │                 │
          └────────── HTTP / REST ────────────┘
                            │
                            ▼
                    ASP.NET Core API
                            │
              ┌─────────────┴─────────────┐
              │                           │
              ▼                           ▼
       Sensor Management          Attachment Management
```

---

# Project Structure

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
│   └── ...
│
├── RUN.cs
│
└── README.md
```

---

# Sensor Management

Sensors contain information such as:

* MAC address / unique identifier
* Deployment location
* Node ID
* Sensor category

The supported sensor categories are:

```text
Environmental
Power Consumption
Actuator
```

The API validates sensor registrations before adding them to the system.

Duplicate Node IDs and duplicate MAC addresses are rejected.

---

# Payload Simulation

The Smart X UI provides a sensor payload management interface.

The general workflow is:

1. Start Smart X.
2. Register a sensor.
3. Select the registered sensor.
4. Start payload simulation.
5. The Python simulator communicates with the API.
6. Simulated sensor activity is generated.

This allows the system to be tested without physical sensor hardware.

---

# Attachment Management

Smart X supports attachments associated with individual sensors.

Supported file extensions include:

```text
.txt
.log
.cfg
.conf
.ini
.json
.xml
.yaml
.yml
.csv
.jpg
.jpeg
.png
.webp
.pdf
```

The maximum attachment size is:

```text
50 MB
```

Attachments can be:

* Uploaded
* Listed
* Downloaded
* Deleted

The API validates the uploaded file before storing it.

---

# REST API

## Base URL

```text
http://localhost:8080
```

## Sensor Route

```text
/api/Sensors
```

---

## Sensor Endpoints

| Method   | Endpoint                | Description                      |
| -------- | ----------------------- | -------------------------------- |
| `GET`    | `/api/Sensors`          | Retrieves all registered sensors |
| `GET`    | `/api/Sensors/{nodeId}` | Retrieves a specific sensor      |
| `POST`   | `/api/Sensors`          | Registers a new sensor           |
| `DELETE` | `/api/Sensors/{nodeId}` | Deletes a sensor                 |

---

## Attachment Endpoints

| Method   | Endpoint                                           | Description                        |
| -------- | -------------------------------------------------- | ---------------------------------- |
| `GET`    | `/api/Sensors/{nodeId}/attachments`                | Retrieves attachments for a sensor |
| `POST`   | `/api/Sensors/{nodeId}/attachments`                | Uploads an attachment              |
| `GET`    | `/api/Sensors/{nodeId}/attachments/{attachmentId}` | Downloads an attachment            |
| `DELETE` | `/api/Sensors/{nodeId}/attachments/{attachmentId}` | Deletes an attachment              |

`attachmentId` is a GUID identifying the attachment.

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

# Validation

The API performs server-side validation when sensors are registered.

The following information is required:

* MAC address / unique identifier
* Location
* Node ID
* Category

The API also checks for duplicate:

* Node IDs
* MAC addresses / unique identifiers

Invalid categories are rejected.

Valid categories are:

```text
Environmental
Power Consumption
Actuator
```

---

# HTTP Status Codes

| Status Code       | Description                                  |
| ----------------- | -------------------------------------------- |
| `200 OK`          | Request completed successfully               |
| `201 Created`     | Resource was successfully created            |
| `204 No Content`  | Resource was successfully deleted            |
| `400 Bad Request` | Request failed validation                    |
| `404 Not Found`   | Requested resource does not exist            |
| `409 Conflict`    | Resource conflicts with an existing resource |

---

# File Validation

Uploaded files are validated before being stored.

The API checks:

* Whether a file was supplied
* Whether the file is empty
* File size
* File extension
* Whether the associated sensor exists

The maximum permitted file size is:

```text
50 MB
```

Filenames are also sanitised before being stored.

---

# Object-Oriented Design

The C# implementation uses object-oriented programming principles.

The application separates responsibilities between different components, including:

* Models
* Controllers
* Services
* UI components
* API communication

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

---

## Requirements

Before running Smart X, ensure the following are installed:

* .NET SDK
* Python 3
* Docker Desktop
* Git

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
  │
  ├── Smart X API
  │
  ├── Docker
  │
  ├── Smart X UI
  │
  └── Python Sensor Simulator
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
dotnet run RUN.cs
```

---

# Testing

The system can be tested through several components.

## API Testing

The REST API can be tested using:

* cURL
* Postman
* Python `requests`
* Smart X UI

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

# Example Workflow

```text
Start Application
       │
       ▼
dotnet run RUN.cs
       │
       ▼
Register Sensor
       │
       ▼
Sensor Added to API
       │
       ▼
Sensor Displayed in UI
       │
       ▼
Start Python Simulator
       │
       ▼
Simulator Discovers Sensor
       │
       ▼
Simulated Sensor Activity
       │
       ▼
API Processes Requests
       │
       ▼
UI Displays Sensor Information
```

---

# Technologies Demonstrated

This project demonstrates practical use of:

* C#
* ASP.NET Core
* Avalonia UI
* XAML
* Python
* REST APIs
* HTTP communication
* JSON
* Docker
* File handling
* Input validation
* Object-oriented programming
* Client-server architecture
* Git and GitHub

