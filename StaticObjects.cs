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
            turlog("Turgunda initiating... path=" + path);
            try
            {
                ConnectToCassettes();
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

        public static Dictionary<string, factograph.CassetteInfo> cassettesInfo = new Dictionary<string, factograph.CassetteInfo>();
        private static Dictionary<string, factograph.RDFDocumentInfo> docsInfo = new Dictionary<string, factograph.RDFDocumentInfo>();
        public static void LoadFromCassettes()
        {
            sema2012m.DbEntry.InitiateDb();
            var fogfilearr = GetFogFiles(cassettesInfo).ToArray();
            foreach (string dbfile in fogfilearr.Select(x => x.filePath))
            {
                var xdb = XElement.Load(dbfile);
                sema2012m.DbEntry.LoadXFlow(sema2012m.DbEntry.ConvertXFlow(xdb.Elements()).ToArray());
            }
        }
        public static void CheckDatabase()
        {
            var fogfilearr = GetFogFiles(cassettesInfo).ToArray();
            foreach (string dbfile in fogfilearr.Select(x => x.filePath))
            {
                var xdb = XElement.Load(dbfile);
                sema2012m.DbEntry.CheckXFlow(sema2012m.DbEntry.ConvertXFlow(xdb.Elements()), turlog);
            }
        }
        public static void ConnectToCassettes()
        {
            cassettesInfo = new Dictionary<string, factograph.CassetteInfo>();
            docsInfo = new Dictionary<string, factograph.RDFDocumentInfo>();
            XElement xconfig = XElement.Load(_path + "config.xml");
            var dbname_att = xconfig.Element("database").Attribute("dbname"); 
            if (dbname_att != null) sema2012m.DbEntry.DbName = dbname_att.Value;

            foreach (XElement lc in xconfig.Elements("LoadCassette"))
            {
                bool loaddata = true;
                if (lc.Attribute("regime") != null && lc.Attribute("regime").Value == "nodata") loaddata = false;
                string cassettePath = lc.Value;

                factograph.CassetteInfo ci = null;
                try
                {
                    ci = factograph.Cassette.LoadCassette(cassettePath, loaddata);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при загрузке кассеты [" + cassettePath + "]: " + ex.Message);
                }
                if (ci == null || cassettesInfo.ContainsKey(ci.fullName)) continue;
                cassettesInfo.Add(ci.fullName.ToLower(), ci);
                if (ci.docsInfo != null) foreach (var docInfo in ci.docsInfo)
                    {
                        docsInfo.Add(docInfo.dbId, docInfo);
                        if (loaddata)
                        {
                            try
                            {
                                docInfo.isEditable = (lc.Attribute("write") != null && docInfo.GetRoot().Attribute("counter") != null);
                                //sDataModel.LoadRDF(docInfo.Root);
                                //if (!docInfo.isEditable) docInfo.root = null; //Иногда это действие нужно закомментаривать...
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error in document " + docInfo.uri + "\n" + ex.Message);
                            }
                        }
                    }
            }
        }
        private static IEnumerable<factograph.RDFDocumentInfo> GetFogFiles(Dictionary<string, factograph.CassetteInfo> cassettesInfo)
        {
            foreach (var cpair in cassettesInfo)
            {
                var ci = cpair.Value;
                var toload = ci.loaddata;
                factograph.RDFDocumentInfo di0 = new factograph.RDFDocumentInfo(ci.cassette, true);
                yield return di0;
                var qu = di0.GetRoot().Elements("document").Where(doc => doc.Element("iisstore").Attribute("documenttype").Value == "application/fog");
                foreach (var docnode in qu)
                {
                    var di = new factograph.RDFDocumentInfo(docnode, ci.cassette.Dir.FullName, toload);
                    if (toload) di.ClearRoot();
                    yield return di;
                }
                di0.ClearRoot();
            }
        }
   
    }
}