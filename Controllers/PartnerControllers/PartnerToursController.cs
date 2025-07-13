using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers.PartnerControllers
{
    [ApiController]
    [Route("api/partner/tours")]
    public class PartnerToursController : ControllerBase
    {
        private readonly ITourService _tourService;

        public PartnerToursController(ITourService tourService)
        {
            _tourService = tourService;
        }
        [HttpGet]
        public async Task<IActionResult> GetAllTours([FromQuery] string? status)
        {
            var tours = await _tourService.GetToursByStatusAsync(status);
            return Ok(tours);
        }

        // POST: Create new tour (as Draft)
        [HttpPost]
        public async Task<IActionResult> CreateTour([FromBody] CreateFullTourDto request)
        {
            var userId = GetUserId(); 
            var tourId = await _tourService.CreateTourAsync(request, userId.Value);
            return Ok(new { TourId = tourId });
        }

        // POST: Submit existing draft tour for approval
        [HttpPost("{tourId}/submit")]
        public async Task<IActionResult> SubmitTour(int tourId)
        {
            var userId = GetUserId();
            var success = await _tourService.SubmitTourAsync(tourId, userId.Value);
            if (!success) return NotFound("Tour not found or access denied.");
            return Ok("Tour submitted.");
        }

        [HttpGet("{tourId}")]
        public async Task<IActionResult> GetTourDetail(int tourId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var tour = await _tourService.GetTourDetailAsync(tourId, userId.Value);
            if (tour == null) return NotFound();

            return Ok(tour);
        }
        // PUT: api/partner/tours/{tourId}
        [HttpPut("{tourId}")]
        public async Task<IActionResult> UpdateTour(int tourId, [FromBody] CreateFullTourDto request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var success = await _tourService.UpdateTourAsync(tourId, request, userId.Value);
            if (!success)
                return BadRequest("Tour not found or update failed.");

            return Ok("Tour updated successfully.");
        }
        // DELETE or Save as Draft
        // DELETE: api/partner/tours/{id}?action=delete
        // PUT TO DRAFT: api/partner/tours/{id}?action=to_draft
        [HttpDelete("{tourId}")]
        public async Task<IActionResult> DeleteOrDraftTour(int tourId, [FromQuery] string action)
        {
            var userId = GetUserId();
            var result = await _tourService.DeleteOrDraftTourAsync(tourId, action, userId.Value);
            if (!result) return BadRequest("Invalid action or tour not found.");
            return Ok($"Tour {action} successful.");
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
    }

}
