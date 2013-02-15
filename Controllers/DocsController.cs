using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;

namespace Turgunda2.Controllers
{
    public class DocsController : Controller
    {
        //
        // GET: /Docs/
        static string fn = null;
        public ActionResult GetDZ(string pth)
        {
            string[] parts = pth.Split(new char[] { '/', '.', '_' });
            if (parts.Length < 3) return new EmptyResult();
            factograph.CassetteInfo ci = null;
            if (!CassetteKernel.CassettesConnection.cassettesInfo.TryGetValue(
                "iiss://" + parts[0].ToLower() + "@iis.nsk.su", out ci)) return new EmptyResult();
            string filename = ci.url + "documents/deepzoom/" + pth.Substring(parts[0].Length + 1);
            fn = filename.ToLower();
            // Это для отладки
            //logfileName = @"D:\home\dev\Turgunda2\logs/log.txt";
            //WriteLine(filename);

            string contenttype = fn.EndsWith(".jpg") ? "image/jpeg" : "text/xml";
            // Либо файл есть, либо его нет, а есть архивная сборка
            if (System.IO.File.Exists(filename))
            {
                return new FilePathResult(fn, contenttype);
            }
            else
            {
                // Теперь это либо вход в sarc, либо вообще отсутствующий файл. 
                // Вход в sarc определяется следующим образом: 
                // parts либо {имя_касс}/{имя_папки}/{имя_архива}.xml либо {имя_касс}/{имя_папки}/{имя_архива}_files/...
                string name_folder = parts[1];
                string name_sarc_test = parts[2];
                // Уберем варианты с неправильной конструкцией адреса и отсутствующим архивным файлом
                if (pth[parts[0].Length] != '/' || pth[parts[0].Length + 1 + parts[1].Length] != '/' // проверили первые разделители
                    || name_folder.Length != 4
                    || name_sarc_test.Length != 4
                    ) return new EmptyResult();
                bool fromxml = parts.Length == 4 && pth[parts[0].Length + 1 + parts[1].Length + 1 + parts[2].Length] == '.'
                    && parts[3].ToLower() == "xml";
                bool fromfiles = parts.Length > 4 && pth[parts[0].Length + 1 + parts[1].Length + 1 + parts[2].Length] == '_'
                    && parts[3].ToLower() == "files";
                if (!fromxml && !fromfiles) return new EmptyResult();
                string name_sarc_test_full = ci.url + "documents/deepzoom/" + name_folder + "/" + name_sarc_test + ".sarc2";
                if (!System.IO.File.Exists(name_sarc_test_full)) return new EmptyResult();
                string relative_filename = pth.Substring(parts[0].Length + 1 + parts[1].Length + 1);
                Stream out_stream = Archive.Sarc.GetFileAsStream(name_sarc_test_full, relative_filename);
                return new FileStreamResult(out_stream, contenttype);
            }
            //return new EmptyResult();
        }

        public ActionResult GetPhoto(string u, string s)
        {
            string filename = this.Request.MapPath("..") + "/question.jpg";
            if (!string.IsNullOrEmpty(u) && CassetteKernel.CassettesConnection.cassettesInfo.ContainsKey(u.Substring(0, u.Length - 15).ToLower()))
                filename = CassetteKernel.CassettesConnection.cassettesInfo[u.Substring(0, u.Length - 15).ToLower()].url +
                    "documents/" + s + "/" + u.Substring(u.Length - 9) + ".jpg";
            return new FilePathResult(filename, "image/jpeg");
        }
        public FilePathResult GetVideo(string u)
        {
            //TODO: Хорошо бы поставить по умолчанию что-нибудь осмысленное
            //string path = @"D:\home\dev\Turgunda2\";
            string filename = "question.jpg";
            string video_extension = "flv"; //"mp4";
            if (string.IsNullOrEmpty(u) || !CassetteKernel.CassettesConnection.cassettesInfo.ContainsKey(u.Substring(0, u.Length - 15).ToLower()))
            { return null; }
            string path = CassetteKernel.CassettesConnection.cassettesInfo[u.Substring(0, u.Length - 15).ToLower()].url +
                "documents/medium/";
            filename = u.Substring(u.Length - 9) + ".";
            if (System.IO.File.Exists(path + filename + "mp4")) video_extension = "mp4";
            else if (System.IO.File.Exists(path + filename + "flv")) video_extension = "flv";
            filename += video_extension;

            return new FilePathResult(path + filename, "video/" + video_extension);
        }

        //public static string logfileName = @"D:\home\dev\Turgunda\Turgunda\ClientBin\Cassettes\DZ\test20120526\documents\deepzoom\log.txt";
        //public static object locker = new object();
        //public static void WriteLine(string text)
        //{
        //    lock (locker)
        //    {
        //        try
        //        {
        //            var saver = new StreamWriter(logfileName, true, System.Text.Encoding.UTF8);
        //            saver.WriteLine(DateTime.Now.ToString("s") + " " + text);
        //            saver.Close();
        //        }
        //        catch (Exception)
        //        {
        //        }
        //    }
        //}
    }
}
