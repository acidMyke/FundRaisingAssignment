namespace FundRaisingAssignment.Application.Models
{
    public class Trend
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Value { get; set; }

        public Trend(string title, string description, decimal value)
        {
            Title = title;
            Description = description;
            Value = value;
        }
    }

}
