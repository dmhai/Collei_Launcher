﻿using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Collei_Launcher
{
    public partial class Main_Form : Form
    {

        public bool loaded = false;
        public static Main_Form form;
        public bool is_loading_cc = false;
        public int VerCode;
        public Cloud_Config cc;
        public Local_Config lc;
        public bool is_first_check = true;
        public string config_path = Application.StartupPath + @"\config.json";
        public List<ServersItem_List> servers = new List<ServersItem_List>();
        public int ci;

        public Main_Form()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            form = this;
            InitializeComponent();
            Methods.SetCertificatePolicy();
        }

        private void Main_Form_Shown(object sender, EventArgs e)
        {
            this.Servers_listView.Controls.Add(this.NoServerTip_label);
            bool isdebug = Methods.DebugBuild(Assembly.GetExecutingAssembly());
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string ver = string.Format("(v{0}.{1}.{2})", version.Major, version.Minor, version.Build);
            VerCode = version.Major*100+ version.Minor*10+ version.Build;
            this.Text += ver;
            this.Text += isdebug ? " - Debug" : " - Release";
            Check_Proxy();
            Load_Local_Config(sender,e);
        }

        public void Check_Proxy()
        {
            if (using_proxy().ProxyEnable)
            {
                string show = "检测到您当前开启了系统代理，这可能是上次启动器未正确关闭导致的,代理配置不正确会导致无法连接网络,当前的代理配置如下\n";
                show += Get_Proxy_Text();
                show += "\n若您想关闭代理,请点击“是”，若不想关闭代理，请点击“否”";
                if (MessageBox.Show(show, "要关闭代理吗?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Clear_Proxy();
                }
            }
        }
        public void Set_Proxy(string proxy)
        {
            using (RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
            {
                regkey.SetValue("ProxyEnable", 1);
                regkey.SetValue("ProxyHttp1.1", 1);
                regkey.SetValue("ProxyServer", proxy);
            }
        }

        public void Clear_Proxy()
        {
            using (RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
            {
                try
                {
                    regkey.SetValue("ProxyEnable", 0);
                    regkey.DeleteValue("ProxyServer");
                }
                catch (Exception e)
                {
                    Debug.Print(e.Message);
                }
            }
        }

        public Proxy_Config using_proxy()
        {
            Proxy_Config proxy = new Proxy_Config();
            try
            {
                using (RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (regkey.GetValue("ProxyEnable").ToString() == "1")
                    {
                        proxy.ProxyEnable = true;
                    }
                    object ps = regkey.GetValue("ProxyServer");
                    if (ps != null)
                    {
                        proxy.ProxyServer = ps.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return proxy;
        }


        public string Get_Proxy_Text()
        {
            string st = "代理";
            Proxy_Config pc = using_proxy();
            if (pc.ProxyEnable == true)
            {
                st += "已开启,代理服务器地址:";
                string[] servers = pc.ProxyServer.Split(';');
                for (int i = 0; i < servers.Length; i++)
                {
                    st += "\n" + servers[i];
                }
            }
            else
            {
                st += "未开启";
            }
            Debug.Print(st);
            return st;
        }
        private void Proxy_status_toolStripStatusLabel_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Get_Proxy_Text(), "代理状态", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void Home_tabPage_Enter(object sender, EventArgs e)
        {
            UpdateAndNotice();
        }
        public void UpdateAndNotice()
        {
            Task.Run(() =>
            {
                try
                {
                    while (lc == null)
                    {
                        Debug.Print("等待lc加载");
                        Thread.Sleep(100);
                    }
                    is_loading_cc = true;
                    string ccs = Methods.Get($"http://launcher.bambi5.top/Main?action=Get_Config&data=&lang={Thread.CurrentThread.CurrentUICulture.Name}");
                    if (ccs == null)
                    {
                        is_loading_cc = false;
                        Load_Servers();
                        Notice_label.Text = "获取云配置文件失败";
                        Notice_label.ForeColor = System.Drawing.Color.Red;
                        return;
                    }
                    cc = JsonConvert.DeserializeObject<Cloud_Config>(ccs);
                    Notice_label.Text = cc.config.Notice;
                    Notice_label.ForeColor = System.Drawing.Color.Black;
                    if (is_first_check && VerCode < cc.config.lastvercode && lc.config.lastvercode != cc.config.lastvercode)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine("发现有新版本，是否更新?");
                        stringBuilder.AppendLine("[当前版本]:" + VerCode); ;
                        stringBuilder.AppendLine("[最新版本]:" + cc.config.lastvercode);
                        stringBuilder.AppendLine("[更新内容]:" );
                        stringBuilder.AppendLine(cc.config.lastverstr);
                        stringBuilder.AppendLine();
                        stringBuilder.AppendLine("点击“是”，跳转到更新连接");
                        stringBuilder.AppendLine("点击“否”，跳过此版本");
                        stringBuilder.AppendLine("点击“取消”，本次关闭此消息框");
                        DialogResult dgr = MessageBox.Show(stringBuilder.ToString(), "版本更新提醒", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                        if (dgr == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(cc.UpdateUrl);
                        }
                        else if (dgr == DialogResult.No)
                        {
                            lc.config.lastvercode = cc.config.lastvercode;
                        }
                    }
                    is_first_check = false;
                    is_loading_cc = false;
                    Load_Servers();
                }
                catch (Exception ex)
                {
                    is_loading_cc = false;
                    Load_Servers();
                    Program.Application_Exception(ex);
                }
            });
        }
        private void Auto_close_proxy_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            lc.config.Auto_Close_Proxy = Auto_close_proxy_checkBox.Checked;
            Save_Local_Config();
        }

        private void Main_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            Save_Local_Config();
            if (lc.config.Auto_Close_Proxy)
            {
                Clear_Proxy();
            }
        }
        public void Load_Local_Config(object sender=null, EventArgs e=null)
        {
            Debug.Print(config_path);
            if (File.Exists(config_path))
            {
                Debug.Print("已找到config文件");
                string lcs = File.ReadAllText(config_path);
                try
                {
                    lc = JsonConvert.DeserializeObject<Local_Config>(lcs);
                }
                catch (Exception ex)
                {
                    Debug.Print("解析json时出现错误:" + ex.Message);
                }
            }
            else
            {
                Debug.Print("未找到config文件");
            }

            Local_Config.FixLC(ref lc);
            LoadSettingsToForm(sender,e);
        }
        public void LoadSettingsToForm(object sender = null, EventArgs e = null,bool Save = true)
        {
            Proxy_port_numericUpDown.Value = lc.config.ProxyPort;
            Auto_close_proxy_checkBox.Checked = lc.config.Auto_Close_Proxy;
            Show_Public_Server_checkBox.Checked = lc.config.Show_Public_Server;
            if (lc.config.Game_Path == null)
            {
                lc.config.Game_Path = Methods.GameRegReader.GetGameExePath();
            }
            if (lc.config.Game_Path != null)
            {
                Game_Path_textBox.Text = lc.config.Game_Path;
                if (sender != null&&e!=null)
                {
                    if (File.Exists(Path.GetDirectoryName(lc.config.Game_Path) + @"\YuanShen_Data\Managed\Metadata\global-metadata.dat"))
                    {
                        MetaFile_Input_textBox.Text = Path.GetDirectoryName(lc.config.Game_Path) + @"\YuanShen_Data\Managed\Metadata\global-metadata.dat";
                    }
                    else if (File.Exists(Path.GetDirectoryName(lc.config.Game_Path) + @"\GenshinImpact_Data\Managed\Metadata\global-metadata.dat"))
                    {
                        MetaFile_Input_textBox.Text = Path.GetDirectoryName(lc.config.Game_Path) + @"\GenshinImpact_Data\Managed\Metadata\global-metadata.dat";
                    }
                    if (File.Exists(Path.GetDirectoryName(lc.config.Game_Path) + @"\YuanShen_Data\Native\UserAssembly.dll"))
                    {
                        UAFile_Input_textBox.Text = Path.GetDirectoryName(lc.config.Game_Path) + @"\YuanShen_Data\Native\UserAssembly.dll";
                    }
                    else if (File.Exists(Path.GetDirectoryName(lc.config.Game_Path) + @"\GenshinImpact_Data\Native\UserAssembly.dll"))
                    {
                        UAFile_Input_textBox.Text = Path.GetDirectoryName(lc.config.Game_Path) + @"\GenshinImpact_Data\Native\UserAssembly.dll";
                    }
                }
            }
            CheckChannel_checkBox.Checked = lc.patch.CheckChannel;
            PatchP1_checkBox.Checked = lc.patch.PatchP1;
            Nopatch1_textBox.Text = lc.patch.Nopatch1;
            Patched1_textBox.Text = lc.patch.Patched1;
            Nopatch2_cn_textBox.Text = lc.patch.Nopatch2_cn;
            Nopatch2_os_textBox.Text = lc.patch.Nopatch2_os;
            Patched2_Meta_textBox.Text = lc.patch.Patched2_Meta;
            Patched2_UA_textBox.Text = lc.patch.Patched2_UA;
            Features_cn_textBox.Text = lc.patch.Features_cn;
            Features_os_textBox.Text = lc.patch.Features_os;
            if (lc.patch.SetChannel == Channel.CN)
            {
                CN_Channel_radioButton.Checked = true;
            }
            else if (lc.patch.SetChannel == Channel.OS)
            {
                OS_Channel_radioButton.Checked = true;
            }

            if (Save)
            {
                Save_Local_Config();
            }

        }
        private void Show_Public_Server_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            lc.config.Show_Public_Server = Show_Public_Server_checkBox.Checked;
            Save_Local_Config();
        }
        public void Load_Servers()
        {
            servers.Clear();
            if (lc.config.Show_Public_Server && cc != null)
            {
                for (int i = 0; i < cc.servers.Count; i++)
                {
                    ServersItem_List ser = new ServersItem_List();
                    ser.title = cc.servers[i].title;
                    ser.host = cc.servers[i].host;
                    ser.dispatch = cc.servers[i].dispatch;
                    ser.game = cc.servers[i].game;
                    ser.content = cc.servers[i].content;
                    ser.is_cloud = true;
                    servers.Add(ser);
                }
            }
            for (int i = 0; i < lc.servers.Count; i++)
            {
                ServersItem_List ser = new ServersItem_List();
                ser.title = lc.servers[i].title;
                ser.host = lc.servers[i].host;
                ser.dispatch = lc.servers[i].dispatch;
                ser.game = lc.servers[i].game;
                ser.content = lc.servers[i].content;
                ser.is_cloud = false;
                servers.Add(ser);
            }
            Load_Server_List();
        }
        public void Load_Server_List()
        {
            Servers_listView.BeginUpdate();
            Servers_listView.Items.Clear();
            int local_index = 0;
            for (int i = 0; i < servers.Count; i++)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.Text = servers[i].title;
                lvi.SubItems.Add(servers[i].host);
                lvi.SubItems.Add(servers[i].dispatch + "");
                lvi.SubItems.Add(servers[i].game + "");
                lvi.SubItems.Add("N/A");
                lvi.SubItems.Add("N/A");
                lvi.SubItems.Add("N/A");
                lvi.SubItems.Add(servers[i].content + "");
                if (servers[i].is_cloud)
                {
                    lvi.Tag = -1;
                }
                else
                {
                    lvi.Tag = local_index;
                    local_index++;
                }
                Servers_listView.Items.Add(lvi);
            }
            if(Servers_listView.Items.Count == 0)
            {
                NoServerTip_label.Visible = true;
            }
            else
            {
                NoServerTip_label.Visible = false;
            }
            Servers_listView.EndUpdate();
            loaded = true;
            Load_Server_Status();
        }
        public void Save_Local_Config()
        {
            Debug.Print("正在保存config文件"); ;
            Local_Config.FixLC(ref lc);
            File.WriteAllText(config_path, JsonConvert.SerializeObject(lc));
            Debug.Print("已保存config文件");
            LoadSettingsToForm(null,null,false);
        }
        private void Save_proxy_button_Click(object sender, EventArgs e)
        {
            lc.config.ProxyPort = (ushort)Proxy_port_numericUpDown.Value;
            Save_Local_Config();
        }
        public void Choice_Game_Path_button_Click(object sender, EventArgs e)
        {
            string path = Choice_Path("国服游戏文件|YuanShen.exe|国际服游戏文件|GenshinImpact.exe", "选择游戏文件", null);
            if (path == null)
            {
                return;
            }
            lc.config.Game_Path = path;
            Game_Path_textBox.Text = lc.config.Game_Path;
            Save_Local_Config();
        }

        public string Choice_Path(string Filter = null, string Title = null, string InitialDirectory = null)
        {
            if (InitialDirectory == null)
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            if (Filter != null)
                openFileDialog1.Filter = Filter;
            openFileDialog1.FileName = "";
            openFileDialog1.InitialDirectory = InitialDirectory;
            if (Title != null)
                openFileDialog1.Title = Title;
            DialogResult dr = openFileDialog1.ShowDialog();
            if (dr == DialogResult.OK)
            {
                if (openFileDialog1.FileName == "")
                {
                    MessageBox.Show("请选择一个文件！", "错误信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
                return openFileDialog1.FileName;
            }
            return null;
        }
        private void Game_Path_textBox_TextChanged(object sender, EventArgs e)
        {
            lc.config.Game_Path = Game_Path_textBox.Text;
        }

        private void Servers_List_tabPage_Enter(object sender, EventArgs e)
        {
            Status_timer.Enabled = true;
            if (!is_loading_cc)
            {
                Load_Servers();
                Load_Server_Status();
            }

        }

        private void Servers_listView_MouseDown(object sender, MouseEventArgs e)
        {
            if (!loaded)
            {
                return;
            }
            if (e.Button == MouseButtons.Right)
            {
                var item = Servers_listView.GetItemAt(e.X, e.Y);
                if (item != null)
                {
                    item.Selected = true;
                    ci = item.Index;
                    if (servers[ci].is_cloud)
                    {
                        添加ToolStripMenuItem.Visible = true;
                        连接ToolStripMenuItem.Visible = true;
                        检查连接ToolStripMenuItem.Visible = true;
                        编辑ToolStripMenuItem.Visible = false;
                        删除ToolStripMenuItem.Visible = false;
                        toolStripSeparator1.Visible = true;
                        toolStripSeparator2.Visible = false;
                    }
                    else
                    {
                        添加ToolStripMenuItem.Visible = true;
                        连接ToolStripMenuItem.Visible = true;
                        检查连接ToolStripMenuItem.Visible = true;
                        编辑ToolStripMenuItem.Visible = true;
                        删除ToolStripMenuItem.Visible = true;
                        toolStripSeparator1.Visible = true;
                        toolStripSeparator2.Visible = true;
                    }
                    Servers_contextMenuStrip.Show(Servers_listView, e.Location);
                }
                else
                {
                    添加ToolStripMenuItem.Visible = true;
                    连接ToolStripMenuItem.Visible = false;
                    检查连接ToolStripMenuItem.Visible = false;
                    编辑ToolStripMenuItem.Visible = false;
                    删除ToolStripMenuItem.Visible = false;
                    toolStripSeparator1.Visible = false;
                    toolStripSeparator2.Visible = false;
                    Servers_contextMenuStrip.Show(Servers_listView, e.Location);
                }
            }
        }

        private void Servers_listView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!loaded)
            {
                return;
            }
            var item = Servers_listView.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                return;
            }
            ci = item.Index;
            连接ToolStripMenuItem_Click(null, null);
        }

        private void 添加ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ServersItem ser = Edit_Form.Edit_Server();
            if (ser != null)
            {
                lc.servers.Add(ser);
                Save_Local_Config();
                Load_Servers();
            }
        }

        private void 连接ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (cc != null && cc.config.blacklist.Contains(servers[ci].host))
            {
                MessageBox.Show("此服务器在黑名单中，启动器拒绝连接到此服务器\n如有疑问，请联系bambi@bambi5.top", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else
            {
                Details_Form.Open_Index(servers[ci]);
            }
        }

        private void 编辑ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = (int)Servers_listView.Items[ci].Tag;
            ServersItem ser = Edit_Form.Edit_Server(lc.servers[index]);
            if (ser != null)
            {
                lc.servers[index] = ser;
                Save_Local_Config();
                Load_Servers();
            }
        }

        private void 删除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lc.servers.RemoveAt((int)Servers_listView.Items[ci].Tag);
            Load_Servers();
        }

        private void Open_Check_button_Click(object sender, EventArgs e)
        {
            Check_Form.Open_Form();
        }

        private void 检查连接ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Check_Form.Open_Form(servers[ci]);
        }
        Thread th;
        int stc = 0;
        int etc = 0;
        public void Load_Server_Status()
        {
            if (th != null)
            {
                th.Abort();
            }
            th = new Thread(() =>
              {
                  try
                  {
                      for (int i = 0; i < servers.Count; i++)
                      {
                          int s = i;
                          //while(stc - etc >=3){Thread.Sleep(100);}
                          new Thread(() =>
                          {
                              stc++;
                              try
                              {
                                  string str = "https://" + servers[s].host + ":" + servers[s].dispatch + "/status/server";
                                  Debug.Print(str);
                                  Details_Get ig = Methods.Get_for_Index(str);
                                  if (ig.Use_time >= 0 && ig.StatusCode == System.Net.HttpStatusCode.OK)
                                  {
                                      Def_status.Root df = JsonConvert.DeserializeObject<Def_status.Root>(ig.Result);
                                      Servers_listView.Items[s].SubItems[4].Text = df.status.playerCount.ToString();
                                      Servers_listView.Items[s].SubItems[5].Text = df.status.version;
                                  }
                                  else
                                  {
                                      Servers_listView.Items[s].SubItems[4].Text = "N/A";
                                      Servers_listView.Items[s].SubItems[5].Text = "N/A";
                                  }
                                  try
                                  {
                                      PingReply reply = new Ping().Send(servers[s].host, 1000);

                                      if (reply.Status == IPStatus.Success)
                                          Servers_listView.Items[s].SubItems[6].Text = reply.RoundtripTime + "ms";
                                      /*
                                     var sbuilder = new StringBuilder();
                                      sbuilder.AppendLine(string.Format("Address: {0} ", reply.Address.ToString()));
                                      sbuilder.AppendLine(string.Format("RoundTrip time: {0} ", reply.RoundtripTime));
                                      sbuilder.AppendLine(string.Format("Time to live: {0} ", reply.Options.Ttl));
                                      sbuilder.AppendLine(string.Format("Don't fragment: {0} ", reply.Options.DontFragment));
                                      sbuilder.AppendLine(string.Format("Buffer size: {0} ", reply.Buffer.Length));
                                      Console.WriteLine(sbuilder.ToString());
                                      */
                                  }
                                  catch
                                  {
                                      Servers_listView.Items[s].SubItems[6].Text = "N/A";
                                  }
                              }
                              catch (NullReferenceException e)
                              {
                                  Debug.Print("加载状态出错NullReferenceException:" + e.Message);
                              }
                              catch (Exception e)
                              {
                                  Debug.Print("加载状态出错:" + e.Message);
                              }
                              finally
                              {
                                  etc++;
                              }
                          }).Start();
                      }
                  }
                  catch (Exception ex)
                  {
                      Debug.Print(ex.Message);
                  }
              });
            th.Start();
        }


        private void Status_timer_Tick(object sender, EventArgs e)
        {
            Load_Server_Status();
        }

        private void Main_Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.Environment.Exit(0);
        }

        private void Find_GameExe_button_Click(object sender, EventArgs e)
        {
            string path = Methods.GameRegReader.GetGameExePath();
            if (path == null)
            {
                MessageBox.Show("自动寻找路径失败", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (DialogResult.Yes == MessageBox.Show("找到游戏路径:\n" + path + "\n点击“是”：设置为游戏路径\n点击“否”：不设为游戏路径", "", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                lc.config.Game_Path = path;
            Game_Path_textBox.Text = lc.config.Game_Path;
            Save_Local_Config();
        }

        private void Servers_List_tabPage_Leave(object sender, EventArgs e)
        {
            Status_timer.Enabled = false;
        }

        private void Set_MetaInputpath_button_Click(object sender, EventArgs e)
        {
            string path = Choice_Path("global-metadata文件|global-metadata.dat|所有文件|*.*", "选择文件", Path.GetDirectoryName(lc.config.Game_Path));
            if (path == null)
            {
                return;
            }
            MetaFile_Input_textBox.Text = path;
        }

        private void Outpath_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            Output_panel.Enabled = !INOUT_Meta_checkBox.Checked;
        }

        private void Set_MetaOutputpath_button_Click(object sender, EventArgs e)
        {
            string path = Choice_Save_Path("dat文件|*.dat|所有文件|*.*", "选择保存位置", Path.GetDirectoryName(lc.config.Game_Path), "global-metadata.dat");
            if (path == null)
            {
                return;
            }
            MetaFile_Output_textBox.Text = path;
        }
        public string Choice_Save_Path(string Filter = null, string Title = null, string InitialDirectory = null, string FileName = null)
        {
            if (InitialDirectory == null)
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            if (Filter != null)
                saveFileDialog1.Filter = Filter;
            if (FileName != null)
            {
                saveFileDialog1.FileName = FileName;
            }
            saveFileDialog1.InitialDirectory = InitialDirectory;
            if (Title != null)
                saveFileDialog1.Title = Title;
            DialogResult dr = saveFileDialog1.ShowDialog();
            if (dr == DialogResult.OK)
            {
                if (saveFileDialog1.FileName == "")
                {
                    MessageBox.Show("请选择保存位置！", "错误信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
                return saveFileDialog1.FileName;
            }
            return null;
        }

        public void Meta_Actions(Meta_Action action)
        {
            Show_Meta_Doing_Tip(true);
            string input = MetaFile_Input_textBox.Text;
            string output;
            if (!INOUT_Meta_checkBox.Checked)
            {
                output = MetaFile_Output_textBox.Text;
            }
            else
            {
                output = input;
            }
            if (input == null || output == null || input == "" || output == "")
            {
                MessageBox.Show("路径未选择！", "错误信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var t = new Thread(()=>
            {
                
                string show = "OK";

                try
                {

                    switch (action)
                    {
                        case Meta_Action.Patch:
                            {
                                show = Meta_Patch_Mgr.Patch_File(input, output, lc.patch);
                                break;
                            }
                        case Meta_Action.UnPatch:
                            {
                                show = Meta_Patch_Mgr.UnPatch_File(input, output, lc.patch);
                                break;
                            }
                        case Meta_Action.Encrypt:
                            {
                                Meta_Patch_Mgr.Encrypt_File(input, output);
                                break;
                            }
                        case Meta_Action.Decrypt:
                            {
                                Meta_Patch_Mgr.Decrypt_File(input, output);
                                break;
                            }
                    }

                    MessageBox.Show(show, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    GC.Collect();
                }
                catch(System.Runtime.InteropServices.SEHException)
                {
                    MessageBox.Show("在解包或打包Meta时出现了错误，这可能是由以下原因导致的\n\n①尝试修补(或反修补)已经被解包过的Meta文件\n②Meta文件已被损坏\n\n若您已经解包过Meta，请尝试打包后重试\n若您已经打包过Meta，请尝试解包后重试", "解包/打包时出现错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Show_Meta_Doing_Tip(false);
                }
            });
            t.Start();
            
        }
        public void Show_Meta_Doing_Tip(bool Doing)
        {
            Meta_DoingTip_label.Visible = Doing;
            Meta_Actions_groupBox.Enabled = !Doing;
        }

        private void Decrypt_File_button_Click(object sender, EventArgs e)
        {
            Meta_Actions(Meta_Action.Decrypt);
        }

        private void Encrypt_File_button_Click(object sender, EventArgs e)
        {
            Meta_Actions(Meta_Action.Encrypt);
        }

        private void Patch_Meta_button_Click(object sender, EventArgs e)
        {
            Meta_Actions(Meta_Action.Patch);
        }

        private void UnPatch_Meta_button_Click(object sender, EventArgs e)
        {
            Meta_Actions(Meta_Action.UnPatch);
        }

        private void CheckChannel_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            SetChannel_Panel.Enabled = !CheckChannel_checkBox.Checked;
        }

        private void Delete_PC_button_Click(object sender, EventArgs e)
        {
            lc.patch = new Patch_Config();
            Save_Local_Config();
            MessageBox.Show("OK", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Save_PC_button_Click(object sender, EventArgs e)
        {
            lc.patch.CheckChannel = CheckChannel_checkBox.Checked;
            lc.patch.PatchP1 = PatchP1_checkBox.Checked;
            lc.patch.Nopatch1 = Nopatch1_textBox.Text;
            lc.patch.Patched1 = Patched1_textBox.Text;
            lc.patch.Nopatch2_cn = Nopatch2_cn_textBox.Text;
            lc.patch.Nopatch2_os = Nopatch2_os_textBox.Text;
            lc.patch.Patched2_Meta = Patched2_Meta_textBox.Text;
            lc.patch.Patched2_UA = Patched2_UA_textBox.Text;
            lc.patch.Features_cn = Features_cn_textBox.Text;
            lc.patch.Features_os = Features_os_textBox.Text;
            if(CN_Channel_radioButton.Checked)
            {
                lc.patch.SetChannel = Channel.CN;
            }
            else if(OS_Channel_radioButton.Checked)
            {
                lc.patch.SetChannel = Channel.OS;
            }

            Save_Local_Config();
            MessageBox.Show("OK", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void Set_UAInputpath_button_Click(object sender, EventArgs e)
        {
            string path = Choice_Path("UserAssembly文件|UserAssembly.dll|所有文件|*.*", "选择文件", Path.GetDirectoryName(lc.config.Game_Path));
            if (path == null)
            {
                return;
            }
            UAFile_Input_textBox.Text = path;
        }

        private void Set_UAOutputpath_button_Click(object sender, EventArgs e)
        {
            string path = Choice_Save_Path("dll文件|*.dll|所有文件|*.*", "选择保存位置", Path.GetDirectoryName(lc.config.Game_Path), "UserAssembly.dll");
            if (path == null)
            {
                return;
            }
            UAFile_Output_textBox.Text = path;
        }
        public void UA_Actions(bool ispatch)
        {
            Show_UA_Doing_Tip(true);
            string input = UAFile_Input_textBox.Text;
            string output;
            if (!INOUT_UA_checkBox.Checked)
            {
                output = UAFile_Output_textBox.Text;
            }
            else
            {
                output = input;
            }
            if (input == null || output == null || input == "" || output == "")
            {
                MessageBox.Show("路径未选择！", "错误信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            new Thread(() =>
            {

                string show = "";
                try
                {
                    if (ispatch)
                    {
                        show = UA_Patch_Mgr.Patch_File(input, output, lc.patch);
                    }
                    else
                    {

                        show = UA_Patch_Mgr.UnPatch_File(input, output, lc.patch);
                    }
                    GC.Collect();
                    MessageBox.Show(show, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    Show_UA_Doing_Tip(false);
                }
            }).Start();
        }

        public void Show_UA_Doing_Tip(bool Doing)
        {
            UA_DoingTip_label.Visible = Doing;
            UA_Actions_groupBox.Enabled = !Doing;
        }

        private void INOUT_UA_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            UA_Output_panel.Enabled = !INOUT_UA_checkBox.Checked;
        }

        private void Author_label_Click(object sender, EventArgs e)
        {

            System.Diagnostics.Process.Start("http://launcher.bambi5.top");
        }

        private void NoServerTip_label_MouseDown(object sender, MouseEventArgs e)
        {
            Servers_listView_MouseDown(sender, new MouseEventArgs(e.Button,e.Clicks, e.X + NoServerTip_label.Location.X, e.Y + NoServerTip_label.Location.Y, e.Delta));
        }

        private void Patch_UA_button_Click(object sender, EventArgs e)
        {
            UA_Actions(true);
        }

        private void UnPatch_UA_button_Click(object sender, EventArgs e)
        {
            UA_Actions(false);
        }
    }
}
