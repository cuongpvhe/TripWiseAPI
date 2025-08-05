using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PartnerTourController : ControllerBase
    {
        private readonly IPartnerService _partnerService;
        private readonly ITourService _tourService;

        public PartnerTourController(IPartnerService partnerService, ITourService tourService)
        {
            _partnerService = partnerService;
            _tourService = tourService;
        }
       
        [HttpGet("all-partners")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _partnerService.GetAllAsync();
            return Ok(users);
        }
       
        [HttpGet("partner/{partnerId}/average-rating")]
        public async Task<IActionResult> GetAverageRatingForPartner(int partnerId)
        {
            var avgRating = await _partnerService.GetAverageRatingByPartnerAsync(partnerId);
            return Ok(new { PartnerId = partnerId, AverageRating = avgRating });
        }
    }
}
