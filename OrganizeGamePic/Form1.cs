using System;
using Dapper;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using Log2File1;
using LazZiya.ImageResize;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            TpIdText.Text = "0";
           // Task<int> returnedTaskTResult = InitGameMenuInfo();
            this.l2file = new Log2File(null);
        }
        class GameMenuInfo
        {
            public int TpId { set; get; }
            public int GameCasinoId { set; get; }
            public string Name { set; get; }
            public string Lang { set; get; }
            public string Image { set; get; }
            public string TpName { set; get; }            
            public bool isUsed { set; get; }
        }
        private Dictionary<string,GameMenuInfo> pGameMenuInfoDataList = null;
        private IEnumerable<GameMenuInfo> pGameMenuInfoData = null;
        private readonly Log2File l2file;
        private  String SQLCommand = $@"SELECT GameCasinoInfo.TpId, GameCasinoInfo.GameCasinoId, GameCasinoInfo.`Name`, GameCasinoInfo.Lang, GameCasinoInfo.Image, TPL.TpName AS TpName, 0 AS isUsed FROM GameCasinoInfo LEFT JOIN TpList AS TPL ON TPL.Id = GameCasinoInfo.TpId ORDER BY TPL.TpName;";//where GameCasinoInfo.tpid = 27 

        public async Task<int> InitGameMenuInfo()
        {

            SetStatus($"Table initialization");
            string connetStr2 = "server=18.141.59.251;port=3306;user=nova88;database=Siebog;password=Aa12345678!;";
            var sqlConn = new MySqlConnection(connetStr2);
            {
                
                    pGameMenuInfoData = sqlConn.Query<GameMenuInfo>(SQLCommand);
                //pGameMenuInfoDataList = GameMenuInfo.ToDictionary(x => $"{x.Name}", x => x);
            }
            SetStatus($"InitTable Finish");
            return 0;
        }



        private void label1_Click(object sender, EventArgs e)
        {

        }
        // 將"空白" "&" ":" 換成 "-", 
        // 將"’" "." "™" 移除
        // "-"不連續出現
        // 全部小寫
        // 缺圖語系用en圖補齊
        private string OrgFileNameFilter(string fileName) 

        {
             return fileName = fileName.Replace(" ", "-")
                                        .Replace("&", "-")
                                        .Replace(":", "-")
                                        .Replace("'", "")
                                        .Replace("’", "")
                                        .Replace(".", "")
                                        .Replace("™", "")
                                        .Replace("---", "-")
                                        .Replace("--", "-")
                                        .Replace("\u200B", "")
                                        .Replace(",", "")
                                        .ToLower();
           

        }
        private string DisFileNameFilter(string fileName)
        {
            //fileName = fileName.Replace("-", "");
            fileName = fileName.Replace(" ", "");
            fileName = fileName.Replace("&", "");
            fileName = fileName.Replace("'", "");
            fileName = fileName.Replace("’","");
            fileName = fileName.Replace(".","");
            fileName = fileName.ToLower();

            return fileName;
        }

        private void button1_Click_1(object sender, EventArgs e)
        {

            SetStatus($"Table initialization");
            string connetStr2 = "server=13.250.152.76;port=3306;user=nova88;database=Siebog;password=Aa12345678!;";
            var sqlConn = new MySqlConnection(connetStr2);
            string SQLCommand2;
            if (TpIdText.Text.Equals("0"))
                SQLCommand2 = SQLCommand;
            else
                SQLCommand2 = $@"SELECT GameCasinoInfo.TpId, GameCasinoInfo.GameCasinoId, GameCasinoInfo.`Name`, GameCasinoInfo.Lang, GameCasinoInfo.Image, TPL.TpName AS TpName, 0 AS isUsed FROM GameCasinoInfo LEFT JOIN TpList AS TPL ON TPL.Id = GameCasinoInfo.TpId where GameCasinoInfo.tpid = {TpIdText.Text} ORDER BY TPL.TpName;";//
            
            var GameMenuInfoData = sqlConn.Query<GameMenuInfo>(SQLCommand2);

            //pGameMenuInfoDataList = GameMenuInfo.ToDictionary(x => $"{x.Name}", x => x);

            SetStatus($"InitTable Finish");
            if (GameMenuInfoData.Count() <= 0)
            {
                SetStatus($"Table no data");
                return;
            }

            String sourcePath = SourcePathtext.Text;
            String distPath = Disttext.Text;
            l2file.SetPath(distPath);
            listBox1.Items.Clear();

            SetStatus($"Start Organize");
            if (sourcePath.Length > 0 && distPath.Length > 0 )
            {
                var file1 = Directory.GetFiles(sourcePath);
                foreach (var fileSUBPath in Directory.GetDirectories(sourcePath))
                {
                    var file = Directory.GetFiles(fileSUBPath);
                    foreach (var fileFullPath in Directory.GetFiles(fileSUBPath))
                    {
                        
                        string TpName = fileSUBPath.Substring(fileSUBPath.LastIndexOf("\\")+1, fileSUBPath.Length- fileSUBPath.LastIndexOf("\\")-1);
                        int start = fileFullPath.LastIndexOf("\\");
                        int mid = fileFullPath.LastIndexOf("-");
                        int end = fileFullPath.LastIndexOf(".");
                        if (start > 0 && mid > 0 && end > 0)
                        {
                            string fileName = fileFullPath.Substring(start + 1, mid - (start + 1));

                            fileName = OrgFileNameFilter(fileName);

                            string langWord = fileFullPath.Substring(mid + 1, end - (mid + 1));
                            bool bMatched = false;
                            foreach (var MenuInfoData in GameMenuInfoData)
                            {
                                if (MenuInfoData.Lang.Equals("en"))
                                {
                                    MenuInfoData.Name = OrgFileNameFilter(MenuInfoData.Name);
                                    //  MenuInfoData.Name = MenuInfoData.Name.Replace(" ", "-");
                                    //  MenuInfoData.Name = MenuInfoData.Name.Replace("'", "");

                                    if (String.Compare(MenuInfoData.TpName, TpName, true) == 0 && String.Compare(MenuInfoData.Name, fileName, true) == 0)
                                    //if (String.Compare(MenuInfoData.TpName, TpName, true) == 0 && MenuInfoData.GameCasinoId == 2410034)
                                    {
                                        // string disFileName = DisFileNameFilter(fileName);
                                        OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, langWord, fileName, true);
                                        if (String.Compare(langWord, "en", true) == 0)
                                        {
                                            foreach (var Data in GameMenuInfoData)
                                            {
                                                if (String.Compare(Data.TpName, TpName, true) == 0 && Data.GameCasinoId == MenuInfoData.GameCasinoId && !Data.Lang.Equals("en"))
                                                    OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, Data.Lang, fileName, false);
                                            }
                                            //    OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "cs", fileName, false);
                                           // OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "id", fileName, false);
                                           // OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "th", fileName, false);
                                           // OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "vn", fileName, false);

                                        }
                                        bMatched = true;
                                        MenuInfoData.isUsed = true;
                                        break;
                                    }
                                }

                            }
                            if (!bMatched)
                            {
                                SetStatus($"no Organize  {fileFullPath} ");
                                l2file.Info($"{fileFullPath}", "FileNoMatchLog");
                            }

                        }

                        //MessageBox.Show(filename);
                        //   string tmpfilename = fileName.Replace(sourceWord, desWord);
                        //textBox4.Text = $"{fileName} to {tmpfilename}";
                        //  SetStatus($"{tmpfilename}");
                        //  File.Move(fileName, tmpfilename);
                    }
                }
                foreach (var MenuInfoData in GameMenuInfoData)
                {
                    if (!MenuInfoData.isUsed && MenuInfoData.Lang.Equals("en") && MenuInfoData.Image!= null)
                    {
                        string sorFileName = MenuInfoData.Image.Substring(0,MenuInfoData.Image.LastIndexOf("."));
                        string filePath = $"{sourcePath}\\{MenuInfoData.TpName}\\{sorFileName}-{MenuInfoData.Lang}.jpg";
                        if (File.Exists(filePath))
                        {
                            var fileName = OrgFileNameFilter(MenuInfoData.Name);

                            OrganizeFile(filePath, distPath, MenuInfoData.TpName, MenuInfoData.Lang, fileName, true);
                            l2file.Info($"{MenuInfoData.TpName} {MenuInfoData.Image} to  {fileName} {MenuInfoData.GameCasinoId}", "reNameFile");
                            MenuInfoData.isUsed = true;
                            if (String.Compare(MenuInfoData.Lang, "en", true) == 0)
                            {
                                foreach (var Data in GameMenuInfoData)
                                {
                                    if (String.Compare(Data.TpName, MenuInfoData.TpName, true) == 0 && Data.GameCasinoId == MenuInfoData.GameCasinoId && !Data.Lang.Equals("en"))
                                    {
                                        string secFilePath = $"{sourcePath}\\{MenuInfoData.TpName}\\{sorFileName}-{Data.Lang}.jpg";
                                        if (File.Exists(secFilePath))
                                            OrganizeFile(secFilePath, distPath, MenuInfoData.TpName, Data.Lang, fileName, false);
                                        else
                                        {
                                            secFilePath = $"{sourcePath}\\{MenuInfoData.TpName}\\{sorFileName}-{MenuInfoData.Lang}.jpg";
                                            OrganizeFile(secFilePath, distPath, MenuInfoData.TpName, Data.Lang, fileName, false);

                                        }

                                    }
                                }
                                //    OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "cs", fileName, false);
                                // OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "id", fileName, false);
                                // OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "th", fileName, false);
                                // OrganizeFile(fileFullPath, distPath, MenuInfoData.TpName, "vn", fileName, false);

                            }

                        }


                    }
                    
                }
                foreach (var MenuInfoData in GameMenuInfoData)
                {
                    if (!MenuInfoData.isUsed && MenuInfoData.Lang.Equals("en"))
                    {
                        l2file.Info($"{MenuInfoData.TpName} {MenuInfoData.Name} {MenuInfoData.GameCasinoId}", "TableNoMatchLog");
                    }
                    else
                    {
                        MenuInfoData.isUsed = false;
                    }
                }
                SetStatus($"Finish");
            }
            else
            {
                SetStatus($"No Input");
            }


        }
        
        void OrganizeFile( string sourcefFullPath,string disPath,string folderNameTPID,string Lang,string filename,bool bDeleteExitFile)
        {
            string desfullPath = $"{disPath}\\{folderNameTPID}";
            string disfilename = $"{filename}-{Lang}";
            string desfilefullPath = $"{desfullPath}\\{OrgFileNameFilter(disfilename)}.jpg";
            if (!Directory.Exists(desfullPath))
            {
                Directory.CreateDirectory(desfullPath);  //新增資料夾
            }
            if (File.Exists(desfilefullPath))
            {
                if (bDeleteExitFile)
                    File.Delete(desfilefullPath); // 已存在檔案刪除
                else
                    return;
            }

            var fileinfo = new FileInfo(sourcefFullPath);
            if (fileinfo.Length > (1024 * 70))
            {
                var img = Image.FromFile(sourcefFullPath);
                int quality = 90;
                if (fileinfo.Length > (200 * 1024))
                    quality = 70;
                img.SaveAs(desfilefullPath, quality);
                SetStatus($"Down quality {sourcefFullPath} to {desfilefullPath}");
                l2file.Info($"Down quality {sourcefFullPath} to {desfilefullPath}", "SuccessLog");
            }
            else
            {
                File.Copy(sourcefFullPath, desfilefullPath);
                SetStatus($"{sourcefFullPath} to {desfilefullPath}");
                l2file.Info($"{sourcefFullPath} to {desfilefullPath}", "SuccessLog");

            }

        }
        void SetStatus(String Status)
        {
            listBox1.Items.Add(Status);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
        }
        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog path = new FolderBrowserDialog();
            path.ShowDialog();
            this.SourcePathtext.Text = path.SelectedPath;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog path = new FolderBrowserDialog();
            path.ShowDialog();
            this.Disttext.Text = path.SelectedPath;
        }
    }
}
