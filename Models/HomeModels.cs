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
            new string[] {"http://fogid.net/o/name", "имя"},
            new string[] {"http://fogid.net/o/from-date", "нач.дата"},
            new string[] {"http://fogid.net/o/to-date", "кон.дата"},
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
        public static XElement format_p = new XElement("record",
                new XAttribute("type", ONames.t_person),
                new XElement("field", new XAttribute("prop", ONames.p_name)),
                new XElement("field", new XAttribute("prop", ONames.p_fromdate), new XElement("label", "рожд.")),
                new XElement("inverse", new XAttribute("prop", ONames.p_reflected),
                    new XElement("record", new XAttribute("type", ONames.FOG + "reflection"),
                        new XElement("direct", new XAttribute("prop", ONames.p_indoc),
                            new XElement("record",
                                new XElement("field", new XAttribute("prop", ONames.p_fromdate)),
                                new XElement("inverse", new XAttribute("prop", ONames.p_fordocument),
                                    new XElement("record", new XAttribute("type", ONames.FOG + "FileStore"),
                                        new XElement("field", new XAttribute("prop", ONames.FOG + "uri"))
                    )))))),
                new XElement("inverse", new XAttribute("prop", ONames.p_hastitle),
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
            XElement xres = sema2012m.DbEntry.GetItemByIdFormatted(id, format);
            //var query = format;
            XElement table = new XElement("table",
                new XElement("thead",
                    new XElement("tr",
                        format.Elements().Where(el => el.Name == "field")
                        .Select(el => {
                            string prop = el.Attribute("prop").Value;
                            XElement label = el.Element("label");
                            string columnname = label != null ? label.Value : 
                                (Common.OntNames.ContainsKey(prop)?Common.OntNames[prop]:prop);
                            return new XElement("th", columnname); 
                        }))),
                new XElement("tbody",
                    new XElement("tr",
                        new XElement("td", "Cell 1.1"),
                        new XElement("td", "Cell 1.2"),
                        new XElement("td", "Cell 1.3")),
                    new XElement("tr",
                        new XElement("td", "Cell 2.1"),
                        new XElement("td", "Cell 2.2"),
                        new XElement("td", "Cell 2.3")),
                    null));

            this.xresult = table;
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