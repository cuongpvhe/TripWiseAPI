namespace TripWiseAPI.Services
{
    public interface IJsonRepairService
    {
        Task<string?> TryRepairAsync(string brokenJson);
    }
}
