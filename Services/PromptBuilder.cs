using System.Globalization;
using System.Text.Json;
using TripWiseAPI.Model;
namespace TripWiseAPI.Services
{
    public class PromptBuilder : IPromptBuilder
    {
        public string Build(TravelRequest request, string budgetVNDFormatted, string relatedKnowledge, List<string>? previousAddresses = null)
        {
            string filterNote = $"Ưu tiên địa điểm phù hợp với nhóm '{request.GroupType}', ăn uống kiểu '{request.DiningStyle}', chủ đề '{request.Preferences}', ngân sách tối đa {budgetVNDFormatted} đồng.";

            string dayNote = request.Days switch
            {
                <= 2 => "Tạo trải nghiệm đậm nét, không lan man. Ưu tiên những nơi biểu tượng và đồ ăn đặc sắc.",
                <= 4 => "Cân bằng giữa ăn uống, khám phá, nghỉ ngơi. Không dồn quá nhiều hoạt động trong ngày.",
                _ => "Lịch trình có nhịp độ thoải mái, kết hợp giữa hoạt động vui chơi, thư giãn và văn hóa địa phương."
            };
            var limitedPrevious = previousAddresses?.TakeLast(30).ToList();
            string exclusionNote = "";
            if (limitedPrevious != null && limitedPrevious.Any())
            {
                exclusionNote = """
                - Không được trùng các địa điểm đã xuất hiện trong các ngày trước đó (dưới đây):
                """ + string.Join("\n", limitedPrevious.Select(a => $"  - {a}"));
            }

            return $$"""
                {{filterNote}}
                {{dayNote}}

                Bạn là một hướng dẫn viên du lịch AI chuyên nghiệp của nền tảng TravelMate. Hãy tạo lịch trình {{request.Days}} ngày tại {{request.Destination}} cho nhóm {{request.GroupType}}, theo chủ đề "{{request.Preferences}}", với ngân sách khoảng {{budgetVNDFormatted}} đồng.

                === THÔNG TIN CHUYẾN ĐI ===
                - Ngày khởi hành: {{request.TravelDate:dd/MM/yyyy}}
                - Phương tiện di chuyển: {{(request.Transportation ?? "tự túc")}}
                - Phong cách ăn uống: {{(request.DiningStyle ?? "địa phương")}}
                - Chỗ ở mong muốn: {{(request.Accommodation ?? "3 sao")}}

                === HƯỚNG DẪN TẠO LỊCH TRÌNH ===
                - Mỗi ngày phải có hoạt động trải đều các khung: sáng, trưa, chiều, tối
                - Sáng (07:00–10:30), trưa (11:00–13:00), chiều (14:30–17:00), tối (18:00–21:00)
                - Ưu tiên các hoạt động ăn uống, tham quan đặc sắc, nghỉ ngơi hợp lý
                - Có thể kèm mẹo hữu ích cho du khách như: "nên đến sớm để tránh đông", "nên đặt bàn trước"
                - Không được lặp lại hoạt động trong cùng một ngày
                - Cho khách hàng biết thời điểm nên đến từng địa điểm (sáng/chiều/tối) hoặc giờ cụ thể để có trải nghiệm tốt nhất

                === RÀNG BUỘC BẮT BUỘC ===
                - Mỗi hoạt động phải có các trường:
                  - `"starttime"`: định dạng HH:mm
                  - `"endtime"`: định dạng HH:mm, hợp lý với thời lượng
                  - `"description"`: mô tả ngắn gọn hoạt động
                  - `"estimatedCost"`: số nguyên, đơn vị VNĐ, không có ký hiệu hoặc dấu phẩy
                  - `"transportation"`: ghi rõ phương tiện (VD: "Grab", "Taxi", "Đi bộ", "Xe máy")
                  - `"address"`: phải là địa chỉ cụ thể, hợp lệ (VD: "95 Ông Ích Khiêm, Thanh Khê, Đà Nẵng"), tham khảo những bài viết du lịch uy tín liên quan đến {{request.Destination}}
                  - `"placeDetail"`: mô tả sinh động, giải thích lý do nên đến
                  - "placeDetail" không được viết kiểu: “nơi lý tưởng để tham quan”, “rất nổi tiếng”, “được nhiều người yêu thích” nếu không có chi tiết cụ thể.
                                  VD đúng: "Chợ Bến Thành – khu chợ nổi tiếng với hơn 100 năm lịch sử, nơi du khách có thể mua đặc sản và thử món bánh tráng trộn nổi tiếng."
                                  VD sai: "Chợ nổi tiếng, có nhiều món ăn ngon, thích hợp để khám phá."
                  - `"mapUrl"`: link đúng định dạng Google Maps
                  - `"image"`: nếu có thumbnail thì dùng, nếu không thì để chuỗi rỗng `""`

                - CẤM HOÀN TOÀN các cụm từ sau trong bất kỳ trường nào:
                  - "tự chọn", "tùy chọn", "tùy ý", "tự do lựa chọn", "ven biển", "gần", bao gồm bất kỳ cụm từ nào yêu cầu khách hàng tự quyết định, lựa chọn, đoán địa điểm, hoặc tự tìm nơi ăn/chơi/nghỉ

                - Mỗi ngày phải có trường `"weatherNote"`: mô tả thời tiết ngắn gọn dựa trên `"weatherDescription"` và `"temperatureCelsius"`

                === NGUỒN ĐỊA ĐIỂM ===
                - Ưu tiên địa điểm có trong danh sách `relatedKnowledge` nếu phù hợp logic chuyến đi
                - Nếu cần mở rộng, chỉ lấy địa điểm:
                  - Có thật, có địa chỉ, có trên Google Maps
                  - Nằm trong bài viết/blog/review du lịch uy tín về {{request.Destination}}
                - **Không được tự nghĩ ra hoặc phỏng đoán địa điểm không kiểm chứng**
                {{exclusionNote}}

                === OUTPUT FORMAT ===
                Trả về duy nhất một object JSON theo định dạng sau. Không thêm bất kỳ giải thích, markdown, hoặc văn bản ngoài nào.

                {
                  "version": "v1.0",
                  "totalCost": 1234567,
                  "days": [
                    {
                      "dayNumber": 1,
                      "title": "string",
                      "dailyCost": 123456,
                      "weatherNote": "string",
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

                === START DATA ===
                {{relatedKnowledge}}
                === END DATA ===
                """;
                        }


        public string BuildUpdatePrompt(TravelRequest request, ItineraryResponse originalResponse, string userInstruction)
        {
            var originalJson = JsonSerializer.Serialize(
                originalResponse,
                new JsonSerializerOptions { WriteIndented = true }
            );

            var budget = request.BudgetVND.ToString("N0", CultureInfo.InvariantCulture);
            var travelDate = request.TravelDate.ToString("dd/MM/yyyy");

            return $$"""
        Bạn là một trợ lý du lịch AI. Nhiệm vụ của bạn là **cập nhật lịch trình dưới đây một cách chính xác theo yêu cầu người dùng**, đồng thời **tuân thủ đầy đủ các tiêu chuẩn dữ liệu đầu ra**.

        ### Yêu cầu người dùng:
        {{userInstruction}}

        ### Hướng dẫn cập nhật:
        - Phân tích kỹ yêu cầu người dùng để xác định rõ **ngày nào và hoạt động nào cần được thay đổi**.
        - Nếu yêu cầu chỉ rõ một ngày (ví dụ: "ngày 2") thì chỉ cập nhật đúng ngày đó.
        - Nếu yêu cầu mang tính khái quát hoặc không nêu rõ thời gian cụ thể (ví dụ: "tôi muốn ăn bún chả vào buổi sáng"), bạn phải **chủ động xác định vị trí phù hợp nhất để thay đổi hoặc chèn thêm hoạt động hợp lý**.
        - Nếu lịch trình gốc chưa có hoạt động phù hợp, bạn có thể thêm hoạt động mới vào khung giờ thích hợp (ví dụ sáng: 07:00–09:00).
        - **Với mỗi ngày, nếu chỉ một số hoạt động được yêu cầu thay đổi, hãy giữ nguyên tất cả các hoạt động còn lại như lịch trình gốc. Tuyệt đối không được viết lại toàn bộ danh sách hoạt động nếu không cần thiết.**
        - **Chỉ thay đổi những phần cần thiết**, giữ nguyên phần khác.
        - Không viết lại toàn bộ lịch trình nếu không có yêu cầu cụ thể.
        - Nếu món ăn, địa điểm hoặc yêu cầu không có trong dữ liệu gốc, hãy **chủ động đề xuất một địa điểm phù hợp, thực tế và phổ biến trong khu vực** (Google Maps).
        - **Nếu không chắc vị trí thay đổi ở đâu, hãy chèn vào thời điểm hợp lý nhất dựa trên ngữ cảnh.**
        - Nếu không có thay đổi nào được yêu cầu rõ ràng hoặc hợp lý để chèn, hãy giữ nguyên toàn bộ lịch trình gốc.

        ### Yêu cầu định dạng dữ liệu:
        - Mỗi ngày (chỉ những ngày có thay đổi) cần bao gồm:
          - dayNumber
          - title
          - dailyCost
          - weatherNote (gợi ý dựa trên thời tiết và nhiệt độ)
          - activities: Danh sách hoạt động trong ngày, mỗi hoạt động bao gồm:
            - starttime (định dạng "HH:mm")
            - endtime (định dạng "HH:mm")
            - description (mô tả ngắn gọn)
            - estimatedCost (số nguyên, đơn vị: VND)
            - transportation (phương tiện di chuyển)
            - address (tên địa điểm + địa chỉ cụ thể)
            - placeDetail (mô tả điểm đến, nét đặc biệt, thời điểm nên đi: sáng/chiều/tối)
            - mapUrl (nếu có)
            - image (dùng thumbnail từ dữ liệu gốc nếu có, nếu không thì để chuỗi rỗng "")

        ### Tiêu chuẩn địa điểm:
        - Tên địa điểm **phải cụ thể và thực tế**, xuất hiện phổ biến trên Google Maps.
        - Địa chỉ phải đầy đủ: tên địa điểm + số nhà (nếu có) + đường + phường/xã + quận/huyện + tỉnh/thành.
        - Tuyệt đối không dùng mô tả mơ hồ như: "quán ăn địa phương", "chợ trung tâm", "gần khu du lịch", "tùy chọn".
        - **Không được ghi "chưa xác định", "địa điểm cụ thể chưa xác định", hoặc bất kỳ cụm từ nào ám chỉ địa điểm chưa rõ ràng.** Nếu không xác định được từ dữ liệu gốc thì phải đề xuất một địa điểm cụ thể, phổ biến và hợp lý với ngữ cảnh.

        ### Thông tin chuyến đi:
        - Địa điểm: {{request.Destination}}
        - Ngày khởi hành: {{travelDate}}
        - Số ngày: {{request.Days}}
        - Ngân sách: {{budget}} VND
        - Nhóm: {{request.GroupType}}
        - Phong cách ăn uống: {{request.DiningStyle}}
        - Di chuyển: {{request.Transportation}}
        - Nơi ở mong muốn: {{request.Accommodation}}

        ### Lịch trình gốc:
        {{originalJson}}

        ### Kết quả mong muốn:
        - Trả về dữ liệu JSON hợp lệ theo định dạng dưới đây.
        - Nếu có thay đổi, chỉ bao gồm các ngày có thay đổi trong danh sách "days".
        - Nếu không có thay đổi gì, vẫn PHẢI trả về days chứa đầy đủ dữ liệu gốc.
        - ⚠️ Tuyệt đối không được trả về "days": []
        + Trả về JSON với field bắt buộc "days" (array), kể cả khi chỉ có một ngày.
        + Không trả về "Itinerary". Tất cả dữ liệu phải nằm trong "days".
        + Nếu có hoạt động mới được chèn, giữ nguyên các hoạt động cũ và chỉ thêm phần cần thay đổi.
        

        ```json
        {
          "version": "v1.0",
          "totalCost": 123456,
          "days": [
            {
              "dayNumber": 2,
              "title": "string",
              "dailyCost": 123456,
              "weatherNote": "string",
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
        ```
        """;
}


    }
}
