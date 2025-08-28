using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Reflection;
using System.IO;

using EventByListen.Language;
using System.Xml;
using Newtonsoft.Json;
using System.Net;
using TINYXMLTRANS;

namespace EventByListen
{
    public partial class EventByListen : Form
    {
        private int m_lLogNum = 0;
        private string szShowData = null;
        private bool bIsAlarmStart = false;
        private string m_strAlarmType = "";
        WebClient client = new WebClient();
        private HttpListener listener = new HttpListener();
        
        public class ContantData
        {
            public string filename { get; set; }
            public string ContentType { get; set; }
            public string Content { get; set; }
        }

        public EventByListen()
        {
            InitializeComponent();
            comboBoxLanguage.SelectedIndex = 0;
            MultiLanguage.SetDefaultLanguage(comboBoxLanguage.Text);
            foreach (Form form in Application.OpenForms)
            {
                MultiLanguage.LoadLanguage(form);
            }
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            AddDevice dlg = new AddDevice();
            dlg.ShowDialog();
            dlg.Dispose();
        }

        /// <summary>
        /// 匹配相同的子byte数组
        /// </summary>
        /// <param name="src">目标byte序列</param>
        /// <param name="index">从目标序列的index位置开始匹配</param>
        /// <param name="value">用来匹配的序列</param>
        /// <returns>参数错误或未匹配到返回-1，否则返回value在src上出现的位置</returns>
        internal int IndexOf(byte[] src, int index, byte[] value)
        {
            if (src == null || value == null)
            {
                return -1;
            }

            if (src.Length == 0 || src.Length < index
                || value.Length == 0 || src.Length < value.Length)
            {
                return -1;
            }
            for (int i = index; i < src.Length - value.Length; i++)
            {
                if (src[i] == value[0])
                {
                    if (value.Length == 1)
                    {
                        return i;
                    }
                    bool flag = true;
                    for (int j = 1; j < value.Length; j++)
                    {
                        if (src[i + j] != value[j])
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private List<ContantData> SeparteMutipart(byte[] mutiData)
        {
            string strReceivedData = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(mutiData);

            List<ContantData> contentList = new List<ContantData>();

            String strBoundary = "";
            int iBoundaryStart = strReceivedData.IndexOf("--");
            int iBoundaryEnd = strReceivedData.IndexOf("\r\n", iBoundaryStart);
            strBoundary = strReceivedData.Substring(iBoundaryStart, iBoundaryEnd - iBoundaryStart);

            int iDataEnd = strReceivedData.IndexOf(strBoundary + "--");
            int iOffSet = 0;

            while (iOffSet < iDataEnd)
            {
                int iHeadStart = strReceivedData.IndexOf(strBoundary, iOffSet);
                int iHeadEnd = strReceivedData.IndexOf("\r\n\r\n", iHeadStart);
                String strHead = strReceivedData.Substring(iHeadStart, iHeadEnd - iHeadStart + "\r\n\r\n".Length);

                int iNextHeadStart = strReceivedData.IndexOf(strBoundary, iHeadEnd);

                //get content type
                String strType = String.Empty;
                int iTypeStart = strHead.IndexOf("Content-Type: ");
                if (iTypeStart != -1)
                {
                    int iTypeEnd = strHead.IndexOf("\r\n", iTypeStart + "Content-Type: ".Length);
                    if (iTypeEnd != -1)
                    {
                        strType = strHead.Substring(iTypeStart + "Content-Type: ".Length, iTypeEnd - iTypeStart - "Content-Type: ".Length);
                    }
                }

                String strFileName = String.Empty;
                int iFileNameStart = strHead.IndexOf("filename=\"");
                if (iFileNameStart != -1)
                {
                    int iFileNameEnd = strHead.IndexOf("\"", iFileNameStart + "filename=\"".Length);
                    if (iFileNameEnd != -1)
                    {
                        strFileName = strHead.Substring(iFileNameStart + "filename=\"".Length, iFileNameEnd - iFileNameStart - "filename=\"".Length);
                    }
                }

                string strContent = strReceivedData.Substring(iHeadEnd + "\r\n\r\n".Length, iNextHeadStart - iHeadEnd - "\r\n\r\n".Length);

                ContantData contentItem = new ContantData();
                contentItem.ContentType = strType;
                contentItem.Content = strContent;
                contentItem.filename = strFileName;
                contentList.Add(contentItem);

                iOffSet = iNextHeadStart;
            }
            return contentList;
        }

        public bool ParseAlarmData(byte[] data)
        {
            try
            {
                //拆分数据
                List<ContantData> datalist = SeparteMutipart(data);
                /////////解析报文内容，生成报警信息以及图片信息，存储并显示出来////////////
                string strEventData = "";
                string strAlarmTime = "";
                foreach(ContantData childData in datalist)
                {
                    ProcessBodyData(childData, ref strEventData, ref strAlarmTime);
                }
                this.Invoke(new EventHandler(delegate
                {
                    listViewAlarmInfo.BeginUpdate();
                    ListViewItem item = new ListViewItem();
                    item.Text = (++m_lLogNum).ToString();
                    item.SubItems.Add(strAlarmTime);
                    item.SubItems.Add(strEventData);
                    listViewAlarmInfo.Items.Add(item);
                    listViewAlarmInfo.EndUpdate();
                }));
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("ParseAlarmData Exception raised!");
                Console.WriteLine("\nMessage:{0}", e.ToString());
                return false;
            }
        }

        private void ProcessBodyData(ContantData struData, ref string strEventData, ref string strAlarmTime)
        {
            string strAlarmInfo = struData.Content;   //从--MIME_boundary开始的报文
            string ACSPicStr = " ACSPic: \n";
            string TemperaturePicStr = " TemperaturePic: \n";
            StringBuilder strPrintInfoBuild = new StringBuilder();
            string strAlarmPicPathIndex = "";

            if (struData.ContentType.Contains("application/xml"))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(strAlarmInfo);
                XmlNode rootNode = xmlDoc.DocumentElement;
                for (int i = 0; i < rootNode.ChildNodes.Count; i++)
                {
                    if (rootNode.ChildNodes[i].Name.Equals("dateTime"))
                    {
                        strAlarmTime = rootNode.ChildNodes[i].InnerText;
                        continue;
                    }
                    strEventData = strEventData + rootNode.ChildNodes[i].Name + ":" + rootNode.ChildNodes[i].InnerText + ";";
                }
            }
            else if (struData.ContentType.Contains("application/json"))//TODO JSON报警的处理
            {
                if (strAlarmInfo.Contains("AccessControllerEvent"))
                {
                    m_strAlarmType = "AccessControllerEvent";
                    AlarmInfo alarmInfo;
                    alarmInfo = JsonConvert.DeserializeObject<AlarmInfo>(strAlarmInfo);
                    ACSAlarmStruConvertToStr(alarmInfo, strPrintInfoBuild);
                    strEventData = strPrintInfoBuild.ToString();
                    strAlarmTime = alarmInfo.dateTime;
                }
                else if (strAlarmInfo.Contains("FaceTemperatureMeasurementEvent"))
                {
                    m_strAlarmType = "FaceTemperatureMeasurementEvent";
                    FaceTemperature struFaceTemp;
                    struFaceTemp = JsonConvert.DeserializeObject<FaceTemperature>(strAlarmInfo);
                    FaceTempAlarmStruConvertToStr(struFaceTemp, strPrintInfoBuild);
                    strAlarmTime = struFaceTemp.dateTime;
                }
                else
                {
                    m_strAlarmType = "unknown";
                    strPrintInfoBuild.Append("unknown alarmType; ");
                    strAlarmTime = "unknown";
                }
                strEventData = strPrintInfoBuild.ToString();
            }
            else if (strAlarmInfo.Contains("AccessControllerEvent"))//TODO JSON报警的处理
            {
                 m_strAlarmType = "AccessControllerEvent";
                 AlarmInfo alarmInfo;
                 alarmInfo = JsonConvert.DeserializeObject<AlarmInfo>(strAlarmInfo);
                 ACSAlarmStruConvertToStr(alarmInfo, strPrintInfoBuild);
                 strEventData = strPrintInfoBuild.ToString();
                 strAlarmTime = alarmInfo.dateTime;
                strEventData = strPrintInfoBuild.ToString();
            }
            else if (struData.ContentType.Contains("image/"))
            {
                byte[] byData = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(struData.Content);
                string strListenPath = Path.Combine(Application.StartupPath, DateTime.Now.ToString("yyyy-MM-dd"));
                string datatimenow = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                string strData = strEventData;
                if (!Directory.Exists(strListenPath))
                {
                    Directory.CreateDirectory(strListenPath);
                }

                switch (m_strAlarmType)
                {
                    case "AccessControllerEvent":
                        strAlarmPicPathIndex = Path.Combine(strListenPath, datatimenow + "_"
                        + "AccessControllerEvent_" + "contentID_" + struData.filename + ".jpg");
                        strEventData += ACSPicStr;
                        break;
                    case "FaceTemperatureMeasurementEvent":
                        strAlarmPicPathIndex = Path.Combine(strListenPath, datatimenow + "_"
                        + "FaceTemperature_" + "contentID_" + struData.filename + ".jpg");
                        strEventData += TemperaturePicStr;
                        break;
                    case "unknown":
                        strAlarmPicPathIndex = Path.Combine(strListenPath, datatimenow + "_"
                        + "Unknown_" + "contentID_" + struData.filename + ".jpg");
                        break;
                    default:  //这里进不来
                        break;
                }
                strEventData += strAlarmPicPathIndex;
                strEventData += "; ";
                try
                {
                    //图片保存为文件
                    FileStream fs = new FileStream(strAlarmPicPathIndex, FileMode.Create);
                    fs.Write(byData, 0, byData.Length);
                    fs.Flush();
                    fs.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("ParseAlarmData Write File Exception raised!");
                    Console.WriteLine("\nMessage:{0}", e.Message);
                }
            }
        }

        private void FaceTempAlarmStruConvertToStr(FaceTemperature struFaceTemp, StringBuilder strPrintInfoBuild)
        {

            strPrintInfoBuild.Append("ipAddress:" + struFaceTemp.ipAddress + "; ");
            strPrintInfoBuild.Append("eventType:" + struFaceTemp.eventType + "; ");
            if (struFaceTemp.FaceTemperatureMeasurementEvent.currTemperature != 0.0)
            {
                strPrintInfoBuild.Append("currTemperature:" + struFaceTemp.FaceTemperatureMeasurementEvent.currTemperature + "; ");
            }
            if (struFaceTemp.FaceTemperatureMeasurementEvent.isAbnomalTemperature != null)
            {
                strPrintInfoBuild.Append("isAbnomalTemperature:" + struFaceTemp.FaceTemperatureMeasurementEvent.isAbnomalTemperature + "; ");
            }

            if (struFaceTemp.FaceTemperatureMeasurementEvent.mask != null)
            {
                strPrintInfoBuild.Append("mask:" + struFaceTemp.FaceTemperatureMeasurementEvent.mask + "; "); 
            }
            //strPrintInfoBuild.Append(" ipv6Address:" + struFaceTemp.ipv6Address);
            //strPrintInfoBuild.Append(" portNo:" + struFaceTemp.portNo);
            //strPrintInfoBuild.Append(" protocol:" + struFaceTemp.protocol);
            //strPrintInfoBuild.Append(" macAddress:" + struFaceTemp.macAddress);
            //strPrintInfoBuild.Append(" channelID:" + struFaceTemp.channelID);
            //strPrintInfoBuild.Append(" dateTime:" + struFaceTemp.dateTime);
            //strPrintInfoBuild.Append(" activePostCount:" + struFaceTemp.activePostCount);
            //strPrintInfoBuild.Append(" eventState:" + struFaceTemp.eventState);
            //strPrintInfoBuild.Append(" eventDescription:" + struFaceTemp.eventDescription);
            //strPrintInfoBuild.Append(" deviceName:" + struFaceTemp.FaceTemperatureMeasurementEvent.deviceName);
            //strPrintInfoBuild.Append(" serialNo:" + struFaceTemp.FaceTemperatureMeasurementEvent.serialNo);
            //strPrintInfoBuild.Append(" thermometryUnit:" + struFaceTemp.FaceTemperatureMeasurementEvent.thermometryUnit);
            //strPrintInfoBuild.Append(" positionX:" + struFaceTemp.FaceTemperatureMeasurementEvent.RegionCoordinates.positionX);
            //strPrintInfoBuild.Append(" positionY:" + struFaceTemp.FaceTemperatureMeasurementEvent.RegionCoordinates.positionY);
            //strPrintInfoBuild.Append(" remoteCheck:" + struFaceTemp.FaceTemperatureMeasurementEvent.remoteCheck);
        }

        private void ACSAlarmStruConvertToStr(AlarmInfo alarmInfo, StringBuilder strAlarmInfoBuild)
        {

            strAlarmInfoBuild.Append("ipAddress:" + alarmInfo.ipAddress + "; ");
            strAlarmInfoBuild.Append("eventType:" + alarmInfo.eventType + "; ");
            strAlarmInfoBuild.Append("dateTime:" + alarmInfo.dateTime + "; ");
            strAlarmInfoBuild.Append("majorEventType:" + alarmInfo.AccessControllerEvent.majorEventType + "; ");
            strAlarmInfoBuild.Append("subEventType:" + alarmInfo.AccessControllerEvent.subEventType + "; ");
            strAlarmInfoBuild.Append("employeeNo:" + alarmInfo.AccessControllerEvent.employeeNo + "; ");
            strAlarmInfoBuild.Append("cardNo:" + alarmInfo.AccessControllerEvent.cardNo + "; ");
            if (alarmInfo.AccessControllerEvent.name != null)
            {
                strAlarmInfoBuild.Append("name:" + alarmInfo.AccessControllerEvent.name + "; ");
            }
            if (alarmInfo.AccessControllerEvent.currTemperature != 0.0)
            {
                strAlarmInfoBuild.Append("currTemperature:" + alarmInfo.AccessControllerEvent.currTemperature + "; ");
            }
            if (alarmInfo.AccessControllerEvent.isAbnomalTemperature != null)
            {
                strAlarmInfoBuild.Append("isAbnomalTemperature:" + alarmInfo.AccessControllerEvent.isAbnomalTemperature + "; ");
            }
            if (alarmInfo.AccessControllerEvent.mask != null)
            {
                strAlarmInfoBuild.Append("mask:" + alarmInfo.AccessControllerEvent.mask + "; ");
            }
            //strAlarmInfoBuild.Append("attendanceStatus:" + alarmInfo.AccessControllerEvent.attendanceStatus)
            //strAlarmInfoBuild.Append("+cardNo:" + alarmInfo.AccessControllerEvent.cardNo);
            //strAlarmInfoBuild.Append("+activePostCount:" + alarmInfo.activePostCount);
            //strAlarmInfoBuild.Append("+deviceName:" + alarmInfo.AccessControllerEvent.deviceName);
            //strAlarmInfoBuild.Append("+cardType:" + alarmInfo.AccessControllerEvent.cardType);
            //strAlarmInfoBuild.Append("+whiteListNo:" + alarmInfo.AccessControllerEvent.whiteListNo);
            //strAlarmInfoBuild.Append("+reportChannel:" + alarmInfo.AccessControllerEvent.reportChannel);
            //strAlarmInfoBuild.Append("+cardReaderKind:" + alarmInfo.AccessControllerEvent.cardReaderKind);
            //strAlarmInfoBuild.Append("+cardReaderNo:" + alarmInfo.AccessControllerEvent.cardReaderNo);
            //strAlarmInfoBuild.Append("+doorNo:" + alarmInfo.AccessControllerEvent.doorNo);
            //strAlarmInfoBuild.Append("+verifyNo:" + alarmInfo.AccessControllerEvent.verifyNo);
            //strAlarmInfoBuild.Append("+alarmInNo:" + alarmInfo.AccessControllerEvent.alarmInNo);
            //strAlarmInfoBuild.Append("+alarmOutNo:" + alarmInfo.AccessControllerEvent.alarmOutNo);
            //strAlarmInfoBuild.Append("+caseSensorNo:" + alarmInfo.AccessControllerEvent.caseSensorNo);
            //strAlarmInfoBuild.Append("+RS485No:" + alarmInfo.AccessControllerEvent.RS485No);
            //strAlarmInfoBuild.Append("+multiCardGroupNo:" + alarmInfo.AccessControllerEvent.multiCardGroupNo);
            //strAlarmInfoBuild.Append("+remoteCheck:" + alarmInfo.AccessControllerEvent.remoteCheck);
            //strAlarmInfoBuild.Append("+serialNo:" + alarmInfo.AccessControllerEvent.serialNo);
            //strAlarmInfoBuild.Append("+accessChannel:" + alarmInfo.AccessControllerEvent.accessChannel);
            //strAlarmInfoBuild.Append("+deviceNo:" + alarmInfo.AccessControllerEvent.deviceNo);
            //strAlarmInfoBuild.Append("+netUser:" + alarmInfo.AccessControllerEvent.netUser);
            //strAlarmInfoBuild.Append("+remoteHostAddr:" + alarmInfo.AccessControllerEvent.remoteHostAddr);
            //strAlarmInfoBuild.Append("+ipv6Address:" + alarmInfo.ipv6Address);
            //strAlarmInfoBuild.Append("+portNo:" + alarmInfo.portNo);
            //strAlarmInfoBuild.Append("+protocol:" + alarmInfo.protocol);
            //strAlarmInfoBuild.Append("+macAddress:" + alarmInfo.macAddress);
            //strAlarmInfoBuild.Append("+channelID:" + alarmInfo.channelID);
            //strAlarmInfoBuild.Append("+eventState:" + alarmInfo.eventState);
            //strAlarmInfoBuild.Append("+eventDescription:" + alarmInfo.eventDescription);

        }

        private void btnDeploy_Click(object sender, EventArgs e)
        {
            if (!bIsAlarmStart)
            {
                int iListenPort = Convert.ToInt32(this.port_textBox.Text);
                if(ISAPI_StartListen(new string[] { "http://" + this.ip_textBox.Text + ":" + iListenPort.ToString() + "/" }))
                {
                    this.port_textBox.Enabled = false;
                    this.ip_textBox.Enabled = false;
                    bIsAlarmStart = true;
                    btnListen.Text = "Stop";
                }
            }
            else
            {
                ISAPI_StopListen();
                this.port_textBox.Enabled = true;
                this.ip_textBox.Enabled = true;
                bIsAlarmStart = false;
                btnListen.Text = "Start";
            }
        }

        public bool ISAPI_StartListen(string[] prefixes)
        {
            try
            {
                if (!HttpListener.IsSupported)
                {
                    MessageBox.Show("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                    return false;
                }
                // URI prefixes are required,          
                if (prefixes == null || prefixes.Length == 0)
                    throw new ArgumentException("prefixes");

                // Add the prefixes.      
                foreach (string s in prefixes)
                {
                    listener.Prefixes.Add(s);
                }
                listener.Start();

                listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Start Listen Failed!" + ex.Message.ToString(), "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        public void ListenerCallback(IAsyncResult result)
        {
            try
            {
                HttpListener listener = (HttpListener)result.AsyncState;
                // Call EndGetContext to complete the asynchronous operation.
                HttpListenerContext context = listener.EndGetContext(result);
                HttpListenerRequest request = context.Request;
                // && request.RawUrl.Contains(request.UserHostAddress)
                if (request.HttpMethod == "POST")
                {
                    // Obtain a response object.
                    context.Response.StatusCode = 200;

                    //deal post 
                    Stream stream = context.Request.InputStream;
                    //System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.UTF8);
                    //String body = reader.ReadToEnd();

                    List<byte> byteList = new List<byte>();
                    if (request.ContentType.ToLower().Contains("multipart"))
                    {
                        //表单类型 造个假HTTP头，便于后面解析boundary
                        byteList.AddRange(Encoding.Default.GetBytes("Content-Type: " + request.ContentType + "\r\n\r\n"));
                    }

                    byte[] buffer = new byte[10240];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        while (true)
                        {
                            int read = stream.Read(buffer, 0, buffer.Length);
                            if (read <= 0)
                            {
                                break;
                            }
                            ms.Write(buffer, 0, read);
                        }
                        byteList.AddRange(ms.ToArray());
                    }

                    byte[] streamBytes = byteList.ToArray();
                    ParseAlarmData(streamBytes);
                    //m_SyncContext.Post(SetTextSafePost, streamBytes);
                    context.Response.Close();
                }

                listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message.ToString();
                MessageBox.Show("ListenerCallback exception: " + errorMsg);
            }
        }

        public void ISAPI_StopListen()
        {
            try
            {
                listener.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Stop Listen Failed! " + ex.Message.ToString());
            }
        }

        private void listViewAlarmInfo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewAlarmInfo.SelectedItems.Count > 0)
            {
                szShowData = null;
                szShowData = this.listViewAlarmInfo.FocusedItem.SubItems[2].Text.ToString();
                if (picIndex1.Image != null)
                {
                    picIndex1.Image.Dispose();
                    picIndex1.Image = null;
                }
                if (picIndex2.Image != null)
                {
                    picIndex2.Image.Dispose();
                    picIndex2.Image = null;
                }

                if (szShowData != null)
                {
                    if (szShowData.Contains(" TemperaturePic: \n"))
                    {
                        ShowTemperaturePic(szShowData);
                        ShowOtherData();
                    }
                    if (szShowData.Contains(" ACSPic: \n"))
                    {
                        //解析图片
                        ShowAcsPic(szShowData);
                        ShowOtherData();
                    }
                    else
                    {
                        ShowOtherData();
                    }
                }
            }
        }

        private void ShowAcsPic(string szShowData)
        {
            string ACSPicStr = " ACSPic: \n";
            try
            {
                string strFirstImagePath = MidStrEx(szShowData, ACSPicStr, ";");
                picIndex1.Image = Image.FromFile(strFirstImagePath);

                string steCheckSecondImage = strFirstImagePath + "; " + ACSPicStr;
                if (szShowData.Contains(steCheckSecondImage))
                {
                    string strSecondImagePath = MidStrEx(szShowData, steCheckSecondImage, ";");
                    picIndex2.Image = Image.FromFile(strSecondImagePath);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "Fail to Get Picture", MessageBoxButtons.OK);
            }
        }

        private void ShowTemperaturePic(string szShowData)
        {
            string TemperaturePicStr = " TemperaturePic: \n";
            try
            {
                picIndex1.Image = Image.FromFile(MidStrEx(szShowData, TemperaturePicStr, ";"));
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "Fail to Get Picture", MessageBoxButtons.OK);
            }
        }

        private void ShowOtherData()
        {
            textBoxTime.Text = listViewAlarmInfo.FocusedItem.SubItems[1].Text.ToString();

            textBoxEmployeeNo.Text = null;
            textBoxEmployeeNo.Text = MidStrEx(szShowData, "employeeNo:", ";");
            textBoxCardNo.Text = null;
            textBoxCardNo.Text = MidStrEx(szShowData, "cardNo:", ";");
        }

        //本函数用于截取字符串
        public string MidStrEx(string sourse, string startstr, string endstr)
        {
            string result = string.Empty;
            int startindex, endindex;
            try
            {
                startindex = sourse.IndexOf(startstr);
                if (startindex == -1)
                    return result;
                string tmpstr = sourse.Substring(startindex + startstr.Length);
                endindex = tmpstr.IndexOf(endstr);
                if (endindex == -1)
                    return result;
                result = tmpstr.Remove(endindex);
            }
            catch (Exception e)
            {
                MessageBox.Show("MidStrEx Err:" + e.Message);
            }
            return result;
        }

        private void listViewAlarmInfo_DoubleClick(object sender, EventArgs e)
        {
            if (listViewAlarmInfo.SelectedItems.Count > 0)
            {
                string ShowData = this.listViewAlarmInfo.FocusedItem.SubItems[2].Text.ToString();
                string RemoteCheck = MidStrEx(ShowData, "remoteCheck:", "+");
                if (RemoteCheck.ToLower().Equals("true"))
                {
                    DialogResult dr = MessageBox.Show("RemoteCheck success?", "remoteCheck", MessageBoxButtons.YesNo);
                    if (dr == DialogResult.Yes)
                    {
                        int serialNo = Convert.ToInt32(MidStrEx(ShowData, "serialNo:", "+"));
                        string strInput = "{\"RemoteCheck\":{\"serialNo\":" + serialNo + ",\"checkResult\":\"success\",\"info\":\"Hello, this is a test message!\"}}";
                        string strUrl = "/ISAPI/AccessControl/remoteCheck?format=json";

                        client.Credentials = new NetworkCredential(AddDevice.struDeviceInfo.strUsername, AddDevice.struDeviceInfo.strPassword);
                        client.BaseAddress = "http://" + AddDevice.struDeviceInfo.strDeviceIP + ":" + AddDevice.struDeviceInfo.strHttpPort;
                        client.Headers.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
                        byte[] responseData = client.UploadData(strUrl, "PUT", System.Text.Encoding.Default.GetBytes(strInput));
                        if (responseData != null)
                        {
                            string strRes = Encoding.UTF8.GetString(responseData);
                            if (strRes.ToUpper().Contains("OK"))
                            {
                                //MessageBox.Show("advanced config set succ!");
                            }
                        }
                    }
                    else if (dr == DialogResult.No)
                    {
                        int serialNo = Convert.ToInt32(MidStrEx(ShowData, "serialNo:", "+"));
                        string strInput = "{\"RemoteCheck\":{\"serialNo\":" + serialNo + ",\"checkResult\":\"failed\",\"info\":\"Hello, RemoteCheck failed!\"}}";
                        string strUrl = "/ISAPI/AccessControl/remoteCheck?format=json";

                        client.Credentials = new NetworkCredential(AddDevice.struDeviceInfo.strUsername, AddDevice.struDeviceInfo.strPassword);
                        client.BaseAddress = "http://" + AddDevice.struDeviceInfo.strDeviceIP + ":" + AddDevice.struDeviceInfo.strHttpPort;
                        client.Headers.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
                        byte[] responseData = client.UploadData(strUrl, "PUT", System.Text.Encoding.Default.GetBytes(strInput));
                        if (responseData != null)
                        {
                            string strRes = Encoding.UTF8.GetString(responseData);
                            if (strRes.ToUpper().Contains("OK"))
                            {
                                //MessageBox.Show("advanced config set succ!");
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show(this.listViewAlarmInfo.FocusedItem.SubItems[2].Text.ToString(), "Alarm Info");
                }
            }
        }

        private void comboBoxLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxLanguage.Text != null)
            {
                MultiLanguage.SetDefaultLanguage(comboBoxLanguage.Text);
                foreach (Form form in Application.OpenForms)
                {
                    MultiLanguage.LoadLanguage(form);
                }

                if (comboBoxLanguage.Text == "English")
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
                }
                else if (comboBoxLanguage.Text == "Chinese")
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FormNotifyHostsConfiguration form = new FormNotifyHostsConfiguration();
            form.Show();
        }

    }
}
