using TecNM.Residency.Documents;

namespace TecNM.Residency.Students;

public class StudentFormatDocumentsDto
{
    public long StudentId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public long? ProjectId { get; set; }
    public string? ProjectTitle { get; set; }
    public List<DocumentResponseDto> Documents { get; set; } = new();
}
