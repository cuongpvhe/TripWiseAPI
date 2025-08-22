using System.Text.Json;
using TripWiseAPI.Model;

namespace TripWiseAPI.Utils
{
    /// <summary>
    /// Utility class để so sánh hai lịch trình du lịch và phát hiện sự khác biệt.
    /// </summary>
    public static class ItineraryComparer
    {
        /// <summary>
        /// Kiểm tra xem hai lịch trình có giống nhau không.
        /// </summary>
        /// <param name="original">Lịch trình gốc</param>
        /// <param name="updated">Lịch trình sau khi cập nhật</param>
        /// <returns>True nếu hai lịch trình giống nhau</returns>
        public static bool AreItinerariesIdentical(ItineraryResponse original, ItineraryResponse updated)
        {
            if (original == null || updated == null)
                return false;

            // So sánh số ngày
            if (original.Itinerary.Count != updated.Itinerary.Count)
                return false;

            // So sánh từng ngày
            for (int i = 0; i < original.Itinerary.Count; i++)
            {
                var originalDay = original.Itinerary[i];
                var updatedDay = updated.Itinerary[i];

                if (!AreDaysIdentical(originalDay, updatedDay))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Kiểm tra xem hai ngày trong lịch trình có giống nhau không.
        /// </summary>
        private static bool AreDaysIdentical(ItineraryDay originalDay, ItineraryDay updatedDay)
        {
            // So sánh thông tin cơ bản của ngày
            if (originalDay.DayNumber != updatedDay.DayNumber ||
                originalDay.Title != updatedDay.Title ||
                originalDay.Activities.Count != updatedDay.Activities.Count)
                return false;

            // So sánh từng hoạt động
            for (int i = 0; i < originalDay.Activities.Count; i++)
            {
                var originalActivity = originalDay.Activities[i];
                var updatedActivity = updatedDay.Activities[i];

                if (!AreActivitiesIdentical(originalActivity, updatedActivity))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Kiểm tra xem hai hoạt động có giống nhau không.
        /// </summary>
        private static bool AreActivitiesIdentical(ItineraryActivity originalActivity, ItineraryActivity updatedActivity)
        {
            return originalActivity.StartTime == updatedActivity.StartTime &&
                   originalActivity.EndTime == updatedActivity.EndTime &&
                   originalActivity.Description == updatedActivity.Description &&
                   originalActivity.Address == updatedActivity.Address &&
                   originalActivity.Transportation == updatedActivity.Transportation &&
                   originalActivity.EstimatedCost == updatedActivity.EstimatedCost &&
                   originalActivity.PlaceDetail == updatedActivity.PlaceDetail;
        }

        /// <summary>
        /// Phân tích sự khác biệt giữa hai lịch trình và tạo thông báo thân thiện.
        /// </summary>
        /// <param name="original">Lịch trình gốc</param>
        /// <param name="updated">Lịch trình sau khi cập nhật</param>
        /// <param name="userMessage">Tin nhắn yêu cầu từ người dùng</param>
        /// <returns>Thông báo mô tả sự thay đổi hoặc thiếu thay đổi</returns>
        public static ItineraryComparisonResult AnalyzeChanges(ItineraryResponse original, ItineraryResponse updated, string userMessage)
        {
            if (AreItinerariesIdentical(original, updated))
            {
                return new ItineraryComparisonResult
                {
                    HasChanges = false,
                    IsIdentical = true,
                    Message = GenerateNoChangeMessage(userMessage),
                    DetailedMessage = "Lịch trình hiện tại của bạn đã phù hợp với yêu cầu này rồi, vì vậy không cần thay đổi gì thêm.",
                    ChangesCount = 0
                };
            }

            var changes = DetectSpecificChanges(original, updated);
            return new ItineraryComparisonResult
            {
                HasChanges = true,
                IsIdentical = false,
                Message = $"Đã cập nhật thành công {changes.Count} thay đổi trong lịch trình của bạn.",
                DetailedMessage = string.Join("\n", changes),
                ChangesCount = changes.Count
            };
        }

        /// <summary>
        /// Tạo thông báo thân thiện khi không có thay đổi nào.
        /// </summary>
        private static string GenerateNoChangeMessage(string userMessage)
        {
            var friendlyMessages = new[]
            {
                "Lịch trình hiện tại của bạn đã rất phù hợp với yêu cầu này rồi! ✨",
                "Có vẻ như lịch trình của bạn đã hoàn hảo cho yêu cầu này rồi đấy! 👌",
                "Lịch trình hiện tại đã bao gồm những gì bạn muốn rồi, không cần điều chỉnh thêm! 😊",
                "Tuyệt vời! Lịch trình của bạn đã sẵn sàng cho yêu cầu này! 🎯",
                "Lịch trình hiện tại đã phù hợp với mong muốn của bạn rồi! ⭐"
            };

            var random = new Random();
            return friendlyMessages[random.Next(friendlyMessages.Length)];
        }

        /// <summary>
        /// Phát hiện các thay đổi cụ thể giữa hai lịch trình.
        /// </summary>
        private static List<string> DetectSpecificChanges(ItineraryResponse original, ItineraryResponse updated)
        {
            var changes = new List<string>();

            for (int i = 0; i < Math.Min(original.Itinerary.Count, updated.Itinerary.Count); i++)
            {
                var originalDay = original.Itinerary[i];
                var updatedDay = updated.Itinerary[i];

                // Kiểm tra thay đổi title ngày
                if (originalDay.Title != updatedDay.Title)
                {
                    changes.Add($"📅 Ngày {originalDay.DayNumber}: Đã cập nhật chủ đề từ \"{originalDay.Title}\" thành \"{updatedDay.Title}\"");
                }

                // Kiểm tra thay đổi hoạt động
                var activityChanges = DetectActivityChanges(originalDay, updatedDay);
                changes.AddRange(activityChanges);
            }

            return changes;
        }

        /// <summary>
        /// Phát hiện thay đổi trong các hoạt động của một ngày.
        /// </summary>
        private static List<string> DetectActivityChanges(ItineraryDay originalDay, ItineraryDay updatedDay)
        {
            var changes = new List<string>();

            for (int i = 0; i < Math.Min(originalDay.Activities.Count, updatedDay.Activities.Count); i++)
            {
                var originalActivity = originalDay.Activities[i];
                var updatedActivity = updatedDay.Activities[i];

                if (!AreActivitiesIdentical(originalActivity, updatedActivity))
                {
                    changes.Add($"🔄 Ngày {originalDay.DayNumber}, hoạt động {i + 1}: Đã thay đổi từ \"{originalActivity.Description}\" thành \"{updatedActivity.Description}\"");
                }
            }

            // Kiểm tra hoạt động mới được thêm
            if (updatedDay.Activities.Count > originalDay.Activities.Count)
            {
                for (int i = originalDay.Activities.Count; i < updatedDay.Activities.Count; i++)
                {
                    changes.Add($"➕ Ngày {originalDay.DayNumber}: Đã thêm hoạt động mới \"{updatedDay.Activities[i].Description}\"");
                }
            }

            return changes;
        }
    }

    /// <summary>
    /// Kết quả so sánh lịch trình.
    /// </summary>
    public class ItineraryComparisonResult
    {
        public bool HasChanges { get; set; }
        public bool IsIdentical { get; set; }
        public string Message { get; set; } = string.Empty;
        public string DetailedMessage { get; set; } = string.Empty;
        public int ChangesCount { get; set; }
    }
}