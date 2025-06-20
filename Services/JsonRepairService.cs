using System.Text.Json;
using System.Text.RegularExpressions;

namespace TripWiseAPI.Services
{
    public class JsonRepairService : IJsonRepairService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public JsonRepairService(IHttpClientFactory factory, IConfiguration config)
        {
            _httpClient = factory.CreateClient("Gemini");
            _apiKey = config["Gemini:ApiKey"];
        }

        public async Task<string?> TryRepairAsync(string brokenJson)
        {
            var prompt = $"""
        Bạn đã trả về đoạn JSON sau nhưng nó bị lỗi không phân tích được:

        {brokenJson}

        Hãy sửa lại JSON này đúng định dạng version "v1.0", không có chữ nào bên ngoài JSON. Trả lại JSON duy nhất, không thêm lời giải thích.
        """;

            var payload = new
            {
                contents = new[] {
                new {
                    parts = new[] {
                        new { text = prompt }
                    }
                }
            },
                generationConfig = new
                {
                    maxOutputTokens = 1000,
                    temperature = 0.3
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}", payload);

            if (!response.IsSuccessStatusCode) return null;

            string text = await response.Content.ReadAsStringAsync();
            string content = JsonDocument.Parse(text)
                .RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString();

            return Regex.Match(content, @"\{[\s\S]*\}").Value;
        }
    }
}
