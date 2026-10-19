
using System;

namespace Smart_X_API.Services;

public sealed class SensorUnitDetectionService
{
    public SensorUnitResult Detect(string? sensorName, string? category)
    {
        var value = Normalize(category);

        if (string.IsNullOrWhiteSpace(value))
            return Unknown();

        if (Matches(value,
            "actuator", "actuators", "relay", "smart relay",
            "switch", "smart switch", "pump", "water pump",
            "motor", "valve", "solenoid", "door lock",
            "fan controller", "light controller"))
        {
            return State("Category");
        }

        if (Matches(value,
            "temperature", "temperature sensor", "air temperature",
            "water temperature", "soil temperature", "thermal"))
            return Known("°C");

        if (Matches(value,
            "humidity", "relative humidity", "air humidity",
            "environmental humidity"))
            return Known("%RH");

        if (Matches(value,
            "soil moisture", "moisture", "soil water content"))
            return Known("%");

        if (Matches(value,
            "co2", "carbon dioxide", "carbon dioxide concentration"))
            return Known("ppm");

        if (Matches(value,
            "air quality", "air pollution", "gas concentration",
            "carbon monoxide", "co", "voc", "tvoc",
            "methane", "ammonia gas"))
            return Known("ppm", true);

        if (Matches(value,
            "particulate matter", "pm1", "pm1.0", "pm2.5",
            "pm10", "dust concentration"))
            return Known("µg/m³");

        if (Matches(value,
            "atmospheric pressure", "barometric pressure",
            "air pressure", "barometer", "pressure"))
            return Known("hPa");

        if (Matches(value,
            "water pressure", "hydraulic pressure",
            "industrial pressure"))
            return Known("kPa", true);

        if (Matches(value,
            "wind speed", "wind velocity", "anemometer"))
            return Known("m/s");

        if (Matches(value,
            "wind direction", "wind bearing", "wind vane"))
            return Known("°");

        if (Matches(value,
            "rainfall", "rain gauge", "precipitation",
            "rain accumulation"))
            return Known("mm");

        if (Matches(value,
            "light", "light level", "illuminance",
            "ambient light", "lux"))
            return Known("lx");

        if (Matches(value, "uv index", "ultraviolet index"))
            return Known("index");

        if (Matches(value,
            "solar radiation", "solar irradiance", "irradiance",
            "uv radiation"))
            return Known("W/m²");

        if (Matches(value,
            "noise", "noise level", "sound level",
            "sound pressure level"))
            return Known("dB");

        if (Matches(value,
            "water flow", "flow rate", "flow meter",
            "liquid flow", "volumetric flow"))
            return Known("L/min");

        if (Matches(value,
            "water level", "tank level", "liquid level",
            "reservoir level"))
            return Known("cm", true);

        if (Matches(value,
            "water consumption", "water usage", "water volume"))
            return Known("L");

        if (Matches(value,
            "water quality", "water ph", "soil ph",
            "ph sensor", "acidity", "alkalinity"))
            return Known("pH");

        if (Matches(value,
            "dissolved oxygen", "water oxygen"))
            return Known("mg/L");

        if (Matches(value,
            "water conductivity", "electrical conductivity",
            "conductivity", "ec"))
            return Known("µS/cm");

        if (Matches(value,
            "total dissolved solids", "tds"))
            return Known("ppm");

        if (Matches(value, "salinity"))
            return Known("ppt");

        if (Matches(value, "turbidity", "water clarity"))
            return Known("NTU");

        if (Matches(value,
            "voltage", "electrical voltage", "battery voltage",
            "ac voltage", "dc voltage"))
            return Known("V");

        if (Matches(value,
            "current", "electrical current", "amperage"))
            return Known("A");

        if (Matches(value,
            "power", "power consumption", "power draw",
            "active power", "electrical power"))
            return Known("W", true);

        if (Matches(value,
            "energy", "energy consumption", "energy usage",
            "electrical energy"))
            return Known("kWh");

        if (Matches(value, "power factor"))
            return Known("ratio");

        if (Matches(value, "reactive power"))
            return Known("var");

        if (Matches(value, "apparent power"))
            return Known("VA");

        if (Matches(value,
            "frequency", "electrical frequency", "mains frequency"))
            return Known("Hz");

        if (Matches(value, "resistance", "electrical resistance"))
            return Known("Ω");

        if (Matches(value, "capacitance"))
            return Known("F");

        if (Matches(value,
            "battery level", "battery charge", "state of charge",
            "battery health"))
            return Known("%");

        if (Matches(value,
            "battery cycles", "cycle count", "pulse count"))
            return Known("count");

        if (Matches(value,
            "distance", "range", "distance measurement"))
            return Known("m");

        if (Matches(value,
            "proximity", "presence", "occupancy",
            "motion detection", "motion", "door contact",
            "window contact", "smoke detection", "leak detection"))
            return State("Category");

        if (Matches(value,
            "speed", "vehicle speed", "linear speed"))
            return Known("m/s");

        if (Matches(value,
            "rotational speed", "rpm", "rotation speed"))
            return Known("rpm");

        if (Matches(value,
            "acceleration", "linear acceleration"))
            return Known("m/s²");

        if (Matches(value,
            "orientation", "heading", "tilt", "inclination",
            "angular position"))
            return Known("°");

        if (Matches(value,
            "gyroscope", "angular velocity", "rotation rate"))
            return Known("°/s");

        if (Matches(value, "vibration", "vibration velocity"))
            return Known("mm/s");

        if (Matches(value, "weight", "mass", "load cell"))
            return Known("kg");

        if (Matches(value, "force", "tension"))
            return Known("N");

        if (Matches(value, "torque"))
            return Known("N·m");

        if (Matches(value,
            "gps latitude", "latitude"))
            return Known("° latitude");

        if (Matches(value,
            "gps longitude", "longitude"))
            return Known("° longitude");

        if (Matches(value,
            "environmental", "environment", "environmental sensors"))
            return Unknown();

        if (Matches(value,
            "power consumption sensors", "electrical",
            "electrical sensors", "energy monitoring"))
        {
            return Known("W", true);
        }

        if (Matches(value, "water", "water sensors"))
            return Unknown();

        if (Matches(value, "weather", "weather sensors"))
            return Unknown();

        if (Matches(value, "industrial", "industrial sensors"))
            return Unknown();

        return Unknown();
    }

    private static SensorUnitResult Known(
        string unit,
        bool requiresConfiguration = false)
    {
        return new SensorUnitResult(
            unit,
            "measurement",
            "Category",
            requiresConfiguration);
    }

    private static SensorUnitResult State(string source)
    {
        return new SensorUnitResult(
            null,
            "state",
            source,
            false);
    }

    private static SensorUnitResult Unknown()
    {
        return new SensorUnitResult(
            null,
            "unknown",
            "Unrecognised category",
            true);
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('/', ' ');
    }

    private static bool Matches(string value, params string[] categories)
    {
        foreach (var category in categories)
        {
            if (string.Equals(
                value,
                Normalize(category),
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record SensorUnitResult(
    string? Unit,
    string Type,
    string DeterminedBy,
    bool RequiresConfiguration);
