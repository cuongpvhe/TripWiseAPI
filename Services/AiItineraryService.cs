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
        private readonly IWikimediaImageService _imageService;
        private const int MaxDaysPerChunk = 3;

        public AiItineraryService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            IPromptBuilder promptBuilder,
            IJsonRepairService repairService,
            IWikimediaImageService imageService)
        {
            _httpClient = httpClientFactory.CreateClient("Gemini");
            _apiKey = config["Gemini:ApiKey"];
            _promptBuilder = promptBuilder;
            _repairService = repairService;
            _imageService = imageService;
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

            var imageUrlsUsed = new HashSet<string>();
            var allDays = new List<ItineraryDay>();

            string fallbackImage = "https://cdn.thuvienphapluat.vn/uploads/tintuc/2024/02/23/viet-nam-nam-tren-ban-dao-nao.jpg";

            foreach (var d in parsed.Days)
            {
                var activities = await Task.WhenAll(d.Activities.Select(async a =>
                {
                    string imageUrl = a.Image;

                    bool isFallbackImage = string.IsNullOrWhiteSpace(imageUrl)
                        || imageUrl.Contains("unsplash")
                        || imageUrl.Contains("wikipedia")
                        || imageUrl.Contains("example.com")
                        || imageUrl.Contains("vietflag.vn");

                    if (isFallbackImage)
                    {
                        Console.WriteLine($"[Image] Fallback detected: {imageUrl}");

                        // 🔁 Ưu tiên tìm ảnh theo địa điểm cụ thể
                        string searchKeyword = !string.IsNullOrWhiteSpace(a.Address)
                            ? a.Address
                            : !string.IsNullOrWhiteSpace(a.PlaceDetail)
                                ? a.PlaceDetail
                                : request.Destination;

                        Console.WriteLine($"[Image] Searching image for: {searchKeyword}");

                        var imageCandidates = await _imageService.SearchImageUrlsAsync(searchKeyword);
                        Console.WriteLine($"[Image] Wikimedia search returned {imageCandidates.Count} result(s)");

                        imageUrl = imageCandidates.FirstOrDefault(url => !imageUrlsUsed.Contains(url)) ?? fallbackImage;

                        if (!imageUrlsUsed.Contains(imageUrl))
                        {
                            imageUrlsUsed.Add(imageUrl);
                            Console.WriteLine($"[Image] Selected new image from Wikimedia or fallback: {imageUrl}");
                        }
                        else
                        {
                            Console.WriteLine($"[Image] Reused fallback or duplicate image: {imageUrl}");
                        }
                    }

                    return new ItineraryActivity
                    {
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Description = a.Description,
                        EstimatedCost = a.EstimatedCost,
                        Transportation = a.Transportation,
                        Address = a.Address,
                        PlaceDetail = a.PlaceDetail,
                        Image = imageUrl,
                        MapUrl = string.IsNullOrWhiteSpace(a.Address) ? null
                            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                    };
                }));

                allDays.Add(new ItineraryDay
                {
                    DayNumber = d.DayNumber,
                    Title = d.Title,
                    DailyCost = d.DailyCost,
                    WeatherNote = d.WeatherNote,
                    Activities = activities.ToList()
                });
            }

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
        public async Task<ItineraryResponse> UpdateItineraryAsync(TravelRequest originalRequest,ItineraryResponse originalResponse,string userInstruction)
        {
            string prompt = _promptBuilder.BuildUpdatePrompt(originalRequest, originalResponse, userInstruction);


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
            Console.WriteLine("🧠 Prompt gửi lên:\n" + prompt);
            Console.WriteLine("📨 Kết quả Gemini trả về:\n" + raw);


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

            var imageUrlsUsed = new HashSet<string>();
            var allDays = new List<ItineraryDay>();
            string fallbackImage = "https://cdn.thuvienphapluat.vn/uploads/tintuc/2024/02/23/viet-nam-nam-tren-ban-dao-nao.jpg";

            foreach (var d in parsed.Days)
            {
                var activities = await Task.WhenAll(d.Activities.Select(async a =>
                {
                    string imageUrl = a.Image;

                    bool isFallbackImage = string.IsNullOrWhiteSpace(imageUrl)
                        || imageUrl.Contains("unsplash")
                        || imageUrl.Contains("wikipedia")
                        || imageUrl.Contains("example.com")
                        || imageUrl.Contains("vietflag.vn");

                    if (isFallbackImage)
                    {
                        string searchKeyword = !string.IsNullOrWhiteSpace(a.Address)
                            ? a.Address
                            : !string.IsNullOrWhiteSpace(a.PlaceDetail)
                                ? a.PlaceDetail
                                : originalRequest.Destination;

                        var imageCandidates = await _imageService.SearchImageUrlsAsync(searchKeyword);
                        imageUrl = imageCandidates.FirstOrDefault(url => !imageUrlsUsed.Contains(url)) ?? fallbackImage;

                        if (!imageUrlsUsed.Contains(imageUrl))
                        {
                            imageUrlsUsed.Add(imageUrl);
                        }
                    }

                    return new ItineraryActivity
                    {
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Description = a.Description,
                        EstimatedCost = a.EstimatedCost,
                        Transportation = a.Transportation,
                        Address = a.Address,
                        PlaceDetail = a.PlaceDetail,
                        Image = imageUrl,
                        MapUrl = string.IsNullOrWhiteSpace(a.Address) ? null
                            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                    };
                }));

                allDays.Add(new ItineraryDay
                {
                    DayNumber = d.DayNumber,
                    Title = d.Title,
                    DailyCost = d.DailyCost,
                    WeatherNote = d.WeatherNote,
                    Activities = activities.ToList()
                });
            }

            return new ItineraryResponse
            {
                Destination = originalRequest.Destination,
                Days = originalRequest.Days,
                Preferences = originalRequest.Preferences,
                TravelDate = originalRequest.TravelDate,
                Transportation = originalRequest.Transportation,
                DiningStyle = originalRequest.DiningStyle,
                GroupType = originalRequest.GroupType,
                Accommodation = originalRequest.Accommodation,
                TotalEstimatedCost = parsed.TotalCost,
                Budget = (int)originalRequest.BudgetVND,
                Itinerary = allDays,
                SuggestedAccommodation = $"https://www.google.com/maps/search/?q=khách+sạn+{Uri.EscapeDataString(originalRequest.Accommodation ?? "3 sao")}+sao+{Uri.EscapeDataString(originalRequest.Destination)}",
                HasMore = false,
                NextStartDate = null
            };
        }


        private string ExtractJson(string raw)
        {
            raw = raw.Replace("```json", "").Replace("```", "").Trim();
            raw = Regex.Replace(raw, "[“”]", "\"")
                      .Replace("\u200b", "")
                      .Replace("\u00a0", " ");
            var match = Regex.Match(raw, @"\{[\s\S]*\}");
            return match.Success ? match.Value : raw;
        }
    }
}
