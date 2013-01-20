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
        // По идентификатору мы получаем 1) откорректированный идентификатор; 2) тип записи; 3) формат (раскрытия) записи
        // Эту информацию дополняем меткой типа, пытаемся прочитать и зафиксировать имя записи и uri документного контента
        public PortraitModel(string id)
        {
            DateTime tt0 = DateTime.Now;
            XElement rec_format;
            this.type_id = GetFormat(id, out rec_format);
            this.typelabel = Common.OntNames.Where(pair => pair.Key == type_id).Select(pair => pair.Value).FirstOrDefault();
            if (this.typelabel == null) this.typelabel = type_id;
            // Получим портретное х-дерево
            XElement xtree = StaticObjects.GetItemById(id, rec_format);
            // По дереву вычислим и зафиксируем остальные поля
            // поле идентификатора
            this.id = xtree.Attribute("id").Value;
            // поле имени
            XElement field_name = xtree.Elements("field")
                .Where(fi => fi.Attribute("prop").Value == ONames.p_name)
                .OrderBy(fi => { 
                    XAttribute lang_att = fi.Attribute(ONames.xmllang);
                    return lang_att==null?"" : lang_att.Value;
                })
                .FirstOrDefault();
            this.name = field_name == null ? "Noname" : field_name.Value;
            // поле uri
            if (type_id == ONames.FOG + "photo-doc")
            {
                var uri_el = GetUri(xtree);
                if (uri_el != null) uri = uri_el.Value;
            }

            this.xresult = ConvertToResultStructure(rec_format, xtree);

            this.message = "duration=" + ((DateTime.Now - tt0).Ticks / 10000L);
            //this.look = xrecord;
        }
        public static string GetFormat(string id, out XElement rec_format)
        {
            string type_id; 
            // Нам нужен формат. Сначала поищем его в кеше
            if (!StaticObjects.formatByIdCache.TryGetValue(id, out rec_format))
            {  // если его там нет, то нам хотя бы нужет тип. Его мы определяем сделав начальный запрос с примитивным форматом
                XElement f_primitive = new XElement("record");
                XElement xtree0 = StaticObjects.GetItemById(id, f_primitive);
                if (xtree0 == null) { }; // Как-то надо поступить с диагностикой ошибок//
                type_id = xtree0.Attribute("type").Value;
                // Теперь установим нужный формат
                XElement xformat = Common.formats.Elements("record")
                    .FirstOrDefault(re => re.Attribute("type") != null && re.Attribute("type").Value == type_id);
                if (xformat != null)
                {
                    StaticObjects.formatByIdCache.Add(id, xformat);
                }
                else
                {
                    xformat = new XElement("record",
                        new XAttribute("type", type_id),
                        new XElement("field", new XAttribute("prop", ONames.p_name)));
                }
                rec_format = xformat;
            }
            else type_id = rec_format.Attribute("type").Value;
            return type_id;
        }
        public static XElement ConvertToResultStructure(XElement xformat, XElement xtree)
        {
            XElement result = new XElement("Result",
                new XElement("header", ScanForFields(xformat)),
                GetRecordRow(xtree, xformat),
                null);
            foreach (var f_inv in xformat.Elements("inverse"))
            {
                string prop = f_inv.Attribute("prop").Value;
                var queryForInverse = xtree.Elements("inverse").Where(el => el.Attribute("prop").Value == prop).ToArray();
                XElement ip = new XElement("ip", new XAttribute("prop", prop), new XElement("label", Common.OntNames[prop]));
                foreach (var f_rec in f_inv.Elements("record"))
                {
                    XAttribute view_att = f_rec.Attribute("view");
                    string recType = f_rec.Attribute("type").Value;
                    var queryForRecords = queryForInverse.Select(inve => inve.Element("record"))
                        .Where(re => re != null && re.Attribute("type").Value == recType).ToArray();
                    XElement ir = new XElement("ir", 
                        new XAttribute("type", recType),
                        view_att==null?null: new XAttribute(view_att),
                        new XElement("label", Common.OntNames[recType]),
                        new XElement("header", ScanForFields(f_rec)));
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
        private static XElement GetRecordRow(XElement item, XElement frecord)
        {
            IEnumerable<XElement> fieldsAndDirects = item.Elements().Where(el => el.Name == "field" || el.Name == "direct");
            var inv_props_atts = frecord.Elements("inverse").Where(fel => fel.Attribute("attname") != null)
                //.Select(fel => new { prop = fel.Attribute("prop").Value, attname = fel.Attribute("attname").Value })
                .ToDictionary(fel => fel.Attribute("prop").Value, fel => fel.Attribute("attname").Value);
            IEnumerable<XAttribute> atts = item.Elements("inverse")
                .Where(el => inv_props_atts.ContainsKey(el.Attribute("prop").Value))
                .Select(el => new XAttribute(XName.Get(inv_props_atts[el.Attribute("prop").Value]), el.Value));
            XElement r = new XElement("r",
                new XAttribute("id", item.Attribute("id").Value),
                new XAttribute("type", item.Attribute("type").Value),
                atts,
                ScanForFieldValues(fieldsAndDirects, frecord),
                null);
            return r;
        }
        private static IEnumerable<XElement> ScanForFieldValues(IEnumerable<XElement> fieldsAndDirects, XElement frecord)
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
                    //XAttribute pr_att = prop == ONames.p_name ? new XAttribute("prop", prop) : null;
                    if (field == null) yield return new XElement("c");
                    else
                    {
                        //XAttribute valueTypeAtt = f_el.Attribute("type");
                        string value = field.Value;
                        //if (valueTypeAtt != null) value = Common.GetEnumStateLabel(valueTypeAtt.Value, value);
                        yield return new XElement("c", value);
                    }
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
                        XElement f_rec = f_el.Element("record");
                        if (frecord != null) yield return GetRecordRow(record, f_rec); // не знаю зачем проверка
                    }
                }
            }
        }
        // Статический метод: рекурсивное сканирование с выявлением всех определенных форматом записи полей
        private static IEnumerable<XElement> ScanForFields(XElement frecord)
        {
            foreach (var el in frecord.Elements())
            {
                if (el.Name == "field") yield return GetLabel(el);
                else if (el.Name == "direct")
                {
                    yield return new XElement("d", new XAttribute("prop", el.Attribute("prop").Value),
                        new XElement("label", Common.OntNames[el.Attribute("prop").Value]),
                        el.Elements("record").Select(re => new XElement("r", new XAttribute(re.Attribute("type")),
                            ScanForFields(re))));
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
            return new XElement("f",
                p == null ? new XAttribute("prop", "nop") : new XAttribute("prop", p.Value),
                t == null ? null : new XAttribute("valueType", t.Value),
                label,
                null);
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
    public class RecordModel
    {
        // Если eid == bid, то это базовая запись 
        public string bid { get; set; } // идентификатор "внешней" записи 
        public string eid { get; set; } // идентификатор "этой" записи
        public string etype { get; set; } // идентификатор типа е-записи
        public string iprop { get; set; } // идентификатор обратного (между базовой и этой) отношения
        public int nc { get; set; } // количество колонок, заменяемы при редактировании
        private XElement _xresult;
        public XElement GetXResult() { return _xresult; }

        private XElement _format = null;
        public XElement CalculateFormat()
        { _format = StaticObjects.GetEditFormat(etype, iprop); return _format; }
        private XElement _xtree;
        public void SetXTree(XElement xtree) { _xtree = xtree; }
        /// <summary>
        /// Модель записи. Выполняется последовательно: конструирование модели, загрузка данных, формирование x-результата.
        /// Вместо второго этапа может быть заполнение x-дерева "со стороны"
        /// </summary>
        /// <param name="id"></param>
        /// <param name="eid"></param>
        /// <param name="etype"></param>
        /// <param name="iprop"></param>
        /// <param name="nc"></param>
        //public RecordModel(string bid, string eid, string etype, string iprop, int nc)
        //{
        //    this.bid = bid;
        //    this.eid = eid;
        //    this.etype = etype;
        //    this.iprop = iprop;
        //    this.nc = nc;
        //}
        public RecordModel() { }

            
        public void LoadFromDb() 
        {
            CalculateFormat();
            _xtree = StaticObjects.GetItemById(eid, _format);
        }
        public void MakeXResult() 
        {
            _xresult = PortraitModel.ConvertToResultStructure(_format, _xtree);

            //look = xresult;
        }

        public XElement look = null;
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
        public string searchstring;
        public string type;
        public SearchModel(string searchstring, string type)
        {
            DateTime tt0 = DateTime.Now;
            this.searchstring = searchstring;
            //type = "http://fogid.net/o/person"; // для отладки
            this.type = type;
            var query = StaticObjects.SearchByName(searchstring)
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
                });
            if (this.type != null) query = query.Where(res => res.type == this.type);
            _results = query
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
        { 
            XElement record = StaticObjects.GetItemByIdSpecial(id);
            this.id = record.Attribute("id").Value;
            string type_id = record.Attribute("type")==null?"notype": record.Attribute("type").Value;
            this.type_id = type_id;
            this.type = Common.OntNames.Where(pair => pair.Key == type_id).Select(pair => pair.Value).FirstOrDefault();
            if (this.type == null) this.type = type_id;
            var name_el = record.Elements("field").Where(f => f.Attribute("prop").Value == sema2012m.ONames.p_name)
                .FirstOrDefault();
            this.name = name_el == null ? "noname" : name_el.Value;
            farr = record.Elements("field")
                .Select(el => new Field() { prop = el.Attribute("prop").Value, value = el.Value, lang = (el.Attribute(sema2012m.ONames.xmllang) == null? null : el.Attribute(sema2012m.ONames.xmllang).Value) })
                .ToArray();
            darr = record.Elements("direct")
                .Select(el => new Direct() { prop = el.Attribute("prop").Value, resource = el.Element("record").Attribute("id").Value })
                .ToArray();
            iarr = record.Elements("inverse")
                .Select(el => new Inverse() { prop = el.Attribute("prop").Value, about = el.Element("record").Attribute("id").Value })
                .ToArray();
        }
    }
    // Вспомогательная модель конвертации данных
    public class ConvertModel
    {
        public string entityvalue { get; set; }
        public string entityid { get; set; }
    }
    // Вспомогательная модель для сканирования фог-документов
    
    public class EntityPlacesModel
    {
        public string entityvalue { get; set; }
        public KeyValuePair<string, string>[] places; 
    }
}