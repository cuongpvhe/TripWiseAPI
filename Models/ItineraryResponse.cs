using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TripWiseAPI.Model
{
    public class ItineraryResponse
    {
        public string Destination { get; set; }
        public int Days { get; set; }
        public string Preferences { get; set; }

        public string Transportation { get; set; }
        public string DiningStyle { get; set; }
        public string GroupType { get; set; }
        public string Accommodation { get; set; }
        public DateTime TravelDate { get; set; }

        public List<ItineraryDay> Itinerary { get; set; }
        public int TotalEstimatedCost { get; set; }
        public Decimal Budget { get; set; }

        public string SuggestedAccommodation { get; set; }

        public bool HasMore { get; set; }
        public DateTime? NextStartDate { get; set; }
        public List<string>? PreviousAddresses { get; set; }

        // ✅ THÊM CÁC TRƯỜNG MỚI ĐỂ XỬ LÝ SO SÁNH
        /// <summary>
        /// Thông báo về kết quả cập nhật (có thay đổi hay không)
        /// </summary>
        public string? UpdateMessage { get; set; }

        /// <summary>
        /// Chi tiết về những thay đổi đã thực hiện
        /// </summary>
        public string? UpdateDetails { get; set; }

        /// <summary>
        /// Có thay đổi trong lịch trình hay không
        /// </summary>
        public bool? HasChanges { get; set; }
    }

    public class ItineraryDay
    {
        public int DayNumber { get; set; }
        public string Title { get; set; }
        public List<ItineraryActivity> Activities { get; set; }
        public int DailyCost { get; set; }
        public string WeatherDescription { get; set; }
        public double TemperatureCelsius { get; set; }
        public string? WeatherNote { get; set; }
    }

    public class ItineraryActivity
    {
        [JsonPropertyName("starttime")]
        public string StartTime { get; set; }

        [JsonPropertyName("endtime")]
        public string EndTime { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("estimatedCost")]
        public int? EstimatedCost { get; set; }

        [JsonPropertyName("transportation")]
        public string Transportation { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("placeDetail")]
        public string PlaceDetail { get; set; }

        public string MapUrl { get; set; }

        [JsonPropertyName("image")]
        public string Image { get; set; }
    }

    public class JsonItineraryFormat
    {
        [JsonPropertyName("days")]
        public List<ItineraryDayRaw> Days { get; set; }

        [JsonPropertyName("totalCost")]
        public int TotalCost { get; set; }
    }

    public class ItineraryDayRaw
    {
        [JsonPropertyName("dayNumber")]
        public int DayNumber { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("dailyCost")]
        public int DailyCost { get; set; }

        [JsonPropertyName("activities")]
        public List<ItineraryActivity> Activities { get; set; }

        [JsonPropertyName("weatherNote")]
        public string? WeatherNote { get; set; }
    }

    public class ItineraryChunkResponse
    {
        public bool Success { get; set; }
        public bool HasMore { get; set; }
        public DateTime? NextStartDate { get; set; }
        public int NextChunkIndex { get; set; }
        public List<ItineraryDay> Itinerary { get; set; } = new();
        public int TotalEstimatedCost { get; set; }
    }

    public class UpdateChunkRequest
    {
        public string UserMessage { get; set; } = string.Empty;
        public int StartDay { get; set; } = 1;
        public int ChunkSize { get; set; } = 2;
    }
}
