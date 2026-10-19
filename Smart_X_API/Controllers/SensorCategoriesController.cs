using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Controllers;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorCategoriesController : ControllerBase
{
    private readonly SensorCategoryStore _store;

    public SensorCategoriesController(SensorCategoryStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_store.GetAll());
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var category = _store.Get(id);

        return category is null
            ? NotFound("Sensor category was not found.")
            : Ok(category);
    }

    [HttpPost]
    public IActionResult Create([FromBody] SensorCategory request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Category name is required.");

        if (request.Name.Trim().Length > 80)
            return BadRequest("Category name cannot exceed 80 characters.");

        if (request.Description?.Length > 250)
            return BadRequest("Description cannot exceed 250 characters.");

        if (_store.NameExists(request.Name.Trim()))
            return Conflict("A category with this name already exists.");

        var category = _store.Add(
            request.Name,
            request.Description ?? string.Empty);

        return CreatedAtAction(
            nameof(GetById),
            new { id = category.Id },
            category);
    }

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] SensorCategory request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Category name is required.");

        if (request.Name.Trim().Length > 80)
            return BadRequest("Category name cannot exceed 80 characters.");

        if (request.Description?.Length > 250)
            return BadRequest("Description cannot exceed 250 characters.");

        var existing = _store.Get(id);

        if (existing is null)
            return NotFound("Sensor category was not found.");

        if (_store.NameExists(request.Name.Trim(), id))
            return Conflict("A category with this name already exists.");

        var oldName = existing.Name;
        var updated = _store.Update(
            id,
            request.Name,
            request.Description ?? string.Empty);

        if (updated is not null &&
            !oldName.Equals(updated.Name, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var sensor in SensorsController.Sensors.Where(
                         x => x.Category.Equals(
                             oldName, StringComparison.OrdinalIgnoreCase)))
            {
                sensor.Category = updated.Name;
            }
        }

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var category = _store.Get(id);

        if (category is null)
            return NotFound("Sensor category was not found.");

        if (SensorsController.Sensors.Any(
                x => x.Category.Equals(
                    category.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(
                "This category cannot be deleted because registered sensors use it.");
        }

        return _store.Delete(id)
            ? NoContent()
            : NotFound("Sensor category was not found.");
    }
}