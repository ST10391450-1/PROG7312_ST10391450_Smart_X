import time
import random
import requests
from datetime import datetime, timezone


API_URL = "http://localhost:8080"

DISCOVERY_INTERVAL = 2
TELEMETRY_INTERVAL = 2


class SensorSimulator:

    def __init__(self):
        self.sensors = {}
        self.running = True

    # Gets all sensors currently registered with the API.
    def get_registered_sensors(self):
        try:
            response = requests.get(
                f"{API_URL}/api/Sensors",
                timeout=10
            )

            response.raise_for_status()

            return response.json()

        except requests.RequestException as error:
            print(f"[API] Unable to retrieve sensors: {error}")
            return []

    # Checks for new or removed sensors.
    def discover_sensors(self):
        registered_sensors = self.get_registered_sensors()
        registered_macs = set()

        for sensor in registered_sensors:
            mac_address = sensor.get("macAddress", "").strip()

            if not mac_address:
                continue

            mac = mac_address.lower()
            registered_macs.add(mac)

            if mac not in self.sensors:
                print(
                    f"[CONNECTED] "
                    f"{sensor.get('nodeId', 'Unknown')} | "
                    f"MAC: {mac_address} | "
                    f"Category: {sensor.get('category', 'Unknown')}"
                )

            self.sensors[mac] = sensor

        removed_sensors = [
            mac
            for mac in self.sensors
            if mac not in registered_macs
        ]

        for mac in removed_sensors:
            sensor = self.sensors.pop(mac)

            print(
                f"[DISCONNECTED] "
                f"{sensor.get('nodeId', 'Unknown')} | "
                f"MAC: {sensor.get('macAddress', mac)}"
            )

    # Generates telemetry based on each sensor's category.
    def generate_telemetry(self):
        for sensor in list(self.sensors.values()):
            category = sensor.get("category", "").strip().lower()

            try:
                if category == "environmental":
                    self.send_temperature(sensor)

                elif category == "power consumption":
                    self.send_power(sensor)

                elif category == "actuator":
                    self.send_switch(sensor)

            except requests.RequestException as error:
                print(
                    f"[{sensor.get('nodeId', 'Unknown')}] "
                    f"Telemetry failed: {error}"
                )

    # Sends a simulated temperature reading.
    def send_temperature(self, sensor):
        temperature = round(
            random.uniform(20.0, 28.0),
            2
        )

        packet = {
            "deviceId": sensor["macAddress"],
            "timestamp": self.timestamp(),
            "sensorCategory": sensor["category"],
            "value": temperature
        }

        response = requests.post(
            f"{API_URL}/api/Telemetry/temperature",
            json=packet,
            timeout=10
        )

        self.handle_response(response, sensor, temperature)

    # Sends a simulated power reading.
    def send_power(self, sensor):
        power = random.randint(100, 2500)

        packet = {
            "deviceId": sensor["macAddress"],
            "timestamp": self.timestamp(),
            "sensorCategory": sensor["category"],
            "value": power
        }

        response = requests.post(
            f"{API_URL}/api/Telemetry/power",
            json=packet,
            timeout=10
        )

        self.handle_response(response, sensor, power)

    # Sends a simulated actuator state.
    def send_switch(self, sensor):
        state = random.choice([True, False])

        packet = {
            "deviceId": sensor["macAddress"],
            "timestamp": self.timestamp(),
            "sensorCategory": sensor["category"],
            "value": state
        }

        response = requests.post(
            f"{API_URL}/api/Telemetry/switch",
            json=packet,
            timeout=10
        )

        self.handle_response(response, sensor, state)

    # Displays the result of a telemetry request.
    def handle_response(self, response, sensor, value):
        node_id = sensor.get("nodeId", "Unknown")
        mac_address = sensor.get("macAddress", "Unknown")

        if response.ok:
            print(
                f"[TELEMETRY] "
                f"{node_id} | "
                f"{mac_address} | "
                f"{value}"
            )
        else:
            print(
                f"[REJECTED] "
                f"{node_id} | "
                f"HTTP {response.status_code} | "
                f"{response.text}"
            )

    @staticmethod
    def timestamp():
        return datetime.now(timezone.utc).isoformat()

    # Runs the simulator until it is stopped.
    def run(self):
        print("=" * 45)
        print("       Smart X Sensor Simulator")
        print("=" * 45)
        print()
        print(f"API: {API_URL}")
        print()
        print("Waiting for sensors registered through Smart X...")
        print("Press Ctrl+C to stop.")
        print()

        last_discovery = 0
        last_telemetry = 0

        try:
            while self.running:
                current_time = time.time()

                if current_time - last_discovery >= DISCOVERY_INTERVAL:
                    self.discover_sensors()
                    last_discovery = current_time

                if current_time - last_telemetry >= TELEMETRY_INTERVAL:
                    self.generate_telemetry()
                    last_telemetry = current_time

                time.sleep(0.1)

        except KeyboardInterrupt:
            print()
            print("Stopping simulator...")

        finally:
            self.running = False
            print("Simulator stopped.")


if __name__ == "__main__":
    simulator = SensorSimulator()
    simulator.run()
