using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TourUserController : ControllerBase
    {
        private readonly ITourUserService _tourUserService;
        public TourUserController(ITourUserService tourUserService)
        {
            _tourUserService = tourUserService;
        }
        // GET: api/public/tours/approved
        [HttpGet("approved")]
        public async Task<IActionResult> GetApprovedTours()
        {
            var tours = await _tourUserService.GetApprovedToursAsync();
            return Ok(tours);
        }

        // GET: api/public/tours/approved/{tourId}
        [HttpGet("approved/{tourId}")]
        public async Task<IActionResult> GetApprovedTourDetail(int tourId)
        {
            var detail = await _tourUserService.GetApprovedTourDetailAsync(tourId);
            if (detail == null)
                return NotFound("Không tìm thấy tour.");
            return Ok(detail);
        }
    }
}
