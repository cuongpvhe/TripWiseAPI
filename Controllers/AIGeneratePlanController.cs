using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using TripWiseAPI.Services;
using TripWiseAPI.Model;
using TripWiseAPI.Models;

namespace SimpleChatboxAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIGeneratePlanController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly VectorSearchService _vectorSearchService;
        private const decimal DefaultExchangeRate = 25000;

        public AIGeneratePlanController(IHttpClientFactory httpClientFactory, IConfiguration configuration, TripWiseDBContext _context)
        {
            _httpClient = httpClientFactory.CreateClient("Gemini");
            _apiKey = configuration["Gemini:ApiKey"];
            _dbContext = _context;
            Console.OutputEncoding = Encoding.UTF8;
            _vectorSearchService = new VectorSearchService(httpClientFactory.CreateClient());
        }

        [HttpPost("CreateItinerary")]
        [ProducesResponseType(typeof(ItineraryResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> CreateItinerary([FromBody] TravelRequest request)
        {
            // Kiểm tra các tham số đầu vào bằng các attribute đã có sẵn (Optional: Cải thiện tính rõ ràng)
            if (string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest(new { success = false, error = "Destination is required" });

            if (request.TravelDate == default)
                return BadRequest(new { success = false, error = "Travel date is required" });

            if (request.TravelDate < DateTime.Today)
                return BadRequest(new { success = false, error = "Travel date must be today or later" });

            if (request.Days <= 0)
                return BadRequest(new { success = false, error = "Days must be a positive number" });

            if (string.IsNullOrWhiteSpace(request.Preferences))
                return BadRequest(new { success = false, error = "Purpose of trip (Preferences) is required" });

            if (request.BudgetVND <= 0)
                return BadRequest(new { success = false, error = "Budget must be a positive number (VND)" });

            int budgetVND = (int)request.BudgetVND;
            string budgetVNDFormatted = budgetVND.ToString("N0", CultureInfo.InvariantCulture);


            // Lấy nội dung từ Vector API
            string relatedKnowledge = await _vectorSearchService.RetrieveRelevantJsonEntries(
                request.Destination, 7, request.GroupType ?? "", request.DiningStyle ?? "", request.Preferences ?? ""
            );

            Console.WriteLine("[DEBUG] Nội dung liên quan lấy từ Vector API:");
            Console.WriteLine(relatedKnowledge);

            string prompt = BuildPrompt(request, budgetVNDFormatted, relatedKnowledge);

            Console.WriteLine("[DEBUG] Prompt gửi đến Gemini:");
            Console.WriteLine(prompt);

            // Gửi request đến Gemini API
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
                    maxOutputTokens = 3200,
                    temperature = 0.7
                }
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}", payload);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, new { success = false, error = "Gemini API error", detail = await response.Content.ReadAsStringAsync() });

                string responseContent = await response.Content.ReadAsStringAsync();
                string geminiText = JsonDocument.Parse(responseContent)
                    .RootElement.GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                geminiText = geminiText.Replace("```json", "").Replace("```", "").Trim();

                var startIndex = geminiText.IndexOf('{');
                var endIndex = geminiText.LastIndexOf('}');
                if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
                    return BadRequest(new { success = false, error = "Gemini returned malformed JSON." });

                var cleanedJson = geminiText.Substring(startIndex, endIndex - startIndex + 1).Trim();
                cleanedJson = cleanedJson
                    .Replace("“", "\"")
                    .Replace("”", "\"")
                    .Replace("’", "'")
                    .Replace("\u00a0", " ")
                    .Replace("\u200b", "")
                    .Replace("\r\n", "\n");
                cleanedJson = Regex.Replace(cleanedJson, @",(?=\s*[}\]])", "");

                Console.WriteLine("[DEBUG] Cleaned JSON before parsing:");
                Console.WriteLine(cleanedJson);

                JsonDocument jsonDoc;
                try
                {
                    jsonDoc = JsonDocument.Parse(cleanedJson);
                }
                catch
                {
                    var repaired = await TryRepairJsonAsync(cleanedJson);
                    if (repaired == null)
                        return BadRequest(new { success = false, error = "Gemini returned invalid JSON and repair failed." });

                    try
                    {
                        jsonDoc = JsonDocument.Parse(repaired);
                    }
                    catch
                    {
                        return BadRequest(new { success = false, error = "Repaired JSON still invalid." });
                    }
                }

                var parsed = JsonSerializer.Deserialize<JsonItineraryFormat>(jsonDoc.RootElement.GetRawText());

                if (parsed?.Days == null || parsed.Days.Count == 0)
                    return BadRequest(new { success = false, error = "Itinerary is empty or invalid." });

                var itinerary = parsed.Days.Select(d => new ItineraryDay
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
                        MapUrl = string.IsNullOrWhiteSpace(a.Address)
                            ? null
                            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                    }).ToList()
                }).ToList();

                string accommodationSearchUrl = $"https://www.google.com/maps/search/?q=khách+sạn+{Uri.EscapeDataString(request.Accommodation ?? "3 sao")}+sao+{Uri.EscapeDataString(request.Destination)}";

                var result = new ItineraryResponse
                {
                    success = true,
                    budgetVND = request.BudgetVND,
                    data = new ItineraryResponse
                    {
                        Destination = request.Destination,
                        Days = request.Days,
                        Preferences = request.Preferences,
                        TravelDate = request.TravelDate,
                        Transportation = request.Transportation,
                        DiningStyle = request.DiningStyle,
                        GroupType = request.GroupType,
                        Accommodation = request.Accommodation,
                        TotalEstimatedCost = parsed.TotalCost,
                        Budget = budgetVND,
                        Itinerary = itinerary,
                        SuggestedAccommodation = accommodationSearchUrl
                    }
                });
            }
            catch (Exception ex)
            {
                // Xử lý lỗi khi gọi API hoặc khi có bất kỳ lỗi nào trong quá trình xử lý
                return StatusCode(500, new { success = false, error = "An error occurred while processing your request.", detail = ex.Message });
            }
        }

        // Tách Logic xử lý Prompt ra hàm riêng
        private string BuildPrompt(TravelRequest request, string budgetVNDFormatted, string relatedKnowledge)
        {
            string filterNote = $"Hãy ưu tiên các địa điểm phù hợp với nhóm '{request.GroupType}', ăn uống kiểu '{request.DiningStyle}', mục đích '{request.Preferences}', và ngân sách tối đa {budgetVNDFormatted} đồng.";

            var dayNote = request.Days switch
            {
                <= 2 => "Tập trung vào các điểm nổi bật nhất, không dàn trải.",
                <= 4 => "Phân bổ thời gian hợp lý giữa khám phá và nghỉ ngơi.",
                > 4 => "Tạo lịch trình đều đặn, có cả hoạt động và thư giãn."
            };

            return $$"""
                {{filterNote}}

                Bạn là một hướng dẫn viên du lịch AI chuyên nghiệp. Hãy tạo lịch trình {{request.Days}} ngày tại {{request.Destination}}, theo chủ đề "{{request.Preferences}}", với ngân sách khoảng {{budgetVNDFormatted}} đồng.

                Thông tin chuyến đi:
                - Ngày khởi hành: {{request.TravelDate:dd/MM/yyyy}}
                - Phương tiện di chuyển: {{(request.Transportation ?? "tự túc")}}
                - Phong cách ăn uống: {{(request.DiningStyle ?? "địa phương")}}
                - Nhóm người đi: {{(request.GroupType ?? "2 người")}}
                - Chỗ ở mong muốn: {{(request.Accommodation ?? "3 sao")}}

                MÔ TẢ ĐỊA ĐIỂM:
                - Trường "placeDetail" bắt buộc phải có trong mỗi hoạt động
                - Nội dung placeDetail mô tả địa điểm đó có gì hay, đặc biệt, nổi bật gì về cảnh quan – lịch sử – đặc sản – văn hóa
                - Giải thích vì sao nên đến vào thời điểm đó trong ngày (sáng/chiều/tối)
                - Viết giống như bạn đang giới thiệu địa điểm này cho du khách

                Yêu cầu khi tạo lịch trình:
                - {{dayNote}}
                - Ưu tiên các địa điểm xuất hiện trong danh sách bên dưới
                - estimatedCost phải là số nguyên (VD: 150000)
                - Mỗi activity phải có thời gian cụ thể bắt đầu và kết thúc theo định dạng HH:mm (VD: "08:00", "14:30")
                - Thời gian cụ thể bắt đầu và kết thúc phải phù hợp với mỗi địa điểm của lịch trình. Ví dụ: ăn sáng thường chỉ tầm 30 phút tới 1 tiếng
                - lịch trình cần có thời gian cụ thể đủ sáng|trưa|chiều|tối
                - Nếu các địa điểm trong dữ liệu không đủ để tạo thành một lịch trình hoàn chỉnh thì hãy tạo thêm địa điểm mới phù hợp.
                - Mỗi activity **bắt buộc** phải có trường "image". 
                  - Nếu địa điểm có sẵn trường "thumbnail" trong dữ liệu đầu vào thì dùng chính nó làm "image"
                  - Nếu không có, hãy tạo một đường link ảnh minh họa đẹp, thực tế và phù hợp với địa điểm (ví dụ: Unsplash, Pexels, v.v.)

                Ví dụ:
                {
                  "name": "Chè Liên",
                  "address": "189 Hoàng Diệu, Đà Nẵng",
                  "city": "Đà Nẵng",
                  "cost": "30.000 VND",
                  "interests": "Street food;Dessert",
                  "thumbnail": "https://example.com/image.jpg"
                }
                === START DATA ===
                {{relatedKnowledge}}
                === END DATA ===

                Trả về kết quả JSON chuẩn, không giải thích, không thêm text nào bên ngoài:
                {
                  "version": "v1.0",
                  "totalCost": 123456,
                  "days": [
                    {
                      "dayNumber": 1,
                      "title": "string",
                      "dailyCost": 123456,
                      "activities": [
                        {
                          "starttime": "08:00",
                          "endtime": "10:00",
                          "description": "string",
                          "estimatedCost": 123456,
                          "transportation": "string",
                          "address": "string",
                          "placeDetail": "string",
                          "mapUrl": "string",
                          "image": "string"
                        }
                      ]
                    }
                  ]
                }
                """;
        }


        // Try sửa JSON bị lỗi nếu cần
        private async Task<string?> TryRepairJsonAsync(string brokenJson)
        {
            var repairPrompt = $"""
        Bạn đã trả về đoạn JSON sau nhưng nó bị lỗi không phân tích được:

        {brokenJson}

        Hãy sửa lại JSON này đúng định dạng version "v1.0", không có chữ nào bên ngoài JSON. Trả lại JSON duy nhất, không thêm lời giải thích.
        """;

            var payload = new
            {
                contents = new[] {
                new {
                    parts = new[] {
                        new { text = repairPrompt }
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

            var raw = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[DEBUG] Repaired JSON raw response:");
            Console.WriteLine(raw);

            try
            {
                string text = JsonDocument.Parse(raw)
                    .RootElement.GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                string cleaned = text.Replace("```json", "").Replace("```", "").Trim();
                Console.WriteLine("[DEBUG] Repaired Cleaned JSON:");
                Console.WriteLine(cleaned);
                JsonDocument.Parse(cleaned);
                return cleaned;
            }
            catch
            {
                Console.WriteLine("[ERROR] JSON sau sửa vẫn không parse được.");
                return null;
            }
        }
        private async Task SaveToGenerateTravelPlanAsync(TravelRequest request, ItineraryResponse response, ClaimsPrincipal user)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            int? UserId = null;

            if (int.TryParse(userIdClaim, out int parsedId))
            {
                UserId = parsedId;
            }

            var entity = new GenerateTravelPlan
            {
                ConversationId = Guid.NewGuid().ToString(),
                UserId = UserId,
                TourId = null,
                MessageRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                }),

                MessageResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                }),
                ResponseTime = DateTime.UtcNow
            };

            _dbContext.GenerateTravelPlans.Add(entity);
            await _dbContext.SaveChangesAsync();
        }

        [HttpPost("SaveTourFromGenerated/{generatePlanId}")]
        public async Task<IActionResult> SaveTourFromGenerated(int generatePlanId)
        {
            var generatePlan = await _dbContext.GenerateTravelPlans
                .FirstOrDefaultAsync(p => p.Id == generatePlanId);

            if (generatePlan == null || string.IsNullOrEmpty(generatePlan.MessageResponse))
                return NotFound("Không tìm thấy MessageResponse.");

            using var doc = JsonDocument.Parse(generatePlan.MessageResponse);
            var root = doc.RootElement;

            // Lấy các thông tin từ JSON
            string destination = root.GetProperty("Destination").GetString();
            int days = root.GetProperty("Days").GetInt32();
            string preferences = root.GetProperty("Preferences").GetString();
            string transportation = root.GetProperty("Transportation").GetString();
            string diningStyle = root.GetProperty("DiningStyle").GetString();
            string groupType = root.GetProperty("GroupType").GetString();
            string accommodation = root.GetProperty("Accommodation").GetString();
            DateTime travelDate = root.GetProperty("TravelDate").GetDateTime();
            int totalEstimatedCost = root.GetProperty("TotalEstimatedCost").GetInt32();
            string suggestedAccommodation = root.GetProperty("SuggestedAccommodation").GetString();

            // Tạo tour
            var tour = new Tour
            {
                TourName = $"Tour {destination} - {travelDate:dd/MM/yyyy} - {groupType}",
                Description = $"Chuyến đi {destination} cho {groupType}, ưu tiên {preferences}, di chuyển bằng {transportation}",
                Duration = days.ToString(), 
                Price = totalEstimatedCost,
                Location = destination,
                MaxGroupSize = 10,
                Category = preferences,
                TourNote = $"Lưu trú: {accommodation}, Ăn uống: {diningStyle}",
                TourInfo = $"Gợi ý KS: {suggestedAccommodation}",
                //TourTypesId = 1,
                CreatedDate = DateTime.UtcNow
                
            };

            _dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync(); // cần lấy TourID

            // Duyệt lịch trình
            var itinerary = root.GetProperty("Itinerary");
            foreach (var day in itinerary.EnumerateArray())
            {
                int dayNumber = day.GetProperty("DayNumber").GetInt32();
                string title = day.GetProperty("Title").GetString();

                foreach (var activity in day.GetProperty("Activities").EnumerateArray())
                {
                    string timeOfDay = activity.GetProperty("timeOfDay").GetString();
                    string description = activity.GetProperty("description").GetString();
                    int estimatedCost = activity.GetProperty("estimatedCost").GetInt32();
                    string address = activity.GetProperty("address").GetString();
                    string placeDetail = activity.GetProperty("placeDetail").GetString();

                    // Thêm TourAttractions
                    var attraction = new TourAttraction
                    {
                        TourAttractionsName = description,
                        Price = estimatedCost,
                        Localtion = address,
                        Category = title,
                        CreatedDate = DateTime.UtcNow
                        
                    };
                    _dbContext.TourAttractions.Add(attraction);
                    await _dbContext.SaveChangesAsync();
                    int? timeSlotValue = timeOfDay.ToLower() switch
                    {
                        "sáng" => 1,
                        "trưa" => 2,
                        "chiều" => 3,
                        "tối" => 4,
                        _ => null // hoặc 0 nếu bạn muốn gán giá trị mặc định
                    };

                    // Thêm TourItinerary
                    var itineraryItem = new TourItinerary
                    {
                        ItineraryName = title,
                        TourId = tour.TourId,
                        DayNumber = dayNumber,
                        TourAttractionsId = attraction.TourAttractionsId,
                        //ActivityTypeId = 1,
                        Category = timeOfDay,
                        Description = placeDetail,

                        TimeSlot = timeSlotValue,
                        CreatedDate = DateTime.UtcNow
                        
                    };
                    _dbContext.TourItineraries.Add(itineraryItem);
                }
            }

            await _dbContext.SaveChangesAsync();
            return Ok("Đã lưu thành công tour từ MessageResponse.");
        }


    }

}
