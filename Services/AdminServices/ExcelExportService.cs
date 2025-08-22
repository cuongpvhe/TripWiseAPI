using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public class ExcelExportService
    {
        public byte[] ExportRevenueFromExcelTemplate(
            string templatePath,
            List<RevenueDetailDto> details,
            List<RevenueSummaryDto> totals,
            DateTime fromDate,
            DateTime toDate)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new FileInfo(templatePath));

            var sheet = package.Workbook.Worksheets["RevenueReport"];
            if (sheet == null)
                throw new Exception("Không tìm thấy sheet RevenueReport");

            // === 1. TIÊU ĐỀ CHÍNH ===
            sheet.Cells[1, 1].Value = $"BẢNG CHI TIẾT DOANH THU ({fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy})";
            sheet.Cells[1, 1, 1, 9].Merge = true;
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 13;
            sheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // === 2. HEADER TIẾNG VIỆT ===
            var headerRow = 2;
            string[] headers = new[]
            {
        "STT", "Tháng", "Loại giao dịch", "Tên mặt hàng/gói", "Số tiền", "Mã người dùng", "Email", "Họ tên", "Ngày giao dịch"
    };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = sheet.Cells[headerRow, col + 1];
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // === 3. DỮ LIỆU CHI TIẾT ===
            // Xóa toàn bộ nội dung + định dạng các dòng dữ liệu cũ (tránh lỗi dòng 4)
            int detailStartRow = 3;
            int detailRowCount = details.Count;
            for (int r = detailStartRow; r < detailStartRow + detailRowCount + 10; r++)
            {
                sheet.Row(r).StyleID = 0; // Xóa style dòng
                for (int c = 1; c <= 8; c++)
                {
                    sheet.Cells[r, c].Clear(); // Xóa nội dung + style từng ô
                }
            }
            for (int i = 0; i < details.Count; i++)
            {
                var d = details[i];
                int row = detailStartRow + i;

                sheet.Cells[row, 1].Value = i + 1; // STT
                sheet.Cells[row, 2].Value = d.Month;
                sheet.Cells[row, 3].Value = d.TransactionType;
                sheet.Cells[row, 4].Value = d.ItemDescription;
                sheet.Cells[row, 5].Value = d.Amount;
                sheet.Cells[row, 5].Style.Numberformat.Format = "#,##0 \"VNĐ\"";
                sheet.Cells[row, 6].Value = d.UserID;
                sheet.Cells[row, 7].Value = d.Email;
                sheet.Cells[row, 8].Value = d.FullName;
                sheet.Cells[row, 9].Value = d.TransactionDate;
                sheet.Cells[row, 9].Style.Numberformat.Format = "dd/MM/yyyy HH:mm:ss";

                // Bo viền từng ô
                for (int col = 1; col <= 9; col++)
                {
                    sheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }
            }

            // === 4. TIÊU ĐỀ "TỔNG DOANH THU THEO THÁNG" ===
            int summaryTitleRow = detailStartRow + details.Count + 2;
            sheet.Cells[summaryTitleRow, 1].Value = $"TỔNG DOANH THU THEO THÁNG ({fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy})";
            sheet.Cells[summaryTitleRow, 1, summaryTitleRow, 9].Merge = true;
            sheet.Cells[summaryTitleRow, 1].Style.Font.Bold = true;
            sheet.Cells[summaryTitleRow, 1].Style.Font.Size = 12;
            sheet.Cells[summaryTitleRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // === 5. HEADER TỔNG DOANH THU ===
            int summaryHeaderRow = summaryTitleRow + 1;
            sheet.Cells[summaryHeaderRow, 1].Value = "STT";
            sheet.Cells[summaryHeaderRow, 2].Value = "Tháng";
            sheet.Cells[summaryHeaderRow, 3].Value = "Tổng đặt tour";
            sheet.Cells[summaryHeaderRow, 4].Value = "Tổng gói";
            sheet.Cells[summaryHeaderRow, 5].Value = "Tổng huỷ đặt tour";
            sheet.Cells[summaryHeaderRow, 6].Value = "Tổng tiền tour";
            sheet.Cells[summaryHeaderRow, 7].Value = "Tổng tiền gói";
            sheet.Cells[summaryHeaderRow, 8].Value = "Tổng tiền từ huỷ tour";
            sheet.Cells[summaryHeaderRow, 9].Value = "Tổng cộng";

            // Định dạng header
            var headerSummary = sheet.Cells[summaryHeaderRow, 1, summaryHeaderRow, 9];
            headerSummary.Style.Font.Bold = true;
            headerSummary.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerSummary.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            

            // BO VIỀN CHO TỪNG Ô
            headerSummary.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            headerSummary.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            headerSummary.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            headerSummary.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            // === 6. DỮ LIỆU TỔNG DOANH THU ===
            int summaryDataRow = summaryHeaderRow + 1;
            for (int i = 0; i < totals.Count; i++)
            {
                var t = totals[i];
                int row = summaryDataRow + i;

                sheet.Cells[row, 1].Value = i + 1; // STT
                sheet.Cells[row, 2].Value = t.Month;
                sheet.Cells[row, 3].Value = t.TotalBookings;
                sheet.Cells[row, 4].Value = t.TotalPlans;
                sheet.Cells[row, 5].Value = t.TotalCancelled;
                sheet.Cells[row, 6].Value = t.TotalBookingRevenue;
                sheet.Cells[row, 7].Value = t.TotalPlanRevenue;
                sheet.Cells[row, 8].Value = t.CancelledRevenue;
                sheet.Cells[row, 9].Value = t.TotalCombinedRevenue;

                for (int col = 6; col <= 9; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0 \"VNĐ\"";
                }

                // Bo viền từng ô từ STT đến Tổng cộng
                for (int col = 1; col <= 9; col++)
                {
                    sheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }
            }

            // === 7. AUTO CĂN CỘT ===
            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }

        public byte[] ExportPartnerPerformanceToExcel(string templatePath, List<PartnerPerformanceDto> data, DateTime fromDate, DateTime toDate)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var fileInfo = new FileInfo(templatePath);
            using var package = new ExcelPackage(fileInfo);
            var sheet = package.Workbook.Worksheets["PartnerPerformance"];

            if (sheet == null)
                throw new Exception("Không tìm thấy sheet 'PartnerPerformance' trong file template.");

            // Tiêu đề động từ A1
            sheet.Cells[1, 1].Value = $"BÁO CÁO HIỆU SUẤT ĐỐI TÁC ({fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy})";
            sheet.Cells[1, 1, 1, 8].Merge = true;
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 12;
            sheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Header cố định tại dòng 2 (Việt hóa)
            sheet.Cells[2, 1].Value = "STT";
            sheet.Cells[2, 2].Value = "Mã đối tác";
            sheet.Cells[2, 3].Value = "Tên đối tác";
            sheet.Cells[2, 4].Value = "Số tour cung cấp";
            sheet.Cells[2, 5].Value = "Số lượt đặt";
            sheet.Cells[2, 6].Value = "Tổng doanh thu";
            sheet.Cells[2, 7].Value = "Số lượt huỷ đặt tour";
            sheet.Cells[2, 8].Value = "Tổng doanh thu từ huỷ đặt tour";

            // Style cho header
            var headerRange = sheet.Cells[2, 1, 2, 8];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightSkyBlue);

            // Ghi dữ liệu từ dòng 3
            int startRow = 3;
            for (int i = 0; i < data.Count; i++)
            {
                var row = startRow + i;
                var item = data[i];

                sheet.Cells[row, 1].Value = i + 1; // STT
                sheet.Cells[row, 2].Value = item.PartnerID;
                sheet.Cells[row, 3].Value = item.PartnerName;
                sheet.Cells[row, 4].Value = item.TotalToursProvided;
                sheet.Cells[row, 5].Value = item.TotalBookings;
                sheet.Cells[row, 6].Value = item.TotalRevenue;
                sheet.Cells[row, 6].Style.Numberformat.Format = "#,##0 \"VNĐ\"";
                sheet.Cells[row, 7].Value = item.TotalCancelled;
                sheet.Cells[row, 8].Value = item.CancelledRevenue;
                sheet.Cells[row, 8].Style.Numberformat.Format = "#,##0 \"VNĐ\"";
            }

            // Kẻ viền toàn bảng
            int endRow = startRow + data.Count - 1;
            var fullTableRange = sheet.Cells[2, 1, endRow, 8];
            fullTableRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            fullTableRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            fullTableRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            fullTableRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            // Tự động căn chỉnh cột
            sheet.Cells.AutoFitColumns();

            return package.GetAsByteArray();
        }
        public byte[] ExportTourBookingStatsExcel(string templatePath, List<TourBookingStatDto> data)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new FileInfo(templatePath));
            var sheet = package.Workbook.Worksheets["TourBookingStats"];

            // Header dòng 2
            sheet.Cells[2, 1].Value = "STT";
            sheet.Cells[2, 2].Value = "Tháng";
            sheet.Cells[2, 3].Value = "Mã Tour";
            sheet.Cells[2, 4].Value = "Tên Tour";
            sheet.Cells[2, 5].Value = "Số lượt đặt";
            sheet.Cells[2, 6].Value = "Tổng doanh thu";
            sheet.Cells[2, 7].Value = "Số lượt huỷ đặt tour";
            sheet.Cells[2, 8].Value = "Tổng doanh thu từ huỷ đặt tour";

            var headerRange = sheet.Cells[2, 1, 2, 8];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightSkyBlue);
            headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);

            int startRow = 3;
            for (int i = 0; i < data.Count; i++)
            {
                var row = startRow + i;
                var item = data[i];

                sheet.Cells[row, 1].Value = i + 1; // STT
                sheet.Cells[row, 2].Value = item.Month;
                sheet.Cells[row, 3].Value = item.TourID;
                sheet.Cells[row, 4].Value = item.TourName;
                sheet.Cells[row, 5].Value = item.TotalBookings;
                sheet.Cells[row, 6].Value = item.TotalRevenue;
                sheet.Cells[row, 6].Style.Numberformat.Format = "#,##0 \"VNĐ\"";
                sheet.Cells[row, 7].Value = item.TotalCancelled;
                sheet.Cells[row, 8].Value = item.CancelledRevenue;
                sheet.Cells[row, 8].Style.Numberformat.Format = "#,##0 \"VNĐ\"";

                // Border từng dòng
                sheet.Cells[row, 1, row, 8].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                sheet.Cells[row, 1, row, 8].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                sheet.Cells[row, 1, row, 8].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                sheet.Cells[row, 1, row, 8].Style.Border.Right.Style = ExcelBorderStyle.Thin;
            }

            sheet.Cells.AutoFitColumns();
            return package.GetAsByteArray();
        }

    }
}
