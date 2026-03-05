namespace HR.Web.Models
{
    public class PositionQuestionOptionVM
    {
        public int? Id { get; set; }
        public int QuestionOptionId { get; set; }
        public string OptionText { get; set; }
        public decimal DefaultPoints { get; set; }
        public decimal? OverridePoints { get; set; }
    }
}













