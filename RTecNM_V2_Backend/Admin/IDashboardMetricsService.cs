using TecNM.Residency.Common;

namespace TecNM.Residency.Admin;

public interface IDashboardMetricsService
{
    Task<Result<DashboardMetricsResponseDto>> GetDashboardMetricsAsync(long? careerId = null);
}
