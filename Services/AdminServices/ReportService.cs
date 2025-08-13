using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public class ReportService : IReportService
    {
        private readonly IConfiguration _configuration;
        private readonly TripWiseDBContext _dbContext;
        public ReportService(IConfiguration configuration, TripWiseDBContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
        }

        public async Task<(List<RevenueDetailDto> Details, List<RevenueSummaryDto> Totals)> GetRevenueSummaryAsync(DateTime fromDate, DateTime toDate)
        {
            var details = new List<RevenueDetailDto>();
            var totals = new List<RevenueSummaryDto>();

            using var conn = new SqlConnection(_configuration.GetConnectionString("DBContext"));
            using var cmd = new SqlCommand("sp_GetRevenueCombinedSeparated", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@FromDate", fromDate);
            cmd.Parameters.AddWithValue("@ToDate", toDate);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            // Read Detail records (ResultSet 1)
            while (await reader.ReadAsync())
            {
                details.Add(new RevenueDetailDto
                {
                    Month = reader["Month"].ToString(),
                    TransactionType = reader["TransactionType"].ToString(),
                    ItemDescription = reader["ItemDescription"].ToString(),
                    Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                    UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                    Email = reader["Email"].ToString(),
                    FullName = reader["FullName"].ToString(),
                    TransactionDate = reader.GetDateTime(reader.GetOrdinal("TransactionDate"))
                });
            }

            // Move to ResultSet 2
            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    totals.Add(new RevenueSummaryDto
                    {
                        Month = reader["Month"].ToString(),
                        TotalBookingRevenue = reader.GetDecimal(reader.GetOrdinal("TotalBookingRevenue")),
                        TotalPlanRevenue = reader.GetDecimal(reader.GetOrdinal("TotalPlanRevenue")),
                        TotalCombinedRevenue = reader.GetDecimal(reader.GetOrdinal("TotalCombinedRevenue"))
                    });
                }
            }

            return (details, totals);
        }
        public async Task<List<PartnerPerformanceDto>> GetPartnerPerformanceAsync(DateTime fromDate, DateTime toDate)
        {
            var result = new List<PartnerPerformanceDto>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DBContext"));
            using var command = new SqlCommand("sp_GetPartnerPerformanceStats", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@FromDate", fromDate);
            command.Parameters.AddWithValue("@ToDate", toDate);

            await conn.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PartnerPerformanceDto
                {
                    PartnerID = reader.GetInt32(0),
                    PartnerName = reader.GetString(1),
                    TotalToursProvided = reader.GetInt32(2),
                    TotalBookings = reader.GetInt32(3),
                    TotalRevenue = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
                });
            }
            return result;
        }
        public async Task<List<TourBookingStatDto>> GetTourBookingStatsAsync(DateTime fromDate, DateTime toDate)
        {
            var result = new List<TourBookingStatDto>();

            using var conn = new SqlConnection(_configuration.GetConnectionString("DBContext"));
            using var command = new SqlCommand("sp_GetTourBookingStats", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@FromDate", fromDate);
            command.Parameters.AddWithValue("@ToDate", toDate);

            await conn.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new TourBookingStatDto
                {
                    Month = reader.GetString(0),
                    TourID = reader.GetInt32(1),
                    TourName = reader.GetString(2),
                    TotalBookings = reader.GetInt32(3),
                    TotalRevenue = reader.GetDecimal(4)
                });
            }

            return result;
        }
        public async Task<List<AnnualAdminStatDto>> GetAnnualAdminStatsAsync(int? year)
        {
            var result = new List<AnnualAdminStatDto>();

            using var conn = new SqlConnection(_configuration.GetConnectionString("DBContext"));
            using var command = new SqlCommand("sp_GetAnnualAdminStats", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@Year", year ?? (object)DBNull.Value);

            await conn.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new AnnualAdminStatDto
                {
                    Month = reader.GetInt32(reader.GetOrdinal("Month")),
                    BookingRevenue = reader.GetDecimal(reader.GetOrdinal("BookingRevenue")),
                    PlanRevenue = reader.GetDecimal(reader.GetOrdinal("PlanRevenue")),
                    TotalBookings = reader.GetInt32(reader.GetOrdinal("TotalBookings")),
                    TotalPlans = reader.GetInt32(reader.GetOrdinal("TotalPlans"))
                });
            }

            return result;
        }
       
        public async Task<List<DashboardStatisticsDto>> GetDashboardStatistics()
        {
            var result = new List<DashboardStatisticsDto>();

            using var conn = new SqlConnection(_configuration.GetConnectionString("DBContext"));
            using var command = new SqlCommand("sp_GetDashboardStatistics", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            await conn.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {

                result.Add(new DashboardStatisticsDto
                {
                    TotalUsers = reader.GetInt32(reader.GetOrdinal("TotalUsers")),
                    TotalReviews = reader.GetInt32(reader.GetOrdinal("TotalReviews")),
                    TotalComments = reader.GetInt32(reader.GetOrdinal("TotalComments")),
                    TotalStarRatings = reader.GetInt32(reader.GetOrdinal("TotalStarRatings")),
                    TotalPartners = reader.GetInt32(reader.GetOrdinal("TotalPartners")),
                    TotalTours = reader.GetInt32(reader.GetOrdinal("TotalTours")),
                    TotalBlogs = reader.GetInt32(reader.GetOrdinal("TotalBlogs")),
                    TotalPlans = reader.GetInt32(reader.GetOrdinal("TotalPlans")),
                    TotalTourBookings = reader.GetInt32(reader.GetOrdinal("TotalTourBookings")),
                    TotalPlanPurchases = reader.GetInt32(reader.GetOrdinal("TotalPlanPurchases")),
                });
            }
            return result;
        }
    }

}
