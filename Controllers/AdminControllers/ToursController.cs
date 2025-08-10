using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Services;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/tours")]
    public class AdminToursController : ControllerBase
    {
        private readonly IManageTourService _manageTourService;
        private readonly IAIGeneratePlanService _aIGeneratePlanService;


        public AdminToursController(IManageTourService manageTourService, IAIGeneratePlanService aIGeneratePlanService)
        { 
            _aIGeneratePlanService = aIGeneratePlanService;
            _manageTourService = manageTourService; 
        }

        [HttpGet("all-tour")]
        public async Task<IActionResult> GetAllTours([FromQuery] string? status, [FromQuery] int? partnerId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var tours = await _manageTourService.GetToursByStatusAsync(status, partnerId, fromDate, toDate);
            return Ok(tours);
        }


        [HttpGet("{tourId}")]
        public async Task<IActionResult> GetTourDetail(int tourId)
        {
            var tour = await _manageTourService.GetTourDetailForAdminAsync(tourId);
            if (tour == null) return NotFound();
            return Ok(tour);
        }

        [HttpPost("{tourId}/approve")]
        public async Task<IActionResult> ApproveTour(int tourId)
        {
            var adminId = GetAdminId();
            var result = await _manageTourService.ApproveTourAsync(tourId, adminId.Value);
            if (!result) return NotFound("Tour not found.");
            return Ok("Tour approved successfully.");
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
        [HttpPost("{tourId}/submitupdatedraft")]
        public async Task<IActionResult> SubmitUpdateDraft(int tourId)
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
        [HttpPost("reject-update")]
        public async Task<IActionResult> RejectDraftUpdate(int tourId, [FromBody] string reason)
        {
            var userId = GetAdminId(); // Lấy ID partner hiện tại

            var result = await _manageTourService.RejectDraftAsync(tourId, reason, userId.Value);
            if (!result)
                return NotFound("Không tìm thấy bản nháp tương ứng");

            return Ok("Đã từ chối bản nháp thành công");
        }

    }

}
