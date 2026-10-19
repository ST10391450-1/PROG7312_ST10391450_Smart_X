using System.Text.Json;
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public sealed class SensorCategoryStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private List<SensorCategory> _categories = new();

    public SensorCategoryStore(IWebHostEnvironment environment)
    {
        var directory = Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(directory);

        _filePath = Path.Combine(directory, "sensor-categories.json");

        lock (_lock)
        {
            if (File.Exists(_filePath))
            {
                _categories = JsonSerializer.Deserialize<List<SensorCategory>>(
                    File.ReadAllText(_filePath)) ?? new();
            }

            if (_categories.Count == 0)
            {
                _categories =
                [
                    new SensorCategory
                    {
                        Name = "Environmental",
                        Description = "Environmental monitoring sensors"
                    },
                    new SensorCategory
                    {
                        Name = "Power Consumption",
                        Description = "Electrical and energy monitoring sensors"
                    },
                    new SensorCategory
                    {
                        Name = "Actuator",
                        Description = "Devices that control physical equipment"
                    }
                ];

                Save();
            }
        }
    }

    public List<SensorCategory> GetAll()
    {
        lock (_lock)
            return _categories.OrderBy(x => x.Name).ToList();
    }

    public SensorCategory? Get(Guid id)
    {
        lock (_lock)
            return _categories.FirstOrDefault(x => x.Id == id);
    }

    public bool NameExists(string name, Guid? excludingId = null)
    {
        lock (_lock)
            return _categories.Any(x =>
                x.Id != excludingId &&
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public SensorCategory Add(string name, string description)
    {
        lock (_lock)
        {
            var category = new SensorCategory
            {
                Name = name.Trim(),
                Description = description.Trim()
            };

            _categories.Add(category);
            Save();
            return category;
        }
    }

    public SensorCategory? Update(Guid id, string name, string description)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(x => x.Id == id);
            if (category is null)
                return null;

            category.Name = name.Trim();
            category.Description = description.Trim();

            Save();
            return category;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(x => x.Id == id);
            if (category is null)
                return false;

            _categories.Remove(category);
            Save();
            return true;
        }
    }

    private void Save()
    {
        var temporaryPath = _filePath + ".tmp";

        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(
                _categories,
                new JsonSerializerOptions { WriteIndented = true }));

        File.Move(temporaryPath, _filePath, true);
    }
}