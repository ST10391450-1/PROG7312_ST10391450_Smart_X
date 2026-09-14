using Smart_X_API.Models;

namespace Smart_X_API.Services;

public static class TestDataService
{
    public static void Seed(
        List<SensorRegistration> sensors,
        LocationService locationService)
    {
        SeedLocations(locationService);
        SeedSensors(sensors);
    }

    private static void SeedLocations(
        LocationService locationService)
    {
        locationService.AddLocation(
            new DeploymentLocation
            {
                Name = "Cape Town Office",
                Type = "Location",
                IsConfigured = true,
                Children =
                {
                    new DeploymentLocation
                    {
                        Name = "Ground Floor",
                        Type = "Location",
                        IsConfigured = true,
                        Children =
                        {
                            new DeploymentLocation
                            {
                                Name = "Server Room",
                                Type = "Location",
                                IsConfigured = true
                            },
                            new DeploymentLocation
                            {
                                Name = "Reception",
                                Type = "Location",
                                IsConfigured = true
                            }
                        }
                    },
                    new DeploymentLocation
                    {
                        Name = "First Floor",
                        Type = "Location",
                        IsConfigured = true,
                        Children =
                        {
                            new DeploymentLocation
                            {
                                Name = "Operations",
                                Type = "Location",
                                IsConfigured = true
                            },
                            new DeploymentLocation
                            {
                                Name = "Data Centre",
                                Type = "Location",
                                IsConfigured = true
                            }
                        }
                    }
                }
            });

        locationService.AddLocation(
            new DeploymentLocation
            {
                Name = "Warehouse",
                Type = "Location",
                IsConfigured = true,
                Children =
                {
                    new DeploymentLocation
                    {
                        Name = "Loading Bay",
                        Type = "Location",
                        IsConfigured = true
                    },
                    new DeploymentLocation
                    {
                        Name = "Storage Area",
                        Type = "Location",
                        IsConfigured = true
                    }
                }
            });

        locationService.AddLocation(
            new DeploymentLocation
            {
                Name = "Manufacturing Plant",
                Type = "Location",
                IsConfigured = true,
                Children =
                {
                    new DeploymentLocation
                    {
                        Name = "Production Floor",
                        Type = "Location",
                        IsConfigured = true,
                        Children =
                        {
                            new DeploymentLocation
                            {
                                Name = "Line 1",
                                Type = "Location",
                                IsConfigured = true
                            },
                            new DeploymentLocation
                            {
                                Name = "Line 2",
                                Type = "Location",
                                IsConfigured = true
                            }
                        }
                    }
                }
            });
    }

    private static void SeedSensors(
        List<SensorRegistration> sensors)
    {
        sensors.AddRange(
            new[]
            {
                new SensorRegistration
                {
                    NodeId = "TEMP-001",
                    MacAddress = "AA:BB:CC:00:00:01",
                    Category = "Environmental",
                    Location = "Cape Town Office / Ground Floor / Server Room"
                },

                new SensorRegistration
                {
                    NodeId = "TEMP-002",
                    MacAddress = "AA:BB:CC:00:00:02",
                    Category = "Environmental",
                    Location = "Cape Town Office / Ground Floor / Reception"
                },

                new SensorRegistration
                {
                    NodeId = "TEMP-003",
                    MacAddress = "AA:BB:CC:00:00:03",
                    Category = "Environmental",
                    Location = "Cape Town Office / First Floor / Operations"
                },

                new SensorRegistration
                {
                    NodeId = "TEMP-004",
                    MacAddress = "AA:BB:CC:00:00:04",
                    Category = "Environmental",
                    Location = "Cape Town Office / First Floor / Data Centre"
                },

                new SensorRegistration
                {
                    NodeId = "TEMP-005",
                    MacAddress = "AA:BB:CC:00:00:05",
                    Category = "Environmental",
                    Location = "Manufacturing Plant / Production Floor / Line 1"
                },

                new SensorRegistration
                {
                    NodeId = "POWER-001",
                    MacAddress = "AA:BB:CC:00:01:01",
                    Category = "Power Consumption",
                    Location = "Cape Town Office / Ground Floor / Server Room"
                },

                new SensorRegistration
                {
                    NodeId = "POWER-002",
                    MacAddress = "AA:BB:CC:00:01:02",
                    Category = "Power Consumption",
                    Location = "Cape Town Office / First Floor / Data Centre"
                },

                new SensorRegistration
                {
                    NodeId = "POWER-003",
                    MacAddress = "AA:BB:CC:00:01:03",
                    Category = "Power Consumption",
                    Location = "Warehouse / Storage Area"
                },

                new SensorRegistration
                {
                    NodeId = "POWER-004",
                    MacAddress = "AA:BB:CC:00:01:04",
                    Category = "Power Consumption",
                    Location = "Manufacturing Plant / Production Floor / Line 1"
                },

                new SensorRegistration
                {
                    NodeId = "POWER-005",
                    MacAddress = "AA:BB:CC:00:01:05",
                    Category = "Power Consumption",
                    Location = "Manufacturing Plant / Production Floor / Line 2"
                },

                new SensorRegistration
                {
                    NodeId = "ACT-001",
                    MacAddress = "AA:BB:CC:00:02:01",
                    Category = "Actuator",
                    Location = "Manufacturing Plant / Production Floor / Line 1"
                },

                new SensorRegistration
                {
                    NodeId = "ACT-002",
                    MacAddress = "AA:BB:CC:00:02:02",
                    Category = "Actuator",
                    Location = "Manufacturing Plant / Production Floor / Line 2"
                },

                new SensorRegistration
                {
                    NodeId = "ACT-003",
                    MacAddress = "AA:BB:CC:00:02:03",
                    Category = "Actuator",
                    Location = "Warehouse / Loading Bay"
                },

                new SensorRegistration
                {
                    NodeId = "ENV-001",
                    MacAddress = "AA:BB:CC:00:03:01",
                    Category = "Environmental",
                    Location = "Warehouse / Loading Bay"
                },

                new SensorRegistration
                {
                    NodeId = "ENV-002",
                    MacAddress = "AA:BB:CC:00:03:02",
                    Category = "Environmental",
                    Location = "Manufacturing Plant / Production Floor / Line 2"
                }
            });
    }
}