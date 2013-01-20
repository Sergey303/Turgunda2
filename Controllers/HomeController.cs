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
        public ActionResult Search(string searchstring, string type) 
        {
            string t = string.IsNullOrEmpty(type) ? null : type;
            var model = new Turgunda2.Models.SearchModel(searchstring, t);
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
                Turgunda2.StaticObjects.LoadFromCassettesExpress();
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
        //
        // ==================== Редактирование данных =====================
        //
        public ActionResult NewRecord(string searchstring, string type)
        {
            if (type == null) type = "http://fogid.net/o/person";
            string nid = StaticObjects.CreateNewItem(searchstring, type, User.Identity.Name);
            return RedirectToAction("Portrait", "Home", new { id = nid });
        }
        public ActionResult AddInvRelation(string eid, string prop, string rtype)
        {
            StaticObjects.AddInvRelation(eid, prop, rtype, User.Identity.Name);
            //string nid = StaticObjects.CreateNewItem(searchstring, type, User.Identity.Name);
            return RedirectToAction("Portrait", "Home", new { id = eid });
        }
        //[HttpPost]
        //public PartialViewResult EditForm(string id, string eid, string etype, string iprop, int nc)
        //{
        //    string chk = Request.Params["chk"]; // проверка ОДНОЙ введенной связи
        //    string ok = Request.Params["ok"]; // фиксация изменений, запоминание в БД
        //    Turgunda2.Models.RecordModel rm = new Models.RecordModel(id, eid, etype, iprop, nc);
        //    if (chk == null && ok == null)
        //    { // формирование формы редактирования
        //        rm.LoadFromDb();
        //        rm.MakeXResult();
        //    }
        //    else
        //    { // Собирание данных из реквеста
        //        XElement format = rm.format;
        //        XElement xtree = new XElement("record", new XAttribute("id", eid), new XAttribute("type", etype));
        //        int i = 0;
        //        foreach (XElement fd in format.Elements().Where(el => el.Name == "field" || el.Name == "direct"))
        //        {
        //            string prop = fd.Attribute("prop").Value;
        //            string par = Request.Params["f_" + i];
        //            if (string.IsNullOrEmpty(par)) { }
        //            else if (fd.Name == "field")
        //            {
        //                xtree.Add(new XElement("field", new XAttribute("prop", prop), par));
        //            }
        //            else
        //            {
        //                string did = "id999"; //Request.Params["d_" + i];
        //                string tid = Request.Params["t_" + i];
        //                if (!string.IsNullOrEmpty(did) && !string.IsNullOrEmpty(tid))
        //                {
        //                    xtree.Add(new XElement("direct", new XAttribute("prop", prop),
        //                        new XElement("record", new XAttribute("id", did), new XAttribute("type", tid),
        //                            new XElement("field", new XAttribute("prop", sema2012m.ONames.p_name), par))));
        //                }
        //            }
        //            i++;
        //        }
        //        rm.xtree = xtree;
        //        rm.MakeXResult();
        //    }
        //    return PartialView(rm);
        //}
        [HttpPost]
        public PartialViewResult EditForm(Turgunda2.Models.RecordModel rm)
        {
            string chk = Request.Params["chk"]; // проверка ОДНОЙ введенной связи
            string ok = Request.Params["ok"]; // фиксация изменений, запоминание в БД
            //Turgunda2.Models.RecordModel rm = new Models.RecordModel(id, eid, etype, iprop, nc);
            if (chk == null && ok == null)
            { // формирование формы редактирования
                rm.LoadFromDb();
                rm.MakeXResult();
            }
            else
            { // Собирание данных из реквеста
                XElement format = rm.CalculateFormat();
                XElement xtree = new XElement("record", new XAttribute("id", rm.eid), new XAttribute("type", rm.etype));
                int i = 0;
                foreach (XElement fd in format.Elements().Where(el => el.Name == "field" || el.Name == "direct"))
                {
                    string prop = fd.Attribute("prop").Value;
                    string par = Request.Params["f_" + i];
                    if (string.IsNullOrEmpty(par)) { }
                    else if (fd.Name == "field")
                    {
                        xtree.Add(new XElement("field", new XAttribute("prop", prop), par));
                    }
                    else
                    {
                        string did = "id999"; //Request.Params["d_" + i];
                        string tid = Request.Params["t_" + i];
                        if (!string.IsNullOrEmpty(did) && !string.IsNullOrEmpty(tid))
                        {
                            xtree.Add(new XElement("direct", new XAttribute("prop", prop),
                                new XElement("record", new XAttribute("id", did), new XAttribute("type", tid),
                                    new XElement("field", new XAttribute("prop", sema2012m.ONames.p_name), par))));
                        }
                    }
                    i++;
                }
                rm.SetXTree(xtree);
                rm.MakeXResult();
            }
            return PartialView("EditForm", rm);
        }

        //[HttpPost]
        public PartialViewResult SetVariant(Turgunda2.Models.RecordModel rmodel)
        {
            //Turgunda2.Models.RecordModel rmodel = new Models.RecordModel();
            return PartialView("EditForm", rmodel);
        }

        /// ==================  Вспомогательные и отладочные входы =================
        public ActionResult Convert()
        {
            Turgunda2.Models.ConvertModel cm = new Models.ConvertModel();
            return View(cm);
        }
        [HttpPost]
        public ActionResult Convert(Turgunda2.Models.ConvertModel cm)
        {
            if (!string.IsNullOrEmpty(cm.entityid))
            {
                int index;
                if (Int32.TryParse(cm.entityid, out index))
                {
                    string value = StaticObjects.SpecialGetEntityNameByIndex(index);
                    cm.entityvalue = value;
                }
            }
            return View(cm);
        }
        public ActionResult EntityPlaces()
        {
            Turgunda2.Models.EntityPlacesModel epm = new Models.EntityPlacesModel();
            return View(epm);
        }
        [HttpPost]
        public ActionResult EntityPlaces(Turgunda2.Models.EntityPlacesModel epm)
        {
            if (epm.entityvalue != null)
            {
                epm.places = StaticObjects.SpecialEntityPlaces(epm.entityvalue).ToArray();
            }
            return View(epm);
        }
    }
}
