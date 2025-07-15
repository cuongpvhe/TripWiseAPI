using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Text.RegularExpressions;
using TripWiseAPI.Models;
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
            var orderCode = $"user_{model.UserId}_{model.OrderType}_{model.PlanId}_{tick}";
            var pay = new VnPayLibrary();
            var timeNow = TimeHelper.GetVietnamTime();

            pay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
            pay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"]);
            pay.AddRequestData("vnp_TmnCode", _configuration["Vnpay:TmnCode"]);
            pay.AddRequestData("vnp_Amount", (model.Amount * 100).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"]);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"]);
            pay.AddRequestData("vnp_OrderInfo", model.OrderDescription);
            pay.AddRequestData("vnp_OrderType", model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", _configuration["PaymentCallBack:ReturnUrl"]);
            pay.AddRequestData("vnp_TxnRef", orderCode);
            // 💾 Lưu PaymentTransaction vào DB
            var transaction = new PaymentTransaction
            {
                OrderCode = orderCode,
                UserId = model.UserId,
                Amount = model.Amount,
                PaymentStatus = "Pending",
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = model.UserId,
            };

            _dbContext.PaymentTransactions.Add(transaction);
            _dbContext.SaveChanges();

            model.OrderCode = orderCode;

            return pay.CreateRequestUrl(_configuration["Vnpay:BaseUrl"], _configuration["Vnpay:HashSecret"]);
        }

        public async Task HandlePaymentCallbackAsync(IQueryCollection query)
        {
            var pay = new VnPayLibrary();
            var response = pay.GetFullResponseData(query, _configuration["Vnpay:HashSecret"]);

            var orderCode = query["vnp_TxnRef"].ToString();

            if (string.IsNullOrEmpty(orderCode))
                throw new Exception("Thiếu mã đơn hàng (vnp_TxnRef).");

            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode);

            if (transaction == null)
                throw new Exception($"Không tìm thấy giao dịch với mã {orderCode}");

            transaction.VnpTransactionNo = query["vnp_TransactionNo"];
            transaction.BankCode = query["vnp_BankCode"];
            transaction.PaymentTime = TimeHelper.GetVietnamTime();
            transaction.ModifiedDate = TimeHelper.GetVietnamTime();
            transaction.ModifiedBy = transaction.UserId;

            var responseCode = query["vnp_ResponseCode"];
            var transactionStatus = query["vnp_TransactionStatus"];

            if (response.Success && responseCode == "00" && transactionStatus == "00")
            {
                transaction.PaymentStatus = "Success";
            }
            else if (responseCode == "24")
            {
                transaction.PaymentStatus = "Canceled";
                transaction.RemovedDate = TimeHelper.GetVietnamTime();
                transaction.RemovedBy = transaction.UserId;
                transaction.RemovedReason = "Người dùng huỷ thanh toán tại VNPay";
            }
            else
            {
                transaction.PaymentStatus = "Failed";
            }

            await _dbContext.SaveChangesAsync();

            // Nếu là plan và thanh toán thành công thì nâng cấp plan
            if (transaction.PaymentStatus == "Success" &&
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

            // Nếu là tour
            //else if (status == "Success" && orderCode.Contains("tour", StringComparison.OrdinalIgnoreCase))
            //{
            //    var match = Regex.Match(orderCode, @"user_(\d+)_tour_(\d+)_");

            //    if (match.Success)
            //    {
            //        var userId = int.Parse(match.Groups[1].Value);
            //        var tourId = int.Parse(match.Groups[2].Value);

            //        Console.WriteLine($"✅ Parsed Tour: userId={userId}, tourId={tourId}");
            //        await _tourService.ConfirmBookingAsync(userId, tourId);
            //    }
            //    else
            //    {
            //        Console.WriteLine("❌ Không match được OrderCode tour.");
            //    }
            //}
         
        }

    }
}
