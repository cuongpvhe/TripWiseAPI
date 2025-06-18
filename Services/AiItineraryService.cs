using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TripWiseAPI.Model;

namespace TripWiseAPI.Services
{
    public class AiItineraryService : IAiItineraryService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IPromptBuilder _promptBuilder;
        private readonly IJsonRepairService _repairService;
        private const int MaxDaysPerChunk = 3;

        public AiItineraryService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            IPromptBuilder promptBuilder,
            IJsonRepairService repairService)
        {
            _httpClient = httpClientFactory.CreateClient("Gemini");
            _apiKey = config["Gemini:ApiKey"];
            _promptBuilder = promptBuilder;
            _repairService = repairService;
            Console.OutputEncoding = Encoding.UTF8;
        }

        public async Task<ItineraryResponse> GenerateItineraryAsync(TravelRequest request, string relatedKnowledge)
        {
            int budgetVND = (int)request.BudgetVND;
            string budgetFormatted = budgetVND.ToString("N0", CultureInfo.InvariantCulture);

            int daysToGenerate = Math.Min(request.Days, MaxDaysPerChunk);

            var subRequest = new TravelRequest
            {
                Destination = request.Destination,
                TravelDate = request.TravelDate,
                Days = daysToGenerate,
                Preferences = request.Preferences,
                BudgetVND = request.BudgetVND,
                Transportation = request.Transportation,
                DiningStyle = request.DiningStyle,
                GroupType = request.GroupType,
                Accommodation = request.Accommodation
            };

            string prompt = _promptBuilder.Build(subRequest, budgetFormatted, relatedKnowledge);

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
                    maxOutputTokens = 4096,
                    temperature = 0.7
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}", payload);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Gemini API failed: {await response.Content.ReadAsStringAsync()}");

            string text = await response.Content.ReadAsStringAsync();
            string raw = JsonDocument.Parse(text)
                .RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString();

            string cleanedJson = ExtractJson(raw);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(cleanedJson);
            }
            catch
            {
                string? repaired = await _repairService.TryRepairAsync(cleanedJson);
                if (repaired == null) throw new Exception("JSON repair failed");
                doc = JsonDocument.Parse(repaired);
            }

            var parsed = JsonSerializer.Deserialize<JsonItineraryFormat>(doc.RootElement.GetRawText());
            if (parsed?.Days == null) throw new Exception("Invalid or empty itinerary");

            var allDays = parsed.Days.Select(d => new ItineraryDay
            {
                DayNumber = d.DayNumber,
                Title = d.Title,
                DailyCost = d.DailyCost,
                Activities = d.Activities.Select(a => new ItineraryActivity
                {
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Description = a.Description,
                    EstimatedCost = a.EstimatedCost,
                    Transportation = a.Transportation,
                    Address = a.Address,
                    PlaceDetail = a.PlaceDetail,
                    Image = a.Image,
                    MapUrl = string.IsNullOrWhiteSpace(a.Address) ? null
                        : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                }).ToList()
            }).ToList();

            return new ItineraryResponse
            {
                Destination = request.Destination,
                Days = daysToGenerate,
                Preferences = request.Preferences,
                TravelDate = request.TravelDate,
                Transportation = request.Transportation,
                DiningStyle = request.DiningStyle,
                GroupType = request.GroupType,
                Accommodation = request.Accommodation,
                TotalEstimatedCost = parsed.TotalCost,
                Budget = budgetVND,
                Itinerary = allDays,
                SuggestedAccommodation = $"https://www.google.com/maps/search/?q=khách+sạn+{Uri.EscapeDataString(request.Accommodation ?? "3 sao")}+sao+{Uri.EscapeDataString(request.Destination)}",
                HasMore = request.Days > MaxDaysPerChunk,
                NextStartDate = request.Days > MaxDaysPerChunk
                    ? request.TravelDate.AddDays(MaxDaysPerChunk)
                    : null
            };
        }


        private string ExtractJson(string raw)
        {
            raw = raw.Replace("```json", "").Replace("```", "").Trim();
            raw = Regex.Replace(raw, @"[“”]", "\"")
                      .Replace("\u200b", "")
                      .Replace("\u00a0", " ");
            var match = Regex.Match(raw, @"\{[\s\S]*\}");
            return match.Success ? match.Value : raw;
        }
    }
}
