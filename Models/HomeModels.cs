using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using sema2012m;

namespace Turgunda2.Models
{
    public abstract class Relation { }
    public class Field : Relation { public string prop; public string value; public string lang; }
    public class Direct : Relation { public string prop; public string resource; }
    public class Inverse : Relation { public string prop; public string about; public string name; }
    public class Common
    {
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
        public static string GetNameFromRecord(sema2012m.EntityInfo record)
        {
            return record.RecordElements
                .Where(re => !re.IsObjectProperty && re.Predicate == sema2012m.ONames.p_name)
                .OrderByDescending(re => re.Lang)
                .Select(re => re.Value)
                .FirstOrDefault();
        }
        public static XElement format_uni = new XElement("record",
                new XElement("field", new XAttribute("prop", ONames.p_name)),
                new XElement("field", new XAttribute("prop", ONames.p_fromdate), new XElement("label", "нач.дата")),
                null);
        public static XElement format_с_list = new XElement("record",
                new XAttribute("type", ONames.FOG + "collection"),
                new XElement("field", new XAttribute("prop", ONames.p_name)),
                new XElement("field", new XAttribute("prop", ONames.p_description)),
                new XElement("field", new XAttribute("prop", ONames.p_fromdate)),
                new XElement("field", new XAttribute("prop", ONames.p_todate)),
                new XElement("inverse", new XAttribute("prop", ONames.p_incollection),
                new XElement("label", ""),
                    new XElement("record", new XAttribute("type", ONames.FOG + "collection-member"),
                        new XElement("direct", new XAttribute("prop", ONames.p_collectionitem), new XElement("label", ""),
                            new XElement("record",
                                new XElement("label", ""),
                                new XElement("field", new XAttribute("prop", ONames.p_name)),
                                null)))));
        public static XElement format_p = new XElement("record",
                new XAttribute("type", ONames.t_person),
                new XElement("field", new XAttribute("prop", ONames.p_name)),
                new XElement("field", new XAttribute("prop", ONames.p_fromdate), new XElement("label", "рожд.")),
                new XElement("direct", new XAttribute("prop", ONames.p_father), new XElement("label", "отец"),
                    new XElement("record", new XElement("field", new XAttribute("prop", ONames.p_name))),
                    null),
                new XElement("inverse", new XAttribute("prop", ONames.p_reflected),
                    new XElement("label", "отражения"),
                    new XElement("record", new XAttribute("type", ONames.FOG + "reflection"),
                        new XElement("direct", new XAttribute("prop", ONames.p_indoc),
                            new XElement("record",
                                new XElement("label", ""),
                                new XElement("field", new XAttribute("prop", ONames.p_name)),
                                new XElement("field", new XAttribute("prop", ONames.p_fromdate)),
                                new XElement("inverse", new XAttribute("prop", ONames.p_fordocument),
                                    new XElement("record", new XAttribute("type", ONames.FOG + "FileStore"),
                                        new XElement("field", new XAttribute("prop", ONames.FOG + "uri"))
                    )))))),
                new XElement("inverse", new XAttribute("prop", ONames.p_hastitle),
                    new XElement("label", "титулы/награды"),
                    new XElement("record",
                        new XElement("field", new XAttribute("prop", ONames.p_fromdate)),
                        new XElement("field", new XAttribute("prop", ONames.p_degree))
                        )),
                null
                );

    }
    public class PortraitModel
    {
        public string id;
        public string type_id;
        public string type;
        public string name;
        public XElement xresult;
        public XElement xinverse;
        public XElement look;
        public PortraitModel(string id)
        {
            var record = sema2012m.DbEntry.GetRecordById(id);
            this.id = record.LastId;
            string type_id = record.TypeId;
            this.type_id = type_id;
            this.type = Common.OntNames.Where(pair => pair.Key == type_id).Select(pair => pair.Value).FirstOrDefault();
            if (this.type == null) this.type = type_id;
            this.name = Common.GetNameFromRecord(record);
            XElement format = new XElement("record", new XElement("field", new XAttribute("prop", ONames.p_name)));
            if (type_id == ONames.t_person) format = Common.format_p;
            else if (type_id == ONames.FOG + "collection") format = Common.format_с_list;
            XElement xres = sema2012m.DbEntry.GetItemByIdFormatted(id, format);
            var resultset = new XElement[] { xres };
            XElement table_main = ConstructTable(format, resultset);

            XElement xrecord = new XElement("record", 
                //new XAttribute("id", format.Attribute("id").Value),
                //new XAttribute("type", format.Attribute("type").Value),
                table_main);
            foreach (var finv in format.Elements("inverse"))
            {
                var label = finv.Element("label");
                XElement inverse = new XElement("inverse", new XAttribute("prop", finv.Attribute("prop").Value),
                    label == null ? null : new XElement(label));
                var inverse_p_set = xres.Elements("inverse")
                    .Where(inv => inv.Attribute("prop").Value == finv.Attribute("prop").Value).ToArray();
                foreach (var frec in finv.Elements("record"))
                {
                    XAttribute t_att = frec.Attribute("type");
                    var record_t_set = inverse_p_set.Elements("record")
                        .Where(re => t_att == null ? true : re.Attribute("type").Value == t_att.Value)
                        .ToArray();
                    XElement tab = ConstructTable(frec, record_t_set);
                    inverse.Add(new XElement("record",
                        //new XAttribute(rec.Attribute("id")),
                        t_att==null ? null : new XAttribute(frec.Attribute("type")),
                        tab));
                }
                xrecord.Add(inverse);
            }
                
            //foreach (var rec in 
            //var query = format.Elements("inverse")
            //look = xrecord;
            //look = xres;

            this.xresult = xrecord;
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
                                return new XElement("td",
                                    new XElement("a", new XAttribute("href", "?id="+re_id), 
                                        name_el==null ? "link" : name_el.Value));
                            }
                        }))),
                    null));
            return table;
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