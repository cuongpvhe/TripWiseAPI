using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class AppSetting
    {
        public int Id { get; set; }
        public string Key { get; set; } = null!;
        public string? Value { get; set; }
    }
}
