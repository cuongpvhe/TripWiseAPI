namespace TripWiseAPI.Models.DTO
{
    public class ReportDto
    {
    }
    public class RevenueDetailDto
    {
        public string Month { get; set; }
        public string TransactionType { get; set; }
        public string ItemDescription { get; set; }
        public decimal Amount { get; set; }
        public int UserID { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    public class RevenueSummaryDto
    {
        public string Month { get; set; }
        public decimal TotalBookingRevenue { get; set; }
        public decimal TotalPlanRevenue { get; set; }
        public decimal TotalCombinedRevenue { get; set; }
    }
    public class PartnerPerformanceDto
    {
        public int PartnerID { get; set; }
        public string PartnerName { get; set; } = string.Empty;
        public int TotalToursProvided { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
    }
    public class TourBookingStatDto
    {
        public string Month { get; set; }
        public int TourID { get; set; }
        public string TourName { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class AnnualAdminStatDto
    {
        public int Month { get; set; }
        public decimal BookingRevenue { get; set; }
        public decimal PlanRevenue { get; set; }
        public int TotalBookings { get; set; }
        public int TotalPlans { get; set; }
    }
    public class DashboardStatisticsDto
    {
        public int TotalUsers { get; set;}
        public int TotalReviews { get; set; }
        public int TotalComments { get; set; }
        public int TotalStarRatings { get; set; }
        public int TotalPartners { get; set; }
        public int TotalTours { get; set; }
        public int TotalBlogs { get; set; }
        public int TotalPlans { get; set; }
        public int TotalTourBookings { get; set; }
        public int TotalPlanPurchases { get; set; }
    }
    public class PartnerTourStatisticsDto
    {
        public string MonthYear { get; set; }
        public int PartnerID { get; set; }
        public string CompanyName { get; set; }
        public int TotalTours { get; set; }
        public int TotalBookedTours { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
