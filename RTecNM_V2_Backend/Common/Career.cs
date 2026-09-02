using System.ComponentModel.DataAnnotations.Schema;

namespace TecNM.Residency.Common;

[Table("careers")]
public class Career : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Acronym { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
}
