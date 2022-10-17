﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Collei_Launcher
{
    public partial class Edit_Form : Form
    {
        public Edit_Form()
        {
            InitializeComponent();
        }

        public ServersItem rser = null;

        private ServersItem Private_Edit_Server(ServersItem ser = null)
        {
            if(ser ==null)
            {
                this.ShowDialog();
                return rser;
            }
            Title_textBox.Text = ser.title;
            Host_textBox.Text = ser.host;
            Dispatch_port_numericUpDown.Value = ser.dispatch;
            Content_textBox.Text=ser.content;
            this.ShowDialog();
            return rser;
        }
        public static ServersItem Edit_Server(ServersItem ser = null)
        { 
             Edit_Form edit = new Edit_Form();
           return edit.Private_Edit_Server(ser);
        }

        private void Update_button_Click(object sender, EventArgs e)
        { 
            if(Title_textBox.Text==""||Host_textBox.Text == "")
            {
                MessageBox.Show("服务器名称或地址没有填写！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Methods.HasChinese(Host_textBox.Text))
            {
                if (DialogResult.Cancel == MessageBox.Show("当前服务器域名中含有中文，可能会导致无法正常连接！\n\n点击“确定”:保存此服务器\n点击“取消”:返回修改", "温馨提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Information))
                {
                    return;
                }
            }
            rser = new ServersItem();
            rser.title = Title_textBox.Text;
            rser.host = Host_textBox.Text;
            rser.dispatch = (ushort)Dispatch_port_numericUpDown.Value;
            rser.content = Content_textBox.Text;
            User_Close = true;
            this.Close();
        }

        private void Cancel_button_Click(object sender, EventArgs e)
        {
            rser = null;
            User_Close = true;
            this.Close();
        }
        public bool User_Close = false;
        private void Edit_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(!User_Close)
            {
                if (Title_textBox.Text != "" && Host_textBox.Text != "")
                {
                    DialogResult dialog = MessageBox.Show("是否保存更改?\n点击“是”：保存更改\n点击“否”：取消更改\n点击“取消”：取消关闭窗口", "是否保存更改?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (dialog == DialogResult.Yes)
                    {
                        if (Title_textBox.Text == "" || Host_textBox.Text == "")
                        {
                            MessageBox.Show("服务器名称或地址没有填写！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            e.Cancel = true;
                            return;
                        }
                        rser = new ServersItem();
                        rser.title = Title_textBox.Text;
                        rser.host = Host_textBox.Text;
                        rser.dispatch = (ushort)Dispatch_port_numericUpDown.Value;
                        rser.content = Content_textBox.Text;
                    }
                    else if (dialog == DialogResult.No)
                    {
                        rser = null;
                    }
                    else if (dialog == DialogResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
        }
    }
}
