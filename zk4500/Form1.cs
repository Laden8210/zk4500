using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxZKFPEngXControl;
using Newtonsoft.Json;


namespace zk4500
{
    public partial class Form1 : Form
    {
        private AxZKFPEngX ZkFprint = new AxZKFPEngX();
        private bool Check;
        private IpifyResponse ipifyResponse;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;

        public Form1()
        {
            InitializeComponent();
            InitializeNotifyIcon();


        }

        private void InitializeNotifyIcon()
        {

            contextMenu = new ContextMenuStrip();
            ToolStripMenuItem openMenuItem = new ToolStripMenuItem("Open");
            ToolStripMenuItem closeMenuItem = new ToolStripMenuItem("Close");

            openMenuItem.Click += new EventHandler(OpenMenuItem_Click);
            closeMenuItem.Click += new EventHandler(CloseMenuItem_Click);

            contextMenu.Items.Add(openMenuItem);
            contextMenu.Items.Add(closeMenuItem);

  
            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            notifyIcon.DoubleClick += new EventHandler(NotifyIcon_DoubleClick);
        }

        private void OpenMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void CloseMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            Application.Exit();
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }


        private void Form1_Load(object sender, EventArgs e)
        {

            Controls.Add(ZkFprint);
            InitialAxZkfp();
            _ = getIPAdress();

        }

        private void InitialAxZkfp()
        {
            try
            {
                if (ZkFprint.InitEngine() == 0)
                {
                    ZkFprint.FPEngineVersion = "9";
                    deviceSerial.Text += " " + ZkFprint.SensorSN + " Count: " + ZkFprint.SensorCount.ToString() + " Index: " + ZkFprint.SensorIndex.ToString();
                    ShowHintInfo("Device successfully connected");

                }

            }
            catch (Exception ex)
            {
                ShowHintInfo("Device init err, error: " + ex.Message);
            }
        }

        private async Task getIPAdress()
        {
            try
            {

                string apiUrl = "https://api.ipify.org?format=json";

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();


                        ipifyResponse = JsonConvert.DeserializeObject<IpifyResponse>(jsonResponse);


                        label2.Text = "IP Address: " + ipifyResponse.ip;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to retrieve IP address. Status code: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
            }
        }



        private async void zkFprint_OnCapture(object sender, IZKFPEngXEvents_OnCaptureEvent e)
        {
            string template = ZkFprint.EncodeTemplate1(e.aTemplate);

            txtTemplate.Text = template;


            Graphics g = fpicture.CreateGraphics();
            Bitmap bmp = new Bitmap(fpicture.Width, fpicture.Height);
            g = Graphics.FromImage(bmp);
            int dc = g.GetHdc().ToInt32();
            ZkFprint.PrintImageAt(dc, 0, 0, bmp.Width, bmp.Height);
            g.Dispose();
            fpicture.Image = bmp;

            byte[] imageBytes;
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                imageBytes = ms.ToArray();
            }

            await semaphore.WaitAsync();
            try
            {
                await SendImageToApiAsync(imageBytes, template);
                await Task.Delay(1000); 
            }
            finally
            {
                semaphore.Release();
            }

        }


        private async void zkFprint_OnCaptureRegister(object sender, IZKFPEngXEvents_OnCaptureEvent e)
        {
            string template = ZkFprint.EncodeTemplate1(e.aTemplate);
            txtTemplate.Text = template;

            Graphics g = fpicture.CreateGraphics();
            Bitmap bmp = new Bitmap(fpicture.Width, fpicture.Height);
            g = Graphics.FromImage(bmp);
            int dc = g.GetHdc().ToInt32();
            ZkFprint.PrintImageAt(dc, 0, 0, bmp.Width, bmp.Height);
            g.Dispose();
            fpicture.Image = bmp;

            byte[] imageBytes;
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                imageBytes = ms.ToArray();
            }



            var data = await FetchDataFromApi();

            if (data == null)
            {
                return;
            }

            foreach (var item in data)
            {
                string dataTemplate = item.template;


                if (ZkFprint.VerFingerFromStr(ref template, dataTemplate, false, ref Check))
                {
                    ShowHintInfo("Verified");
                    await semaphore.WaitAsync();
                    try
                    {
                        await SendEMployeeToApiAsync(item.employee_id);
                        await Task.Delay(1000); // 1 second delay
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    break;
                }
                else
                    ShowHintInfo("Not Verified");
            }



        }



        private void ShowHintInfo(String s)
        {
            prompt.Text = "Status: " + s;
        }


        private void btnRegister_Click(object sender, EventArgs e)
        {



        }



        private async void button1_Click(object sender, EventArgs e)
        {


        }

        private async Task SendImageToApiAsync(byte[] imageBytes, string template)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var content = new MultipartFormDataContent();
                    var imageContent = new ByteArrayContent(imageBytes);
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

                    content.Add(imageContent, "fingerprint_image", "fingerprint.jpg");
                    var templateContent = new StringContent(template);
                    content.Add(templateContent, "template");
                    var ipContent = new StringContent(ipifyResponse.ip);
                    content.Add(ipContent, "ipaddress");

                    HttpResponseMessage response = await client.PostAsync("http://localhost/fingerprint-integration/api/retrieve-fingerprint.php", content);

                    if (response.IsSuccessStatusCode)
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();

                        ShowHintInfo(apiResponse);
                    }
                    else
                    {

                    }
                    {
                        ShowHintInfo("Failed to send image. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowHintInfo("Error sending image: " + ex.Message);
            }


        }
        private async Task SendEMployeeToApiAsync(string id)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var content = new MultipartFormDataContent();

                    var templateContent = new StringContent(id);
                    content.Add(templateContent, "id");
                    var ipContent = new StringContent(ipifyResponse.ip);
                    content.Add(ipContent, "ip");

                    HttpResponseMessage response = await client.PostAsync("https://rfidborrowsystem.online/api/fingerprintReceived", content);

                    if (response.IsSuccessStatusCode)
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();

                        ShowHintInfo(apiResponse);
                    }
                    else
                    {
                        ShowHintInfo("Failed to send image. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowHintInfo("Error sending image: " + ex.Message);
            }
        }

        private async Task<List<Fingerprint>> FetchDataFromApi()
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync("https://rfidborrowsystem.online/api/fingerprintData");


                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    ShowHintInfo(responseBody);
                    return JsonConvert.DeserializeObject<List<Fingerprint>>(responseBody);
                }
                else
                {
                    ShowHintInfo("Failed to send image. Status code: " + response.StatusCode);
                }
                return null;

            }
        }


        private class Fingerprint
        {
            public string employee_id { get; set; }
            public string template { get; set; }
            public string fingerprint_data { get; set; }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void btnConfig_Click(object sender, EventArgs e)
        {
            ZkFprint.OnCapture += zkFprint_OnCapture;
            ZkFprint.BeginCapture();
            ShowHintInfo("Register");
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            ZkFprint.OnCapture += zkFprint_OnCaptureRegister;
            ZkFprint.BeginCapture();
            ShowHintInfo("Listening");
        }
    }
}
