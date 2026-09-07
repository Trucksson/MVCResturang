namespace MVCResturang.Models
{
    public class Booking
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public DateTime Date { get; set; }

        public TimeSpan Time { get; set; }

        public int NumberOfGuests { get; set; }


    }
}
