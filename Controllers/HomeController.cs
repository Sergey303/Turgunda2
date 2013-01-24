﻿using System;
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
            StaticObjects.Init();
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
        /// <summary>
        /// EditForm вычисляет (строит, корректирует) модель записи и направляет ее на View построения фрагмента редактирования.
        /// Возможно несколько случаев запуска EditForm: 1) начальное построение объекта класса RecordModel, который представляет 
        /// собой формируемую копию имеющейся записи; 2) коррекция модели и построение формы; 3) фиксация результата в БД.
        /// Носителем состояния объекта являются публичные переменные, передаваемые от построения к построению. Признаком
        /// первого варианта является булевский признак firsttime, который потом "гасится" до конца итераций.  
        /// </summary>
        /// <param name="rmodel"></param>
        /// <returns></returns>
        [HttpPost]
        public PartialViewResult EditForm(Turgunda2.Models.RecordModel rmodel)
        {
            string chk = Request.Params["chk"]; // проверка ОДНОЙ введенной связи
            string ok = Request.Params["ok"]; // фиксация изменений, запоминание в БД
            //Turgunda2.Models.RecordModel rm = new Models.RecordModel(id, eid, etype, iprop, nc);
            if (rmodel.firsttime)
            { // формирование формы редактирования
                rmodel.firsttime = false;
                rmodel.LoadFromDb();
                rmodel.MakeLocalRecord();
                XElement[] arr = rmodel.GetHeaderFlow().ToArray();
                //rmodel.MakeXResult();
            }
            else
            { // Собирание данных из реквеста
                XElement format = rmodel.CalculateFormat();
                XElement[] hrows = rmodel.GetHeaderFlow().ToArray();
                if (chk != null)
                {   // Проверка. Находим первый ряд такой, что: а) это прямое отношение, б) набран текст и есть тип, в) нет ссылки. 
                    // Делаем поисковый запрос через SearchModel. Результаты SearchResult[] помещаем в ViewData под именем searchresults,
                    // а в ViewData["searchindex"] поместим индекс
                    var pair = hrows.Select((hr, ind) => new { hr = hr, ind = ind })
                        .FirstOrDefault(hrind =>
                        {
                            var hr = hrind.hr;
                            if (hr.Name != "d") return false;
                            var ind = hrind.ind;
                            if (string.IsNullOrEmpty(rmodel.GetFValue(ind)) || string.IsNullOrEmpty(rmodel.GetTValue(ind))) return false;
                            if (!string.IsNullOrEmpty(rmodel.GetPValue(ind))) return false;
                            return true;
                        });
                    if (pair != null) 
                    {
                        int ind = pair.ind;
                        // Ничего проверять не буду
                        Turgunda2.Models.SearchModel sm = new Models.SearchModel(rmodel.GetFValue(ind), rmodel.GetTValue(ind));
                        ViewData["searchresults"] = sm.Results;
                        ViewData["searchindex"] = ind;
                    }
                }
                else if (ok != null)
                { // Запоминание
                    // Соберем получившуюся запись
                    XElement record = new XElement(sema2012m.ONames.GetXName(rmodel.etype),
                        new XAttribute(sema2012m.ONames.rdfabout, rmodel.eid),
                        hrows.Select((fd, ind) => new { fd = fd, ind = ind })
                        .Select(fdind => 
                        {
                            XElement fd = fdind.fd;
                            int ind = fdind.ind;
                            XName xprop = sema2012m.ONames.GetXName(fd.Attribute("prop").Value);
                            if (fd.Name == "f")
                            {
                                string value = rmodel.GetFValue(ind);
                                if (!string.IsNullOrEmpty(value)) 
                                    return new XElement(xprop, rmodel.GetFValue(ind)); // Надо определить еще нужен ли язык и какой
                            }
                            else if (fd.Name == "d")
                            {
                                string pvalue = rmodel.GetPValue(ind);
                                if (!string.IsNullOrEmpty(pvalue))
                                    return new XElement(xprop,
                                    new XAttribute(sema2012m.ONames.rdfresource, rmodel.GetPValue(ind)));
                            }
                            return (XElement)null;
                        }));
                     // Пошлем эту запись на изменение
                    StaticObjects.ChangeItem(record, User.Identity.Name);

                    return PartialView("EditFormFinal", rmodel);
                }
                else if (rmodel.command != null && rmodel.command == "SetVariant")
                { // Выбор варианта значения для связывания
                    string[] parts = rmodel.exchange.Split('|');
                    int ind = Int32.Parse(parts[0]);
                    string p_id = parts[1];
                    string p_name = parts[2];
                    rmodel.SetPValue(ind, p_id);
                    rmodel.SetVValue(ind, p_name);
                    rmodel.CalculateFormat();
                }
                else if (rmodel.command != null && rmodel.command == "SetVariantNew")
                { // Связывание с новым значением
                    string[] parts = rmodel.exchange.Split('|');
                    int ind = Int32.Parse(parts[0]);
                    string p_type = parts[1];
                    string p_name = parts[2];
                    string nid = StaticObjects.CreateNewItem(p_name, p_type, User.Identity.Name);
                    rmodel.SetPValue(ind, nid);
                    rmodel.SetVValue(ind, p_name);
                    rmodel.CalculateFormat();
                }
                else
                { // Остальное

                }
            }
            return PartialView("EditForm", rmodel);
        }

        //[HttpPost]
        public PartialViewResult SetVariant(Turgunda2.Models.RecordModel rmodel)
        {
            //Turgunda2.Models.RecordModel rmodel = new Models.RecordModel();
            //return PartialView("EditForm", rmodel);
            return EditForm(rmodel);
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
