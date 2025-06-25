using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class VnpayLog
    {
        public int LogId { get; set; }
        public string? OrderCode { get; set; }
        public string? ResponseData { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
    }
}
