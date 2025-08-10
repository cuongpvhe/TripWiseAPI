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

        public VnPayService(IConfiguration config, TripWiseDBContext dbContext, IPlanService planService)
        {
            _configuration = config;
            _dbContext = dbContext;
            _planService = planService;
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

        public async Task<string> CreateBookingAndPayAsync(BuyTourRequest request, int userId, HttpContext context)
        {
            // Lấy thông tin user
            var user = await _dbContext.Users
                .Where(u => u.UserId == userId)
                .Select(u => new { u.FirstName, u.LastName, u.Email, u.PhoneNumber })
                .FirstOrDefaultAsync();

            if (user == null)
                throw new Exception("Người dùng không tồn tại.");

            // Kiểm tra các trường bắt buộc và lưu lại tên trường thiếu
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(user.FirstName))
                missingFields.Add("FirstName");
            if (string.IsNullOrWhiteSpace(user.LastName))
                missingFields.Add("LastName");
            if (string.IsNullOrWhiteSpace(user.Email))
                missingFields.Add("Email");
            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
                missingFields.Add("PhoneNumber");

            if (missingFields.Any())
            {
                var fields = string.Join(", ", missingFields);
                throw new Exception($"Vui lòng cập nhật đầy đủ thông tin cá nhân: {fields}.");
            }

            var tour = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.TourId == request.TourId && t.RemovedDate == null);

            if (tour == null)
                throw new Exception("Tour không tồn tại.");

            if (tour.PriceAdult == null || tour.PriceAdult <= 0)
                throw new Exception("Tour chưa có giá người lớn hợp lệ.");

            var totalPeople = request.NumAdults + request.NumChildren5To10 + request.NumChildrenUnder5;

            if (tour.MaxGroupSize.HasValue && totalPeople > tour.MaxGroupSize.Value)
                throw new Exception($"Tổng số người ({totalPeople}) vượt quá số lượng tối đa cho phép ({tour.MaxGroupSize}).");

            var now = TimeHelper.GetVietnamTime();

            // ✅ Tính tổng tiền
            var totalAmount = (decimal)(
                request.NumAdults * tour.PriceAdult.Value +
                request.NumChildren5To10 * (tour.PriceChild5To10 ?? 0) +
                request.NumChildrenUnder5 * (tour.PriceChildUnder5 ?? 0)
            );

            // Bước 1: Tạo booking
            var booking = new Booking
            {
                UserId = userId,
                TourId = request.TourId,
                Quantity = totalPeople,
                TotalAmount = totalAmount,
                BookingStatus = PaymentStatus.Pending,
                CreatedDate = now,
                CreatedBy = userId,
                OrderCode = "temp" // Tạm thời, sẽ cập nhật lại
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            // Bước 2: Gán OrderCode
            booking.OrderCode = $"user_{userId}_booking_{booking.BookingId}";
            booking.ModifiedDate = now;
            booking.ModifiedBy = userId;
            await _dbContext.SaveChangesAsync();

            // Bước 3: Tạo transaction
            var transaction = new PaymentTransaction
            {
                OrderCode = booking.OrderCode,
                UserId = userId,
                Amount = totalAmount,
                PaymentStatus = PaymentStatus.Pending,
                CreatedDate = now,
                CreatedBy = userId
            };
            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();

            // Bước 4: Tạo link thanh toán
            var paymentModel = new PaymentInformationModel
            {
                UserId = userId,
                Amount = totalAmount,
                Name = $"Tour: {tour.TourName}",
                OrderDescription = $"Thanh toán tour \"{tour.TourName}\" cho {request.NumAdults} người lớn, {request.NumChildren5To10} trẻ 5-10 tuổi, {request.NumChildrenUnder5} dưới 5 tuổi. Tổng tiền {totalAmount:N0} VND",
                OrderType = "booking",
                BookingId = booking.BookingId,
                OrderCode = booking.OrderCode,
            };

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
        }

    }
}
