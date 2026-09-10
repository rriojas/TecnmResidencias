using System.Text.Json;
using System.Text.Json.Serialization;

namespace TecNM.Residency.Projects;

public record UpdateProjectDto(
    string Title = "",
    string? ProjectType = null,
    string ProblemStatement = "",
    string Justification = "",
    string GeneralObjective = "",
    List<string>? SpecificObjectives = null,
    long? CompanyId = null,
    long? AdvisorId = null,
    [property: JsonPropertyName("objectives")] JsonElement? Objectives = null
)
{
    public List<string> GetNormalizedObjectives()
    {
        var result = new List<string>();
        if (SpecificObjectives != null && SpecificObjectives.Count > 0)
        {
            foreach (var s in SpecificObjectives)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    result.Add(s.Trim());
            }
        }

        if (Objectives.HasValue && Objectives.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in Objectives.Value.EnumerateArray())
            {
                if (elem.ValueKind == JsonValueKind.String)
                {
                    var val = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                        result.Add(val.Trim());
                }
                else if (elem.ValueKind == JsonValueKind.Object)
                {
                    if (elem.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                    {
                        var val = descProp.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                            result.Add(val.Trim());
                    }
                    else if (elem.TryGetProperty("Description", out var descProp2) && descProp2.ValueKind == JsonValueKind.String)
                    {
                        var val = descProp2.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                            result.Add(val.Trim());
                    }
                }
            }
        }

        return result.Distinct().ToList();
    }
}
