using System.Collections.Generic;

namespace Smart_X_UI.Models;

// Normalised payload sent to POST api/Sensors/import.
public class SensorImportRequest
{
    public List<SensorCategory> Categories { get; set; } = new();
    public List<string> Locations { get; set; } = new();
    public List<SensorRegistration> Sensors { get; set; } = new();
}

public class SensorImportResult
{
    public int CategoriesCreated { get; set; }
    public int CategoriesSkipped { get; set; }
    public int LocationsCreated { get; set; }
    public int SensorsImported { get; set; }
    public int SensorsSkipped { get; set; }
    public List<string> Messages { get; set; } = new();
}
