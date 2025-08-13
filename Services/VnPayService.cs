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

        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            var tick = TimeHelper.GetVietnamTime().Ticks.ToString();
            var orderCode = model.OrderType switch
            {
                "plan" => $"user_{model.UserId}_plan_{model.PlanId}",
                "booking" => $"user_{model.UserId}_booking_{model.BookingId}",
                _ => throw new Exception("OrderType không hợp lệ")
            };

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
                    CreatedDate = timeNow,
                    CreatedBy = model.UserId,
                };
                _dbContext.PaymentTransactions.Add(transaction);
            }

            _dbContext.SaveChanges();

            model.OrderCode = orderCode;

            return pay.CreateRequestUrl(_configuration["Vnpay:BaseUrl"], _configuration["Vnpay:HashSecret"]);
        }

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
			await _logService.LogAsync(userId: userId, action: "BuyPlan", message: $"Người dùng {userId} mua gói {plan.PlanName} giá {plan.Price:N0} VND", statusCode: 200, createdBy: userId, createdDate:DateTime.Now);
			return CreatePaymentUrl(paymentModel, context);
        }
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
                int? bookingId = null;

                if (!string.IsNullOrEmpty(transaction.OrderCode))
                {
                    if (transaction.OrderCode.Contains("plan"))
                    {
                        var match = Regex.Match(transaction.OrderCode, @"user_(\d+)_plan_(\d+)_");
                        if (match.Success)
                        {
                            int planId = int.Parse(match.Groups[2].Value);
                            planName = await _dbContext.Plans
                                .Where(p => p.PlanId == planId && p.RemovedDate == null)
                                .Select(p => p.PlanName)
                                .FirstOrDefaultAsync();
                        }
                    }
                    else if (transaction.OrderCode.Contains("booking"))
                    {
                        var match = Regex.Match(transaction.OrderCode, @"user_(\d+)_booking_(\d+)");
                        if (match.Success)
                        {
                            bookingId = int.Parse(match.Groups[2].Value);
                            var bookingInfo = await _dbContext.Bookings
                                .Where(b => b.BookingId == bookingId)
                                .Select(b => new { b.BookingId, b.Tour.TourName, b.TourId })
                                .FirstOrDefaultAsync();

                            if (bookingInfo != null)
                            {
                                tourName = bookingInfo.TourName;
                                tourId = bookingInfo.TourId;
                                bookingId = bookingInfo.BookingId;
                            }
                        }
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

        public async Task<BookingDetailDto?> GetBookingDetailAsync(int bookingId)
        {
            var booking = await (from b in _dbContext.Bookings
                                 join u in _dbContext.Users on b.UserId equals u.UserId
                                 join t in _dbContext.Tours on b.TourId equals t.TourId
                                 // PaymentTransaction không nối bằng navigation property
                                 join pt in _dbContext.PaymentTransactions
                                     on b.OrderCode equals pt.OrderCode into ptJoin
                                 from pt in ptJoin.DefaultIfEmpty()
                                 where b.BookingId == bookingId
                                 select new BookingDetailDto
                                 {
                                     BookingId = b.BookingId,
                                     TourName = t.TourName,
                                     OrderCode = b.OrderCode,
                                     StartDate = t.StartTime,
                                     PaymentStatus = pt != null ? pt.PaymentStatus : null,
                                     BankCode = pt != null ? pt.BankCode : null,
                                     VnpTransactionNo = pt != null ? pt.VnpTransactionNo : null,

                                     UserEmail = u.Email,
                                     PhoneNumber = u.PhoneNumber,
                                     PriceAdult = t.PriceAdult,
                                     PriceChild5To10 = t.PriceChild5To10,
                                     PriceChildUnder5 = t.PriceChildUnder5,
                                     Amount = b.TotalAmount,
                                     PaymentTime = pt != null ? pt.PaymentTime : null,
                                     CreatedDate = b.CreatedDate
                                 })
                                 .FirstOrDefaultAsync();

            return booking;
        }
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

            var totalPeople = request.NumAdults + request.NumChildren5To10 + request.NumChildrenUnder5;
            if (totalPeople > tour.MaxGroupSize)
                throw new Exception($"Số lượng người vượt quá giới hạn {tour.MaxGroupSize}.");

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
                TotalAmount = totalAmount,
                BookingStatus = PaymentStatus.Draft,
                CreatedDate = now,
                CreatedBy = userId,
                OrderCode = $"draft_{Guid.NewGuid()}",
                ExpiredDate = now.AddMinutes(5) 
            };

            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            return new BookingDetailDto
            {
                BookingId = booking.BookingId,
                TourName = tour.TourName,
                OrderCode = booking.OrderCode,
                StartDate = tour.StartTime,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserEmail = user.Email,
                PhoneNumber = user.PhoneNumber,
                PriceAdult = tour.PriceAdult,
                PriceChild5To10 = tour.PriceChild5To10,
                PriceChildUnder5 = tour.PriceChildUnder5,
                Amount = booking.TotalAmount,
                CreatedDate = booking.CreatedDate,
                ExpiredDate = booking.ExpiredDate
            };
        }

        public async Task<BookingDetailDto> UpdateBookingDraftAsync(UpdateBookingRequest request, int userId)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Tour)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId && b.UserId == userId);

            if (booking == null || booking.BookingStatus != "Draft")
                throw new Exception("Không tìm thấy booking nháp hợp lệ.");

            // ✅ Nếu hết hạn → xóa và báo lỗi
            if (booking.ExpiredDate.HasValue && booking.ExpiredDate < TimeHelper.GetVietnamTime())
            {
                _dbContext.Bookings.Remove(booking);
                await _dbContext.SaveChangesAsync();
                throw new Exception("Đặt tour đã hết thời gian chờ, vui lòng tạo lại.");
            }

            var totalPeople = request.PriceAdult + request.PriceChild5To10 + request.PriceChildUnder5;
            if (totalPeople > booking.Tour.MaxGroupSize)
                throw new Exception($"Số lượng người vượt quá giới hạn {booking.Tour.MaxGroupSize}.");

            booking.Quantity = (int)totalPeople;
            booking.TotalAmount =
                (decimal)((request.PriceAdult * (booking.Tour.PriceAdult ?? 0)) +
                (request.PriceChild5To10 * (booking.Tour.PriceChild5To10 ?? 0)) +
                (request.PriceChildUnder5 * (booking.Tour.PriceChildUnder5 ?? 0)));
            booking.ModifiedDate = TimeHelper.GetVietnamTime();
            booking.ModifiedBy = userId;
            // Kiểm tra các trường bắt buộc
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(request.FirstName)) missingFields.Add("Họ");
            if (string.IsNullOrWhiteSpace(request.LastName)) missingFields.Add("Tên");
            if (string.IsNullOrWhiteSpace(request.PhoneNumber)) missingFields.Add("Số điện thoại");
            if (string.IsNullOrWhiteSpace(request.UserEmail)) missingFields.Add("Email");
            // Kiểm tra định dạng email nếu không rỗng
            if (!string.IsNullOrWhiteSpace(request.UserEmail))
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(request.UserEmail))
                    throw new Exception("Email không hợp lệ.");
            }
            if (missingFields.Any())
                throw new Exception($"Vui lòng điền đầy đủ thông tin: {string.Join(", ", missingFields)} trước khi thanh toán.");

            // Gán dữ liệu cập nhật, chỉ khi hợp lệ
            booking.User.FirstName = request.FirstName;
            booking.User.LastName = request.LastName;
            booking.User.PhoneNumber = request.PhoneNumber;
            booking.User.Email = request.UserEmail;


            await _dbContext.SaveChangesAsync();

            return new BookingDetailDto
            {
                BookingId = booking.BookingId,
                TourName = booking.Tour.TourName,
                OrderCode = booking.OrderCode,
                StartDate = booking.Tour.StartTime,
                FirstName = booking.User.FirstName,
                LastName = booking.User.LastName,
                UserEmail = booking.User.Email,
                PhoneNumber = booking.User.PhoneNumber,
                PriceAdult = booking.Tour.PriceAdult,
                PriceChild5To10 = booking.Tour.PriceChild5To10,
                PriceChildUnder5 = booking.Tour.PriceChildUnder5,
                Amount = booking.TotalAmount,
                PaymentStatus = booking.BookingStatus,
                CreatedDate = booking.CreatedDate,
                ExpiredDate = booking.ExpiredDate
            };
        }

        public async Task<string> ConfirmBookingAndPayAsync(int bookingId, int userId, HttpContext context)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Tour)
                .Include(b => b.User) 
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);


            if (booking == null || booking.BookingStatus != "Draft")
                throw new Exception("Không tìm thấy booking nháp để xác nhận.");

            // ✅ Nếu hết hạn → xóa và báo lỗi
            if (booking.ExpiredDate.HasValue && booking.ExpiredDate < TimeHelper.GetVietnamTime())
            {
                _dbContext.Bookings.Remove(booking);
                await _dbContext.SaveChangesAsync();
                throw new Exception("Đặt tour đã hết thời gian chờ, vui lòng tạo lại.");
            }
            // Kiểm tra các trường bắt buộc
            var missingFields = new List<string>();

            if (booking.User == null)
            {
                missingFields.Add("Thông tin người dùng chưa tồn tại");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(booking.User.FirstName))
                    missingFields.Add("Họ");

                if (string.IsNullOrWhiteSpace(booking.User.LastName))
                    missingFields.Add("Tên");

                if (string.IsNullOrWhiteSpace(booking.User.PhoneNumber))
                    missingFields.Add("Số điện thoại");

                if (string.IsNullOrWhiteSpace(booking.User.Email))
                    missingFields.Add("Email");
            }

            if (missingFields.Any())
                throw new Exception($"Vui lòng điền đầy đủ thông tin: {string.Join(", ", missingFields)} trước khi thanh toán.");

            booking.BookingStatus = PaymentStatus.Pending;
            booking.OrderCode = $"user_{userId}_booking_{booking.BookingId}";
            booking.ModifiedDate = TimeHelper.GetVietnamTime();
            booking.ModifiedBy = userId;

            await _dbContext.SaveChangesAsync();

            var transaction = new PaymentTransaction
            {
                OrderCode = booking.OrderCode,
                UserId = userId,
                Amount = booking.TotalAmount,
                PaymentStatus = PaymentStatus.Pending,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = userId
            };

            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();

            var paymentModel = new PaymentInformationModel
            {
                UserId = userId,
                Amount = booking.TotalAmount,
                Name = $"Tour: {booking.Tour.TourName}",
                OrderDescription = $"Thanh toán tour {booking.Tour.TourName} cho {booking.Quantity} người. Tổng: {booking.TotalAmount:N0} VND",
                OrderType = "booking",
                BookingId = booking.BookingId,
                OrderCode = booking.OrderCode,
            };
			await _logService.LogAsync(userId: userId, action: "Create", message: $"Người dùng {userId} đặt tour {booking.Tour.TourName} với mã đơn {booking.OrderCode} - Số tiền: {booking.TotalAmount:N0} VND", statusCode: 201, createdBy: userId);
			return CreatePaymentUrl(paymentModel, context);
        }

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
                if (transaction.PaymentStatus == PaymentStatus.Success &&
                    orderCode.Contains("plan", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(orderCode, @"user_(\d+)_plan_(\d+)_");
                    if (match.Success)
                    {
                        var userId = int.Parse(match.Groups[1].Value);
                        var planId = int.Parse(match.Groups[2].Value);
                        await _planService.UpgradePlanAsync(userId, planId);
                    }
                }

                // 🔹 Nếu là booking, update BookingStatus đồng bộ với PaymentStatus
                if (orderCode.Contains("booking", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(orderCode, @"user_(\d+)_booking_(\d+)");
                    if (match.Success)
                    {
                        var userId = int.Parse(match.Groups[1].Value);
                        var bookingId = int.Parse(match.Groups[2].Value);

                        var booking = await _dbContext.Bookings
                            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

                        if (booking != null)
                        {
                            booking.ModifiedDate = TimeHelper.GetVietnamTime();
                            booking.ModifiedBy = userId;
                            booking.BookingStatus = transaction.PaymentStatus switch
                            {
                                PaymentStatus.Success => PaymentStatus.Success,     // hoặc "Confirmed"
                                PaymentStatus.Canceled => PaymentStatus.Canceled,
                                PaymentStatus.Failed => PaymentStatus.Failed,
                                _ => booking.BookingStatus
                            };
                            _dbContext.Entry(booking).State = EntityState.Modified;
                        }
                        

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


        public static class PaymentStatus
        {
            public const string Pending = "Pending";
            public const string Success = "Success";
            public const string Failed = "Failed";
            public const string Canceled = "Canceled";
            public const string Draft = "Draft";
        }

    }
}
