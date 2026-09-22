using System;

namespace HR.Web.Models
{
    public class PositionView
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public virtual User User { get; set; }

        public int PositionId { get; set; }

        public virtual Position Position { get; set; }

        public DateTime ViewedAtUtc { get; set; }

        public int LoginCountAtView { get; set; }

        public bool IsOpenAtView { get; set; }
    }
}
