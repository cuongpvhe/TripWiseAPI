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
        private readonly VectorSearchService _vectorSearchService; // Thêm VectorSearchService

        public AiItineraryService(
            IHttpClientFactory httpClientFactory, 
            IConfiguration config, 
            IPromptBuilder promptBuilder, 
            IJsonRepairService repairService, 
            IWikimediaImageService imageService, 
            IGoogleMapsPlaceService googleMapsPlaceService,
            VectorSearchService vectorSearchService) // Inject VectorSearchService
        {
            _httpClient = httpClientFactory.CreateClient("Gemini");
            _apiKey = config["Gemini:ApiKey"];
            _promptBuilder = promptBuilder;
            _repairService = repairService;
            _WikimediaImageService = imageService;
            _googleMapsPlaceService = googleMapsPlaceService;
            _vectorSearchService = vectorSearchService;
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
        "https://static.vinwonders.com/2022/12/du-lich-viet-nam-05.jpg",
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
                    maxOutputTokens = 40000,
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
        "https://static.vinwonders.com/2022/12/du-lich-viet-nam-05.jpg",
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
    try
    {
        // Validate input parameters
        if (originalRequest == null)
            throw new ArgumentNullException(nameof(originalRequest), "Original request cannot be null");

        if (originalResponse == null)
            throw new ArgumentNullException(nameof(originalResponse), "Original response cannot be null");

        if (string.IsNullOrWhiteSpace(userInstruction))
            throw new ArgumentException("User instruction cannot be empty", nameof(userInstruction));

        if (string.IsNullOrWhiteSpace(originalRequest.Destination))
            throw new ArgumentException("Destination cannot be empty", nameof(originalRequest.Destination));

        Console.WriteLine($"[UpdateItinerary] Starting update for destination: {originalRequest.Destination}");
        Console.WriteLine($"[UpdateItinerary] User instruction: {userInstruction}");

        // Lấy relatedKnowledge với timeout và retry
        string relatedKnowledge;
        try
        {
            relatedKnowledge = await _vectorSearchService.RetrieveRelevantJsonEntriesForUpdate(
                originalRequest.Destination, 
                userInstruction,
                3,
                originalRequest.GroupType ?? "", 
                originalRequest.DiningStyle ?? "", 
                originalRequest.Preferences ?? "");

            Console.WriteLine($"[UpdateItinerary] RelatedKnowledge retrieved successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] Warning: Failed to retrieve related knowledge: {ex.Message}");
            relatedKnowledge = ""; // Continue with empty knowledge if vector search fails
        }

        // Build prompt
        string prompt;
        try
        {
            prompt = _promptBuilder.BuildUpdatePrompt(originalRequest, originalResponse, userInstruction, relatedKnowledge);
            Console.WriteLine($"[UpdateItinerary] Prompt built successfully, length: {prompt.Length}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] Error building prompt: {ex.Message}");
            throw new InvalidOperationException("Failed to build update prompt", ex);
        }

        // Call Gemini API with timeout
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
                maxOutputTokens = 40000,
                temperature = 0.7
            }
        };

        HttpResponseMessage response;
        string text, raw;
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // 2 minute timeout
            
            response = await _httpClient.PostAsJsonAsync(
                $"v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}", 
                payload, 
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[UpdateItinerary] Gemini API error: {response.StatusCode} - {errorContent}");
                throw new HttpRequestException($"Gemini API failed with status {response.StatusCode}: {errorContent}");
            }

            text = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Gemini API returned empty response");
            }

            var jsonDoc = JsonDocument.Parse(text);
            if (!jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Invalid Gemini API response format");
            }

            raw = candidates[0]
                .GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString();

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException("Gemini API returned empty content");
            }

            Console.WriteLine($"[UpdateItinerary] Gemini response received, length: {raw.Length}");
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[UpdateItinerary] Gemini API timeout: {ex.Message}");
            throw new TimeoutException("Gemini API request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[UpdateItinerary] HTTP error: {ex.Message}");
            throw;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[UpdateItinerary] JSON parsing error: {ex.Message}");
            throw new InvalidOperationException("Failed to parse Gemini API response", ex);
        }

        Console.WriteLine(" Prompt gửi lên:\n" + prompt);
        Console.WriteLine(" Kết quả Gemini trả về:\n" + raw);

        // Extract and parse JSON
        string cleanedJson;
        JsonDocument doc;
        JsonItineraryFormat parsed;

        try
        {
            cleanedJson = ExtractJson(raw);
            
            if (string.IsNullOrWhiteSpace(cleanedJson))
            {
                throw new InvalidOperationException("Could not extract valid JSON from Gemini response");
            }

            Console.WriteLine($"[UpdateItinerary] Cleaned JSON length: {cleanedJson.Length}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] JSON extraction error: {ex.Message}");
            throw new InvalidOperationException("Failed to extract JSON from response", ex);
        }

        try
        {
            doc = JsonDocument.Parse(cleanedJson);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[UpdateItinerary] JSON parsing failed, attempting repair: {ex.Message}");
            
            try
            {
                string? repaired = await _repairService.TryRepairAsync(cleanedJson);
                if (string.IsNullOrWhiteSpace(repaired))
                {
                    throw new InvalidOperationException("JSON repair service failed to fix the response");
                }
                doc = JsonDocument.Parse(repaired);
                Console.WriteLine("[UpdateItinerary] JSON successfully repaired");
            }
            catch (Exception repairEx)
            {
                Console.WriteLine($"[UpdateItinerary] JSON repair failed: {repairEx.Message}");
                throw new InvalidOperationException("Failed to parse and repair JSON response", repairEx);
            }
        }

        try
        {
            parsed = JsonSerializer.Deserialize<JsonItineraryFormat>(doc.RootElement.GetRawText());
            
            if (parsed?.Days == null)
            {
                Console.WriteLine("[UpdateItinerary] Warning: Parsed itinerary has null days, returning original response");
                return originalResponse;
            }

            if (parsed.Days.Count == 0)
            {
                Console.WriteLine("[UpdateItinerary] Gemini returned no updated days, keeping original itinerary");
                return originalResponse;
            }

            Console.WriteLine($"[UpdateItinerary] Successfully parsed {parsed.Days.Count} updated days");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[UpdateItinerary] Deserialization error: {ex.Message}");
            throw new InvalidOperationException("Failed to deserialize itinerary format", ex);
        }

        // Process images and build response
        var imageUrlsUsed = new HashSet<string>();
        var fallbackImages = new[]
        {
            "https://cdn.thuvienphapluat.vn/uploads/tintuc/2024/02/23/viet-nam-nam-tren-ban-dao-nao.jpg",
            "https://cdn.tcdulichtphcm.vn/upload/4-2022/images/2022-11-16/1668604785-6.jpeg",
            "https://static.vinwonders.com/2022/12/du-lich-viet-nam-05.jpg",
            "https://s-aicmscdn.vietnamhoinhap.vn/vnhn-media/24/1/18/dulichvn_65a88ab6bdc3a.jpg",
            "https://dulichtoday.vn/wp-content/uploads/2017/04/pho-co-Hoi-An.jpg"
        };
        var random = new Random();

        List<string> wikimediaImages;
        try
        {
            wikimediaImages = await _WikimediaImageService.SearchImageUrlsAsync(originalRequest.Destination);
            Console.WriteLine($"[UpdateItinerary] Retrieved {wikimediaImages.Count} Wikimedia images");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] Wikimedia fetch error: {ex.Message}");
            wikimediaImages = new List<string>();
        }

        var mergedDays = new List<ItineraryDay>();

        try
        {
            foreach (var originalDay in originalResponse.Itinerary)
            {
                var updatedDay = parsed.Days.FirstOrDefault(d => d.DayNumber == originalDay.DayNumber);

                if (updatedDay == null)
                {
                    mergedDays.Add(originalDay); // Keep original if no changes
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
                                Console.WriteLine($"[UpdateItinerary] Wikimedia image used: {imageUrl}");
                            }
                            else
                            {
                                imageUrl = fallbackImages[random.Next(fallbackImages.Length)];
                                Console.WriteLine("[UpdateItinerary] Fallback image used");
                            }
                        }

                        return new ItineraryActivity
                        {
                            StartTime = a.StartTime ?? "08:00",
                            EndTime = a.EndTime ?? "09:00",
                            Description = a.Description ?? "Hoạt động",
                            EstimatedCost = a.EstimatedCost,
                            Transportation = a.Transportation ?? "Đi bộ",
                            Address = a.Address ?? "",
                            PlaceDetail = a.PlaceDetail ?? "",
                            Image = imageUrl,
                            MapUrl = string.IsNullOrWhiteSpace(a.Address)
                                ? null
                                : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                        };
                    }));

                    mergedDays.Add(new ItineraryDay
                    {
                        DayNumber = updatedDay.DayNumber,
                        Title = updatedDay.Title ?? $"Ngày {updatedDay.DayNumber}",
                        DailyCost = updatedDay.DailyCost,
                        WeatherNote = updatedDay.WeatherNote ?? "Thời tiết không xác định",
                        Activities = updatedActivities.ToList()
                    });
                }
            }

            Console.WriteLine($"[UpdateItinerary] Successfully processed {mergedDays.Count} days");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] Error processing activities: {ex.Message}");
            throw new InvalidOperationException("Failed to process updated activities", ex);
        }

        // Build final response
        try
        {
            var finalResponse = new ItineraryResponse
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

            Console.WriteLine($"[UpdateItinerary] Update completed successfully. Total cost: {finalResponse.TotalEstimatedCost}");
            return finalResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] Error building final response: {ex.Message}");
            throw new InvalidOperationException("Failed to build final response", ex);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[UpdateItinerary] Unexpected error: {ex.GetType().Name} - {ex.Message}");
        Console.WriteLine($"[UpdateItinerary] Stack trace: {ex.StackTrace}");
        throw;
    }
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
