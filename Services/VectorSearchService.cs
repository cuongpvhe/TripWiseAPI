using System.Net.Http.Json;

namespace TripWiseAPI.Services
{
    public class VectorSearchService
    {
        private readonly HttpClient _httpClient;

        public VectorSearchService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> RetrieveRelevantJsonEntries(
            string destination,
            int topK = 7,
            string groupType = "",
            string diningStyle = "",
            string preferences = "")
        {
            // Xây dựng query nâng cao từ các tham số
            string query = BuildQuery(destination, preferences);

            // Tạo request payload
            var request = new
            {
                destination = destination,
                query = query,
                top_k = topK,
                group_type = groupType,
                dining_style = diningStyle,
                preferences = preferences
            };

            // Thực hiện gọi API tới Flask
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5005/search", request);

            // Kiểm tra phản hồi của API và log lỗi chi tiết nếu cần
            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = await response.Content.ReadAsStringAsync();
                throw new Exception($"Vector API query failed. Error: {errorDetail}");
            }

            // Trả về kết quả từ API dưới dạng chuỗi
            return await response.Content.ReadAsStringAsync();
        }

        private string BuildQuery(string destination, string preferences)
        {
            // Xử lý chuỗi query nếu preferences có giá trị
            if (!string.IsNullOrWhiteSpace(preferences))
            {
                return $"{preferences} tại {destination}".Trim();
            }

            // Nếu preferences trống, chỉ trả về destination
            return destination.Trim();
        }
    }
}
