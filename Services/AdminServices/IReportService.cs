using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IReportService
    {
        Task<(List<RevenueDetailDto> Details, List<RevenueSummaryDto> Totals)> GetRevenueSummaryAsync(DateTime fromDate, DateTime toDate);
        Task<List<PartnerPerformanceDto>> GetPartnerPerformanceAsync(DateTime fromDate, DateTime toDate);
        Task<List<TourBookingStatDto>> GetTourBookingStatsAsync(DateTime fromDate, DateTime toDate);
        Task<List<AnnualAdminStatDto>> GetAnnualAdminStatsAsync(int? year);
    }
}
