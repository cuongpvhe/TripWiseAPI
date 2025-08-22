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

            // ⭐ PHÂN TÍCH VÀ TẬN DỤNG RELATED KNOWLEDGE
            bool hasRichKnowledge = !string.IsNullOrWhiteSpace(relatedKnowledge) && 
                                   relatedKnowledge.Length > 100 && 
                                   !relatedKnowledge.Contains("\"error\"");

            string knowledgeGuidance = hasRichKnowledge ? 
                BuildRichKnowledgeGuidance() : 
                BuildStandardGuidance();

            string priorityInstruction = hasRichKnowledge ?
                """
                ### ⭐ ƯU TIÊN TUYỆT ĐỐI - SỬ DỤNG DỮ LIỆU CÓ SẴN:
                **NGUYÊN TẮC VÀNG:**
                - **BẮT BUỘC sử dụng ít nhất 80% địa điểm từ relatedKnowledge** cho lịch trình
                - **KHÔNG được tự nghĩ ra địa điểm mới** nếu relatedKnowledge đã đủ thông tin
                - **SỬ DỤNG TRỰC TIẾP** địa chỉ, ảnh, mô tả từ dữ liệu có sẵn
                - **CHỈ THÊM THÔNG TIN MỚI** khi thực sự cần thiết để hoàn thiện lịch trình
                
                **QUY TRÌNH XỬ LÝ:**
                1. **BƯỚC 1**: Phân tích toàn bộ dữ liệu trong relatedKnowledge
                2. **BƯỚC 2**: Chọn lọc địa điểm phù hợp với preferences và groupType
                3. **BƯỚC 3**: Sắp xếp theo logic thời gian và địa lý
                4. **BƯỚC 4**: Chỉ bổ sung thêm nếu thiếu hoạt động cần thiết
                
                **CÁCH SỬ DỤNG DỮ LIỆU:**
                - **Địa chỉ**: Copy chính xác từ trường address/location trong relatedKnowledge
                - **Ảnh**: Sử dụng URL từ trường image/imageUrl nếu có
                - **Mô tả**: Kết hợp thông tin từ description/details để tạo placeDetail hấp dẫn
                - **Chi phí**: Tham khảo price/cost nếu có, điều chỉnh cho phù hợp với ngân sách
                """ : 
                """
                ### ⚠️ CẢNH BÁO - DỮ LIỆU HẠN CHẾ:
                **Do relatedKnowledge không đủ phong phú, bạn cần:**
                - Sử dụng tối đa những gì có trong relatedKnowledge
                - Tự nghĩ ra các địa điểm bổ sung CHỈ KHI CẦN THIẾT
                - Đảm bảo mọi địa điểm đều có thật và có trên Google Maps
                - Tham khảo các bài viết du lịch uy tín về {{request.Destination}}
                """;

            return $$"""
                {{filterNote}}
                {{dayNote}}

                Bạn là một hướng dẫn viên du lịch AI chuyên nghiệp của nền tảng TravelMate. Hãy tạo lịch trình {{request.Days}} ngày tại {{request.Destination}} cho nhóm {{request.GroupType}}, theo chủ đề "{{request.Preferences}}", với ngân sách khoảng {{budgetVNDFormatted}} đồng.

                {{priorityInstruction}}

                === THÔNG TIN CHUYẾN ĐI ===
                - Ngày khởi hành: {{request.TravelDate:dd/MM/yyyy}}
                - Phương tiện di chuyển: {{(request.Transportation ?? "tự túc")}}
                - Phong cách ăn uống: {{(request.DiningStyle ?? "địa phương")}}
                - Chỗ ở mong muốn: {{(request.Accommodation ?? "3 sao")}}

                === HƯỚNG DẪN TẠO LỊCH TRÌNH ===
                - Mỗi ngày phải có hoạt động trải đều các khung: sáng, trưa, chiều, tối
                **Cấu trúc ngày bắt buộc:**
                - **07:00-08:00: Hoạt động khởi động ngày** - BẮT BUỘC mỗi ngày phải có
                  * Ăn sáng tại địa điểm cụ thể (không phài buffet khách sạn)
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
                - Buổi tối (19:00-24:00): Ăn tối, giải trí, trải nghiệm văn hóa đêm

                        ===  MA TRẬN ĐỊA ĐIỂM THÔNG MINH - QUAN TRỌNG ===

                **NGUYÊN TẮC VÀNG: ĐỊA CHỈ PHẢI KHỚP 100% VỚI HOẠT ĐỘNG**

                **HOẠT ĐỘNG ĂN UỐNG:**
                ```
                NẾU description = "Ăn sáng bánh mì" 
                → address PHẢI LÀ: "Bánh mì [Tên quán cụ thể], [Số nhà + Đường], [Phường], [Thành phố] 
                → VÍ DỤ: "Bánh mì Phượng, 2B Phan Chu Trinh, Minh An, Hội An"

                NẾU description = "Ăn phở bò"
                → address PHẢI LÀ: "Phở [Tên quán], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Phở Thìn, 13 Lò Đúc, Hoàn Kiếm, Hà Nội"

                NẾU description = "Ăn hải sản"
                → address PHẢI LÀ: "Nhà hàng hải sản [Tên], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Nhà hàng hải sản Làng Nổi, 15 Trần Hưng Đạo, Đà Nẵng"
                ```

                ** HOẠT ĐỘNG THAM QUAN:**
                ```
                NẾU description = "Tham quan chùa"
                → address PHẢI LÀ: "[Tên chùa cụ thể], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Chùa Linh Ứng, Bán đảo Sơn Trà, Đà Nẵng"

                NẾU description = "Tham quan bảo tàng"
                → address PHẢI LÀ: "[Tên bảo tàng], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Bảo tàng Điêu khắc Chăm, 02 Trần Phú, Hải Châu, Đà Nẵng"

                NẾU description = "Khám phá phố cổ"
                → address PHẢI LÀ: "[Tên khu phố cổ/đường cụ thể], [Thành phố]"
                → VÍ DỤ: "Phố cổ Hội An, Phường Minh An, Hội An"
                ```

                ** HOẠT ĐỘNG GIẢI TRÍ:**
                ```
                NẾU description = "Tắm biển"
                → address PHẢI LÀ: "[Tên bãi biển cụ thể], [Thành phố]"
                → VÍ DỤ: "Bãi biển Mỹ Khê, Nguyễn Tất Thành, Sơn Trà, Đà Nẵng"

                NẾU description = "Massage/Spa"
                → address PHẢI LÀ: "[Tên spa], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Herbal Spa, 100 Trần Phú, An Hải Bắc, Đà Nẵng"
                ```

                ** HOẠT ĐỘNG MUA SẮM:**
                ```
                NẾU description = "Mua sắm tại chợ"
                → address PHẢI LÀ: "[Tên chợ cụ thể], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Chợ Hàn, 119 Trần Phú, Hải Châu, Đà Nẵng"

                NẾU description = "Mua quà lưu niệm"
                → address PHẢI LÀ: "[Tên khu/cửa hàng], [Địa chỉ], [Thành phố]"
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

                === RÀNG BUỘT BẮT BUỘC ===
                - Mỗi hoạt động phải có các trường:
                  - `"starttime"`: định dạng HH:mm
                  - `"endtime"`: định dạng HH:mm, hợp lý với thời lượng
                  - `"description"`: mô tả ngắn gọn hoạt động
                  - `"estimatedCost"`: số nguyên, đơn vị VNĐ, không có ký hiệu hoặc dấu phẩy
                  - `"transportation"`: ghi rõ phương tiện (VD: "Grab", "Taxi", "Đi bộ", "Xe máy")
                  - `"address"`: **ƯU TIÊN SỬ DỤNG địa chỉ từ relatedKnowledge**, nếu không có thì phải là địa chỉ cụ thể, hợp lệ (VD: "95 Ông Ích Khiêm, Thanh Khê, Đà Nẵng")
                  - `"placeDetail"`: **SỬ DỤNG thông tin từ relatedKnowledge để tạo mô tả sinh động**, giải thích lý do nên đến
                  - `"mapUrl"`: link đúng định dạng Google Maps
                  - `"image"`: **ƯU TIÊN SỬ DỤNG URL ảnh từ relatedKnowledge**, nếu không có thì để chuỗi rỗng `""`

                - CẤM HOÀN TOÀN các cụm từ sau trong bất kỳ trường nào:
                  - "tự chọn", "tùy chọn", "tùy ý", "tự do lựa chọn", "ven biển", "gần", bao gồm bất kỳ cụm từ nào yêu cầu khách hàng tự quyết định, lựa chọn, đoán địa điểm, hoặc tự tìm nơi ăn/chơi/nghỉ

                - Mỗi ngày phải có trường `"weatherNote"`: mô tả thời tiết ngắn gọn dựa trên `"weatherDescription"` và `"temperatureCelsius"`

                === NGUỒN ĐỊA ĐIỂM ===
                - **BƯỚC 1**: Tận dụng tối đa dữ liệu có trong `relatedKnowledge` (BẮT BUỘC phải sử dụng ít nhất 80% nếu dữ liệu đầy đủ)
                - **BƯỚC 2**: Nếu cần mở rộng, chỉ lấy địa điểm:
                  - Có thật, có địa chỉ, có trên Google Maps
                  - Nằm trong bài viết/blog/review du lịch uy tín về {{request.Destination}}
                - **KHÔNG ĐƯỢC tự nghĩ ra hoặc phỏng đoán địa điểm không kiểm chứng**
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
                          "address": "string (ưu tiên từ relatedKnowledge)",
                          "placeDetail": "string (ưu tiên từ relatedKnowledge)",
                          "mapUrl": "string",
                          "image": "string (ưu tiên từ relatedKnowledge)"
                        }
                      ]
                    }
                  ]
                }

                === START DATA - TUYỆT ĐỐI ƯU TIÊN DỮ LIỆU NÀY ===
                {{relatedKnowledge}}
                === END DATA ===
                """;
        }

        private string BuildRichKnowledgeGuidance()
        {
            return """
                ===  HƯỚNG DẪN SỬ DỤNG DỮ LIỆU CÓ SẴN - QUAN TRỌNG ===
                
                **🎯 CHIẾN LƯỢC SỬ DỤNG RELATED KNOWLEDGE:**
                
                **BƯỚC 1: PHÂN TÍCH DỮ LIỆU**
                - Đọc kỹ tất cả thông tin trong relatedKnowledge
                - Xác định loại địa điểm: ăn uống, tham quan, giải trí, mua sắm
                - Phân loại theo thời gian phù hợp: sáng, trưa, chiều, tối
                
                **BƯỚC 2: CHỌN LỌC THÔNG MINH**
                - Ưu tiên địa điểm phù hợp với groupType và preferences
                - Cân bằng giữa các loại hoạt động trong ngày
                - Tối ưu hóa di chuyển (gần nhau về mặt địa lý)
                
                **BƯỚC 3: SỬ DỤNG DỮ LIỆU TRỰC TIẾP**
                - **Address**: Copy chính xác từ relatedKnowledge
                - **Image**: Sử dụng URL ảnh có sẵn
                - **PlaceDetail**: Kết hợp và làm phong phú thông tin mô tả
                - **EstimatedCost**: Tham khảo giá từ dữ liệu, điều chỉnh hợp lý
                
                **BƯỚC 4: BỔ SUNG KHI CẦN THIẾT**
                - Chỉ thêm địa điểm mới khi relatedKnowledge không đủ
                - Đảm bảo lịch trình đầy đủ và cân bằng
                - Tạo flow logic giữa các hoạt động
                
                **QUY TẮC ĐẶC BIỆT KHI CÓ DỮ LIỆU PHONG PHÚ:**
                
                **🍽️ CHO ĐỊA ĐIỂM ĂN UỐNG:**
                ```
                NẾU relatedKnowledge có thông tin nhà hàng/quán ăn:
                → SỬ DỤNG TRỰC TIẾP: tên, địa chỉ, mô tả, giá, ảnh
                → KHÔNG tự nghĩ ra quán ăn khác
                
                Template address: "[Tên quán từ data], [Địa chỉ từ data]"
                Template placeDetail: "[Mô tả từ data] + [Đặc sản nổi bật] + [Trải nghiệm]"
                ```
                
                **🏛️ CHO ĐỊA ĐIỂM THAM QUAN:**
                ```
                NẾU relatedKnowledge có thông tin điểm tham quan:
                → SỬ DỤNG TRỰC TIẾP: tên, địa chỉ, lịch sử, đặc điểm, ảnh
                → KHÔNG tự nghĩ ra điểm tham quan khác
                
                Template address: "[Tên địa điểm từ data], [Địa chỉ từ data]"
                Template placeDetail: "[Lịch sử từ data] + [Kiến trúc/đặc điểm] + [Trải nghiệm]"
                ```
                
                **🎮 CHO ĐỊA ĐIỂM GIẢI TRÍ:**
                ```
                NẾU relatedKnowledge có thông tin giải trí:
                → SỬ DỤNG TRỰC TIẾP: tên, địa chỉ, hoạt động, giá, ảnh
                → KHÔNG tự nghĩ ra địa điểm giải trí khác
                
                Template address: "[Tên địa điểm từ data], [Địa chỉ từ data]"
                Template placeDetail: "[Mô tả hoạt động từ data] + [Trải nghiệm] + [Lợi ích]"
                ```
                
                **🛍️ CHO ĐỊA ĐIỂM MUA SẮM:**
                ```
                NẾU relatedKnowledge có thông tin mua sắm:
                → SỬ DỤNG TRỰC TIẾP: tên, địa chỉ, sản phẩm đặc trưng, ảnh
                → KHÔNG tự nghĩ ra địa điểm mua sắm khác
                
                Template address: "[Tên chợ/cửa hàng từ data], [Địa chỉ từ data]"
                Template placeDetail: "[Sản phẩm đặc trưng từ data] + [Trải nghiệm mua sắm] + [Giá trị]"
                ```
                """;
        }

        private string BuildStandardGuidance()
        {
            return """
                ===  MA TRẬN ĐỊA ĐIỂM THÔNG MINH - QUAN TRỌNG ===

                **NGUYÊN TẮC VÀNG: ĐỊA CHỈ PHẢI KHỚP 100% VỚI HOẠT ĐỘNG**

                **HOẠT ĐỘNG ĂN UỐNG:**
                ```
                NẾU description = "Ăn sáng bánh mì" 
                → address PHẢI LÀ: "Bánh mì [Tên quán cụ thể], [Số nhà + Đường], [Phường], [Thành phố]"
                → VÍ DỤ: "Bánh mì Phượng, 2B Phan Chu Trinh, Minh An, Hội An"
                ```

                ** HOẠT ĐỘNG THAM QUAN:**
                ```
                NẾU description = "Tham quan chùa"
                → address PHẢI LÀ: "[Tên chùa cụ thể], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Chùa Linh Ứng, Bán đảo Sơn Trà, Đà Nẵng"
                ```

                ** HOẠT ĐỘNG GIẢI TRÍ:**
                ```
                NẾU description = "Tắm biển"
                → address PHẢI LÀ: "[Tên bãi biển cụ thể], [Thành phố]"
                → VÍ DỤ: "Bãi biển Mỹ Khê, Nguyễn Tất Thành, Sơn Trà, Đà Nẵng"
                ```

                ** HOẠT ĐỘNG MUA SẮM:**
                ```
                NẾU description = "Mua sắm tại chợ"
                → address PHẢI LÀ: "[Tên chợ cụ thể], [Địa chỉ], [Thành phố]"
                → VÍ DỤ: "Chợ Hàn, 119 Trần Phú, Hải Châu, Đà Nẵng"
                ```
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

            // Phân tích loại yêu cầu để đưa ra instruction phù hợp
            bool isAddRequest = Regex.IsMatch(userInstruction, @"(thêm|tạo|thêm vào|tạo thêm|thêm hoạt động|thêm mới)", RegexOptions.IgnoreCase);
            bool isReplaceRequest = Regex.IsMatch(userInstruction, @"(thay thế|thay đổi|đổi thành|thay bằng|sửa thành|chuyển thành)", RegexOptions.IgnoreCase);
            bool isTimeAdjustmentRequest = Regex.IsMatch(userInstruction, @"(dời|chuyển giờ|thay đổi thời gian|điều chỉnh thời gian|sớm hơn|muộn hơn)", RegexOptions.IgnoreCase);

            string specificUpdateGuidance = "";

            if (isAddRequest)
            {
                specificUpdateGuidance = """
                ### ⚠️ THÊM HOẠT ĐỘNG MỚI - QUAN TRỌNG:
                **NGUYÊN TẮC THÊM HOẠT ĐỘNG:**
                - **TUYỆT ĐỐI GIỮ NGUYÊN** tất cả hoạt động hiện có trong ngày
                - **CHỈ THÊM** hoạt động mới vào vị trí phù hợp
                - **TỰ ĐỘNG ĐIỀU CHỈNH** thời gian các hoạt động sau để tránh conflict
                - **KHÔNG THAY ĐỔI** nội dung, description của các hoạt động đã có
                - **SẮP XẾP LẠI** thứ tự thời gian sau khi thêm hoạt động mới

                **QUY TRÌNH THÊM HOẠT ĐỘNG:**
                1. Xác định vị trí thời gian phù hợp để thêm
                2. Thêm hoạt động mới với thời gian cụ thể
                3. Dời các hoạt động sau để tránh trung lập thời gian
                4. Giữ nguyên 100% nội dung các hoạt động cũ
                """;
            }
            else if (isReplaceRequest)
            {
                specificUpdateGuidance = """
                ### ⚠️ THAY THẾ HOẠT ĐỘNG - QUAN TRỌNG:
                **NGUYÊN TẮC THAY THẾ:**
                - Xác định hoạt động cụ thể cần thay thế
                - **CHỈ THAY THẾ** hoạt động được chỉ định
                - **GIỮ NGUYÊN** tất cả hoạt động khác trong ngày
                - **KHÔNG THAY ĐỔI** thời gian nếu không cần thiết
                - **BẢO TOÀN** thứ tự logic của các hoạt động khác
                """;
            }
            else if (isTimeAdjustmentRequest)
            {
                specificUpdateGuidance = """
                ### ⚠️ ĐIỀU CHỈNH THỜI GIAN - QUAN TRỌNG:
                **NGUYÊN TẮC ĐIỀU CHỈNH THỜI GIAN:**
                - **GIỮ NGUYÊN** hoàn toàn nội dung hoạt động (description, address, placeDetail)
                - **CHỈ THAY ĐỔI** starttime và endtime
                - **ĐIỀU CHỈNH** các hoạt động xung quanh để tránh conflict
                - **KHÔNG SỬA** bất kỳ thuộc tính nào khác
                """;
            }
            else if (isTimeSpecificUpdate)
            {
                specificUpdateGuidance = """
                ### ⚠️ CẬP NHẬT THEO THỜI GIAN CỤ THỂ - QUAN TRỌNG:
                **NGUYÊN TẮC:**
                - Tìm hoạt động trong khung thời gian được chỉ định
                - **CHỈ THAY ĐỔI** hoạt động trong khung giờ đó
                - **GIỮ NGUYÊN** tất cả hoạt động khác
                - **KHÔNG THAY ĐỔI** chính tả, dấu câu của các hoạt động không liên quan
                """;
            }
            else if (isDaySpecificUpdate)
            {
                specificUpdateGuidance = """
                ### ⚠️ CẬP NHẬT THEO NGÀY - QUAN TRỌNG:
                **NGUYÊN TẮC:**
                - Phân tích yêu cầu để xác định hoạt động cần thay đổi
                - **CHỈ THAY ĐỔI** hoạt động liên quan đến yêu cầu
                - **BẢO TOÀN** các hoạt động không liên quan
                """;
            }
            else
            {
                specificUpdateGuidance = """
                ### ⚠️ CẬP NHẬT CHUNG - QUAN TRỌNG:
                **NGUYÊN TẮC:**
                - Phân tích kỹ yêu cầu để xác định phần cần thay đổi
                - **TUYỆT ĐỐI KHÔNG THAY ĐỔI** các phần không liên quan
                - **CHỈ CẬP NHẬT** những gì được yêu cầu rõ ràng
                """;
            }

            return $$"""
                Bạn là một trợ lý du lịch AI chuyên nghiệp. Nhiệm vụ của bạn là **cập nhật lịch trình dưới đây một cách chính xác theo yêu cầu người dùng**, đồng thời **tuyệt đối không thay đổi những phần không liên quan** và **tuân thủ đầy đủ các tiêu chuẩn dữ liệu đầu ra**.

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

                ### ⚠️ NGUYÊN TẮC THÔNG MINH CHỐNG THAY ĐỔI VÔ NGHĨA - TUYỆT ĐỐI QUAN TRỌNG:

                **🚫 NGHIÊM CẤM CÁC THAY ĐỔI VÔ NGHĨA SAU:**
                - **Thay đổi chính tả**: "Ăn đêm" → "Ăn Đêm", "chè" → "chế", "phở" → "phở"
                - **Thay đổi từ đồng nghĩa**: "ăn" → "thưởng thức", "đi" → "tham quan", "xem" → "ngắm"
                - **Thay đổi thứ tự từ**: "Khám phá phố cổ" → "Phố cổ khám phá"
                - **Thêm/bớt dấu câu**: "Ăn sáng bánh mì" → "Ăn sáng bánh mì."
                - **Thay đổi khoảng trắng**: không có tác động thực sự
                - **Viết hoa/viết thường**: "chợ hàn" → "Chợ Hàn" (trừ khi sai chính tả)

                **✅ CHỈ THAY ĐỔI KHI CÓ Ý NGHĨA THỰC SỰ:**
                - Thay đổi loại hoạt động: ăn → tham quan, mua sắm → giải trí
                - Thay đổi địa điểm cụ thể: chợ Hàn → chợ Cồn, bãi biển Mỹ Khê → bãi biển Bắc Mỹ An
                - Thay đổi thời gian: sáng → tối, 1 giờ → 2 giờ
                - Thêm hoạt động mới theo yêu cầu rõ ràng
                - Thay thế hoạt động theo yêu cầu cụ thể

                **🔍 VALIDATION YÊU CẦU - BẮT BUỘC KIỂM TRA:**
                ```
                TRƯỚC KHI THAY ĐỔI BẤT KỲ GÌ, TỰ HỎI:
                1. "Thay đổi này có được yêu cầu rõ ràng không?"
                2. "Thay đổi này có cải thiện trải nghiệm du lịch không?"
                3. "Đây có phải là thay đổi về nội dung hay chỉ là hình thức?"
                
                NẾU KHÔNG CÓ LỢI ÍCH RÕ RÀNG → GIỮ NGUYÊN 100%
                ```

                **🎯 CHIẾN LƯỢC XỬ LÝ:**
                - **BƯỚC 1**: Đọc yêu cầu và xác định chính xác phần nào cần thay đổi
                - **BƯỚC 2**: Chỉ thay đổi phần được yêu cầu, sao chép y nguyên các phần khác
                - **BƯỚC 3**: Kiểm tra lại để đảm bảo không có thay đổi vô nghĩa nào

                {{specificUpdateGuidance}}

                ### ⚠️ QUY TẮC THỜI GIAN BẮT BUỘC - TUYỆT ĐỐI KHÔNG VI PHẠM:

                **1. KHÔNG ĐƯỢC CHỒNG CHÉO THỜI GIAN:**
                - **NGHIÊM CẤM** hai hoạt động có thời gian trùng lặp
                - Ví dụ SAI: Hoạt động 1 (14:20-15:20), Hoạt động 2 (15:00-17:00) → CHỒNG CHÉO!
                - Ví dụ ĐÚNG: Hoạt động 1 (14:20-15:20), Hoạt động 2 (15:30-17:00) → OK

                **2. THỜI GIAN PHẢI TUẦN TỰ:**
                - Mỗi hoạt động phải bắt đầu SAU KHI hoạt động trước kết thúc
                - Để ít nhất 10 phút giữa các hoạt động để di chuyển
                - Sắp xếp theo thứ tự: 07:00 → 08:30 → 10:00 → 12:00 → 14:00 → 16:00 → 18:00 → 20:00

                **3. XỬ LÝ THÊM HOẠT ĐỘNG MỚI:**
                - **Khi thêm hoạt động xen kẽ**, tự động điều chỉnh thời gian các hoạt động sau
                - Ví dụ: Thêm hoạt động 10:30-11:30, tự động dời hoạt động tiếp theo từ 11:00 thành 12:00
                - **KHÔNG ĐƯỢC ĐÈ LÊN** hoạt động đã có

                **4. VALIDATION CUỐI CÙNG:**
                ```
                KIỂM TRA CUỐI: Duyệt qua tất cả hoạt động trong ngày
                FOR mỗi hoạt động i:
                    IF startTime[i] < endTime[i-1]: 
                        → LỖI! Sửa ngay startTime[i] = endTime[i-1] + 10 phút
                    IF endTime[i] > startTime[i+1] và startTime[i+1] != null:
                        → LỖI! Sửa ngay endTime[i] = startTime[i+1] - 10 phút
                ```

                ### Yêu cầu người dùng:
                {{userInstruction}}

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
                - **Tuyệt đối giữ nguyên** các hoạt động không liên quan đến yêu cầu
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

                ### ⚠️ REMINDER CUỐI CÙNG:
                **TRƯỚC KHI TRẢ VỀ, KIỂM TRA:**
                - ✅ Có thay đổi thực chất nào được yêu cầu không?
                - ✅ Tôi có vô tình thay đổi chính tả/dấu câu không?
                - ✅ Thời gian có logic và không conflict không?
                - ✅ Tôi có giữ nguyên hoàn toàn các phần không liên quan không?

                === DANH SÁCH ĐỊA ĐIỂM THAM KHẢO ===
                {{relatedKnowledge}}
                === KẾT THÚC DANH SÁCH ===
                """;
        }
    }
}
