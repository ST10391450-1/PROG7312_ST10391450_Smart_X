namespace Smart_X_API.Models;

public class DeploymentLocation
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public List<DeploymentLocation> Children { get; set; } = new();
}