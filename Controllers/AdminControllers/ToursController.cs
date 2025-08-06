using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/tours")]
    public class AdminToursController : ControllerBase
    {
        private readonly IManageTourService _manageTourService;
        private readonly TripWiseDBContext _dbContext;

        private readonly ITourService _tourService;

        public AdminToursController( TripWiseDBContext dbContext, IManageTourService manageTourService, ITourService tourService)
        {
            _tourService = tourService;
            _dbContext = dbContext;
            _manageTourService = manageTourService; 
        }

        [HttpGet("all-tour")]
        public async Task<IActionResult> GetAllTours([FromQuery] string? status)
        { 
            var tours = await _manageTourService.GetToursByStatusAsync(status);
            return Ok(tours);
        }
        

        [HttpGet("{tourId}")]
        public async Task<IActionResult> GetTourDetail(int tourId)
        {
            var tour = await _manageTourService.GetTourDetailForAdminAsync(tourId);
            if (tour == null) return NotFound();
            return Ok(tour);
        }

       

        [HttpPost("{tourId}/reject")]
        public async Task<IActionResult> RejectTour(int tourId, [FromBody] string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("Rejection reason is required.");

            var adminId = GetAdminId();
            var result = await _manageTourService.RejectTourAsync(tourId, reason, adminId.Value);
            if (!result) return NotFound("Tour not found.");
            return Ok("Tour rejected.");
        }
        [HttpPost("{tourId}/submitdraft")]
        public async Task<IActionResult> SubmitDraft(int tourId)
        {
            var userId = GetAdminId();
            await _manageTourService.SubmitDraftAsync(tourId, userId.Value);
            return Ok(new { message = "Bản nháp đã được duyệt và cập nhật vào tour gốc." });
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
