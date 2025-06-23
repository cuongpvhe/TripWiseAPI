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
    }
}
