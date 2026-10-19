
import time
import random
import requests

from datetime import datetime, timezone


API_URL = "http://localhost:8080"

DISCOVERY_INTERVAL = 2
TELEMETRY_INTERVAL = 2
COMMAND_POLL_INTERVAL = 2
UNIT_REFRESH_INTERVAL = 60


class SensorSimulator:
    def __init__(self):
        self.session = requests.Session()
        self.sensors = {}
        self.detected_units = {}
        self.last_unit_refresh = {}
        self.actuator_states = {}
        self.running = True

    @staticmethod
    def normalize(value):
        return (
            str(value or "")
            .strip()
            .lower()
            .replace("_", " ")
            .replace("-", " ")
            .replace("/", " ")
        )

    @staticmethod
    def timestamp():
        return datetime.now(timezone.utc).isoformat()

    def get_registered_sensors(self):
        try:
            response = self.session.get(
                f"{API_URL}/api/Sensors",
                timeout=10
            )
            response.raise_for_status()

            data = response.json()
            return data if isinstance(data, list) else []

        except (requests.RequestException, ValueError) as error:
            print(f"[DISCOVERY ERROR] {error}")
            return []

    def get_device_id(self, sensor):
        return str(
            sensor.get("macAddress")
            or sensor.get("nodeId")
            or sensor.get("id")
            or ""
        ).strip()

    def get_category(self, sensor):
        return str(sensor.get("category") or "").strip()

    def discover_sensors(self):
        registered = self.get_registered_sensors()
        discovered = {}

        for sensor in registered:
            device_id = self.get_device_id(sensor)

            if not device_id:
                print(f"[SKIPPED] Sensor has no device ID: {sensor}")
                continue

            key = device_id.lower()
            discovered[key] = sensor

            if key not in self.sensors:
                print(
                    f"[CONNECTED] {device_id} | "
                    f"Category: {self.get_category(sensor) or 'Unknown'}"
                )

            self.sensors[key] = sensor

            if self.normalize(self.get_category(sensor)) == "actuator":
                self.actuator_states.setdefault(device_id, False)

            self.detect_unit(sensor)

        for key in list(self.sensors):
            if key not in discovered:
                sensor = self.sensors.pop(key)
                device_id = self.get_device_id(sensor)

                self.detected_units.pop(key, None)
                self.last_unit_refresh.pop(key, None)

                print(f"[DISCONNECTED] {device_id}")

                if not any(
                    self.get_device_id(item) == device_id
                    for item in self.sensors.values()
                ):
                    self.actuator_states.pop(device_id, None)

    def detect_unit(self, sensor, force=False):
        device_id = self.get_device_id(sensor)
        key = device_id.lower()
        now = time.time()

        if (
            not force
            and key in self.detected_units
            and now - self.last_unit_refresh.get(key, 0)
            < UNIT_REFRESH_INTERVAL
        ):
            return self.detected_units[key]

        category = self.get_category(sensor)

        if not category:
            result = {
                "unit": None,
                "type": "unknown",
                "determinedBy": "Unrecognised category",
                "requiresConfiguration": True
            }
            self.detected_units[key] = result
            self.last_unit_refresh[key] = now
            return result

        try:
            response = self.session.post(
                f"{API_URL}/api/UnitDetection/detect",
                json={
                    "sensorName": "",
                    "category": category
                },
                timeout=10
            )
            response.raise_for_status()
            result = response.json()

            detection = {
                "unit": result.get("unit"),
                "type": result.get("type", "unknown"),
                "determinedBy": result.get(
                    "determinedBy",
                    "Category"
                ),
                "requiresConfiguration": result.get(
                    "requiresConfiguration",
                    True
                )
            }

            self.detected_units[key] = detection
            self.last_unit_refresh[key] = now

            print(
                f"[UNIT DETECTION] {device_id} | "
                f"Category: {category} | "
                f"Unit: {detection['unit'] or 'None'} | "
                f"Type: {detection['type']}"
            )

            if detection["requiresConfiguration"]:
                print(
                    f"[CONFIGURATION REQUIRED] {device_id} | "
                    f"Category '{category}' needs a more specific category "
                    "or a manually configured unit."
                )

            return detection

        except (requests.RequestException, ValueError) as error:
            print(f"[UNIT ERROR] {device_id} | {error}")
            return self.detected_units.get(key)

    def generate_telemetry(self):
        for sensor in list(self.sensors.values()):
            device_id = self.get_device_id(sensor)
            category = self.get_category(sensor)

            detection = self.detect_unit(sensor)

            if not detection:
                continue

            if detection["type"] == "state":
                self.send_switch(sensor)
                continue

            if detection["type"] != "measurement":
                print(
                    f"[SKIPPED] {device_id} | "
                    f"Unresolved category: {category}"
                )
                continue

            unit = detection.get("unit")

            try:
                value = self.generate_value(category, unit)

                if value is None:
                    print(
                        f"[UNSUPPORTED] {device_id} | "
                        f"Category: {category} | Unit: {unit}"
                    )
                    continue

                if unit == "°C":
                    self.send_legacy_measurement(
                        sensor, "temperature", value, unit
                    )
                elif unit == "W":
                    self.send_legacy_measurement(
                        sensor, "power", value, unit
                    )
                else:
                    self.send_measurement(sensor, value, unit)

            except (requests.RequestException, ValueError, TypeError) as error:
                print(f"[TELEMETRY ERROR] {device_id} | {error}")

    def generate_value(self, category, unit):
        category = self.normalize(category)

        if unit == "°C":
            return round(random.uniform(18, 32), 2)

        if unit == "%RH":
            return round(random.uniform(30, 85), 2)

        if unit == "%":
            return round(random.uniform(0, 100), 2)

        if unit == "ppm":
            if "co2" in category or "carbon dioxide" in category:
                return round(random.uniform(400, 1500), 1)
            return round(random.uniform(0, 100), 1)

        if unit == "ppb":
            return round(random.uniform(0, 500), 1)

        if unit == "µg/m³":
            return round(random.uniform(1, 100), 2)

        if unit == "hPa":
            return round(random.uniform(980, 1040), 2)

        if unit == "kPa":
            return round(random.uniform(90, 120), 2)

        if unit == "m/s":
            return round(random.uniform(0, 25), 2)

        if unit == "mm":
            return round(random.uniform(0, 20), 2)

        if unit == "lx":
            return round(random.uniform(50, 1500), 2)

        if unit == "index":
            return round(random.uniform(0, 11), 1)

        if unit == "W/m²":
            return round(random.uniform(0, 1000), 2)

        if unit == "dB":
            return round(random.uniform(25, 100), 2)

        if unit == "L/min":
            return round(random.uniform(0, 30), 2)

        if unit == "cm":
            return round(random.uniform(0, 300), 2)

        if unit == "L":
            return round(random.uniform(0, 1000), 2)

        if unit == "pH":
            return round(random.uniform(4, 9), 2)

        if unit == "mg/L":
            return round(random.uniform(0, 15), 2)

        if unit == "µS/cm":
            return round(random.uniform(50, 2000), 2)

        if unit == "ppt":
            return round(random.uniform(0, 40), 2)

        if unit == "NTU":
            return round(random.uniform(0, 100), 2)

        if unit == "V":
            return round(random.uniform(210, 240), 2)

        if unit == "A":
            return round(random.uniform(0.1, 20), 2)

        if unit == "kWh":
            return round(random.uniform(0.1, 25), 3)

        if unit == "ratio":
            return round(random.uniform(0.7, 1.0), 2)

        if unit == "var":
            return round(random.uniform(0, 1000), 2)

        if unit == "VA":
            return round(random.uniform(10, 2500), 2)

        if unit == "Hz":
            return round(random.uniform(49.5, 50.5), 2)

        if unit == "Ω":
            return round(random.uniform(10, 10000), 2)

        if unit == "F":
            return round(random.uniform(0.000001, 0.01), 6)

        if unit == "count":
            return random.randint(0, 1000)

        if unit == "m":
            return round(random.uniform(0.1, 10), 2)

        if unit == "rpm":
            return round(random.uniform(0, 3000), 2)

        if unit == "m/s²":
            return round(random.uniform(0, 15), 2)

        if unit == "°/s":
            return round(random.uniform(-180, 180), 2)

        if unit == "°":
            return round(random.uniform(0, 360), 2)

        if unit == "mm/s":
            return round(random.uniform(0, 50), 2)

        if unit == "kg":
            return round(random.uniform(0.1, 100), 2)

        if unit == "N":
            return round(random.uniform(0, 500), 2)

        if unit == "N·m":
            return round(random.uniform(0, 100), 2)

        if unit == "° latitude":
            return round(random.uniform(-90, 90), 6)

        if unit == "° longitude":
            return round(random.uniform(-180, 180), 6)

        return None

    def send_legacy_measurement(self, sensor, endpoint, value, unit):
        packet = {
            "deviceId": self.get_device_id(sensor),
            "timestamp": self.timestamp(),
            "sensorCategory": self.get_category(sensor),
            "value": value
        }

        response = self.session.post(
            f"{API_URL}/api/Telemetry/{endpoint}",
            json=packet,
            timeout=10
        )

        self.log_response(response, sensor, value, unit)

    def send_measurement(self, sensor, value, unit):
        packet = {
            "deviceId": self.get_device_id(sensor),
            "timestamp": self.timestamp(),
            "sensorCategory": self.get_category(sensor),
            "value": value,
            "unit": unit
        }

        response = self.session.post(
            f"{API_URL}/api/Telemetry/measurement",
            json=packet,
            timeout=10
        )

        self.log_response(response, sensor, value, unit)

    def send_switch(self, sensor):
        device_id = self.get_device_id(sensor)
        state = self.actuator_states.get(device_id, False)

        packet = {
            "deviceId": device_id,
            "timestamp": self.timestamp(),
            "sensorCategory": self.get_category(sensor),
            "value": state
        }

        response = self.session.post(
            f"{API_URL}/api/Telemetry/switch",
            json=packet,
            timeout=10
        )

        self.log_response(response, sensor, state, "state")

    @staticmethod
    def log_response(response, sensor, value, unit):
        device_id = sensor.get("macAddress") or sensor.get("nodeId", "Unknown")

        if response.ok:
            print(
                f"[TELEMETRY] {device_id} | "
                f"{value} {unit} | HTTP {response.status_code}"
            )
        else:
            print(
                f"[TELEMETRY REJECTED] {device_id} | "
                f"HTTP {response.status_code} | {response.text}"
            )

    def process_commands(self):
        try:
            response = self.session.get(
                f"{API_URL}/api/Commands/pending",
                timeout=10
            )
            response.raise_for_status()
            commands = response.json()

            if not isinstance(commands, list):
                print("[COMMAND ERROR] Invalid command response.")
                return

        except (requests.RequestException, ValueError) as error:
            print(f"[COMMAND API ERROR] {error}")
            return

        for command in commands:
            command_id = command.get("id")
            node_id = command.get("nodeId")
            action = command.get("command", "")

            if not command_id:
                print(f"[COMMAND ERROR] Missing command ID: {command}")
                continue

            sensor = next(
                (
                    item for item in self.sensors.values()
                    if str(item.get("nodeId", "")).lower()
                    == str(node_id or "").lower()
                ),
                None
            )

            success = False

            if sensor is None:
                result = "Target device is not registered."
            elif self.normalize(self.get_category(sensor)) != "actuator":
                result = "Target device is not an actuator."
            else:
                device_id = self.get_device_id(sensor)

                if action in ("RelayOn", "PumpStart"):
                    self.actuator_states[device_id] = True
                    success = True
                    result = "Simulated actuator switched on."
                elif action in ("RelayOff", "PumpStop"):
                    self.actuator_states[device_id] = False
                    success = True
                    result = "Simulated actuator switched off."
                else:
                    result = f"Unsupported command: {action}"

            print(
                f"[COMMAND] {node_id} | {action} | "
                f"{'SUCCESS' if success else 'FAILED'} | {result}"
            )

            try:
                result_response = self.session.post(
                    f"{API_URL}/api/Commands/{command_id}/result",
                    json={
                        "success": success,
                        "result": result
                    },
                    timeout=10
                )
                result_response.raise_for_status()

            except requests.RequestException as error:
                print(f"[COMMAND RESULT ERROR] {error}")

    def run(self):
        print("=" * 50)
        print("           Smart X Sensor Simulator")
        print("=" * 50)
        print(f"API URL: {API_URL}")
        print("Category-only unit detection enabled.")
        print("Dynamic sensor discovery enabled.")
        print("Generic measurement telemetry enabled.")
        print("Actuator command polling enabled.")
        print("Press Ctrl+C to stop.")
        print()

        last_discovery = 0
        last_telemetry = 0
        last_command_poll = 0

        try:
            while self.running:
                now = time.time()

                if now - last_discovery >= DISCOVERY_INTERVAL:
                    self.discover_sensors()
                    last_discovery = now

                if now - last_command_poll >= COMMAND_POLL_INTERVAL:
                    self.process_commands()
                    last_command_poll = now

                if now - last_telemetry >= TELEMETRY_INTERVAL:
                    self.generate_telemetry()
                    last_telemetry = now

                time.sleep(0.1)

        except KeyboardInterrupt:
            print("\nStopping simulator...")

        finally:
            self.running = False
            self.session.close()
            print("Simulator stopped.")


if __name__ == "__main__":
    SensorSimulator().run()
