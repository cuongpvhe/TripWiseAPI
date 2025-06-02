using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class TransactionHistory
    {
        public int Id { get; set; }
        public DateTime? Created { get; set; }
        public string? Reason { get; set; }
        public string? Description { get; set; }
        public string? Actor { get; set; }
    }
}
