namespace TecNM.Residency.Searches.Dtos;

public class SearchColumnMetadataDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Type { get; set; } = "Text"; // Text, Integer, Date
    public bool IsSearchable { get; set; } = true;
}

public class SearchSourceMetadataDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string KeyColumn { get; set; } = "id";
    public List<SearchColumnMetadataDto> Columns { get; set; } = new();
}
