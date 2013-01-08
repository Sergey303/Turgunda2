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
            XElement appProfile = XElement.Load(path + "ApplicationProfile.xml");
            Turgunda2.Models.Common.formats = appProfile.Element("formats");
            XElement ontology = XElement.Load(path + "PublicuemCommon/ontology_iis-v10-doc_ruen.xml");
            Turgunda2.Models.Common.LoadOntNamesFromOntology(ontology);

            turlog("Turgunda initiated");
        }

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

        // =========== Редактирование ==========
        // Возвращает идентификатор созданного айтема или null
        public static string CreateNewItem(string name, string type, string username)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            var docinfo = CassetteKernel.CassettesConnection.docsInfo
                .Select(pair => pair.Value)
                .FirstOrDefault(di => di.isEditable && di.owner == username);
            if (docinfo == null) return null;
            int pos = type.LastIndexOfAny(new char[] {'/', '#'});
            if (pos == -1) return null;
            string id = docinfo.NextId();
            XElement xrecord = new XElement(XName.Get(type.Substring(pos + 1), type.Substring(0, pos + 1)),
                new XAttribute(sema2012m.ONames.rdfabout, id),
                new XElement(sema2012m.ONames.p_name, name));
            // Внедрить и сохранить
            xrecord.Add(new XAttribute(sema2012m.ONames.AttModificationTime, DateTime.Now.ToUniversalTime().ToString("s")));
            docinfo.GetRoot().Add(xrecord);
            docinfo.isChanged = true;
            docinfo.Save();

            // Добавить служебные поля
            xrecord.Add(new XAttribute("dbid", docinfo.dbId));
            xrecord.Add(new XAttribute("sender", docinfo.owner));
            // Запомнить действие в логе изменений
            //changelog(xrecord.ToString()); // ========== СДЕЛАТЬ!
            
            return null;
        }
        //public static string CreateSysObject(string type, string name, string username)
        //{
        //    var docinfo = getfilelist().FirstOrDefault(di => di.isEditable && di.owner == username);
        //    if (docinfo == null) return null;
        //    string id = docinfo.NextId();
        //    InsertXRecord(new XElement(type, new XAttribute(ONames.rdfabout, id),
        //        new XElement("name", new XAttribute(ONames.xmllang, "ru"), name)
        //        ), false, docinfo);
        //    return id;
        //}

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