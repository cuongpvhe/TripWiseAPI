using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class AppSetting
    {
        public int Id { get; set; }
        public string Key { get; set; } = null!;
        public string? Value { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? RemovedDate { get; set; }
        public string? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
    }
}
