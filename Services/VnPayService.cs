using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Numerics;
using System.Text.RegularExpressions;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _configuration;
        private readonly TripWiseDBContext _dbContext;
        private readonly IPlanService _planService;
        private readonly IServiceProvider _serviceProvider;
        private readonly FirebaseLogService _logService;
		public VnPayService(IConfiguration config, TripWiseDBContext dbContext, IPlanService planService, IServiceProvider serviceProvider, FirebaseLogService firebaseLog)
        {
            _configuration = config;
            _dbContext = dbContext;
            _planService = planService;
            _serviceProvider = serviceProvider;
			_logService = firebaseLog;
		}

        /// <summary>
        /// Tạo URL thanh toán VNPay cho một đơn hàng/booking.
        /// </summary>
        /// <param ="model">Thông tin đơn hàng cần thanh toán</param>
        /// <param ="context">HttpContext hiện tại</param>
        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            var orderCode = string.IsNullOrEmpty(model.OrderCode)
                ? Guid.NewGuid().ToString("N")   
                : model.OrderCode;               

            var pay = new VnPayLibrary();
            var timeNow = TimeHelper.GetVietnamTime();

            pay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
            pay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"]);
            pay.AddRequestData("vnp_TmnCode", _configuration["Vnpay:TmnCode"]);
            pay.AddRequestData("vnp_Amount", ((long)(model.Amount * 100)).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"]);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"]);
            pay.AddRequestData("vnp_OrderInfo", WebUtility.UrlEncode(model.OrderDescription));
            pay.AddRequestData("vnp_OrderType", model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", _configuration["PaymentCallBack:ReturnUrl"]);
            pay.AddRequestData("vnp_TxnRef", orderCode);

            // 💾 Kiểm tra PaymentTransaction đã tồn tại chưa
            var existingTransaction = _dbContext.PaymentTransactions
                .FirstOrDefault(t => t.OrderCode == orderCode);

            if (existingTransaction != null)
            {
                // Update transaction cũ
                existingTransaction.Amount = model.Amount;
                existingTransaction.PaymentStatus = PaymentStatus.Pending;
                existingTransaction.ModifiedDate = timeNow;
                existingTransaction.ModifiedBy = model.UserId;
            }
            else
            {
                // Thêm mới transaction nếu chưa tồn tại
                var transaction = new PaymentTransaction
                {
                    OrderCode = orderCode,
                    UserId = model.UserId,
                    Amount = model.Amount,
                    PaymentStatus = PaymentStatus.Pending,
                    BookingId = model.BookingId,
                    PlanId = model.PlanId,
                    CreatedDate = timeNow,
                    CreatedBy = model.UserId,
                };
                _dbContext.PaymentTransactions.Add(transaction);
            }

            _dbContext.SaveChanges();

            model.OrderCode = orderCode;

            return pay.CreateRequestUrl(_configuration["Vnpay:BaseUrl"], _configuration["Vnpay:HashSecret"]);
        }

        /// <summary>
        /// Người dùng mua gói (Plan) thông qua VNPay.
        /// </summary>
        /// <param ="request">Thông tin request mua gói</param>
        /// <param ="userId">ID người dùng</param>
        /// <param ="context">HttpContext hiện tại</param>
        public async Task<string> BuyPlanAsync(BuyPlanRequest request, int userId, HttpContext context)
        {
            var plan = await _dbContext.Plans
                .FirstOrDefaultAsync(p => p.PlanId == request.PlanId && p.RemovedDate == null);

            if (plan == null)
                throw new Exception("Gói cước không tồn tại.");

            if (plan.Price == null || plan.Price <= 0)
                throw new Exception("Gói cước không hợp lệ.");

            var paymentModel = new PaymentInformationModel
            {
                UserId = userId,
                Amount = (decimal)plan.Price,
                Name = $"Plan: {plan.PlanName}",
                OrderDescription = $"Thanh toán gói {plan.PlanName} giá {plan.Price:N0} VND",
                OrderType = "plan",
                PlanId = plan.PlanId
            };
			await _logService.LogAsync(userId: userId, action: "Create", message: $"Người dùng {userId} mua gói {plan.PlanName} giá {plan.Price:N0} VND", statusCode: 200, createdBy: userId, createdDate:DateTime.Now);
			return CreatePaymentUrl(paymentModel, context);
        }

        /// <summary>
        /// Lấy lịch sử giao dịch thanh toán của người dùng.
        /// </summary>
        /// <param ="userId">ID người dùng</param>
        /// <param ="status">Trạng thái thanh toán (Pending/Success/Failed...)</param>
        public async Task<List<PaymentTransactionDto>> GetPaymentHistoryAsync(int userId, string? status)
        {
            var query = _dbContext.PaymentTransactions
                .Where(p => p.UserId == userId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.PaymentStatus == status);
            }

            var transactions = await query
                .OrderByDescending(p => p.PaymentTime ?? p.CreatedDate)
                .ToListAsync();

            var result = new List<PaymentTransactionDto>();

            foreach (var transaction in transactions)
            {
                string? planName = null;
                string? tourName = null;
                int? tourId = null;
                int? bookingId = transaction.BookingId;

                if (transaction.PlanId.HasValue)
                {
                    planName = await _dbContext.Plans
                        .Where(p => p.PlanId == transaction.PlanId && p.RemovedDate == null)
                        .Select(p => p.PlanName)
                        .FirstOrDefaultAsync();
                }
                else if (transaction.BookingId.HasValue)
                {
                    var bookingInfo = await _dbContext.Bookings
                        .Where(b => b.BookingId == transaction.BookingId)
                        .Select(b => new { b.BookingId, b.Tour.TourName, b.TourId })
                        .FirstOrDefaultAsync();

                    if (bookingInfo != null)
                    {
                        tourName = bookingInfo.TourName;
                        tourId = bookingInfo.TourId;
                    }
                }

                result.Add(new PaymentTransactionDto
                {
                    TransactionId = transaction.TransactionId,
                    OrderCode = transaction.OrderCode,
                    Amount = transaction.Amount,
                    PaymentStatus = transaction.PaymentStatus,
                    BankCode = transaction.BankCode,
                    PaymentTime = transaction.PaymentTime,
                    CreatedDate = transaction.CreatedDate,
                    PlanName = planName,
                    TourName = tourName,
                    TourId = tourId,
                    BookingId = bookingId
                });
            }
            return result;
        }

        /// <summary>
        /// Lấy chi tiết booking theo ID.
        /// </summary>
        /// <param ="bookingId">ID booking</param>
        public async Task<BookingDetailDto?> GetBookingDetailAsync(int bookingId)
        {
            var booking = await
                (from b in _dbContext.Bookings
                     // PaymentTransaction không nối bằng navigation property
                 join pt in _dbContext.PaymentTransactions on b.OrderCode equals pt.OrderCode into ptJoin
                 from pt in ptJoin.DefaultIfEmpty()
                 where b.BookingId == bookingId
                 select new BookingDetailDto
                 {
                     BookingId = b.BookingId,
                     TourName = b.TourName,   // lấy từ Booking (snapshot)
                     OrderCode = b.OrderCode,
                     PaymentStatus = pt != null ? pt.PaymentStatus : null,
                     BankCode = pt != null ? pt.BankCode : null,
                     VnpTransactionNo = pt != null ? pt.VnpTransactionNo : null,
                     StartDate = b.StartTime,
                     UserEmail = b.UserEmail,   // snapshot từ Booking
                     PhoneNumber = b.PhoneNumber,
                     FirstName = b.FirstName,
                     LastName = b.LastName,

                     PriceAdult = b.PriceAdult,
                     PriceChild5To10 = b.PriceChild5To10,
                     PriceChildUnder5 = b.PriceChildUnder5,

                     NumAdults = b.NumAdults,
                     NumChildren5To10 = b.NumChildren5To10,
                     NumChildrenUnder5 = b.NumChildrenUnder5,
                     Amount = b.TotalAmount,
                     PaymentTime = pt != null ? pt.PaymentTime : null,
                     CreatedDate = b.CreatedDate,

                     RefundAmount = b.RefundAmount,
                     RefundMethod = b.RefundMethod,
                     RefundStatus = b.RefundStatus,
                     RefundDate = b.RefundDate,
                     CancelReason = b.CancelReason
                 })
                .FirstOrDefaultAsync();

            return booking;
        }

        /// <summary>
        /// Tạo booking ở trạng thái nháp (Draft).
        /// </summary>
        /// <param ="request">Thông tin request đặt tour</param>
        /// <param ="userId">ID người dùng</param>
        public async Task<BookingDetailDto> CreateBookingDraftAsync(BuyTourRequest request, int userId)
        {
            var user = await _dbContext.Users
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Email, u.PhoneNumber, u.FirstName, u.LastName })
                .FirstOrDefaultAsync();

            if (user == null)
                throw new Exception("Người dùng không tồn tại.");

            var tour = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.TourId == request.TourId && t.RemovedDate == null);

            if (tour == null)
                throw new Exception("Tour không tồn tại.");

            //Kiểm tra ngày hiện tại có lớn hơn hoặc bằng ngày bắt đầu tour không
            if (tour.StartTime.HasValue && TimeHelper.GetVietnamTime().Date >= tour.StartTime.Value.Date)
            {
                throw new Exception("Tour đã khởi hành không thể đặt trước.");
            }
            //Tính tổng số người đã đặt thành công
            var bookedCount = await _dbContext.Bookings
                .Where(b => b.TourId == request.TourId && (b.BookingStatus == BookingStatus.Success
                    || b.BookingStatus == BookingStatus.CancelPending))
                .SumAsync(b => (int?)b.Quantity) ?? 0;

            //Đảm bảo availableSlots >= 0
            var availableSlots = Math.Max(0, (decimal)(tour.MaxGroupSize - bookedCount));


            if (availableSlots <= 0)
                throw new Exception("Tour đã hết chỗ.");

            var totalPeople = request.NumAdults + request.NumChildren5To10 + request.NumChildrenUnder5;
            if (totalPeople > availableSlots)
                throw new Exception($"Chỉ còn {availableSlots} chỗ trống cho tour này.");

            var totalAmount =
                (request.NumAdults * (tour.PriceAdult ?? 0)) +
                (request.NumChildren5To10 * (tour.PriceChild5To10 ?? 0)) +
                (request.NumChildrenUnder5 * (tour.PriceChildUnder5 ?? 0));

            var now = TimeHelper.GetVietnamTime();

            var booking = new Booking
            {
                UserId = userId,
                TourId = request.TourId,
                Quantity = totalPeople,
                NumAdults = request.NumAdults,
                NumChildren5To10 = request.NumChildren5To10,
                NumChildrenUnder5 = request.NumChildrenUnder5,
                TotalAmount = totalAmount,
                BookingStatus = PaymentStatus.Draft,
                CreatedDate = now,
                CreatedBy = userId,
                OrderCode = $"{Guid.NewGuid()}",
                ExpiredDate = now.AddMinutes(5),

                // 🔹 Ghi cứng thông tin User và Tour vào bảng Booking
                TourName = tour.TourName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserEmail = user.Email,
                StartTime = tour.StartTime,
                PhoneNumber = user.PhoneNumber,
                PriceAdult = tour.PriceAdult,
                PriceChild5To10 = tour.PriceChild5To10,
                PriceChildUnder5 = tour.PriceChildUnder5
            };

            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            return new BookingDetailDto
            {
                BookingId = booking.BookingId,
                TourName = booking.TourName,
                OrderCode = booking.OrderCode,
                StartDate = booking.StartTime,
                FirstName = booking.FirstName,
                LastName = booking.LastName,
                UserEmail = booking.UserEmail,
                PhoneNumber = booking.PhoneNumber,
                PriceAdult = booking.PriceAdult,
                PriceChild5To10 = booking.PriceChild5To10,
                PriceChildUnder5 = booking.PriceChildUnder5,
                NumAdults = booking.NumAdults,
                NumChildren5To10 = booking.NumChildren5To10,
                NumChildrenUnder5 = booking.NumChildrenUnder5,
                Amount = booking.TotalAmount,
                CreatedDate = booking.CreatedDate,
                ExpiredDate = booking.ExpiredDate,
                AvailableSlots = (int)(availableSlots - totalPeople)
            };
        }

        /// <summary>
        /// Cập nhật booking ở trạng thái nháp (Draft).
        /// </summary>
        /// <param ="request">Thông tin cần update</param>
        /// <param ="userId">ID người dùng</param>
        public async Task<BookingDetailDto> UpdateBookingDraftAsync(UpdateBookingRequest request, int userId)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Tour)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId && b.UserId == userId);

            if (booking == null || booking.BookingStatus != "Draft")
                throw new Exception("Không tìm thấy booking nháp hợp lệ.");

            // Nếu hết hạn → xóa và báo lỗi
            if (booking.ExpiredDate.HasValue && booking.ExpiredDate < TimeHelper.GetVietnamTime())
            {
                _dbContext.Bookings.Remove(booking);
                await _dbContext.SaveChangesAsync();
                throw new Exception("Đặt tour đã hết thời gian chờ, vui lòng tạo lại.");
            }

            // Đếm tổng số người đã đặt thành công
            var bookedCount = await _dbContext.Bookings
                .Where(b => b.TourId == booking.TourId && (b.BookingStatus == BookingStatus.Success
                    || b.BookingStatus == BookingStatus.CancelPending))
                .SumAsync(b => (int?)b.Quantity) ?? 0;

            // Đảm bảo không âm
            var availableSlots = Math.Max(0, (decimal)(booking.Tour.MaxGroupSize - bookedCount));

            // Tính đúng số lượng người (nếu request có gửi lên thì update, ngược lại giữ nguyên)
            booking.NumAdults = request.NumAdults ?? booking.NumAdults;
            booking.NumChildren5To10 = request.NumChildren5To10 ?? booking.NumChildren5To10;
            booking.NumChildrenUnder5 = request.NumChildrenUnder5 ?? booking.NumChildrenUnder5;

            var totalPeople = booking.NumAdults + booking.NumChildren5To10 + booking.NumChildrenUnder5;

            if (availableSlots <= 0)
                throw new Exception("Tour đã hết chỗ.");

            if (totalPeople > availableSlots)
                throw new Exception($"Chỉ còn {availableSlots} chỗ trống cho tour này.");

            booking.Quantity = (int)totalPeople;

            // Tính lại Amount khi số lượng thay đổi
            booking.TotalAmount =
                (decimal)((booking.NumAdults * (booking.PriceAdult ?? 0)) +
                (booking.NumChildren5To10 * (booking.PriceChild5To10 ?? 0)) +
                (booking.NumChildrenUnder5 * (booking.PriceChildUnder5 ?? 0)));

            booking.ModifiedDate = TimeHelper.GetVietnamTime();
            booking.ModifiedBy = userId;

            // Cập nhật thông tin User (nếu có truyền thì mới update, ngược lại giữ nguyên)
            if (!string.IsNullOrWhiteSpace(request.FirstName))
                booking.FirstName = request.FirstName;

            if (!string.IsNullOrWhiteSpace(request.LastName))
                booking.LastName = request.LastName;

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                booking.PhoneNumber = request.PhoneNumber;

            await _dbContext.SaveChangesAsync();

            return new BookingDetailDto
            {
                BookingId = booking.BookingId,
                TourName = booking.TourName,
                OrderCode = booking.OrderCode,
                StartDate = booking.StartTime,
                FirstName = booking.FirstName,
                LastName = booking.LastName,
                UserEmail = booking.UserEmail,
                PhoneNumber = booking.PhoneNumber,
                PriceAdult = booking.PriceAdult,
                PriceChild5To10 = booking.PriceChild5To10,
                PriceChildUnder5 = booking.PriceChildUnder5,
                NumAdults = booking.NumAdults,
                NumChildren5To10 = booking.NumChildren5To10,
                NumChildrenUnder5 = booking.NumChildrenUnder5,
                Amount = booking.TotalAmount,
                PaymentStatus = booking.BookingStatus,
                CreatedDate = booking.CreatedDate,
                ExpiredDate = booking.ExpiredDate,
                AvailableSlots = (int)(availableSlots - totalPeople)
            };
        }

        /// <summary>
        /// Xác nhận booking nháp và tạo URL thanh toán VNPay.
        /// </summary>
        /// <param ="bookingId">ID booking</param>
        /// <param ="userId">ID người dùng</param>
        /// <param ="context">HttpContext hiện tại</param>
        public async Task<string> ConfirmBookingAndPayAsync(int bookingId, int userId, HttpContext context)
        {
            var booking = await _dbContext.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            if (booking == null || booking.BookingStatus != "Draft")
                throw new Exception("Không tìm thấy booking nháp để xác nhận.");

            // Nếu hết hạn → xóa và báo lỗi
            if (booking.ExpiredDate.HasValue && booking.ExpiredDate < TimeHelper.GetVietnamTime())
            {
                _dbContext.Bookings.Remove(booking);
                await _dbContext.SaveChangesAsync();
                throw new Exception("Đặt tour đã hết thời gian chờ, vui lòng tạo lại.");
            }

            // Kiểm tra các trường bắt buộc từ snapshot
            var missingFields = new List<string>();

            if (string.IsNullOrWhiteSpace(booking.FirstName))
                missingFields.Add("Họ");

            if (string.IsNullOrWhiteSpace(booking.LastName))
                missingFields.Add("Tên");

            if (string.IsNullOrWhiteSpace(booking.PhoneNumber))
                missingFields.Add("Số điện thoại");

            if (string.IsNullOrWhiteSpace(booking.UserEmail))
                missingFields.Add("Email");

            if (missingFields.Any())
                throw new Exception($"Vui lòng điền đầy đủ thông tin: {string.Join(", ", missingFields)} trước khi thanh toán.");

            booking.BookingStatus = PaymentStatus.Pending;
            booking.ModifiedDate = TimeHelper.GetVietnamTime();
            booking.ModifiedBy = userId;

            await _dbContext.SaveChangesAsync();

            var paymentModel = new PaymentInformationModel
            {
                UserId = userId,
                Amount = booking.TotalAmount,
                Name = $"Tour: {booking.TourName}",
                OrderDescription = $"Thanh toán tour {booking.TourName} cho {booking.Quantity} người. Tổng: {booking.TotalAmount:N0} VND",
                OrderType = "booking",
                BookingId = booking.BookingId,
                OrderCode = booking.OrderCode,
            };

            await _logService.LogAsync(
                userId: userId,
                action: "Create",
                message: $"Người dùng {userId} đặt tour {booking.TourName} với mã đơn {booking.OrderCode} - Số tiền: {booking.TotalAmount:N0} VND",
                statusCode: 201,
                createdBy: userId
            );

            return CreatePaymentUrl(paymentModel, context);
        }

        /// <summary>
        /// Xử lý callback từ VNPay sau khi thanh toán.
        /// </summary>
        /// <param ="query">Query string trả về từ VNPay</param>
        public async Task HandlePaymentCallbackAsync(IQueryCollection query)
        {
            using var dbTransaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var pay = new VnPayLibrary();
                var response = pay.GetFullResponseData(query, _configuration["Vnpay:HashSecret"]);

                var orderCode = query["vnp_TxnRef"].ToString();
                if (string.IsNullOrEmpty(orderCode))
                    throw new Exception("Thiếu mã đơn hàng (vnp_TxnRef).");

                // 🔹 Lấy PaymentTransaction hiện có để update
                var transaction = await _dbContext.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.OrderCode == orderCode);

                if (transaction == null)
                    throw new Exception($"Không tìm thấy giao dịch với mã {orderCode}");

                // Cập nhật thông tin thanh toán (không insert mới)
                transaction.VnpTransactionNo = query["vnp_TransactionNo"];
                transaction.BankCode = query["vnp_BankCode"];
                transaction.PaymentTime = TimeHelper.GetVietnamTime();
                transaction.ModifiedDate = TimeHelper.GetVietnamTime();
                transaction.ModifiedBy = transaction.UserId;

                var responseCode = query["vnp_ResponseCode"];
                var transactionStatus = query["vnp_TransactionStatus"];

                if (response.Success && responseCode == "00" && transactionStatus == "00")
                {
                    transaction.PaymentStatus = PaymentStatus.Success;
                }
                else if (responseCode == "24")
                {
                    transaction.PaymentStatus = PaymentStatus.Canceled;
                    transaction.RemovedDate = TimeHelper.GetVietnamTime();
                    transaction.RemovedBy = transaction.UserId;
                    transaction.RemovedReason = "Người dùng huỷ thanh toán tại VNPay";
                }
                else
                {
                    transaction.PaymentStatus = PaymentStatus.Failed;
                }

                // 🔹 Nếu là plan và thanh toán thành công thì nâng cấp plan
                if (transaction.PaymentStatus == PaymentStatus.Success && transaction.PlanId.HasValue)
                {
                    await _planService.UpgradePlanAsync((int)transaction.UserId, transaction.PlanId.Value);
                }

                if (transaction.BookingId.HasValue)
                {
                    var booking = await _dbContext.Bookings
                        .FirstOrDefaultAsync(b => b.BookingId == transaction.BookingId.Value);

                    if (booking != null)
                    {
                        booking.ModifiedDate = TimeHelper.GetVietnamTime();
                        booking.ModifiedBy = transaction.UserId;
                        booking.BookingStatus = transaction.PaymentStatus switch
                        {
                            PaymentStatus.Success => PaymentStatus.Success, // hoặc "Confirmed"
                            PaymentStatus.Canceled => PaymentStatus.Canceled,
                            PaymentStatus.Failed => PaymentStatus.Failed,
                            _ => booking.BookingStatus
                        };
                        _dbContext.Entry(booking).State = EntityState.Modified;
                    }
                }

                // Lưu cả hai bảng cùng lúc (update, không insert mới)
                await _dbContext.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Xem trước số tiền hoàn khi hủy booking (chưa submit).
        /// </summary>
        /// <param ="bookingId">ID booking</param>
        public async Task<CancelResultDto> PreviewCancelAsync(int bookingId)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Tour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                throw new Exception("Booking không tồn tại.");

            if (booking.BookingStatus != BookingStatus.Success)
                throw new Exception("Chỉ những booking thành công mới được hủy.");

            var now = TimeHelper.GetVietnamTime();
            var daysBefore = (booking.Tour.StartTime - now)?.TotalDays ?? 0;

            decimal refundPercent;
            if (daysBefore >= 20)
                refundPercent = 0.9m;
            else if (daysBefore >= 15)
                refundPercent = 0.5m;
            else if (daysBefore >= 7)
                refundPercent = 0.3m;
            else
                refundPercent = 0.0m;

            var refundAmount = booking.TotalAmount * refundPercent;

            return new CancelResultDto
            {
                BookingId = bookingId,
                RefundAmount = refundAmount,
                RefundPercent = refundPercent,
                Message = $"Khách được hoàn {refundPercent * 100}% = {refundAmount:#,0} VND"
            };
        }

        /// <summary>
        /// Người dùng yêu cầu hủy booking và hoàn tiền.
        /// </summary>
        /// <param ="bookingId">ID booking</param>
        /// <param ="userId">ID người dùng</param>
        /// <param ="refundMethod">Phương thức hoàn tiền</param>
        /// <param ="cancelReason">Lý do hủy</param>
        public async Task<CancelResultDto> CancelBookingAsync(int bookingId, int userId, string refundMethod, string cancelReason)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Tour)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            if (booking == null)
                throw new Exception("Booking không tồn tại.");

            if (booking.BookingStatus != BookingStatus.Success)
                throw new Exception("Chỉ những booking thành công mới được hủy.");

            var preview = await PreviewCancelAsync(bookingId);

            booking.BookingStatus = BookingStatus.CancelPending;
            booking.CancelType = CancelType.UserCancel;
            booking.CancelReason = cancelReason;
            booking.RefundAmount = preview.RefundAmount;
            booking.RefundMethod = refundMethod;
            booking.RefundStatus = RefundStatus.Pending;

            await _dbContext.SaveChangesAsync();

            var fullName = $"{booking.User.FirstName} {booking.User.LastName}";

            var subject = "Yêu cầu hủy Booking #" + booking.BookingId;
            var body = $@"
            Xin chào {fullName},

            Bạn đã gửi yêu cầu hủy booking thành công.
            Lý do huỷ: {cancelReason}
            Số tiền hoàn lại dự kiến: {preview.RefundAmount:N0} VND ({preview.RefundPercent * 100}%)

            Hình thức nhận tiền: {refundMethod}.
            Trạng thái hoàn tiền: Đang chờ admin duyệt.

            Cảm ơn bạn đã sử dụng dịch vụ!
            ";
            await EmailHelper.SendEmailAsync(booking.User.Email, subject, body);

            return new CancelResultDto
            {
                BookingId = booking.BookingId,
                RefundAmount = preview.RefundAmount,
                RefundPercent = preview.RefundPercent,
                CancelReason = cancelReason,
                Message = "Yêu cầu hủy thành công, vui lòng chờ admin xác nhận hoàn tiền"
            };
        }


        public static class PaymentStatus
        {
            public const string Pending = "Pending";
            public const string Success = "Success";
            public const string Failed = "Failed";
            public const string Canceled = "Cancelled";
            public const string Draft = "Draft";
        }
        public static class BookingStatus
        {
            public const string Success = "Success";
            public const string CancelPending = "CancelPending";
            public const string Cancelled = "Cancelled";
        }

        public static class RefundStatus
        {
            public const string Pending = "Pending";               
            public const string Approved = "Approved";             
            public const string Refunded = "Refunded";             
            public const string Rejected = "Rejected";             
        }

        public static class CancelType
        {
            public const string UserCancel = "UserCancel";
        }
    }
}
