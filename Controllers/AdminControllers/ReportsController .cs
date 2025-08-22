using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    /// <summary>
    /// API báo cáo thống kê và xuất Excel cho Admin.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly ExcelExportService _excelExportService;
        private readonly IWebHostEnvironment _env;
        public ReportsController(
        IReportService reportService,
        ExcelExportService excelExportService,
        IWebHostEnvironment env)
        {
            _reportService = reportService;
            _excelExportService = excelExportService;
            _env = env;
        }

        /// <summary>
        /// Lấy thống kê tổng quan theo năm cho Admin.
        /// </summary>
        /// <param name="year">Năm cần thống kê (nếu không truyền sẽ lấy mặc định là năm hiện tại).</param>
        [HttpGet("total-statistic")]
        public async Task<IActionResult> GetAnnualAdminStats([FromQuery] int? year)
        {
            var data = await _reportService.GetAnnualAdminStatsAsync(year);
            return Ok(data);
        }

        /// <summary>
        /// Lấy báo cáo tổng hợp doanh thu trong khoảng thời gian.
        /// </summary>
        /// <param name="fromDate">Ngày bắt đầu.</param>
        /// <param name="toDate">Ngày kết thúc.</param>
        [HttpGet("get-revenue-summary")]
        public async Task<IActionResult> GetRevenueSummary([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var (details, totals) = await _reportService.GetRevenueSummaryAsync(fromDate, toDate);
            return Ok(new
            {
                Details = details,
                Totals = totals
            });
        }

        /// <summary>
        /// Xuất báo cáo doanh thu ra file Excel theo khoảng thời gian.
        /// </summary>
        /// <param name="fromDate">Ngày bắt đầu.</param>
        /// <param name="toDate">Ngày kết thúc.</param>
        [HttpGet("revenue-summary/export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportRevenueSummaryExcel([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (fromDate == default)
                throw new ArgumentException("Ngày bắt đầu không hợp lệ");

            if (toDate == default)
                throw new ArgumentException("Ngày kết thúc không hợp lệ");

            if (fromDate > toDate)
                throw new ArgumentException("Ngày bắt đầu không được lớn hơn ngày kết thúc");

            var (details, totals) = await _reportService.GetRevenueSummaryAsync(fromDate, toDate);

            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "RevenueReportTemplate.xlsx");
            var fileBytes = _excelExportService.ExportRevenueFromExcelTemplate(templatePath,details,totals,fromDate,toDate);

            if (fileBytes == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Xuất báo cáo thất bại.");
            }

            var fileName = $"RevenueReport_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";

            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        /// <summary>
        /// Lấy hiệu suất của đối tác (Partner Performance) theo khoảng thời gian.
        /// </summary>
        /// /// <param name="fromDate">Ngày bắt đầu.</param>
        /// <param name="toDate">Ngày kết thúc.</param>
        [HttpGet("get-partner-performance")]
        public async Task<IActionResult> GetPartnerPerformance([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var performanceList = await _reportService.GetPartnerPerformanceAsync(fromDate, toDate);
            return Ok(performanceList);
        }

        /// <summary>
        /// Xuất báo cáo hiệu suất đối tác ra file Excel.
        /// </summary>
        /// /// <param name="fromDate">Ngày bắt đầu.</param>
        /// <param name="toDate">Ngày kết thúc.</param>
        [HttpGet("partner-performance/export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportPartnerPerformanceExcel([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (fromDate == default)
                throw new ArgumentException("Ngày bắt đầu không hợp lệ");

            if (toDate == default)
                throw new ArgumentException("Ngày kết thúc không hợp lệ");

            if (fromDate > toDate)
                throw new ArgumentException("Ngày bắt đầu không được lớn hơn ngày kết thúc");

            var data = await _reportService.GetPartnerPerformanceAsync(fromDate, toDate);
            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "PartnerPerformanceTemplate.xlsx");
            var bytes = _excelExportService.ExportPartnerPerformanceToExcel(templatePath, data, fromDate, toDate);

            if (bytes == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Xuất báo cáo thất bại.");
            }

            var fileName = $"PartnerPerformance_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        /// <summary>
        /// Lấy thống kê đặt tour trong khoảng thời gian.
        /// </summary>
        /// /// <param name="fromDate">Ngày bắt đầu.</param>
        /// <param name="toDate">Ngày kết thúc.</param>
        [HttpGet("get-tour-booking-stats")]
        public async Task<IActionResult> GetTourBookingStats([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var tourBookingStats = await _reportService.GetTourBookingStatsAsync(fromDate, toDate);
            return Ok(tourBookingStats);
        }

        /// <summary>
        /// Xuất thống kê đặt tour ra file Excel.
        /// </summary>
        /// <param name="fromDate">Ngày bắt đầu.</param>
        /// <param name="toDate">Ngày kết thúc.</param>
        [HttpGet("tour-booking-stats/export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportTourBookingStatsExcel([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (fromDate == default)
                throw new ArgumentException("Ngày bắt đầu không hợp lệ");

            if (toDate == default)
                throw new ArgumentException("Ngày kết thúc không hợp lệ");

            if (fromDate > toDate)
                throw new ArgumentException("Ngày bắt đầu không được lớn hơn ngày kết thúc");
            if (fromDate == default)
                throw new ArgumentException("Ngày bắt đầu không hợp lệ");

            if (toDate == default)
                throw new ArgumentException("Ngày kết thúc không hợp lệ");

            if (fromDate > toDate)
                throw new ArgumentException("Ngày bắt đầu không được lớn hơn ngày kết thúc");

            var data = await _reportService.GetTourBookingStatsAsync(fromDate, toDate);

            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "TourBookingStatsTemplate.xlsx");
            var fileBytes = _excelExportService.ExportTourBookingStatsExcel(templatePath, data);

            if (fileBytes == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Xuất báo cáo thất bại.");
            }

            var fileName = $"TourBookingStats_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        /// <summary>
        /// Lấy thống kê tổng quan để hiển thị trên Dashboard.
        /// </summary>
        [HttpGet("get-dashboard-statistics")]
        public async Task<IActionResult> GetDashboardStatistics()
        {
            var statisticsList = await _reportService.GetDashboardStatistics();
            return Ok(statisticsList);
        }
    }
}
