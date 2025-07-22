using Microsoft.Data.SqlClient;
using System.Data;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public class ReportService : IReportService
    {
        private readonly IConfiguration _configuration;

        public ReportService(IConfiguration configuration)
        {
            _configuration = configuration;
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

    }

}
