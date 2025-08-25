using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.PartnerServices
{
    public class BookingService : IBookingService
    {
        private readonly TripWiseDBContext _dbContext;

        public BookingService(TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Lấy danh sách booking của một đối tác dựa trên PartnerId.
        /// Chỉ lấy những booking có trạng thái thành công (Success) 
        /// hoặc đã hủy có lý do (Cancelled + CancelType != null).
        /// </summary>
        /// <param name="partnerId">ID của đối tác.</param>
        public async Task<List<BookingDto>> GetBookingsByPartnerAsync(int partnerId)
        {
            var bookings = await _dbContext.Bookings
                .Include(b => b.Tour)
                .Include(b => b.User)  // thêm để lấy user info
                .Where(b => b.Tour.PartnerId == partnerId && (b.BookingStatus == "Success"
                        || (b.BookingStatus == "Cancelled" && b.CancelType != null)))
                .OrderByDescending(b => b.CreatedDate)
                .Select(b => new BookingDto
                {
                    BookingId = b.BookingId,
                    TourId = b.TourId,
                    TourName = b.Tour.TourName,
                    UserId = b.UserId,
                    UserName = b.User.FirstName + " " + b.User.LastName,
                    TotalAmount = b.TotalAmount,
                    BookingStatus = b.BookingStatus,
                    RefundStatus = b.RefundStatus,
                    CreatedDate = (DateTime)b.CreatedDate
                })
                .ToListAsync();

            return bookings;
        }

        /// <summary>
        /// Lấy danh sách booking theo TourId.
        /// Chỉ lấy những booking có trạng thái thành công (Success) 
        /// hoặc đã hủy có lý do (Cancelled + CancelType != null).
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        public async Task<List<BookingDto>> GetBookingsByTourIdAsync(int tourId)
        {
            var bookings = await _dbContext.Bookings
                .Include(b => b.Tour)
                .Include(b => b.User) // lấy thông tin user
                .Where(b => b.TourId == tourId && (b.BookingStatus == "Success"
                        || (b.BookingStatus == "Cancelled" && b.CancelType != null)))
                .OrderByDescending(b => b.CreatedDate)
                .Select(b => new BookingDto
                {
                    BookingId = b.BookingId,
                    TourId = b.TourId,
                    TourName = b.Tour.TourName,
                    UserId = b.UserId,
                    UserName = b.User.FirstName + " " + b.User.LastName,
                    TotalAmount = b.TotalAmount,
                    BookingStatus = b.BookingStatus,
                    CreatedDate = b.CreatedDate ?? DateTime.MinValue
                })
                .ToListAsync();

            return bookings;
        }

    }
}
