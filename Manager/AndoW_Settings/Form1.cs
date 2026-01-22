using System;
using System.Diagnostics;
using System.Windows.Forms;
using TurtleTools;



namespace AndoWSettings
{
    public partial class Form1 : Form
    {
        public ServerSettingsManager sServerSettingsManager = new ServerSettingsManager();
        public ServerSettings sServerSettings = new ServerSettings();

        public Form1()
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            try
            {
                InitializeComponent();
                InitEventHandler();

                sServerSettings = sServerSettingsManager.LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        public void InitEventHandler()
        {
            SaveBtn.Click += SaveBtn_Click;  // 포트번호저장
        }

        void SaveBtn_Click(object sender, EventArgs e)
        {
            ProcessTools.KillExeProcess(FNDTools.GetManagerProcName());
            ProcessTools.KillExeProcess(FNDTools.GetHyOnManagerProcName());
            NetworkTools.StopFTPSrv();

            SaveTurtlePort();
        }

        public void SaveTurtlePort()
        {
            if (FTPPort.Text == string.Empty || 
                PasvMinPort.Text == string.Empty || 
                PasvMaxPort.Text == string.Empty)
            {
                MessageBox.Show("포트번호를 입력해 주세요.");
                return;
            }

            try
            {
                int FTPPortNum = Convert.ToInt32(FTPPort.Text);
                int FTPPasvMinPortNum = Convert.ToInt32(PasvMinPort.Text);
                int FTPPasvMaxPortNum = Convert.ToInt32(PasvMaxPort.Text);

                SecurityTools.DeletePort("ftp_port", FTPPortNum);
                SecurityTools.DeletePasvFTPPorts("ftp_ports", FTPPasvMinPortNum, FTPPasvMaxPortNum);

                NetworkTools.SetFTPConfigPort(FTP_TYPE.FileZilla, FTPPortNum, FTPPasvMinPortNum, FTPPasvMaxPortNum);

                sServerSettings.FTP_Port = FTPPortNum;
                sServerSettings.FTP_PasvMinPort = FTPPasvMinPortNum;
                sServerSettings.FTP_PasvMaxPort = FTPPasvMaxPortNum;
                sServerSettings.PreserveAspectRatio = aspect_ratio_chbox.Checked;
                sServerSettings.DataServerIp = dataServerIpTextBox?.Text?.Trim() ?? string.Empty;
                sServerSettings.MessageServerIp = messageServerIpTextBox?.Text?.Trim() ?? string.Empty;

                sServerSettingsManager.SaveData(sServerSettings);

                MessageBox.Show("설정을 저장했습니다.");

                ProcessTools.KillExeProcess(FNDTools.GetManagerProcName());

                Application.Exit();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("포트번호가 제대로 입력되지 않았습니다.");
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            DisplayServerSettings();
        }

        private void DisplayServerSettings()
        {
            FTPPort.Text = sServerSettings.FTP_Port.ToString();
            PasvMinPort.Text = sServerSettings.FTP_PasvMinPort.ToString();
            PasvMaxPort.Text = sServerSettings.FTP_PasvMaxPort.ToString();

            aspect_ratio_chbox.Checked = sServerSettings.PreserveAspectRatio;
            if (dataServerIpTextBox != null)
            {
                dataServerIpTextBox.Text = sServerSettings.DataServerIp ?? string.Empty;
            }
            if (messageServerIpTextBox != null)
            {
                messageServerIpTextBox.Text = sServerSettings.MessageServerIp ?? string.Empty;
            }
        }

        private void showipbtn_Click(object sender, EventArgs e)
        {
            Process.Start("CMD.exe", "/K ipconfig");
        }
    }
}
