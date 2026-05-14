namespace FundRaisingAssignment.Application.Models
{
    public class Issue
    {
        public string Title { get; set; }
        public string Description { get; set; }

        public Issue(string title, string description)
        {
            Title = title;
            Description = description;
        }
    }
}
