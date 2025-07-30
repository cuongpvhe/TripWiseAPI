using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/tours")]
    public class AdminToursController : ControllerBase
    {
        private readonly IManageTourService _tourService;
        private readonly TripWiseDBContext _dbContext;
        private readonly IManageTourService _manageTourService;

        public AdminToursController(IManageTourService tourService, TripWiseDBContext dbContext, IManageTourService manageTourService)
        {
            _tourService = tourService;
            _dbContext = dbContext;
            _manageTourService = manageTourService; 
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingTours()
        {
            var tours = await _manageTourService.GetPendingToursAsync();
            return Ok(tours);
        }

        [HttpGet("{tourId}")]
        public async Task<IActionResult> GetTourDetail(int tourId)
        {
            var tour = await _tourService.GetTourDetailForAdminAsync(tourId);
            if (tour == null) return NotFound();
            return Ok(tour);
        }

        [HttpPost("{tourId}/approve")]
        public async Task<IActionResult> ApproveTour(int tourId)
        {
            var adminId = GetAdminId();
            var result = await _tourService.ApproveTourAsync(tourId, adminId.Value);
            if (!result) return NotFound("Tour not found.");
            return Ok("Tour approved successfully.");
        }

        [HttpPost("{tourId}/reject")]
        public async Task<IActionResult> RejectTour(int tourId, [FromBody] string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("Rejection reason is required.");

            var adminId = GetAdminId();
            var result = await _tourService.RejectTourAsync(tourId, reason, adminId.Value);
            if (!result) return NotFound("Tour not found.");
            return Ok("Tour rejected.");
        }

        private int? GetAdminId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
    }

}
