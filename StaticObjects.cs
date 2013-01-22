using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Data.Common;
using System.Reflection;

namespace Turgunda2
{
    public class StaticObjects
    {
        private static bool _initiated = false;
        public static bool Initiated { get { return _initiated; } }
        private static string _path;
        //private static sema2012m.IAdapter adapter;
        private static sema2012m.Engine engine = null;

        // Для сохранения соответствия id-сущности - формат, по которому строится его портрет. Можно извлекать также имя типа
        public static Dictionary<string, XElement> formatByIdCache;
        public static void Init() { Init(_path); }
        public static void Init(string path)
        {
            // Выявление пути к корневой директории приложения
            if (string.IsNullOrEmpty(path)) return;
            char c = path[path.Length-1];
            _path = path + (c=='/' || c=='\\' ? "" : "/");
            // Инициирование системного лога проекта Тургунда
            InitTurlog(path);
            turlog("Turgunda initiating... path=" + path);

            // Чтение конфигуратора
            XElement xconfig = XElement.Load(path + "config.xml");
            // Определение имени базы данных (или графа), СУБД и connectionstring
            string dbname = "turgunda2"; // Значение по умолчанию
            string dbms = "sqlite"; // Значение по умолчанию
            string connectionstring = "Data Source=" + _path + dbname + ".db3";
            // попытка прочитать эти значения из конфигуратора
            bool config_params_ok = false;
            var database = xconfig.Element("database");
            if (database != null) dbname = database.Attribute("dbname").Value;
            var cs_att = database.Attribute("connectionstring");
            if (cs_att != null) 
            {
                string cs_string = cs_att.Value;
                int pos = cs_string.IndexOf(':');
                if (pos != -1)
                {
                    dbms = cs_string.Substring(0, pos);
                    connectionstring = cs_string.Substring(pos + 1);
                    config_params_ok = true;
                }
            }
            if (!config_params_ok) turlog("Не удалось в конфигураторе прочитать правильный элемент database. Используется параметры по умолчанию.");

            // Зафиксируем адаптер соответствующей СУБД
            string dataprovider = "System.Data.SQLite";
            string engineTypeName = null;
            if (dbms == "sqlite")
            {
                dataprovider = "System.Data.SQLite";
                engineTypeName = "sema2012m.EngineRDB";
            }
            else if (dbms == "mysql")
            {
                dataprovider = "MySql.Data.MySqlClient";
                engineTypeName = "sema2012m.EngineRDB";
            }
            else if (dbms == "mssql")
            {
                dataprovider = "System.Data.SqlClient";
                engineTypeName = "sema2012m.EngineRDB";
            }
            DbProviderFactory factory = DbProviderFactories.GetFactory(dataprovider);
            DbConnection connection = factory.CreateConnection();
            connection.ConnectionString = connectionstring;

            // Это правильно только для использования RDB, для других адаптеров надо что-то другое
            Assembly myAssembly = Assembly.LoadFrom(_path + "bin/" +
                (engineTypeName == "sema2012m.EngineRDB" ? "EngineRDB.dll" :
                (""))
                );
            Type myType = myAssembly.GetType(engineTypeName);

            Type ttype = myType; //Type.GetType(engineTypeName);
            engine = (sema2012m.Engine)Activator.CreateInstance(ttype, new object[] { dbms, connection });


            // Присоединимся к кассетам через список из конфигуратора 
            try
            {
                CassetteKernel.CassettesConnection.ConnectToCassettes(xconfig.Elements("LoadCassette"),
                    s => turlog(s));
            }
            catch (Exception ex)
            {
                turlog("Error while Turgunda initiating: " + ex.Message);
                return;
            }
            // Загрузка данных, если такая потребность имеется
            //if (adapter.NeedToLoad) LoadFromCassettesExpress(s=>turlog(s), s=>turlog(s)); // надо сделать раздельные логи

            // Загрузка профиля и онтологии
            appProfile = XElement.Load(path + "ApplicationProfile.xml");
            Turgunda2.Models.Common.formats = appProfile.Element("formats");
            XElement ontology = XElement.Load(path + "PublicuemCommon/ontology_iis-v10-doc_ruen.xml");
            Turgunda2.Models.Common.LoadOntNamesFromOntology(ontology);
            // Инициализация или чистка кешей
            formatByIdCache = new Dictionary<string, XElement>(); // Надо бы этот кеш убрать!!! -- я убрал только его наполнение (.Add())!

            turlog("Turgunda initiated");
        }
        private static XElement appProfile;

        // Быстрая загрузка данных 
        public static void LoadFromCassettesExpress()
        {
            var fogfilearr = CassetteKernel.CassettesConnection.GetFogFiles().Select(d => d.filePath).ToArray();
            engine.LoadFromCassettesExpress(fogfilearr, turlog, turlog);
        }

        // =========== Основные процедуры ==========
        public static IEnumerable<XElement> SearchByName(string searchstring) { return engine.SearchByName(searchstring); }
        //public static string GetType(string id) { return adapter.GetType(id); }
        public static XElement GetItemById(string id, XElement format) { return engine.GetItemById(id, format); }
        public static XElement GetItemByIdSpecial(string id) { return engine.GetItemByIdBasic(id, true); }
        // Отладочные процедуры
        public static string SpecialGetEntityNameByIndex(int ind) { return engine.SpecialGetEntityNameByIndex(ind); }
        public static IEnumerable<KeyValuePair<string, string>> SpecialEntityPlaces(string id)
        {
            List<KeyValuePair<string, string>> placeList = new List<KeyValuePair<string, string>>();
            foreach (string fogfile in CassetteKernel.CassettesConnection.GetFogFiles().Select(d => d.filePath))
            {
                XElement fog = XElement.Load(fogfile);
                foreach (XElement record in fog.Elements())
                {
                    if (record.Name == "delete")
                    {
                        if (record.Attribute("id").Value == id) placeList.Add(new KeyValuePair<string,string>("del", fogfile));
                    }
                    else if (record.Name == "substitute")
                    {
                        if (record.Attribute(sema2012m.ONames.AttOld_id).Value == id || 
                            record.Attribute(sema2012m.ONames.AttNew_id).Value == id)
                            placeList.Add(new KeyValuePair<string, string>("sub o=" + 
                                record.Attribute(sema2012m.ONames.AttOld_id).Value +
                                " n=" + record.Attribute(sema2012m.ONames.AttNew_id).Value, fogfile));
                    }
                    else
                    {
                        var about = record.Attribute(sema2012m.ONames.rdfabout);
                        if (about != null && about.Value == id) 
                            placeList.Add(new KeyValuePair<string,string>("DEF. t=" + record.Name, fogfile));
                        foreach (XElement el in record.Elements())
                        {
                            var resource = el.Attribute(sema2012m.ONames.rdfresource);
                            if (resource != null && resource.Value == id) placeList.Add(new KeyValuePair<string, string>("use", fogfile));
                        }

                    }
                }
            }
            return placeList;
        }

        // =========== Редактирование ==========
        public static XElement GetEditFormat(string type, string prop)
        {
            XElement res = appProfile.Element("EditRecords").Elements("record").FirstOrDefault(re => re.Attribute("type").Value == type);
            if (res == null) return null;
            XElement resu = new XElement(res);
            if (prop != null)
            {
                XElement forbidden = resu.Elements("direct").FirstOrDefault(di => di.Attribute("prop").Value == prop);
                if (forbidden != null) forbidden.Remove();
            }
            return resu;
        }
        // Возвращает идентификатор созданного айтема или null
        public static string CreateNewItem(string name, string type, string username)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            XElement xrecord = CreateNewXRecordAndSave(type, username, new XElement(sema2012m.ONames.tag_name, name));

            engine.ReceiveXCommand(xrecord);
            
            return xrecord.Attribute(sema2012m.ONames.rdfabout).Value;
        }
        public static void ChangeItem(XElement item, string username)
        {
            factograph.RDFDocumentInfo docinfo = CassetteKernel.CassettesConnection.docsInfo
                .Select(pair => pair.Value)
                .FirstOrDefault(di => di.isEditable && di.owner == username);
            if (docinfo == null) { }

            XElement item_corrected = engine.ReceiveXCommand(item);

            item_corrected.Add(new XAttribute(sema2012m.ONames.AttModificationTime, DateTime.Now.ToUniversalTime().ToString("u")));
            // Внедрить и сохранить
            docinfo.GetRoot().Add(new XElement(item_corrected)); // Делаю клона и записываю в фог-документ
            docinfo.isChanged = true;
            docinfo.Save();
            // Тут еще в лог изменений надо записать
        }

        private static XElement CreateNewXRecordAndSave(string type, string username, params object[] content) // В content помещается содержимое записи
        {
            factograph.RDFDocumentInfo docinfo = CassetteKernel.CassettesConnection.docsInfo
                .Select(pair => pair.Value)
                .FirstOrDefault(di => di.isEditable && di.owner == username);
            if (docinfo == null) { }
            XName xn = GetXName(type);
            string id = docinfo.NextId();
            XElement xrecord = new XElement(xn, new XAttribute(sema2012m.ONames.rdfabout, id), content);
            xrecord.Add(new XAttribute(sema2012m.ONames.AttModificationTime, DateTime.Now.ToUniversalTime().ToString("u")));
            // Внедрить и сохранить
            docinfo.GetRoot().Add(new XElement(xrecord)); // Делаю клона и записываю в фог-документ
            docinfo.isChanged = true;
            docinfo.Save();
            // Добавить служебные поля и сохранить в логе
            //xrecord.Add(new XAttribute("dbid", docinfo.dbId));
            //xrecord.Add(new XAttribute("sender", docinfo.owner));
            // Запомнить действие в логе изменений и синхронизовать(!)
            //changelog(xrecord.ToString()); // ========== СДЕЛАТЬ!

            return xrecord;
        }

        private static XName GetXName(string type) // Надо переместить метод в ONames
        {
            int pos = type.LastIndexOfAny(new char[] { '/', '#' });
            if (pos == -1) { throw new Exception("Error: strange construction of type string: " + type); }
            XName xn = XName.Get(type.Substring(pos + 1), type.Substring(0, pos + 1));
            return xn;
        }
        public static string AddInvRelation(string eid, string prop, string rtype, string username)
        {
            //if (rtype == null || string.IsNullOrEmpty(eid)) return;
            XElement xrecord = CreateNewXRecordAndSave(rtype, username, 
                new XElement(GetXName(prop), 
                    new XAttribute(sema2012m.ONames.rdfresource, eid)));

            engine.ReceiveXCommand(xrecord);
            return xrecord.Attribute(sema2012m.ONames.rdfabout).Value;
        }


        // ====== Вспомогательные процедуры Log - area
        private static object locker = new object();
        private static sema2012m.LogLine turlog = s => { };
        private static void InitTurlog(string path)
        {
            turlog = (string line) =>
            {
                lock (locker)
                {
                    try
                    {
                        var saver = new System.IO.StreamWriter(path + "logs/turlog.txt", true, System.Text.Encoding.UTF8);
                        saver.WriteLine(DateTime.Now.ToString("s") + " " + line);
                        saver.Close();
                    }
                    catch (Exception)
                    {
                        //LogFile.WriteLine("Err in buildlog writing: " + ex.Message);
                    }
                }
            };
        }
    }
}