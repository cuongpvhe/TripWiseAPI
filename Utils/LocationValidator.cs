using System.Text.RegularExpressions;

namespace TripWiseAPI.Utils
{
    /// <summary>
    /// Utility class để validate và kiểm tra các yêu cầu thay đổi địa điểm trong tin nhắn người dùng.
    /// </summary>
    public static class LocationValidator
    {
        /// <summary>
        /// Kiểm tra xem tin nhắn người dùng có chứa yêu cầu thay đổi địa điểm không.
        /// </summary>
        /// <param name="userMessage">Tin nhắn từ người dùng</param>
        /// <param name="currentDestination">Địa điểm hiện tại trong lịch trình</param>
        /// <returns>True nếu phát hiện yêu cầu thay đổi địa điểm, False nếu ngược lại</returns>
        public static bool IsLocationChangeRequest(string userMessage, string currentDestination)
        {
            if (string.IsNullOrWhiteSpace(userMessage) || string.IsNullOrWhiteSpace(currentDestination))
                return false;

            Console.WriteLine($"[LocationValidator] Checking message: '{userMessage}' vs current: '{currentDestination}'");

            // Các pattern để detect thay đổi địa điểm - CẢI THIỆN ĐỘ CHÍNH XÁC
            var locationChangePatterns = new[]
            {
                // Pattern rõ ràng thay đổi địa điểm
                @"đi\s+(đến|tới)\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                @"chuyển\s+(đến|tới)\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                @"sang\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]{3,}?)(?:\s|$|,|\.|!|\?)",
                
                // Thay đổi điểm đến explicit
                @"thay\s+đổi\s+.*(điểm\s+đến|địa\s+điểm).*?(?:thành|sang)\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                @"đổi\s+.*(điểm\s+đến|địa\s+điểm).*?(?:thành|sang)\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                
                // Du lịch ở nơi khác - CHỈ KHI KHÔNG CÓ HOẠT ĐỘNG
                @"^(?!.*(?:ăn|uống|chơi|tham quan|mua sắm|nghỉ ngơi)).*du\s+lịch\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                
                // Từ [địa điểm cũ] đến [địa điểm mới]
                @"từ\s+.+?\s+(?:đến|tới)\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                
                // Pattern nguy hiểm: "ở [địa điểm khác]" - CHỈ KHI KHÔNG CÓ NGÀY/GIỜ/HOẠT ĐỘNG
                @"^(?!.*(?:ngày\s+\d+|giờ|\d{1,2}:\d{2}|ăn|uống|chơi|tham quan|mua)).*(?:ở|tại)\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]{3,}?)(?:\s|$|,|\.|!|\?)"
            };

            // Danh sách các từ loại trừ
            var excludeWords = GetExcludeWords();
            
            // Danh sách các thành phố/tỉnh Việt Nam nổi tiếng để cross-check
            var vietnameseCities = GetVietnameseCities();

            foreach (var pattern in locationChangePatterns)
            {
                var matches = Regex.Matches(userMessage, pattern, RegexOptions.IgnoreCase);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var potentialLocation = ExtractLocationFromMatch(match);
                        
                        if (IsValidLocationChange(potentialLocation, currentDestination, excludeWords, vietnameseCities))
                        {
                            Console.WriteLine($"[LocationValidator] DETECTED location change: '{potentialLocation}' vs current '{currentDestination}'");
                            return true;
                        }
                    }
                }
            }

            Console.WriteLine($"[LocationValidator] No location change detected");
            return false;
        }

        /// <summary>
        /// Trích xuất địa điểm từ regex match
        /// </summary>
        private static string ExtractLocationFromMatch(Match match)
        {
            // Lấy group cuối cùng không rỗng
            for (int i = match.Groups.Count - 1; i >= 1; i--)
            {
                if (!string.IsNullOrWhiteSpace(match.Groups[i].Value))
                {
                    return match.Groups[i].Value.Trim();
                }
            }
            return "";
        }

        /// <summary>
        /// Kiểm tra xem địa điểm được phát hiện có phải là thay đổi hợp lệ không.
        /// </summary>
        private static bool IsValidLocationChange(string potentialLocation, string currentDestination, string[] excludeWords, string[] vietnameseCities)
        {
            if (string.IsNullOrWhiteSpace(potentialLocation) || potentialLocation.Length <= 2)
                return false;

            Console.WriteLine($"[LocationValidator] Checking potential location: '{potentialLocation}'");

            // Loại bỏ các từ không phải địa điểm
            bool isExcludedWord = excludeWords.Any(word => 
                potentialLocation.ToLower().Equals(word.ToLower()) || 
                potentialLocation.ToLower().Contains($" {word.ToLower()} ") ||
                potentialLocation.ToLower().StartsWith($"{word.ToLower()} ") ||
                potentialLocation.ToLower().EndsWith($" {word.ToLower()}"));
            
            if (isExcludedWord)
            {
                Console.WriteLine($"[LocationValidator] '{potentialLocation}' is excluded word");
                return false;
            }

            // Kiểm tra có phải tên thành phố/tỉnh thật không
            bool isRealCity = vietnameseCities.Any(city => 
                potentialLocation.ToLower().Contains(city.ToLower()) || 
                city.ToLower().Contains(potentialLocation.ToLower()));

            if (!isRealCity)
            {
                Console.WriteLine($"[LocationValidator] '{potentialLocation}' is not a real Vietnamese city");
                return false;
            }

            // So sánh với destination hiện tại
            bool isDifferentLocation = !potentialLocation.ToLower().Contains(currentDestination.ToLower()) &&
                                     !currentDestination.ToLower().Contains(potentialLocation.ToLower()) &&
                                     !AreEquivalentLocations(potentialLocation, currentDestination);
            
            Console.WriteLine($"[LocationValidator] Different location check: {isDifferentLocation}");
            return isDifferentLocation;
        }

        /// <summary>
        /// Kiểm tra 2 địa điểm có tương đương không (ví dụ: "Hà Nội" và "Thủ đô")
        /// </summary>
        private static bool AreEquivalentLocations(string location1, string location2)
        {
            var equivalents = new Dictionary<string, string[]>
            {
                { "hà nội", new[] { "thủ đô", "hanoi", "hà nội" } },
                { "hồ chí minh", new[] { "sài gòn", "tp.hcm", "tphcm", "saigon", "hcm" } },
                { "đà nẵng", new[] { "da nang", "đà nẵng" } },
                { "cần thơ", new[] { "can tho", "cần thơ" } },
                { "nha trang", new[] { "nha trang", "khánh hòa" } },
                { "đà lạt", new[] { "da lat", "đà lạt", "lâm đồng" } }
            };

            string loc1Lower = location1.ToLower();
            string loc2Lower = location2.ToLower();

            foreach (var equiv in equivalents)
            {
                bool loc1Match = equiv.Value.Any(e => loc1Lower.Contains(e) || e.Contains(loc1Lower));
                bool loc2Match = equiv.Value.Any(e => loc2Lower.Contains(e) || e.Contains(loc2Lower));
                
                if (loc1Match && loc2Match)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Lấy danh sách các từ loại trừ (không phải địa điểm).
        /// </summary>
        private static string[] GetExcludeWords()
        {
            return new[]
            {
                "ăn", "uống", "chơi", "tham quan", "mua", "sắm", "nghỉ", "ngơi", 
                "spa", "massage", "cà phê", "trà", "bia", "rượu", "karaoke",
                "bar", "pub", "club", "disco", "cinema", "rạp", "phim",
                "bãi biển", "biển", "núi", "rừng", "sông", "hồ", "suối",
                "chùa", "đền", "điện", "phố cổ", "phố đi bộ", "chợ", "siêu thị",
                "khách sạn", "homestay", "resort", "villa", "nhà nghỉ",
                "buổi sáng", "buổi trưa", "buổi chiều", "buổi tối", "ban đêm",
                "sáng", "trưa", "chiều", "tối", "đêm", "ngày", "giờ",
                "bảo tàng", "công viên", "trung tâm", "quán", "cửa hàng",
                "thời gian", "lúc", "khi", "bây giờ", "lúc này", "bánh mì",
                "phở", "bún", "cơm", "chè", "món"
            };
        }

        /// <summary>
        /// Lấy danh sách các thành phố/tỉnh Việt Nam
        /// </summary>
        private static string[] GetVietnameseCities()
        {
            return new[]
            {
                "hà nội", "hanoi", "thủ đô",
                "hồ chí minh", "sài gòn", "tphcm", "tp.hcm", "saigon",
                "đà nẵng", "da nang",
                "hải phòng", "hai phong",
                "cần thơ", "can tho",
                "nha trang", "khánh hòa",
                "đà lạt", "da lat", "lâm đồng",
                "vũng tàu", "vung tau", "bà rịa",
                "huế", "hue", "thừa thiên huế",
                "hội an", "hoi an", "quảng nam",
                "phú quốc", "phu quoc", "kiên giang",
                "quy nhon", "quy nhơn", "bình định",
                "vinh", "nghệ an",
                "thai nguyen", "thái nguyên",
                "nam định", "nam dinh",
                "buon ma thuot", "buôn ma thuột", "đắk lắk",
                "long xuyen", "long xuyên", "an giang",
                "phan thiet", "phan thiết", "bình thuận",
                "ha long", "hạ long", "quảng ninh",
                "sapa", "sa pa", "lào cai",
                "cao bang", "cao bằng",
                "lang son", "lạng sơn",
                "dien bien", "điện biên"
            };
        }

        /// <summary>
        /// Phân tích tin nhắn để trích xuất địa điểm được đề cập.
        /// </summary>
        public static List<string> ExtractMentionedLocations(string userMessage)
        {
            var locations = new List<string>();
            var excludeWords = GetExcludeWords();
            var vietnameseCities = GetVietnameseCities();

            var locationPatterns = new[]
            {
                @"(?:đi|đến|tới|sang|ở|tại|trong)\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                @"du\s+lịch\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)",
                @"muốn\s+đi\s+([A-Za-z\s\u00C0-\u024F\u1E00-\u1EFF]+?)(?:\s|$|,|\.|!|\?)"
            };

            foreach (var pattern in locationPatterns)
            {
                var matches = Regex.Matches(userMessage, pattern, RegexOptions.IgnoreCase);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var location = match.Groups[1].Value.Trim();
                        
                        bool isExcluded = excludeWords.Any(word => 
                            location.ToLower().Contains(word.ToLower()));

                        bool isRealCity = vietnameseCities.Any(city => 
                            location.ToLower().Contains(city.ToLower()) || 
                            city.ToLower().Contains(location.ToLower()));
                        
                        if (!isExcluded && isRealCity && location.Length > 2 && !locations.Contains(location))
                        {
                            locations.Add(location);
                        }
                    }
                }
            }

            return locations;
        }
    }
}