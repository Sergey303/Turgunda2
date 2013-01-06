using System;
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
        private static XElement _ontology;
        public static void LoadOntNamesFromOntology(XElement ontology)
        {
            _ontology = ontology;
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
        public static string GetEnumStateLabel(string enum_type, string state_value)
        {
            var et_def = _ontology.Elements("EnumerationType")
                .FirstOrDefault(et => et.Attribute(ONames.rdfabout).Value == enum_type);
            if (et_def == null) return "";
            var et_state = et_def.Elements("state")
                .FirstOrDefault(st => st.Attribute("value").Value == state_value && st.Attribute(ONames.xmllang).Value == "ru");
            if (et_state == null) return "";
            return et_state.Value;
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
        public XElement look;
        public string message;
        public PortraitModel(string id)
        {
            DateTime tt0 = DateTime.Now;
            // Сначала, базовые поля. Определим, сделав запрос с очень простым форматом
            XElement f_simple = new XElement("record", new XElement("field", new XAttribute("prop", ONames.p_name)));
            XElement xtree0 = StaticObjects.GetItemById(id, f_simple);
            if (xtree0 == null) return; // записи может и не быть
            // заполним базовые поля
            this.id = xtree0.Attribute("id").Value;
            string type_id = xtree0.Attribute("type").Value;
            this.type_id = type_id;
            this.typelabel = Common.OntNames.Where(pair => pair.Key == type_id).Select(pair => pair.Value).FirstOrDefault();
            if (this.typelabel == null) this.typelabel = type_id;
            XElement field_name = xtree0.Elements("field")
                .Where(fi => fi.Attribute("prop").Value == ONames.p_name)
                .OrderBy(fi => { 
                    XAttribute lang_att = fi.Attribute(ONames.xmllang);
                    return lang_att==null?"" : lang_att.Value;
                })
                .FirstOrDefault();
            this.name = field_name == null ? "Noname" : field_name.Value;
            
            // Теперь установим нужный формат
            XElement xformat = Common.formats.Elements("record")
                .FirstOrDefault(re => re.Attribute("type") != null && re.Attribute("type").Value == this.type_id);
            if (xformat == null) xformat = f_simple;
            
            XElement xtree = StaticObjects.GetItemById(id, xformat);
            // Надо попробовать получить uri
            if (type_id == ONames.FOG + "photo-doc")
            {
                var uri_el = GetUri(xtree);
                if (uri_el != null) uri = uri_el.Value;
            }

            this.xresult = ConvertToResultStructure(xformat, xtree);


            this.message = "duration=" + ((DateTime.Now - tt0).Ticks / 10000L);
            //this.look = xrecord;
        }

        private static XElement ConvertToResultStructure(XElement xformat, XElement xtree)
        {
            XElement result = new XElement("Result",
                new XElement("header", ScanForFields(xformat)),
                GetRecordRow(xtree, xformat),
                null);
            foreach (var f_inv in xformat.Elements("inverse"))
            {
                string prop = f_inv.Attribute("prop").Value;
                var queryForInverse = xtree.Elements("inverse").Where(el => el.Attribute("prop").Value == prop).ToArray();
                XElement ip = new XElement("ip", new XAttribute("prop", prop));
                foreach (var f_rec in f_inv.Elements("record"))
                {
                    string recType = f_rec.Attribute("type").Value;
                    var queryForRecords = queryForInverse.Select(inve => inve.Element("record"))
                        .Where(re => re != null && re.Attribute("type").Value == recType).ToArray();
                    XElement ir = new XElement("ir", new XAttribute("type", recType));
                    ip.Add(ir);
                    foreach (var v_rec in queryForRecords)
                    {
                        ir.Add(GetRecordRow(v_rec, f_rec));
                    }
                }
                result.Add(ip);
            }
            return result;
        }
        // Статический метод: рекурсивное сканирование айтема с вычислением всех определенных форматом записи полей
        // Возвращает таблицу <r id="ид.записи" type="ид.типа"><c>значение</c><c>...</c>...<r>..</r>...</r>
        // где c стоят на позициях полей, а r - на позициях прямых отношений
        public static XElement GetRecordRow(XElement item, XElement frecord)
        {
            IEnumerable<XElement> fieldsAndDirects = item.Elements().Where(el => el.Name == "field" || el.Name == "direct");
            XElement r = new XElement("r",
                new XAttribute("id", item.Attribute("id").Value),
                new XAttribute("type", item.Attribute("type").Value),
                ScanForFieldValues(fieldsAndDirects, frecord),
                null);
            return r;
        }
        public static IEnumerable<XElement> ScanForFieldValues(IEnumerable<XElement> fieldsAndDirects, XElement frecord)
        {
            foreach (var f_el in frecord.Elements())
            {
                if (f_el.Name == "field")
                {
                    string prop = f_el.Attribute("prop").Value;
                    // Найдем множество полей, имеющееся в "россыпи"
                    var field = fieldsAndDirects.Where(fd => fd.Name == "field")
                        .Where(f => f.Attribute("prop").Value == prop)
                        .FirstOrDefault();
                    yield return new XElement("c", field == null ? null : field.Value);
                }
                else if (f_el.Name == "direct")
                {
                    string prop = f_el.Attribute("prop").Value;
                    // Найдем один, если есть, элемент direct, удовлетворяющий условиям 
                    var direct = fieldsAndDirects.Where(fd => fd.Name == "direct")
                        .FirstOrDefault(d => d.Attribute("prop").Value == prop);
                    var record = direct == null ? null : direct.Element("record");
                    if (direct == null || record == null) yield return new XElement("r");
                    else
                    {
                        //var query = record.Elements().Where(fdi => fdi.Name == "field" || fdi.Name == "direct");
                        //foreach (var el1 in ScanForFieldValues(query, f_el.Element("record"))) yield return el1;
                        //yield return new XElement("r",
                        //    new XAttribute("id", record.Attribute("id").Value),
                        //    new XAttribute("type", record.Attribute("type").Value),
                        //    ScanForFieldValues(query, f_el.Element("record")));
                        XElement f_rec = f_el.Element("record");
                        if (frecord != null) yield return GetRecordRow(record, f_rec); // не знаю зачем проверка
                    }
                }
            }
        }
        // Статический метод: рекурсивное сканирование с выявлением всех определенных форматом записи полей
        public static IEnumerable<XElement> ScanForFields(XElement frecord)
        {
            foreach (var el in frecord.Elements())
            {
                if (el.Name == "field") yield return GetLabel(el);
                else if (el.Name == "direct")
                {
                    yield return new XElement("d", new XAttribute("prop", el.Attribute("prop").Value),
                        new XElement("label", "LABEL"),
                        ScanForFields(el.Element("record")));
                }
            }
        }
        private static XElement GetLabel(XElement el)
        {
            XAttribute t = el.Attribute("type");
            XAttribute p = el.Attribute("prop");
            string label = "label";
            if (!Common.OntNames.TryGetValue(p.Value, out label))
            {
                label = p.Value;
            }
            return new XElement("h",
                p == null ? new XAttribute("prop", "nop") : new XAttribute("prop", p.Value),
                t == null ? null : new XAttribute("valueType", t.Value),
                label,
                null);
        }

        private static XElement ConstructTable(XElement format, IEnumerable<XElement> resultset)
        {

            XElement table = new XElement("table",
                new XElement("thead",
                    new XElement("tr",
                        format.Elements().Where(el => 
                            (el.Name == "field" || el.Name == "direct") && 
                            (el.Attribute("visible")==null || el.Attribute("visible").Value != "none"))
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
                        format.Elements().Where(el =>
                            (el.Name == "field" || el.Name == "direct") &&
                            (el.Attribute("visible") == null || el.Attribute("visible").Value != "none"))
                        .Select(el =>
                        {
                            string prop = el.Attribute("prop").Value;
                            if (el.Name == "field")
                            {
                                var enum_type_att = el.Attribute("type");
                                var xvalue = xr.Elements("field").FirstOrDefault(f => f.Attribute("prop").Value == prop);
                                return new XElement("td",
                                    new XAttribute("class", "d"),
                                    xvalue == null ? "" :
                                        (enum_type_att == null ? xvalue.Value : Common.GetEnumStateLabel(enum_type_att.Value, xvalue.Value)));
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
                                                    new XElement("img", new XAttribute("src", "../Docs/GetPhoto?s=small&u="+ uri.Value)))),
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

        private static XElement GetUri(XElement xtree)
        {
            var uri = xtree.Elements("inverse")
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
        public string type_name;
    }
    public class SearchModel
    {
        private SearchResult[] _results;
        public SearchResult[] Results { get { return _results; } }
        public string message;
        public SearchModel(string searchstring, string type)
        {
            DateTime tt0 = DateTime.Now;
            //var sr = StaticObjects.SearchByName(searchstring).ToArray();
            _results = StaticObjects.SearchByName(searchstring)
                .Select(xres =>
                {
                    XElement name_el = xres
                        .Elements("field").Where(f => f.Attribute("prop").Value == ONames.p_name)
                        .OrderByDescending(n => n.Attribute(ONames.xmllang) == null ? "ru" : n.Attribute(ONames.xmllang).Value)
                        .FirstOrDefault();
                    string name = name_el == null ? "noname" : name_el.Value;
                    SearchResult res = new SearchResult() { id = xres.Attribute("id").Value, value = name };
                    XAttribute t_att = xres.Attribute("type");
                    if (t_att != null) res.type = t_att.Value;
                    string tname = "";
                    Common.OntNames.TryGetValue(res.type, out tname);
                    res.type_name = tname;
                    return res;
                })
                .OrderBy(s => s.value)
                .ToArray();
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
        { // Этот метод надо будет доделать
            //var record = sema2012m.DbEntry.GetRecordById(id);
            //this.id = record.LastId;
            //string type_id = record.TypeId;
            //this.type_id = type_id;
            //this.type = Common.OntNames.Where(pair => pair.Key == type_id).Select(pair => pair.Value).FirstOrDefault();
            //if (this.type == null) this.type = type_id;
            //this.name = Common.GetNameFromRecord(record);
            //farr = record.RecordElements.Where(el => !el.IsObjectProperty)
            //    .Select(el => new Field() { prop = el.Predicate, value = el.Value, lang = el.Lang })
            //    .ToArray();
            //darr = record.RecordElements.Where(el => el.IsObjectProperty)
            //    .Select(el => new Direct() { prop = el.Predicate, resource = el.Value })
            //    .ToArray();
            //iarr = sema2012m.DbEntry.GetInverseRecordsById(record)
            //    .Select(en => new Inverse()
            //    {
            //        about = en.LastId,
            //        prop = en.RecordElements.Where(re => re.IsObjectProperty).First(re => re.Value == this.id).Predicate,
            //        name = en.RecordElements.Where(re => !re.IsObjectProperty && re.Predicate == sema2012m.ONames.p_name)
            //            .Select(re => re.Value).FirstOrDefault()
            //    }).ToArray();
        }
    }
}