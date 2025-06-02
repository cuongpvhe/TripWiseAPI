using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class SignupOtp
    {
        public string SignupRequestId { get; set; } = null!;
        public string Otpstring { get; set; } = null!;
        public int RequestAttemptsRemains { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
