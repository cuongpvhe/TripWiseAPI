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
        private readonly IWikimediaImageService _WikimediaImageService;
        private const int MaxDaysPerChunk = 3;
        private readonly IGoogleMapsPlaceService _googleMapsPlaceService;

        public AiItineraryService(IHttpClientFactory httpClientFactory, IConfiguration config, IPromptBuilder promptBuilder, IJsonRepairService repairService, IWikimediaImageService imageService, IGoogleMapsPlaceService googleMapsPlaceService)
        {
            _httpClient = httpClientFactory.CreateClient("Gemini");
            _apiKey = config["Gemini:ApiKey"];
            _promptBuilder = promptBuilder;
            _repairService = repairService;
            _WikimediaImageService = imageService;
            _googleMapsPlaceService = googleMapsPlaceService;
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
            Console.WriteLine("===== PROMPT SENT TO GEMINI =====");
            Console.WriteLine(prompt);
            Console.WriteLine("===== END PROMPT =====");

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
                    maxOutputTokens = 25000,
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

            var fallbackImages = new[]
            {
        "https://cdn.thuvienphapluat.vn/uploads/tintuc/2024/02/23/viet-nam-nam-tren-ban-dao-nao.jpg",
        "https://cdn.tcdulichtphcm.vn/upload/4-2022/images/2022-11-16/1668604785-6.jpeg",
        "https://www.ticgroup.vn/uploads/images/images/cat_ba.jpg",
        "https://s-aicmscdn.vietnamhoinhap.vn/vnhn-media/24/1/18/dulichvn_65a88ab6bdc3a.jpg",
        "https://dulichtoday.vn/wp-content/uploads/2017/04/pho-co-Hoi-An.jpg"
    };
            var random = new Random();

            foreach (var d in parsed.Days)
            {
                var activities = await Task.WhenAll(d.Activities.Select(async a =>
                {
                    string? imageUrl = null;

                    if (!string.IsNullOrWhiteSpace(a.Image))
                    {
                        imageUrl = a.Image;
                    }
                    else
                    {
                        try
                        {
                            var imageUrls = await _WikimediaImageService.SearchImageUrlsAsync(request.Destination);
                            var selected = imageUrls.FirstOrDefault(url => !imageUrlsUsed.Contains(url));

                            if (!string.IsNullOrWhiteSpace(selected))
                            {
                                imageUrl = selected;
                                imageUrlsUsed.Add(imageUrl);
                                Console.WriteLine($"[Image] Wikimedia image used: {imageUrl}");
                            }
                            else
                            {
                                Console.WriteLine($"[Image] Wikimedia fallback used for: {request.Destination}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Image] Wikimedia error: {ex.Message}");
                        }

                        imageUrl ??= fallbackImages[random.Next(fallbackImages.Length)];

                        /*
                        // ======= Đã comment đoạn xử lý ảnh từ Google Maps =======
                        var (lat, lng, googleImage) = await _googleMapsPlaceService.GetPlaceInfoAsync(searchKeyword);

                        if (!string.IsNullOrWhiteSpace(googleImage) && !imageUrlsUsed.Contains(googleImage))
                        {
                            imageUrl = googleImage;
                            imageUrlsUsed.Add(imageUrl);
                            Console.WriteLine($"[Image] Google Maps image used: {imageUrl}");
                        }
                        else
                        {
                            Console.WriteLine($"[Image] Google Maps fallback for: {searchKeyword}");
                        }
                        ==========================================================
                        */
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
                        MapUrl = string.IsNullOrWhiteSpace(a.Address)
                            ? null
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


        public async Task<ItineraryChunkResponse> GenerateChunkAsync(
    TravelRequest baseRequest,
    DateTime startDate,
    int chunkSize,
    int chunkIndex,
    string relatedKnowledge,
    List<string> previousAddresses)
        {
            var chunkRequest = new TravelRequest
            {
                Destination = baseRequest.Destination,
                TravelDate = startDate,
                Days = chunkSize,
                Preferences = baseRequest.Preferences,
                BudgetVND = baseRequest.BudgetVND,
                Transportation = baseRequest.Transportation,
                DiningStyle = baseRequest.DiningStyle,
                GroupType = baseRequest.GroupType,
                Accommodation = baseRequest.Accommodation
            };

            string joinedPrevious = string.Join(", ", previousAddresses.Distinct());
            string prompt = _promptBuilder.Build(chunkRequest, baseRequest.BudgetVND.ToString("N0", CultureInfo.InvariantCulture), relatedKnowledge)
                           + $"\n\nLưu ý: Không lặp lại các địa điểm sau trong lịch trình tiếp theo: {joinedPrevious}.";

            Console.WriteLine("===== PROMPT FOR CHUNK =====");
            Console.WriteLine(prompt);

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
                    maxOutputTokens = 25000,
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

            var allDays = new List<ItineraryDay>();
            var imageUrlsUsed = new HashSet<string>();
            var random = new Random();

            var fallbackImages = new[]
            {
        "https://cdn.thuvienphapluat.vn/uploads/tintuc/2024/02/23/viet-nam-nam-tren-ban-dao-nao.jpg",
        "https://cdn.tcdulichtphcm.vn/upload/4-2022/images/2022-11-16/1668604785-6.jpeg",
        "https://www.ticgroup.vn/uploads/images/images/cat_ba.jpg",
        "https://s-aicmscdn.vietnamhoinhap.vn/vnhn-media/24/1/18/dulichvn_65a88ab6bdc3a.jpg",
        "https://dulichtoday.vn/wp-content/uploads/2017/04/pho-co-Hoi-An.jpg"
    };

            List<string> wikimediaImages;
            try
            {
                wikimediaImages = await _WikimediaImageService.SearchImageUrlsAsync(baseRequest.Destination);
                Console.WriteLine($"[Image] Retrieved {wikimediaImages.Count} Wikimedia image(s) for {baseRequest.Destination}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Image] Wikimedia image search failed: {ex.Message}");
                wikimediaImages = new List<string>();
            }

            foreach (var d in parsed.Days)
            {
                var activities = await Task.WhenAll(d.Activities.Select(async a =>
                {
                    string? imageUrl = a.Image;

                    if (string.IsNullOrWhiteSpace(imageUrl))
                    {
                        var availableImages = wikimediaImages.Where(url => !imageUrlsUsed.Contains(url)).ToList();
                        if (availableImages.Any())
                        {
                            imageUrl = availableImages[random.Next(availableImages.Count)];
                            imageUrlsUsed.Add(imageUrl);
                            Console.WriteLine($"[Image] Wikimedia image used: {imageUrl}");
                        }
                        else
                        {
                            imageUrl = fallbackImages[random.Next(fallbackImages.Length)];
                            Console.WriteLine("[Image] Fallback image used.");
                        }

                        /*
                        // ======= Đã comment đoạn xử lý ảnh từ Google Maps =======
                        string searchKeyword = !string.IsNullOrWhiteSpace(a.Address)
                            ? a.Address
                            : a.PlaceDetail ?? baseRequest.Destination;

                        try
                        {
                            var (lat, lng, googleImage) = await _googleMapsPlaceService.GetPlaceInfoAsync(searchKeyword);
                            if (!string.IsNullOrWhiteSpace(googleImage) && !imageUrlsUsed.Contains(googleImage))
                            {
                                imageUrl = googleImage;
                                imageUrlsUsed.Add(imageUrl);
                            }
                        }
                        catch
                        {
                            // fallback
                        }

                        imageUrl ??= fallbackImage;
                        ==========================================================
                        */
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
                        MapUrl = string.IsNullOrWhiteSpace(a.Address)
                            ? null
                            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                    };
                }));

                allDays.Add(new ItineraryDay
                {
                    DayNumber = d.DayNumber + chunkIndex * MaxDaysPerChunk,
                    Title = d.Title,
                    DailyCost = d.DailyCost,
                    WeatherNote = d.WeatherNote,
                    Activities = activities.ToList()
                });
            }

            return new ItineraryChunkResponse
            {
                Success = true,
                Itinerary = allDays,
                TotalEstimatedCost = parsed.TotalCost,
                HasMore = (chunkIndex + 1) * MaxDaysPerChunk < baseRequest.Days,
                NextStartDate = (chunkIndex + 1) * MaxDaysPerChunk < baseRequest.Days
                    ? startDate.AddDays(MaxDaysPerChunk)
                    : null,
                NextChunkIndex = chunkIndex + 1
            };
        }



        public async Task<ItineraryResponse> UpdateItineraryAsync(
    TravelRequest originalRequest,
    ItineraryResponse originalResponse,
    string userInstruction)
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
                    maxOutputTokens = 25000,
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

            Console.WriteLine(" Prompt gửi lên:\n" + prompt);
            Console.WriteLine(" Kết quả Gemini trả về:\n" + raw);

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
            if (parsed?.Days == null)
                throw new Exception("Invalid itinerary format: days == null");

            if (parsed.Days.Count == 0)
            {
                Console.WriteLine(" Gemini không trả về bất kỳ ngày nào được cập nhật. Giữ nguyên lịch trình gốc.");
                return originalResponse;
            }

            Console.WriteLine($"Gemini trả về {parsed.Days.Count} ngày được cập nhật:");
            foreach (var d in parsed.Days)
                Console.WriteLine($"- Ngày {d.DayNumber}: {d.Title} với {d.Activities.Count} hoạt động");

            var imageUrlsUsed = new HashSet<string>();
            var fallbackImages = new[]
            {
        "https://cdn.thuvienphapluat.vn/uploads/tintuc/2024/02/23/viet-nam-nam-tren-ban-dao-nao.jpg",
        "https://cdn.tcdulichtphcm.vn/upload/4-2022/images/2022-11-16/1668604785-6.jpeg",
        "https://www.ticgroup.vn/uploads/images/images/cat_ba.jpg",
        "https://s-aicmscdn.vietnamhoinhap.vn/vnhn-media/24/1/18/dulichvn_65a88ab6bdc3a.jpg",
        "https://dulichtoday.vn/wp-content/uploads/2017/04/pho-co-Hoi-An.jpg"
    };
            var random = new Random();

            List<string> wikimediaImages;
            try
            {
                wikimediaImages = await _WikimediaImageService.SearchImageUrlsAsync(originalRequest.Destination);
                Console.WriteLine($"[Image] Wikimedia image pool size: {wikimediaImages.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Image] Wikimedia fetch error: {ex.Message}");
                wikimediaImages = new List<string>();
            }

            var mergedDays = new List<ItineraryDay>();

            foreach (var originalDay in originalResponse.Itinerary)
            {
                var updatedDay = parsed.Days.FirstOrDefault(d => d.DayNumber == originalDay.DayNumber);

                if (updatedDay == null)
                {
                    mergedDays.Add(originalDay); // giữ nguyên nếu không có thay đổi
                }
                else
                {
                    var updatedActivities = await Task.WhenAll(updatedDay.Activities.Select(async a =>
                    {
                        string? imageUrl = a.Image;

                        if (string.IsNullOrWhiteSpace(imageUrl))
                        {
                            var availableImages = wikimediaImages.Where(url => !imageUrlsUsed.Contains(url)).ToList();

                            if (availableImages.Any())
                            {
                                imageUrl = availableImages[random.Next(availableImages.Count)];
                                imageUrlsUsed.Add(imageUrl);
                                Console.WriteLine($"[Image] Wikimedia image used: {imageUrl}");
                            }
                            else
                            {
                                imageUrl = fallbackImages[random.Next(fallbackImages.Length)];
                                Console.WriteLine("[Image] Fallback image used.");
                            }

                            /*
                            // ======= xử lý ảnh từ Google Maps =======
                            string searchKeyword = !string.IsNullOrWhiteSpace(a.Address)
                                ? a.Address
                                : !string.IsNullOrWhiteSpace(a.PlaceDetail)
                                    ? a.PlaceDetail
                                    : originalRequest.Destination;

                            try
                            {
                                var (lat, lng, googleImage) = await _googleMapsPlaceService.GetPlaceInfoAsync(searchKeyword);

                                if (!string.IsNullOrWhiteSpace(googleImage) && !imageUrlsUsed.Contains(googleImage))
                                {
                                    imageUrl = googleImage;
                                    imageUrlsUsed.Add(imageUrl);
                                    Console.WriteLine($"[Image] Google Maps image used: {imageUrl}");
                                }
                                else
                                {
                                    Console.WriteLine($"[Image] Google Maps fallback for: {searchKeyword}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Image] Google Maps error: {ex.Message}");
                            }

                            imageUrl ??= fallbackImage;
                            ==========================================================
                            */
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
                            MapUrl = string.IsNullOrWhiteSpace(a.Address)
                                ? null
                                : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                        };
                    }));

                    mergedDays.Add(new ItineraryDay
                    {
                        DayNumber = updatedDay.DayNumber,
                        Title = updatedDay.Title,
                        DailyCost = updatedDay.DailyCost,
                        WeatherNote = updatedDay.WeatherNote,
                        Activities = updatedActivities.ToList()
                    });
                }
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
                TotalEstimatedCost = mergedDays.Sum(d => d.DailyCost),
                Budget = (int)originalRequest.BudgetVND,
                Itinerary = mergedDays,
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
