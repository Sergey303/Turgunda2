using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;

namespace Turgunda2
{
    public class StaticObjects
    {
        private static bool _initiated = false;
        public static bool Initiated { get { return _initiated; } }
        private static string _path;
        private static object locker = new object();
        private static sema2012m.LogLine turlog = s => { };
        public static void Init() { Init(_path); }
        public static void Init(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            char c = path[path.Length-1];
            _path = path + (c=='/' || c=='\\' ? "" : "/");
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
            XElement xconfig = XElement.Load(path + "config.xml");
            var dbname_att = xconfig.Element("database").Attribute("dbname");
            if (dbname_att != null) sema2012m.DbEntry.DbName = dbname_att.Value;
            turlog("Turgunda initiating... path=" + path);
            try
            {
                CassetteKernel.CassettesConnection.ConnectToCassettes(xconfig.Elements("LoadCassette"));
            }
            catch (Exception ex)
            {
                turlog("Error while Turgunda initiating: " + ex.Message);
                return;
            }
            // Загрузка профиля
            XElement appProfile = XElement.Load(path + "ApplicationProfile.xml");
            Turgunda2.Models.Common.formats = appProfile.Element("formats");
            XElement ontology = XElement.Load(path + "ontology_iis-v9-doc_ruen.xml");
            Turgunda2.Models.Common.LoadOntNamesFromOntology(ontology);

            turlog("Turgunda initiated");
        }

        public static void LoadFromCassettes()
        {
            sema2012m.DbEntry.InitiateDb();
            var fogfilearr = CassetteKernel.CassettesConnection.GetFogFiles().ToArray();
            foreach (string dbfile in fogfilearr.Select(x => x.filePath))
            {
                var xdb = XElement.Load(dbfile);
                sema2012m.DbEntry.LoadXFlow(sema2012m.DbEntry.ConvertXFlow(xdb.Elements()).ToArray());
            }
        }
        public static void CheckDatabase()
        {
            var fogfilearr = CassetteKernel.CassettesConnection.GetFogFiles().ToArray();
            foreach (string dbfile in fogfilearr.Select(x => x.filePath))
            {
                var xdb = XElement.Load(dbfile);
                sema2012m.DbEntry.CheckXFlow(sema2012m.DbEntry.ConvertXFlow(xdb.Elements()), turlog);
            }
        }
   
    }
}