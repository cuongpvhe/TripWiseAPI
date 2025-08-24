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
        private readonly VectorSearchService _vectorSearchService; 

        public AiItineraryService(
            IHttpClientFactory httpClientFactory, 
            IConfiguration config, 
            IPromptBuilder promptBuilder, 
            IJsonRepairService repairService, 
            IWikimediaImageService imageService, 
            IGoogleMapsPlaceService googleMapsPlaceService,
            VectorSearchService vectorSearchService) 
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
                    maxOutputTokens = 80000,
                    temperature = 0.7
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}", payload);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Gemini API failed: {await response.Content.ReadAsStringAsync()}");

            string text = await response.Content.ReadAsStringAsync();

            // ⭐ THÊM ĐOẠN NÀY ĐỂ LOG TOKEN USAGE
            try 
            {
                using JsonDocument fullResponse = JsonDocument.Parse(text);
                if (fullResponse.RootElement.TryGetProperty("usageMetadata", out var usageMetadata))
                {
                    var promptTokens = usageMetadata.TryGetProperty("promptTokenCount", out var ptc) ? ptc.GetInt32() : 0;
                    var candidatesTokens = usageMetadata.TryGetProperty("candidatesTokenCount", out var ctc) ? ctc.GetInt32() : 0;
                    var totalTokens = usageMetadata.TryGetProperty("totalTokenCount", out var ttc) ? ttc.GetInt32() : 0;
                    
                    Console.WriteLine($"🔥 [TOKEN USAGE] Prompt: {promptTokens:N0}, Output: {candidatesTokens:N0}, Total: {totalTokens:N0}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [TOKEN TRACKING] Could not parse usage: {ex.Message}");
            }

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
        "https://dulichtoday.vn/wp-content/uploads/2017/04/pho-co-Hoi-An.jpg",
        "https://sakos.vn/wp-content/uploads/2024/09/thumb-3.jpg",
        "https://hoangphuan.com/wp-content/uploads/2024/07/tat-tan-tat-kinh-nghiem-du-lich-tour-da-nang-ma-ban-phai-biet.jpg",
        "https://viettintravel.com.vn/wp-content/uploads/2013/06/cau-rong1.png"
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
                    maxOutputTokens = 80000,
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
    string userInstruction, string originalUserMessage)
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
        Console.WriteLine($"[UpdateItinerary] Full instruction: {userInstruction}");
        Console.WriteLine($"[UpdateItinerary] Original user message: {originalUserMessage}");

        // Lấy relatedKnowledge - SỬ DỤNG CHỈ ORIGINAL USER MESSAGE
        string relatedKnowledge;
        try
        {
            relatedKnowledge = await _vectorSearchService.RetrieveRelevantJsonEntriesForUpdate(
                originalRequest.Destination, 
                originalUserMessage,  // CHỈ GỬI USER MESSAGE GỐC
                3,
                originalRequest.GroupType ?? "", 
                originalRequest.DiningStyle ?? "", 
                originalRequest.Preferences ?? "");

            Console.WriteLine($"[UpdateItinerary] Vector search query: {originalUserMessage} tại {originalRequest.Destination}");
            Console.WriteLine($"[UpdateItinerary] RelatedKnowledge retrieved successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] Warning: Failed to retrieve related knowledge: {ex.Message}");
            relatedKnowledge = "";
        }

        // Build prompt - SỬ DỤNG FULL INSTRUCTION CHO AI
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

        // Phần còn lại giữ nguyên như cũ...
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
                maxOutputTokens = 80000,
                temperature = 0.7
            }
        };

        HttpResponseMessage response;
        string text, raw;
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            
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

        Console.WriteLine($" Prompt gửi lên:\n" + prompt);
        Console.WriteLine($" Kết quả Gemini trả về:\n" + raw);

        // Extract and parse JSON
        string cleanedJson;
        
        try
        {
            cleanedJson = ExtractJson(raw);
            
            if (string.IsNullOrWhiteSpace(cleanedJson))
            {
                throw new InvalidOperationException("Could not extract valid JSON from Gemini response");
            }

            Console.WriteLine($"[UpdateItinerary] Cleaned JSON length: {cleanedJson.Length}");
            
            // ✅ THÊM LOGIC NÀY - KIỂM TRA AI ERROR RESPONSE TRƯỚC KHI PARSE
            if (cleanedJson.Contains("LOCATION_CONFLICT") || cleanedJson.Contains("\"error\""))
            {
                Console.WriteLine("[UpdateItinerary] AI detected location conflict, throwing exception");
                throw new ArgumentException("AI_DETECTED_LOCATION_CONFLICT: " + cleanedJson);
            }
        }
        catch (ArgumentException)
        {
            // Re-throw ArgumentException (location conflict)
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateItinerary] JSON extraction error: {ex.Message}");
            throw new InvalidOperationException("Failed to extract JSON from response", ex);
        }

        // ✅ TIẾP TỤC PARSE CHỈ KHI KHÔNG CÓ ERROR
        JsonDocument doc;
        JsonItineraryFormat parsed;

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
            "https://dulichtoday.vn/wp-content/uploads/2017/04/pho-co-Hoi-An.jpg",
            "https://sakos.vn/wp-content/uploads/2024/09/thumb-3.jpg",
            "https://hoangphuan.com/wp-content/uploads/2024/07/tat-tan-tat-kinh-nghiem-du-lich-tour-da-nang-ma-ban-phai-biet.jpg",
            "https://viettintravel.com.vn/wp-content/uploads/2013/06/cau-rong1.png"

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
                    mergedDays.Add(originalDay);
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

// Thêm method này vào AiItineraryService (3 tham số - backward compatibility)
public async Task<ItineraryResponse> UpdateItineraryAsync(
    TravelRequest originalRequest,
    ItineraryResponse originalResponse,
    string userInstruction)
{
    // Extract user message từ userInstruction
    string extractedUserMessage = ExtractUserMessageFromInstruction(userInstruction);
    
    // Gọi method 4 tham số
    return await UpdateItineraryAsync(originalRequest, originalResponse, userInstruction, extractedUserMessage);
}

// Helper method để extract user message - CẬP NHẬT
private string ExtractUserMessageFromInstruction(string userInstruction)
{
    if (string.IsNullOrWhiteSpace(userInstruction))
        return "";

    // Các pattern để extract user message - THÊM PATTERN MỚI
    var patterns = new[]
    {
        // Pattern mới: "ngày X, HH:mm - HH:mm [action]"
        @"ngày\s*(\d+),?\s*(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})\s*(.+?)(?:\.|$)",
        
        // Pattern mới: "ngày X [action]" (không có thời gian)
        @"ngày\s*(\d+),?\s*(.+?)(?:\.|$)",
        
        // Pattern cũ: "Trong ngày X, tôi muốn..."
        @"Trong ngày \d+,?\s*tôi muốn\s*(.+?)(?:\.|$)",
        
        // Pattern cũ: "Ngày X tôi muốn..."  
        @"Ngày \d+\s*tôi muốn\s*(.+?)(?:\.|$)", 
        
        // Pattern cũ: "tôi muốn..."
        @"tôi muốn\s*(.+?)(?:\.|$)",
        
        // Pattern cũ: "thay đổi ... thành ..."
        @"thay đổi.*?thành\s*(.+?)(?:\.|$)",
        
        // Pattern cũ: Fallback - tìm text sau "muốn"
        @"muốn\s*(.+?)(?:\.|$)"
    };

    foreach (var pattern in patterns)
    {
        var match = Regex.Match(userInstruction.Trim(), pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            string extracted = "";
            
            // Handle pattern đặc biệt cho format "ngày X, HH:mm - HH:mm [action]"
            if (pattern.Contains(@"(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})") && match.Groups.Count >= 5)
            {
                // Trích xuất action từ group 4 (sau thời gian)
                extracted = match.Groups[4].Value.Trim();
                Console.WriteLine($"[ExtractUserMessage] Time-specific pattern matched");
                Console.WriteLine($"[ExtractUserMessage] Day: {match.Groups[1].Value}, Time: {match.Groups[2].Value}-{match.Groups[3].Value}");
            }
            // Handle pattern "ngày X [action]" (không có thời gian)
            else if (pattern.Contains(@"ngày\s*(\d+),?\s*(.+?)") && match.Groups.Count >= 3)
            {
                extracted = match.Groups[2].Value.Trim();
                Console.WriteLine($"[ExtractUserMessage] Day-specific pattern matched");
                Console.WriteLine($"[ExtractUserMessage] Day: {match.Groups[1].Value}");
            }
            // Handle các pattern khác (group 1)
            else if (match.Groups.Count > 1)
            {
                extracted = match.Groups[1].Value.Trim();
                Console.WriteLine($"[ExtractUserMessage] General pattern matched");
            }

            if (!string.IsNullOrWhiteSpace(extracted))
            {
                Console.WriteLine($"[ExtractUserMessage] Pattern: {pattern}");
                Console.WriteLine($"[ExtractUserMessage] Extracted: {extracted}");
                return extracted;
            }
        }
    }

    Console.WriteLine($"[ExtractUserMessage] No pattern matched, using full instruction");
    return userInstruction;
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
