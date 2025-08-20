using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions; // THÊM DÒNG NÀY
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
                **Cấu trúc ngày bắt buộc:**
                - **07:00-08:00: Hoạt động khởi động ngày** - BẮT BUỘC mỗi ngày phải có
                  * Ăn sáng tại địa điểm cụ thể (không phải buffet khách sạn)
                  * Hoặc hoạt động nhẹ nhàng khởi động (cà phê, tản bộ, chợ sáng)
                  * Hoạt động này giúp du khách chuẩn bị tinh thần cho ngày mới

                - **08:30-10:30: Hoạt động chính buổi sáng** - Hoạt động tốn nhiều sức lực
                  * Tham quan các điểm đến chính, leo núi, khám phá
                  * Hoạt động đòi hỏi thể lực và tập trung cao
                  * Tận dụng thời tiết mát mẻ buổi sáng

                **Khung giờ linh hoạt trong ngày:**
                - Không bắt buộc phải tuân thủ cứng nhắc các khung giờ cố định
                - Thời gian có thể điều chỉnh linh hoạt tùy theo:
                  * Tính chất hoạt động (ăn uống, tham quan, mua sắm, giải trí)
                  * Thời gian di chuyển giữa các địa điểm
                  * Giờ mở cửa/đóng cửa của địa điểm
                  * Thời điểm tốt nhất để trải nghiệm (ví dụ: ngắm hoàng hôn, chợ đêm)

                **Nguyên tắc sắp xếp thời gian:**
                - Buổi trưa (11:00-14:00): Ăn trưa, nghỉ ngơi, hoạt động trong nhà
                - Buổi chiều (14:00-18:00): Tham quan, mua sắm, hoạt động ngoài trời
                - Buổi tối (18:00-22:00): Ăn tối, giải trí, trải nghiệm văn hóa đêm

                **Lưu ý quan trọng:**
                - Ưu tiên logic thời gian và địa lý khi sắp xếp hoạt động
                - Tránh di chuyển qua lại nhiều lần trong ngày
                - Cân nhắc thời gian nghỉ ngơi phù hợp
                - Có thể kèm mẹo hữu ích: "nên đến sớm để tránh đông", "đặt bàn trước"
                - Không được lặp lại hoạt động trong cùng một ngày
                - Cho biết thời điểm tối ưu để trải nghiệm từng địa điểm
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
                - Tận dụng tối đa dữ liệu có trong danh sách `relatedKnowledge` nếu phù hợp logic chuyến đi
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


        public string BuildUpdatePrompt(TravelRequest request, ItineraryResponse originalResponse, string userInstruction, string relatedKnowledge)
        {
            var originalJson = JsonSerializer.Serialize(
                originalResponse,
                new JsonSerializerOptions { WriteIndented = true }
            );

            var budget = request.BudgetVND.ToString("N0", CultureInfo.InvariantCulture);
            var travelDate = request.TravelDate.ToString("dd/MM/yyyy");

            // Phân tích format input để xử lý đúng
            bool isTimeSpecificUpdate = Regex.IsMatch(userInstruction, @"ngày\s*\d+,?\s*\d{1,2}:\d{2}\s*-\s*\d{1,2}:\d{2}", RegexOptions.IgnoreCase);
            bool isDaySpecificUpdate = Regex.IsMatch(userInstruction, @"ngày\s*\d+", RegexOptions.IgnoreCase);
            bool isActivitySpecificUpdate = userInstruction.Contains("Trong ngày") && 
                                           userInstruction.Contains("hoạt động '") && 
                                           userInstruction.Contains("cần được thay đổi");

            string specificUpdateGuidance = "";

            if (isTimeSpecificUpdate)
            {
                specificUpdateGuidance = """
                ### ⚠️ QUAN TRỌNG - CẬP NHẬT THEO THỜI GIAN CỤ THỂ:
                - Người dùng đã chỉ định rõ ngày và khung thời gian cần thay đổi.
                - Tìm hoạt động trong khung thời gian được chỉ định và thay thế.
                - Nếu không tìm thấy hoạt động chính xác trong khung giờ đó, tìm hoạt động gần nhất trong ngày.
                - CHỈ thay đổi hoạt động được chỉ định, GIỮ NGUYÊN TẤT CẢ hoạt động khác.
                - Đảm bảo thời gian của hoạt động mới phù hợp với khung giờ được yêu cầu.
                """;
            }
            else if (isDaySpecificUpdate)
            {
                specificUpdateGuidance = """
                ### ⚠️ QUAN TRỌNG - CẬP NHẬT THEO NGÀY:
                - Người dùng đã chỉ định ngày cần thay đổi.
                - Phân tích yêu cầu để xác định hoạt động nào trong ngày cần thay đổi.
                - Nếu không rõ hoạt động cụ thể, đề xuất thay đổi hợp lý nhất.
                """;
            }
            else if (isActivitySpecificUpdate)
            {
                specificUpdateGuidance = """
                ### ⚠️ QUAN TRỌNG - CẬP NHẬT HOẠT ĐỘNG CỤ THỂ:
                - Yêu cầu này đã xác định rõ hoạt động cần thay đổi.
                - CHỈ thay đổi hoạt động được chỉ định, GIỮ NGUYÊN TẤT CẢ hoạt động khác trong ngày đó.
                """;
            }
            else
            {
                specificUpdateGuidance = """
                ### HƯỚNG DẪN CẬP NHẬT CHUNG:
                - Phân tích kỹ yêu cầu để xác định chính xác phần nào cần thay đổi.
                - Nếu không rõ vị trí cụ thể, hãy đề xuất thay đổi hợp lý nhất.
                """;
            }

            return $$"""
                Bạn là một trợ lý du lịch AI chuyên nghiệp. Nhiệm vụ của bạn là **cập nhật lịch trình dưới đây một cách chính xác theo yêu cầu người dùng**, đồng thời **tuân thủ đầy đủ các tiêu chuẩn dữ liệu đầu ra**.

                ### ⚠️ KIỂM TRA CONFLICT ĐỊA ĐIỂM TRƯỚC KHI XỬ LÝ - BẮT BUỘC:
                **BƯỚC KIỂM TRA:**
                - Lịch trình hiện tại: {{request.Destination}}
                - Yêu cầu người dùng: {{userInstruction}}
                
                **Nếu trong yêu cầu có đề cập đến thành phố/tỉnh/vùng KHÁC** ngoài {{request.Destination}} (ví dụ: "ở Hà Nội", "tại Sài Gòn", "đi Huế", "sang Nha Trang", "ở TP.HCM"), thì:
                  
                **DỪNG XỬ LÝ NGAY** và chỉ trả về JSON này:
                ```json
                {
                  "error": "LOCATION_CONFLICT",
                  "message": "Yêu cầu của bạn liên quan đến địa điểm khác ngoài {{request.Destination}}. Để đi đến địa điểm mới, vui lòng tạo hành trình mới thay vì cập nhật hành trình hiện tại.",
                  "currentDestination": "{{request.Destination}}",
                  "suggestion": "Tạo hành trình mới",
                  "actionRequired": "CREATE_NEW_ITINERARY"
                }
                ```
                
                **CHỈ tiếp tục xử lý nếu KHÔNG có conflict địa điểm.**

                ### ⚠️ RÀNG BUỘC VỀ ĐỊA ĐIỂM - QUAN TRỌNG:
                - **ĐIỂM ĐẾN CỐ ĐỊNH**: Lịch trình này được tạo cho {{request.Destination}} và KHÔNG THỂ thay đổi
                - **TẤT CẢ hoạt động mới phải nằm trong khu vực {{request.Destination}}** hoặc các điểm tham quan lân cận thuộc cùng tỉnh/thành
                - **CHỈ cập nhật hoạt động, thời gian, nội dung** - KHÔNG thay đổi địa điểm tỉnh/thành phố

                ### Yêu cầu người dùng:
                {{userInstruction}}

                {{specificUpdateGuidance}}

                ### Hướng dẫn xử lý input formats:
                - **Format "ngày X, HH:mm - HH:mm [action]"**: Tìm hoạt động trong khung thời gian chỉ định và thay thế
                - **Format "ngày X [action]"**: Xác định hoạt động phù hợp nhất trong ngày để thay đổi
                - **Format "Trong ngày X, hoạt động '...' cần thay đổi"**: Thay đổi hoạt động cụ thể được chỉ định

                ### Nguyên tắc cập nhật:
                - **CHÍNH XÁC**: Chỉ thay đổi những gì được yêu cầu, không thay đổi thêm bất kỳ phần nào khác
                - **BẢO TOÀN**: Giữ nguyên tất cả hoạt động, thời gian, địa điểm không liên quan đến yêu cầu
                - **ĐỊA LÝ**: Tất cả địa điểm mới phải thuộc {{request.Destination}} hoặc lân cận gần
                - **LOGIC**: Đảm bảo thời gian và địa lý hợp lý sau khi thay đổi
                - **CỤ THỂ**: Mọi địa điểm phải có tên và địa chỉ thực tế, có thể tìm thấy trên Google Maps

                ### Hướng dẫn cập nhật chi tiết:
                - Khi có chỉ định thời gian cụ thể (HH:mm - HH:mm), ưu tiên tìm hoạt động trong khung giờ đó
                - Nếu không tìm thấy hoạt động chính xác, tìm hoạt động gần nhất về thời gian
                - Khi thay thế hoạt động, có thể điều chỉnh nhẹ thời gian cho phù hợp với yêu cầu
                - **Tuyệt đối không viết lại toàn bộ danh sách hoạt động nếu không cần thiết**

                ### Xử lý thời gian:
                - Nếu user chỉ định thời gian cụ thể, sử dụng thời gian đó hoặc gần đó
                - Đảm bảo không bị trùng lặp thời gian với các hoạt động khác trong ngày
                - Ưu tiên giữ nguyên flow thời gian tự nhiên của ngày

                ### Nguồn địa điểm tham khảo:
                - **Ưu tiên địa điểm có trong danh sách relatedKnowledge bên dưới** nếu phù hợp với yêu cầu cập nhật
                - **CHỈ sử dụng địa điểm thuộc {{request.Destination}} hoặc lân cận gần**
                - Nếu cần mở rộng, chỉ lấy địa điểm:
                  - Có thật, có địa chỉ, có trên Google Maps
                  - Nằm trong {{request.Destination}} hoặc khu vực lân cận cùng tỉnh/thành
                  - Nằm trong bài viết/blog/review du lịch uy tín về {{request.Destination}}
                - **Không được tự nghĩ ra hoặc phỏng đoán địa điểm không kiểm chứng**

                ### Yêu cầu định dạng dữ liệu (CHỈ KHI KHÔNG CÓ LOCATION CONFLICT):
                - Mỗi ngày (chỉ những ngày có thay đổi) cần bao gồm:
                  - dayNumber
                  - title
                  - dailyCost (tính lại nếu có thay đổi chi phí)
                  - weatherNote (giữ nguyên từ dữ liệu gốc)
                  - activities: Danh sách ĐẦY ĐỦ hoạt động trong ngày, bao gồm:
                    - starttime (định dạng "HH:mm")
                    - endtime (định dạng "HH:mm")
                    - description (mô tả ngắn gọn)
                    - estimatedCost (số nguyên, đơn vị: VND)
                    - transportation (phương tiện di chuyển)
                    - address (tên địa điểm + địa chỉ cụ thể TRONG {{request.Destination}})
                    - placeDetail (mô tả điểm đến, nét đặc biệt)
                    - mapUrl (nếu có, hoặc tạo từ address)
                    - image (giữ nguyên từ dữ liệu gốc nếu có, nếu không thì để chuỗi rỗng "")

                ### Tiêu chuẩn địa điểm:
                - Tên địa điểm **phải cụ thể và thực tế**, xuất hiện phổ biến trên Google Maps
                - Địa chỉ phải đầy đủ: tên địa điểm + số nhà (nếu có) + đường + phường/xã + quận/huyện + {{request.Destination}}
                - **Address phải kết thúc bằng {{request.Destination}}** (ví dụ: "123 Đường ABC, Quận XYZ, {{request.Destination}}")
                - Tuyệt đối không dùng mô tả mơ hồ như: "quán ăn địa phương", "chợ trung tâm", "gần khu du lịch", "tùy chọn"
                - **Không được ghi "chưa xác định", "địa điểm cụ thể chưa xác định"**

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

                ### ⚠️ YÊU CẦU ĐẦU RA:
                - **Nếu có LOCATION CONFLICT**: Trả về error JSON như hướng dẫn ở trên
                - **Nếu KHÔNG có conflict**: Trả về dữ liệu JSON cập nhật theo format bên dưới
                - **Bao gồm TẤT CẢ các hoạt động trong ngày được cập nhật**, không chỉ hoạt động thay đổi
                - Nếu cập nhật ngày 2, phải trả về đầy đủ tất cả hoạt động của ngày 2
                - Tuyệt đối không được trả về "days": [] rỗng
                - Không trả về "Itinerary". Tất cả dữ liệu phải nằm trong "days"
                - **Tất cả address phải thuộc {{request.Destination}}**

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
                          "address": "string (phải kết thúc bằng {{request.Destination}})",
                          "placeDetail": "string",
                          "mapUrl": "string",
                          "image": "string"
                        }
                      ]
                    }
                  ]
                }
                ```

                === DANH SÁCH ĐỊA ĐIỂM THAM KHẢO ===
                {{relatedKnowledge}}
                === KẾT THÚC DANH SÁCH ===
                """;
            }


    }
}
