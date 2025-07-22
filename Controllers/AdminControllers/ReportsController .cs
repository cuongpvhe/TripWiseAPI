using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
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
        [HttpGet("revenue-summary/export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportRevenueSummaryExcel([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var (details, totals) = await _reportService.GetRevenueSummaryAsync(fromDate, toDate);

            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "RevenueReportTemplate.xlsx");
            var fileBytes = _excelExportService.ExportRevenueFromExcelTemplate(templatePath,details,totals,fromDate,toDate);

            var fileName = $"RevenueReport_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";

            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        [HttpGet("get-partner-performance")]
        public async Task<IActionResult> GetPartnerPerformance([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var performanceList = await _reportService.GetPartnerPerformanceAsync(fromDate, toDate);
            return Ok(performanceList);
        }

        [HttpGet("partner-performance/export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportPartnerPerformanceExcel([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var data = await _reportService.GetPartnerPerformanceAsync(fromDate, toDate);
            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "PartnerPerformanceTemplate.xlsx");
            var bytes = _excelExportService.ExportPartnerPerformanceToExcel(templatePath, data, fromDate, toDate);

            var fileName = $"PartnerPerformance_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        [HttpGet("get-tour-booking-stats")]
        public async Task<IActionResult> GetTourBookingStats([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var tourBookingStats = await _reportService.GetTourBookingStatsAsync(fromDate, toDate);
            return Ok(tourBookingStats);
        }

        [HttpGet("tour-booking-stats/export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportTourBookingStatsExcel([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var data = await _reportService.GetTourBookingStatsAsync(fromDate, toDate);

            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "TourBookingStatsTemplate.xlsx");
            var fileBytes = _excelExportService.ExportTourBookingStatsExcel(templatePath, data);

            var fileName = $"TourBookingStats_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

    }

}
