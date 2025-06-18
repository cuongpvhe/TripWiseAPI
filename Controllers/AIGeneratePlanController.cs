using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TripWiseAPI.Model;
using TripWiseAPI.Models;

namespace TripWiseAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AIGeneratePlanController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly TripWiseDBContext _dbContext;

        public AIGeneratePlanController(IHttpClientFactory httpClientFactory, IConfiguration configuration, TripWiseDBContext _context)
        {
            _httpClient = httpClientFactory.CreateClient("Gemini");
            _apiKey = configuration["Gemini:ApiKey"];
            _dbContext = _context;
            Console.OutputEncoding = Encoding.UTF8;
        }

        [HttpPost("CreateItinerary")]
        [ProducesResponseType(typeof(ItineraryResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> CreateItinerary([FromBody] TravelRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, error = "Invalid input", details = ModelState });

                if (request.TravelDate < DateTime.Today)
                    return BadRequest(new { success = false, error = "Travel date must be today or later" });

                decimal exchangeRate = 25000;
                int budgetVND = (int)(request.Budget * exchangeRate);
                string budgetVNDFormatted = budgetVND.ToString("N0", CultureInfo.InvariantCulture);

                string prompt = BuildPrompt(request, budgetVNDFormatted);

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

                var response = await _httpClient.PostAsJsonAsync(
                    $"v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}", payload);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, new { success = false, error = "Gemini API error", detail = responseContent });

                string geminiText = JsonDocument.Parse(responseContent)
                    .RootElement.GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                Console.WriteLine("[DEBUG] Gemini raw text:");
                Console.WriteLine(geminiText);

                var startIndex = geminiText.IndexOf('{');
                var endIndex = geminiText.LastIndexOf('}');
                if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
                {
                    Console.WriteLine("[ERROR] JSON boundaries not found in Gemini text");
                    return BadRequest(new { success = false, error = "Gemini returned malformed JSON." });
                }

                var cleanedJson = geminiText.Substring(startIndex, endIndex - startIndex + 1).Trim();
                Console.WriteLine("[DEBUG] Cleaned JSON string:");
                Console.WriteLine(cleanedJson);

                JsonDocument jsonDoc;
                try
                {
                    jsonDoc = JsonDocument.Parse(cleanedJson);
                }
                catch (Exception ex1)
                {
                    Console.WriteLine("[ERROR] Failed to parse cleaned JSON:");
                    Console.WriteLine(ex1.Message);

                    var repaired = await TryRepairJsonAsync(cleanedJson);
                    Console.WriteLine("[DEBUG] Repaired JSON string:");
                    Console.WriteLine(repaired);

                    if (repaired == null)
                        return BadRequest(new { success = false, error = "Gemini returned invalid JSON and repair failed." });

                    try
                    {
                        jsonDoc = JsonDocument.Parse(repaired);
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine("[ERROR] Failed to parse repaired JSON:");
                        Console.WriteLine(ex2.Message);
                        return BadRequest(new { success = false, error = "Even repaired JSON is still invalid." });
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
                        TimeOfDay = a.TimeOfDay,
                        Description = a.Description,
                        EstimatedCost = a.EstimatedCost,
                        Transportation = a.Transportation,
                        Address = a.Address,
                        PlaceDetail = a.PlaceDetail,
                        MapUrl = string.IsNullOrWhiteSpace(a.Address)
                            ? null
                            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(a.Address)}"
                    }).ToList()
                }).ToList();

                string accommodationSearchUrl = $"https://www.google.com/maps/search/?q=khách+sạn+{Uri.EscapeDataString(request.Accommodation ?? "3 sao")}+sao+{Uri.EscapeDataString(request.Destination)}";

                var result = new ItineraryResponse
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
                };

                // Save to DB
                await SaveToGenerateTravelPlanAsync(request, result, User);

                return Ok(new { success = true, exchangeRateUsed = exchangeRate, convertedFromUSD = request.Budget, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private string BuildPrompt(TravelRequest request, string budgetVNDFormatted)
        {
            return
                "Bạn là một hướng dẫn viên du lịch AI chuyên nghiệp. Hãy tạo lịch trình " + request.Days + " ngày tại " + request.Destination +
                ", theo chủ đề \"" + request.Preferences + "\", với ngân sách khoảng " + budgetVNDFormatted + " đồng.\n\n" +
                "Thông tin chuyến đi:\n" +
                "- Ngày khởi hành: " + request.TravelDate.ToString("dd/MM/yyyy") + "\n" +
                "- Phương tiện di chuyển: " + (request.Transportation ?? "tự túc") + "\n" +
                "- Phong cách ăn uống: " + (request.DiningStyle ?? "địa phương") + "\n" +
                "- Nhóm người đi: " + (request.GroupType ?? "2 người") + "\n" +
                "- Chỗ ở mong muốn: " + (request.Accommodation ?? "3 sao") + "\n\n" +
                "Yêu cầu bắt buộc:\n" +
                "- Mỗi hoạt động phải có các trường: timeOfDay, description, estimatedCost, transportation, address\n" +
                "- Giá cả hợp lý theo thị trường Việt Nam năm 2025\n\n" +
                "ĐỊA ĐIỂM & ĐỊA CHỈ:\n" +
                "- Phải gợi ý tên địa điểm nổi bật cụ thể, có thật và phổ biến trên Google Maps\n" +
                "- Không được ghi mơ hồ như: \"quán ăn địa phương\", \"chợ trung tâm\", \"ven hồ\", \"gần khu du lịch\", \"tùy chọn\"\n" +
                "- Gợi ý tên địa điểm cụ thể như sau:\n" +
                "  - Ví dụ: \"Bánh mì xíu mại Cô Ba, 16 Nguyễn Văn Trỗi, Phường 1, Thành phố Đà Lạt\"\n" +
                "  - Ví dụ: \"Cafe Tùng, 6 Khu Hòa Bình, Phường 1, Thành phố Đà Lạt\"\n" +
                "- Địa chỉ phải đầy đủ: tên địa điểm + số nhà (nếu có) + đường + phường/xã + quận/huyện + tỉnh/thành\n" +
                "- Ưu tiên những địa điểm có đánh giá tốt, nhiều người biết, được khách du lịch yêu thích\n" +
                "- Nếu không thể tìm được địa điểm cụ thể → bỏ qua hoạt động đó\n\n" +
                "MÔ TẢ ĐỊA ĐIỂM:\n" +
                "- Trường \"placeDetail\" bắt buộc phải có trong mỗi hoạt động\n" +
                "- Nội dung placeDetail mô tả địa điểm đó có gì hay, đặc biệt, nổi bật gì về cảnh quan – lịch sử – đặc sản – văn hóa\n" +
                "- Giải thích vì sao nên đến vào thời điểm đó trong ngày (sáng/chiều/tối)\n" +
                "- Viết giống như bạn đang giới thiệu địa điểm này cho du khách\n\n" +
                "Trả về JSON duy nhất, không có markdown hoặc chú thích bên ngoài:\n\n" +
                "{ \"days\": [ { \"dayNumber\": 1, \"title\": \"Ngày khám phá\", \"activities\": [ { \"timeOfDay\": \"Sáng\", \"description\": \"Ăn sáng tại quán nổi tiếng\", \"estimatedCost\": 50000, \"transportation\": \"xe máy\", \"address\": \"Bánh mì xíu mại Cô Ba, 16 Nguyễn Văn Trỗi, Phường 1, Thành phố Đà Lạt\", \"placeDetail\": \"Lý do hấp dẫn, lịch sử, phong cảnh hoặc lý do nên đến vào thời điểm này\" } ], \"dailyCost\": 250000 } ], \"totalCost\": 1000000 }";
        }

        private async Task<string?> TryRepairJsonAsync(string brokenJson)
        {
            var repairPrompt = "Bạn đã trả về đoạn JSON sau nhưng nó bị lỗi không phân tích được:\n\n" +
                               brokenJson +
                               "\n\nHãy sửa lại JSON này đúng định dạng version \"v1.0\", không có chữ nào bên ngoài JSON. Trả lại JSON duy nhất, không thêm lời giải thích.";

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
            try
            {
                string text = JsonDocument.Parse(raw)
                    .RootElement.GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                var start = text.IndexOf('{');
                var end = text.LastIndexOf('}');
                if (start == -1 || end == -1 || end <= start) return null;

                return text.Substring(start, end - start + 1).Trim();
            }
            catch
            {
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