namespace MVCResturang.Models
{
    public class BookingAdminViewModel
    {
        public int Id { get; set; }
        public DateTime BookingTime { get; set; }
        public int NumberOfGuests { get; set; }
        public int CustomerId { get; set; }
        public int TableId { get; set; }

        // Customer info
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        // Table info
        public int TableNumber { get; set; }
        public int TableCapacity { get; set; }
    }
}