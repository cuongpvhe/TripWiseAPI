using System.Globalization;
using System.Text.Json;
using TripWiseAPI.Model;
namespace TripWiseAPI.Services
{
    public class PromptBuilder : IPromptBuilder
    {
        public string Build(TravelRequest request, string budgetVNDFormatted, string relatedKnowledge)
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

                ĐỊA ĐIỂM & ĐỊA CHỈ:
                - Phải gợi ý tên địa điểm nổi bật cụ thể, có thật và phổ biến trên Google Maps
                - Không được ghi mơ hồ như: "quán ăn địa phương", "chợ trung tâm", "ven hồ", "gần khu du lịch", "tùy chọn"
                - Gợi ý tên địa điểm cụ thể như sau:
                  - Ví dụ: "Bánh mì xíu mại Cô Ba, 16 Nguyễn Văn Trỗi, Phường 1, Thành phố Đà Lạt"
                  - Ví dụ: "Cafe Tùng, 6 Khu Hòa Bình, Phường 1, Thành phố Đà Lạt"
                - Địa chỉ phải đầy đủ: tên địa điểm + số nhà (nếu có) + đường + phường/xã + quận/huyện + tỉnh/thành
                - Ưu tiên những địa điểm có đánh giá tốt, nhiều người biết, được khách du lịch yêu thích
                - Trường "image" nếu có thể thì phải lấy link ảnh của địa điểm đó ở trên Google Maps

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
                  - Nếu không có thumbnail sẵn, để trường "image" là chuỗi rỗng (""). Hệ thống backend sẽ tự động tìm ảnh minh họa phù hợp dựa trên mô tả địa điểm.
                - Với mỗi ngày, thêm trường "weatherNote": Viết một ghi chú ngắn dựa vào "weatherDescription" (mô tả thời tiết) và "temperatureCelsius" (nhiệt độ C) để đưa lời khuyên cho du khách (VD: "Trời mưa nhẹ, nhớ mang theo ô.", "Nắng gắt buổi trưa, nên dùng kem chống nắng.")
                
                
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
                - Nếu yêu cầu mang tính khái quát hoặc áp dụng cho nhiều ngày (ví dụ: "hoặc tương tự những ngày còn lại"), hãy áp dụng thay đổi cho tất cả các ngày phù hợp.
                - **Với mỗi ngày, nếu chỉ một số hoạt động được yêu cầu thay đổi, hãy giữ nguyên tất cả các hoạt động còn lại trong ngày đó như lịch trình gốc. Tuyệt đối không được viết lại toàn bộ danh sách hoạt động nếu không cần thiết.**
                - **Chỉ thay đổi những phần cần thiết**, giữ nguyên phần khác.
                - Không viết lại toàn bộ lịch trình nếu không có yêu cầu cụ thể.
                - Bạn có thể gặp các dạng yêu cầu sau:
                  - Thay đổi địa điểm cụ thể (ăn uống, tham quan, nghỉ ngơi…)
                  - Thêm hoặc xoá hoạt động trong khung giờ hoặc ngày cụ thể
                  - Điều chỉnh thời gian (ví dụ: bắt đầu muộn hơn, kết thúc sớm hơn)
                  - Cập nhật theo cảm nhận hoặc sở thích (ví dụ: "lịch trình nhẹ nhàng hơn", "ưu tiên món chay", "nhiều chỗ view đẹp để chụp hình")
                - Hãy **diễn giải và hiểu rõ ý định người dùng**, kể cả khi họ nói một cách tự nhiên và không dùng từ khoá rõ ràng.

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
                Chỉ trả về dữ liệu JSON hợp lệ theo định dạng dưới đây, không giải thích hoặc thêm nội dung nào bên ngoài:

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
                """;
        }

    }
}
