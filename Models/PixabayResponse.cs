namespace TripWiseAPI.Models
{
    public class PixabayImage
    {
        public string WebformatURL { get; set; }
    }

    public class PixabayResponse
    {
        public int TotalHits { get; set; }
        public List<PixabayImage> Hits { get; set; }
    }
}
