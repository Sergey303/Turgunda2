﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using sema2012m;

namespace Turgunda2.Models
{
    public class Common
    {
        public static XElement formats = new XElement("formats"); // хранилище основных форматов, загружаемых напр. из ApplicationProfile.xml 
        public static string[][] OntPairs = new string[][] {
            new string[] {"http://fogid.net/o/archive", "архив"},
            new string[] {"http://fogid.net/o/person", "персона"},
            new string[] {"http://fogid.net/o/org-sys", "орг. система"},
            new string[] {"http://fogid.net/o/collection", "коллекция"},
            new string[] {"http://fogid.net/o/document", "документ"},
            new string[] {"http://fogid.net/o/photo-doc", "фото документ"},
            new string[] {"http://fogid.net/o/video-doc", "видео документ"},
            new string[] {"http://fogid.net/o/name", "имя"},
            new string[] {"http://fogid.net/o/from-date", "нач.дата"},
            new string[] {"http://fogid.net/o/to-date", "кон.дата"},
            new string[] {"http://fogid.net/o/in-doc", "в документе"},
            new string[] {"http://fogid.net/o/degree", "степень"},
            new string[] {"http://fogid.net/o/", ""},
        };
        public static Dictionary<string, string> OntNames = new Dictionary<string, string>(
            OntPairs.ToDictionary(pa => pa[0], pa => pa[1]));
        public static void LoadOntNamesFromOntology(XElement ontology)
        {
            var ont_names = ontology.Elements()
                .Where(el => el.Name == "Class" || el.Name == "ObjectProperty" || el.Name == "DatatypeProperty")
                .Where(el => el.Elements("label").Any())
                .Select(el => new
                {
                    type_id = el.Attribute(ONames.rdfabout).Value,
                    label = el.Elements("label").First(lab => lab.Attribute(ONames.xmllang).Value == "ru").Value
                })
                .ToDictionary(pa => pa.type_id, pa => pa.label);
            OntNames = ont_names;
        }

        public static string GetNameFromRecord(sema2012m.EntityInfo record)
        {
            return record.RecordElements
                .Where(re => !re.IsObjectProperty && re.Predicate == sema2012m.ONames.p_name)
                .OrderByDescending(re => re.Lang) // Это чтобы получить русский вариант, в будущем, это надо изменить
                .Select(re => re.Value)
                .FirstOrDefault();
        }
    }
    public class PortraitModel
    {
        public string id;
        public string type_id;
        public string typelabel;
        public string name;
        public string uri = null;
        public XElement xresult;
        public XElement xinverse;
        public XElement look;
        public PortraitModel(string id)
        {
            // Сначала, базовые поля
            var record = sema2012m.DbEntry.GetRecordById(id);
            this.id = record.LastId;
            string type_id = record.TypeId;
            this.type_id = type_id;
            this.typelabel = Common.OntNames.Where(pair => pair.Key == type_id).Select(pair => pair.Value).FirstOrDefault();
            if (this.typelabel == null) this.typelabel = type_id;
            this.name = Common.GetNameFromRecord(record);
            // Теперь таблицы
            XElement format = Common.formats.Elements("record")
                .FirstOrDefault(re => re.Attribute("type") != null && re.Attribute("type").Value == this.type_id);
            if (format == null) format = new XElement("record", new XElement("field", new XAttribute("prop", ONames.p_name)));
            
            //if (type_id == ONames.t_person) format = Common.format_p;
            //else if (type_id == ONames.FOG + "collection") format = Common.format_с_list;
            XElement xres = sema2012m.DbEntry.GetItemByIdFormatted(id, format);
            // Надо попробовать получить uri
            if (type_id == ONames.FOG + "photo-doc")
            {
                var uri_el = GetUri(xres);
                if (uri_el != null) uri = uri_el.Value;
            }

            var resultset = new XElement[] { xres };
            XElement table_main = ConstructTable(format, resultset);

            XElement xrecord = new XElement("record", 
                //new XAttribute("id", format.Attribute("id").Value),
                //new XAttribute("type", format.Attribute("type").Value),
                table_main);
            foreach (var finv in format.Elements("inverse"))
            {
                string prop = finv.Attribute("prop").Value;
                XElement label = finv.Element("label");
                if (label == null && Common.OntNames.ContainsKey(prop)) label = new XElement("label", Common.OntNames[prop]); 
                XElement inverse = new XElement("inverse", new XAttribute("prop", prop),
                    label == null ? null : new XElement(label));
                var inverse_p_set = xres.Elements("inverse")
                    .Where(inv => inv.Attribute("prop").Value == finv.Attribute("prop").Value).ToArray();
                foreach (var frec in finv.Elements("record"))
                {
                    XElement rlabel = frec.Element("label");
                    XAttribute t_att = frec.Attribute("type");
                    var record_t_set = inverse_p_set.Elements("record")
                        .Where(re => t_att == null ? true : re.Attribute("type").Value == t_att.Value)
                        .ToArray();
                    XElement tab = ConstructTable(frec, record_t_set);
                    inverse.Add(new XElement("record",
                        //new XAttribute(rec.Attribute("id")),
                        t_att==null ? null : new XAttribute(frec.Attribute("type")),
                        rlabel==null ? null : new XElement("label", rlabel.Value),
                        tab));
                }
                xrecord.Add(inverse);
            }
                
            this.xresult = xrecord;
            //this.look = xrecord;
        }

        private static XElement ConstructTable(XElement format, IEnumerable<XElement> resultset)
        {
            XElement table = new XElement("table",
                new XElement("thead",
                    new XElement("tr",
                        format.Elements().Where(el => el.Name == "field" || el.Name == "direct")
                        .Select(el =>
                        {
                            string prop = el.Attribute("prop").Value;
                            XElement label = el.Element("label");
                            string columnname = label != null ? label.Value :
                                (Common.OntNames.ContainsKey(prop) ? Common.OntNames[prop] : prop);
                            return new XElement("th", columnname);
                        }))),
                new XElement("tbody",
                    resultset.Select(xr => new XElement("tr",
                        format.Elements().Where(el => el.Name == "field" || el.Name == "direct")
                        .Select(el =>
                        {
                            string prop = el.Attribute("prop").Value;
                            if (el.Name == "field")
                            {
                                var xvalue = xr.Elements("field").FirstOrDefault(f => f.Attribute("prop").Value == prop);
                                return new XElement("td",
                                    new XAttribute("style", "font-weight:bold; color:Black;"),
                                    xvalue == null ? "" : xvalue.Value);
                            }
                            else
                            {
                                var dir = xr.Elements("direct").FirstOrDefault(d => d.Attribute("prop").Value == prop);
                                if (dir == null) return new XElement("td");
                                var re = dir.Element("record");
                                if (re == null) return new XElement("td");
                                string re_id = re.Attribute("id").Value;
                                XElement name_el = re.Elements("field").FirstOrDefault(f => f.Attribute("prop").Value == ONames.p_name);
                                XElement ret_element = new XElement("a", new XAttribute("href", "?id=" + re_id),
                                        name_el == null ? "link" : name_el.Value);
                                XAttribute t_att = re.Attribute("type");
                                string t_id = t_att == null ? null : t_att.Value;
                                if (t_id == ONames.t_photodoc)
                                {
                                    var uri = GetUri(re);
                                    if (uri != null)
                                    {
                                        ret_element = new XElement("div", //new XAttribute("class", "brick"),
                                            new XElement("div",
                                                new XElement("a", new XAttribute("href", "?id=" + re_id),
                                                    new XElement("img", new XAttribute("src", "/Docs/GetPhoto?s=small&u="+ uri.Value)))),
                                            new XElement("div",
                                                new XElement("span", name_el == null ? "link" : name_el.Value)),
                                            null);
                                    }
                                }
                                return new XElement("td", ret_element);
                            }
                        }))),
                    null));
            return table;
        }

        private static XElement GetUri(XElement re)
        {
            var uri = re.Elements("inverse")
                .Where(i => i.Attribute("prop").Value == ONames.p_fordocument)
                .Select(i => i.Elements("record")
                    .Where(r => r.Attribute("type").Value == ONames.FOG + "FileStore")
                    .SelectMany(r => r.Elements("field")
                        .Where(f => f.Attribute("prop").Value == ONames.FOG + "uri")))
                .SelectMany(u => u)
                .FirstOrDefault();
            return uri;
        }
    }
    public class SearchResult 
    {
        public string id;
        public string type;
        public string value;
        public string lang;
    }
    public class SearchModel
    {
        private SearchResult[] _results;
        public SearchResult[] Results { get { return _results; } }
        public string message;
        public SearchModel(string searchstring, string type)
        {
            DateTime tt0 = DateTime.Now;
            _results = sema2012m.DbEntry.SearchByName(searchstring)
                .Select(xres =>
                {
                    SearchResult res = new SearchResult() { id = xres.Attribute("id").Value, value = xres.Value };
                    XAttribute t_att = xres.Attribute("type");
                    if (t_att != null) res.type = t_att.Value;
                    return res;
                }).ToArray();
            message = "duration=" + ((DateTime.Now - tt0).Ticks/10000L);
        }
    }

    /// <summary>
    /// Эта модель - для тонкого анализа структуры данных. Выдаются поля, прямые и обратные ствойства в терминах онтологии 
    /// </summary>
    public abstract class Relation { }
    public class Field : Relation { public string prop; public string value; public string lang; }
    public class Direct : Relation { public string prop; public string resource; }
    public class Inverse : Relation { public string prop; public string about; public string name; }
    public class PortraitSpecialModel
    {
        public string id;
        public string type_id;
        public string type;
        public string name;
        public Field[] farr;
        public Direct[] darr;
        public Inverse[] iarr;
        public PortraitSpecialModel(string id)
        {
            var record = sema2012m.DbEntry.GetRecordById(id);
            this.id = record.LastId;
            string type_id = record.TypeId;
            this.type_id = type_id;
            this.type = Common.OntNames.Where(pair => pair.Key == type_id).Select(pair => pair.Value).FirstOrDefault();
            if (this.type == null) this.type = type_id;
            this.name = Common.GetNameFromRecord(record);
            farr = record.RecordElements.Where(el => !el.IsObjectProperty)
                .Select(el => new Field() { prop = el.Predicate, value = el.Value, lang = el.Lang })
                .ToArray();
            darr = record.RecordElements.Where(el => el.IsObjectProperty)
                .Select(el => new Direct() { prop = el.Predicate, resource = el.Value })
                .ToArray();
            iarr = sema2012m.DbEntry.GetInverseRecordsById(record)
                .Select(en => new Inverse()
                {
                    about = en.LastId,
                    prop = en.RecordElements.Where(re => re.IsObjectProperty).First(re => re.Value == this.id).Predicate,
                    name = en.RecordElements.Where(re => !re.IsObjectProperty && re.Predicate == sema2012m.ONames.p_name)
                        .Select(re => re.Value).FirstOrDefault()
                }).ToArray();
        }
    }
}