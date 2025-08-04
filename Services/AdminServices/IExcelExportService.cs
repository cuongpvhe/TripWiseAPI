// File: Services/Interfaces/IExcelExportService.cs
using System;
using System.Collections.Generic;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IExcelExportService
    {
        byte[] ExportRevenueFromExcelTemplate(
            string templatePath,
            List<RevenueDetailDto> details,
            List<RevenueSummaryDto> totals,
            DateTime fromDate,
            DateTime toDate
        );

        byte[] ExportPartnerPerformanceToExcel(
            string templatePath,
            List<PartnerPerformanceDto> data,
            DateTime fromDate,
            DateTime toDate
        );

        byte[] ExportTourBookingStatsExcel(
            string templatePath,
            List<TourBookingStatDto> data
        );
    }
}
