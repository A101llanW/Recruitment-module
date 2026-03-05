using System.Collections.Generic;
using HR.Web.Models;

namespace HR.Web.ViewModels
{
    public class PositionQuestionViewModel
    {
        public Position Position { get; set; }
        public IEnumerable<Question> AvailableQuestions { get; set; }
        public IEnumerable<PositionQuestion> AssignedQuestions { get; set; }
        public int PositionId { get; set; }
    }
}
