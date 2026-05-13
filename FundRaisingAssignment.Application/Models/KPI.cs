namespace FundRaisingAssignment.Application.Models
{
    public class KPI
    {
        public string Name { get; set; }
        public decimal Value { get; set; }

        public KPI(string name, decimal value)
        {
            Name = name;
            Value = value;
        }
    }
}
