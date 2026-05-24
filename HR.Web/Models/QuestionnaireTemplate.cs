using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HR.Web.Models
{
    public class QuestionnaireTemplate : ITenantEntity
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public int StageCount { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime? UpdatedOn { get; set; }

        public QuestionnaireTemplate()
        {
            StageCount = 1;
            IsActive = true;
            CreatedOn = DateTime.UtcNow;
        }

        public virtual ICollection<QuestionnaireTemplateQuestion> TemplateQuestions { get; set; }
    }
}
