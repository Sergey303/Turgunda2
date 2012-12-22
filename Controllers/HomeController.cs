using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;

namespace Turgunda2.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "Welcome to ASP.NET MVC!";

            return View();
        }

        public ActionResult Portrait(string id)
        {
            Models.PortraitModel pmodel = new Models.PortraitModel(id);
            return View(pmodel);
        }
        public ActionResult Search(string searchstring) 
        {
            var model = new Turgunda2.Models.SearchModel(searchstring, null);
            return View(model);
        }
        public ActionResult PortraitSpecial(string id)
        {
            Models.PortraitSpecialModel pmodel = new Models.PortraitSpecialModel(id);
            return View(pmodel);
        }
        public ActionResult LoadDb()
        {
            return View();
        }
        public ActionResult Reload()
        {
            StaticObjects.Init();
            return RedirectToAction("Index", "Home");
        }
        public PartialViewResult LoadDbAction()
        {
            string message = "База данных загружена";
            System.DateTime tt0 = DateTime.Now;
            try
            {
                //Turgunda2.StaticObjects.LoadFromCassettes();
                //Sema2012.Engine.Reload(true);
                message += ". Время загрузки: " + (DateTime.Now - tt0).Ticks / 10000000L + " сек.";
            }
            catch (Exception ex)
            {
                message = "ОШИБКА при загрузке базы данных: " + ex.Message;
            }
            ViewData["message"] = message;
            return PartialView();
        }
        public ActionResult CheckDatabase()
        {
            //StaticObjects.CheckDatabase();
            return RedirectToAction("Index", "Home");
        }
        public ActionResult About()
        {
            return View();
        }
    }
}
