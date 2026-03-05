using System;
using System.IO;
using System.Web;

namespace HR.Web.Services
{
    public interface IStorageService
    {
        string SaveResume(HttpPostedFileBase file);
    }

    public class StorageService : IStorageService
    {
        public string SaveResume(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                return null;
            }

            var fileName = string.Format("{0}_{1}", Guid.NewGuid(), Path.GetFileName(file.FileName));
            var root = HttpContext.Current.Server.MapPath("~/App_Data/Resumes");
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, fileName);
            file.SaveAs(path);
            return "/App_Data/Resumes/" + fileName;
        }
    }
}










































