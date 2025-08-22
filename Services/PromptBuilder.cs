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
                - Buổi tối (18:00-24:00): Ăn tối, giải trí, trải nghiệm văn hóa đêm

                        ===  MA TRẬN ĐỊA ĐIỂM THÔNG MINH - QUAN TRỌNG ===

                **NGUYÊN TẮC VÀNG: ĐỊA CHỈ PHẢI KHỚP 100% VỚI HOẠT ĐỘNG**

                **HOẠT ĐỘNG ĂN UỐNG:**
                ```
                NẾU description = "Ăn sáng bánh mì" 
                → address PHẢI LÀ: "Bánh mì [Tên quán cụ thể], [Số nhà + Đường], [Phường], {{request.Destination}}"
                → VÍ DỤ: "Bánh mì Phượng, 2B Phan Chu Trinh, Minh An, Hội An"

                NẾU description = "Ăn phở bò"
                → address PHẢI LÀ: "Phở [Tên quán], [Địa chỉ], {{request.Destination}}"
                → VÍ DỤ: "Phở Thìn, 13 Lò Đúc, Hoàn Kiếm, Hà Nội"

                NẾU description = "Ăn hải sản"
                → address PHẢI LÀ: "Nhà hàng hải sản [Tên], [Địa chỉ], {{request.Destination}}"
                → VÍ DỤ: "Nhà hàng hải sản Làng Nổi, 15 Trần Hưng Đạo, Đà Nẵng"
                ```

                ** HOẠT ĐỘNG THAM QUAN:**
                ```
                NẾU description = "Tham quan chùa"
                → address PHẢI LÀ: "[Tên chùa cụ thể], [Địa chỉ], {{request.Destination}}"
                → VÍ DỤ: "Chùa Linh Ứng, Bán đảo Sơn Trà, Đà Nẵng"

                NẾU description = "Tham quan bảo tàng"
                → address PHẢI LÀ: "[Tên bảo tàng], [Địa chỉ], {{request.Destination}}"
                → VÍ DỤ: "Bảo tàng Điêu khắc Chăm, 02 Trần Phú, Hải Châu, Đà Nẵng"

                NẾU description = "Khám phá phố cổ"
                → address PHẢI LÀ: "[Tên khu phố cổ/đường cụ thể], {{request.Destination}}"
                → VÍ DỤ: "Phố cổ Hội An, Phường Minh An, Hội An"
                ```

                ** HOẠT ĐỘNG GIẢI TRÍ:**
                ```
                NẾU description = "Tắm biển"
                → address PHẢI LÀ: "[Tên bãi biển cụ thể], {{request.Destination}}"
                → VÍ DỤ: "Bãi biển Mỹ Khê, Nguyễn Tất Thành, Sơn Trà, Đà Nẵng"

                NẾU description = "Massage/Spa"
                → address PHẢI LÀ: "[Tên spa], [Địa chỉ], {{request.Destination}}"
                → VÍ DỤ: "Herbal Spa, 100 Trần Phú, An Hải Bắc, Đà Nẵng"
                ```

                ** HOẠT ĐỘNG MUA SẮM:**
                ```
                NẾU description = "Mua sắm tại chợ"
                → address PHẢI LÀ: "[Tên chợ cụ thể], [Địa chỉ], {{request.Destination}}"
                → VÍ DỤ: "Chợ Hàn, 119 Trần Phú, Hải Châu, Đà Nẵng"

                NẾU description = "Mua quà lưu niệm"
                → address PHẢI LÀ: "[Tên khu/cửa hàng], [Địa chỉ], {{request.Destination}}"
                → VÍ DỤ: "Khu phố đi bộ An Thượng, An Hải Bắc, Sơn Trà, Đà Nẵng"
                ```

                ===  QUY TẮC PLACEDETAIL HẤP DẪN - QUAN TRỌNG ===

                **MỤC TIÊU: TẠO HỨNG THÚ VÀ MONG MUỐN TRẢI NGHIỆM**

                **❌ CÁCH VIẾT SAI (khô khan, không hấp dẫn):**
                - "Nơi lý tưởng để tham quan"
                - "Rất nổi tiếng, được nhiều người yêu thích"
                - "Có nhiều món ăn ngon"
                - "Thích hợp để khám phá"

                ** CÁCH VIẾT ĐÚNG (sinh động, tạo cảm xúc):**

                ** CHO HOẠT ĐỘNG ĂN:**
                ```
                Template: "[Tên món] tại [tên quán] [đặc điểm nổi bật] + [trải nghiệm cảm giác] + [lý do nên thử]"

                VD1: "Bánh mì Phượng với lớp vỏ giòn rụm, nhân thịt thơm phức và rau sống tươi mát sẽ cho bạn trải nghiệm hương vị đúng điệu Hội An ngay từ miếng đầu tiên."

                VD2: "Phở bò đậm đà với nước dùng ninh từ xương trong 8 tiếng, thịt bò tái mềm ngọt, sẽ làm ấm lòng bạn trong những sáng se lạnh Hà Nội."

                VD3: "Hải sản tươi sống vừa đánh bắt, chế biến theo phong cách đặc trưng miền biển, hứa hẹn bữa tiệc vị giác đáng nhớ bên gia đình."
                ```

                ** CHO HOẠT ĐỘNG THAM QUAN:**
                ```
                Template: "[Mô tả không gian/kiến trúc] + [giá trị lịch sử/văn hóa] + [trải nghiệm cụ thể] + [cảm xúc/kỷ niệm]"

                VD1: "Chùa Linh Ứng với tượng Phật Quan Âm cao 67m sừng sững giữa mây trời, nơi bạn có thể cầu nguyện bình an và ngắm toàn cảnh Đà Nẵng từ trên cao trong không gian thiêng liêng."

                VD2: "Bảo tàng với hơn 300 tác phẩm điêu khắc Chăm tinh xảo, mỗi tác phẩm là một câu chuyện về nền văn minh cổ xưa, giúp bạn hiểu sâu về lịch sử vùng đất này."

                VD3: "Phố cổ với những ngôi nhà ống vàng ươm, lồng đèn đầy màu sắc và mùi hương hoa lài thoang thoảng, sẽ đưa bạn lạc về quá khứ thơ mộng của Hội An."
                ```

                ** CHO HOẠT ĐỘNG GIẢI TRÍ:**
                ```
                Template: "[Mô tả môi trường] + [hoạt động cụ thể] + [cảm giác thư giãn] + [kỷ niệm đáng nhớ]"

                VD1: "Bãi biển Mỹ Khê với làn nước trong xanh, cát trắng mịn màng và sóng nhẹ nhàng, nơi bạn có thể tắm mát, chụp ảnh sống ảo và tận hưởng không khí biển trong lành."

                VD2: "Liệu pháp massage truyền thống với tinh dầu thảo mộc, giúp bạn xả stress sau ngày dài khám phá và tái tạo năng lượng cho hành trình tiếp theo."
                ```

                ** CHO HOẠT ĐỘNG MUA SẮM:**
                ```
                Template: "[Mô tả không gian mua sắm] + [sản phẩm đặc trưng] + [trải nghiệm mua sắm] + [giá trị mang về]"

                VD1: "Chợ Hàn sầm uất với hàng trăm gian hàng, nơi bạn có thể mặc cả vui vẻ, thử đồ ăn vặt ngon và tìm mua những món quà handmade độc đáo mang đậm dấu ấn Đà Nẵng."

                VD2: "Khu phố đi bộ với những cửa hàng boutique xinh xắn, nơi bạn có thể tìm thấy từ áo dài truyền thống đến đồ thủ công mỹ nghệ, tạo nên bộ sưu tập kỷ niệm ý nghĩa."
                ```

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
                - Giá cả của các hoạt động phải hợp lý với từng địa điểm và ngân sách của người dùng.
                - Nếu có giá cả nào chưa hợp lý cần thay đổi, hãy đề xuất giá mới hợp lý hơn kể cả với thông tin từ relatedKnowledge nếu chưa hợp lý cần cập nhật lại.

                === RÀNG BUỘC BẮT BUỘC ===
                - Mỗi hoạt động phải có các trường:
                  - `"starttime"`: định dạng HH:mm
                  - `"endtime"`: định dạng HH:mm, hợp lý với thời lượng
                  - `"description"`: mô tả ngắn gọn hoạt động
                  - `"estimatedCost"`: số nguyên, đơn vị VNĐ, không có ký hiệu hoặc dấu phẩy
                  - `"transportation"`: ghi rõ phương tiện (VD: "Grab", "Taxi", "Đi bộ", "Xe máy")
                  - `"address"`: phải là địa chỉ cụ thể, hợp lệ (VD: "95 Ông Ích Khiêm, Thanh Khê, Đà Nẵng"), tham khảo những bài viết du lịch uy tín liên quan đến {{request.Destination}}
                              Ví dụ sai: "Resort 4 sao, Địa chỉ cụ thể"
                  - Đối với nơi ở, địa chỉ phải là tên khách sạn/nhà nghỉ/homestay/resort cụ thể tại {{request.Destination}}, không dùng loại hình chung chung.
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

                ### ⚠️ NGUYÊN TẮC THÔNG MINH CHỐNG THAY ĐỔI VÔ NGHĨA - QUAN TRỌNG:
                **KIỂM TRA TRƯỚC KHI THAY ĐỔI:**
                - **Nếu yêu cầu người dùng không rõ ràng hoặc mơ hồ**, GIỮ NGUYÊN lịch trình và KHÔNG thay đổi
                - **Nếu lịch trình hiện tại đã phù hợp với yêu cầu**, GIỮ NGUYÊN và trả lời "Lịch trình hiện tại đã phù hợp"
                - **NGHIÊM CẤM** các thay đổi vô nghĩa như:
                  * Thay đổi chính tả nhỏ: "chè" → "chế", "phở" → "phở", "bánh mì" → "bánh mì"
                  * Thay đổi từ đồng nghĩa mà không cải thiện: "ăn" → "thưởng thức", "đi" → "tham quan"
                  * Thay đổi thứ tự từ mà không thay đổi ý nghĩa: "Khám phá phố cổ" → "Phố cổ khám phá"
                  * Thêm/bớt dấu câu hoặc khoảng trắng không ảnh hưởng đến nội dung
                
                **QUY TẮC THAY ĐỔI HỢP LỆ:**
                - Chỉ thay đổi khi có **ý nghĩa thực sự khác biệt** về:
                  * Loại hoạt động (ăn → tham quan, mua sắm → giải trí)
                  * Địa điểm cụ thể (chợ Hàn → chợ Cồn, bãi biển Mỹ Khê → bãi biển Bắc Mỹ An)
                  * Thời gian hoạt động (sáng → tối, 1 giờ → 2 giờ)
                
                **VALIDATION YÊU CẦU:**
                - Kiểm tra từng thay đổi: "Thay đổi này có cải thiện trải nghiệm du lịch không?"
                - Nếu không có lợi ích rõ ràng → GIỮ NGUYÊN
                - Nếu chỉ là thay đổi hình thức mà không cải thiện nội dung → GIỮ NGUYÊN

                ### ⚠️ QUY TẮC THỜI GIAN BẮT BUỘC - TUYỆT ĐỐI KHÔNG VI PHẠM:

                **1. KHÔNG ĐƯỢC CHỒNG CHÉO THỜI GIAN:**
                - **NGHIÊM CẤM** hai hoạt động có thời gian trùng lặp
                - Ví dụ SAI: Hoạt động 1 (14:20-15:20), Hoạt động 2 (15:00-17:00) → CHỒNG CHÉO!
                - Ví dụ ĐÚNG: Hoạt động 1 (14:20-15:20), Hoạt động 2 (15:30-17:00) → OK

                **2. THỜI GIAN PHẢI TUẦN TỰ:**
                - Mỗi hoạt động phải bắt đầu SAU KHI hoạt động trước kết thúc
                - Để ít nhất 10 phút giữa các hoạt động để di chuyển
                - Sắp xếp theo thứ tự: 07:00 → 08:30 → 10:00 → 12:00 → 14:00 → 16:00 → 18:00 → 20:00

                **3. KIỂM TRA LOGIC THỜI GIAN:**
                - **TRƯỚC KHI TRẢ VỀ**, kiểm tra từng ngày:
                  * Hoạt động 1: 07:00-08:00 ✓
                  * Hoạt động 2: 08:30-10:00 ✓ (bắt đầu sau khi hoạt động 1 kết thúc)
                  * Hoạt động 3: 10:30-12:00 ✓ (bắt đầu sau khi hoạt động 2 kết thúc)
                - **NẾU PHÁT HIỆN CONFLICT → SỬA NGAY** hoặc bỏ hoạt động có vấn đề

                **4. XỬ LÝ THÊM HOẠT ĐỘNG MỚI:**
                - **Khi thêm hoạt động xen kẽ**, tự động điều chỉnh thời gian các hoạt động sau
                - Ví dụ: Thêm hoạt động 10:30-11:30, tự động dời hoạt động tiếp theo từ 11:00 thành 12:00
                - **KHÔNG ĐƯỢC ĐÈ LÊN** hoạt động đã có

                **5. VALIDATION CUỐI CÙNG:**
                ```
                KIỂM TRA CUỐI: Duyệt qua tất cả hoạt động trong ngày
                FOR mỗi hoạt động i:
                    IF startTime[i] < endTime[i-1]: 
                        → LỖI! Sửa ngay startTime[i] = endTime[i-1] + 10 phút
                    IF endTime[i] > startTime[i+1] và startTime[i+1] != null:
                        → LỖI! Sửa ngay endTime[i] = startTime[i+1] - 10 phút
                ```

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
                - **HỢP LÝ**: Mọi thay đổi phải có ý nghĩa và cải thiện trải nghiệm du lịch
                - **THỜI GIAN LOGIC**: Tuyệt đối không có conflict hoặc chồng chéo thời gian

                ### ⚠️ KIỂM TRA CUỐI CÙNG TRƯỚC KHI TRẢ VỀ:
                **So sánh lịch trình gốc và mới:**
                - Nếu chỉ có thay đổi về chính tả, dấu câu, thứ tự từ → GIỮ NGUYÊN lịch trình gốc
                - Nếu không có thay đổi ý nghĩa thực sự → GIỮ NGUYÊN lịch trình gốc
                - **KIỂM TRA THỜI GIAN**: Duyệt qua tất cả hoạt động để đảm bảo không có conflict
                - Chỉ trả về lịch trình mới khi có thay đổi **thực chất** cải thiện trải nghiệm VÀ thời gian hợp lý

                ### Hướng dẫn cập nhật chi tiết:
                - Khi có chỉ định thời gian cụ thể (HH:mm - HH:mm), ưu tiên tìm hoạt động trong khung giờ đó
                - Nếu không tìm thấy hoạt động chính xác, tìm hoạt động gần nhất về thời gian
                - **Khi thêm hoạt động xen kẽ**: Tự động điều chỉnh thời gian các hoạt động sau để tránh conflict
                - **Khi thay thế hoạt động**: Giữ nguyên thời gian hoặc điều chỉnh nhẹ cho phù hợp
                - **Tuyệt đối không viết lại toàn bộ danh sách hoạt động nếu không cần thiết**

                ### Xử lý thời gian:
                - **Ưu tiên 1**: Giữ nguyên thời gian của các hoạt động không bị ảnh hưởng
                - **Ưu tiên 2**: Nếu user chỉ định thời gian cụ thể, sử dụng thời gian đó
                - **Ưu tiên 3**: Tự động điều chỉnh các hoạt động xung quanh để tránh conflict
                - **Luôn luôn**: Đảm bảo không bị trùng lặp thời gian với các hoạt động khác trong ngày
                - **Logic flow**: Ưu tiên giữ nguyên flow thời gian tự nhiên của ngày

                ### Nguồn địa điểm tham khảo:
                - **Ưu tiên địa điểm có trong danh sách relatedKnowledge bên dưới** nếu phù hợp với yêu cầu cập nhật
                - **CHỈ sử dụng địa điểm thuộc {{request.Destination}} hoặc lân cận gần**
                - Nếu cần mở rộng, chỉ lấy địa điểm:
                  - Có thật, có địa chỉ, có trên Google Maps
                  - Nằm trong {{request.Destination}} hoặc khu vực lân cận cùng tỉnh/thành
                  - Nằm trong bài viết/blog/review du lịch uy tín về {{request.Destination}}
                - **Không được tự nghĩ ra hoặc phỏng đoán địa điểm không kiểm chứng**

                ### Yêu cầu định dạng dữ liệu (CHỈ KHI KHÔNG CÓ LOCATION CONFLICT):
                - Mỗi ngày (chỉ những ngày có thay đổi **ý nghĩa**) cần bao gồm:
                  - dayNumber
                  - title
                  - dailyCost (tính lại nếu có thay đổi chi phí)
                  - weatherNote (giữ nguyên từ dữ liệu gốc)
                  - activities: Danh sách ĐẦY ĐỦ hoạt động trong ngày **ĐƯỢC SẮP XẾP THEO THỜI GIAN**, bao gồm:
                    - starttime (định dạng "HH:mm") - **BẮT BUỘC phải logic với endtime của hoạt động trước**
                    - endtime (định dạng "HH:mm") - **BẮT BUỘC phải ≤ starttime của hoạt động sau**
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
                - **Nếu KHÔNG có thay đổi ý nghĩa**: Trả về JSON với "days": [] (rỗng) để báo hiệu không có update
                - **Nếu có thay đổi thực sự**: Trả về dữ liệu JSON cập nhật theo format bên dưới
                - **BẮT BUỘC**: Tất cả hoạt động phải được **sắp xếp theo thứ tự thời gian tăng dần**
                - **BẮT BUỘC**: Không được có bất kỳ xung đột thời gian nào
                - **Bao gồm TẤT CẢ các hoạt động trong ngày được cập nhật**, không chỉ hoạt động thay đổi
                - Nếu cập nhật ngày 2, phải trả về đầy đủ tất cả hoạt động của ngày 2
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
