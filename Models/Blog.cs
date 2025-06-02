using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Blog
    {
        public Blog()
        {
            BlogImages = new HashSet<BlogImage>();
        }

        public int BlogId { get; set; }
        public string? BlogName { get; set; }
        public string? BlogContent { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public string? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual ICollection<BlogImage> BlogImages { get; set; }
    }
}
