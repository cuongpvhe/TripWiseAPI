using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Controllers.PartnerControllers
{
    [ApiController]
    [Route("api/partner/tours")]
    public class PartnerToursController : ControllerBase
    {
        private readonly ITourService _tourService;
        private readonly TripWiseDBContext _dbContext;

        public PartnerToursController(ITourService tourService, TripWiseDBContext dbContext)
        {
            _tourService = tourService;
            _dbContext = dbContext;
        }
        [HttpGet]
        public async Task<IActionResult> GetAllTours([FromQuery] string? status)
        {
            var tours = await _tourService.GetToursByStatusAsync(status);
            return Ok(tours);
        }

        [HttpPost("create-tour")]
        public async Task<IActionResult> CreateTour([FromForm] CreateTourDto request)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var data = await _tourService.CreateTourAsync(request, partner.PartnerId);
            return Ok(new { message = "Tạo tour thành công", data });
        }


        // POST: Create itinerary for a tour
        [HttpPost("{tourId}/create-itinerary")]
    public async Task<IActionResult> CreateItinerary(int tourId, [FromBody] CreateItineraryDto request)
    {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var data = await _tourService.CreateItineraryAsync(tourId, request, partner.PartnerId);
        return Ok(new { message = "Tạo lịch trình thành công", data });
    }

    // POST: Create activity (TourAttraction) and update Itinerary with TourAttractionId
    [HttpPost("itinerary/{itineraryId}/create-activity")]
    public async Task<IActionResult> CreateActivity(int itineraryId, [FromForm] ActivityDayDto request)
    {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var data = await _tourService.CreateActivityAsync(itineraryId, request, partner.PartnerId);
        return Ok(new { message = "Tạo hoạt động thành công", data });
    }

        // POST: Submit existing draft tour for approval
        [HttpPost("{tourId}/submit")]
        public async Task<IActionResult> SubmitTour(int tourId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var success = await _tourService.SubmitTourAsync(tourId, partner.PartnerId);
            if (!success) return NotFound("Tour not found or access denied.");
            return Ok("Tour submitted.");
        }

        [HttpGet("{tourId}")]
        public async Task<IActionResult> GetTourDetail(int tourId)
        {

            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var tour = await _tourService.GetTourDetailAsync(tourId, partner.PartnerId);
            if (tour == null) return NotFound();

            return Ok(tour);
        }
        // 
        [HttpPut("update-tour/{tourId}")]
        public async Task<IActionResult> UpdateTour(
        int tourId,
        [FromForm] UpdateTourDto request,
        [FromForm] List<IFormFile>? imageFiles,
        [FromForm] List<string>? imageUrls)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.UpdateTourAsync(tourId, request, partner.PartnerId, imageFiles, imageUrls);
            if (!result) return NotFound("Tour not found");

            return Ok(new { message = "Tour updated successfully" });
        }

        [HttpDelete("delete-tour-images/{imageId}")]
        public async Task<IActionResult> DeleteTourImage(int imageId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.DeleteTourImageAsync(imageId, partner.PartnerId);
            if (!result) return NotFound("Image not found");

            return Ok(new { message = "Image deleted successfully" });
        }
        [HttpPut("update-itinerary/{itineraryId}")]
        public async Task<IActionResult> UpdateItinerary(int itineraryId, [FromBody] CreateItineraryDto request)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.UpdateItineraryAsync(itineraryId, partner.PartnerId, request);
            if (!result) return NotFound("Itinerary not found");

            return Ok(new { message = "Itinerary updated successfully" });
        }

        //[HttpPost("add-itinerary/{tourId}")]
        //public async Task<IActionResult> AddItinerary(int tourId, [FromBody] CreateItineraryDto request)
        //{
        //    var userId = GetUserId();
        //    if (userId == null) return Unauthorized();

        //    var result = await _tourService.AddItineraryAsync(tourId, userId.Value, request);
        //    if (!result) return NotFound("Tour not found");

        //    return Ok(new { message = "Itinerary added successfully" });
        //}

        [HttpDelete("delete-itinerary/{itineraryId}")]
        public async Task<IActionResult> DeleteItinerary(int itineraryId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.DeleteItineraryAsync(partner.PartnerId, itineraryId);
            if (!result) return NotFound("Itinerary not found");

            return Ok(new { message = "Itinerary deleted successfully" });
        }

        [HttpPut("update-activity/{activityId}")]
        public async Task<IActionResult> UpdateActivity(
        int activityId,
        [FromForm] ActivityDayDto request,
        [FromForm] List<IFormFile>? imageFiles,
        [FromForm] List<string>? imageUrls)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.UpdateActivityAsync(activityId, partner.PartnerId, request, imageFiles, imageUrls);
            if (!result) return NotFound("Activity not found");

            return Ok(new { message = "Activity updated successfully" });
        }

        //[HttpPost("add-activity/{itineraryId}")]
        //public async Task<IActionResult> AddActivity(int itineraryId,
        //[FromForm] ActivityDayDto request,
        //[FromForm] List<IFormFile>? imageFiles,
        //[FromForm] List<string>? imageUrls)
        //{
        //    var userId = GetUserId();
        //    if (userId == null) return Unauthorized();

        //    var result = await _tourService.AddActivityAsync(itineraryId, userId.Value, request, imageFiles, imageUrls);
        //    if (!result) return NotFound("Itinerary not found");

        //    return Ok(new { message = "Activity added successfully" });
        //}

        [HttpDelete("delete-activity/{activityId}")]
        public async Task<IActionResult> DeleteActivity(int activityId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var result = await _tourService.DeleteActivityAsync(partner.PartnerId, activityId);
            if (!result) return NotFound("Activity not found");

            return Ok(new { message = "Activity deleted successfully" });
        }

        [HttpDelete("delete-activity-images/{imageId}")]
        public async Task<IActionResult> DeleteAttractionImage(int imageId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.DeleteTourAttractionImageAsync(imageId, partner.PartnerId);
            if (!result) return NotFound("Image not found");

            return Ok(new { message = "Attraction image deleted successfully" });
        }


        [HttpDelete("delete-or-draft-tour/{tourId}")]
        public async Task<IActionResult> DeleteOrDraftTour(int tourId, [FromQuery] string action)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var result = await _tourService.DeleteOrDraftTourAsync(tourId, action, partner.PartnerId);
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
