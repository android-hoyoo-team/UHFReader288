using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
namespace UHFReaderModule
{
    public delegate void ReciveDataCallback(string temp);
    public class Reader
    {
        private SerialPort serialPort1;
        ushort POLYNOMIAL = 0x8408;
        ushort PRESET_VALUE = 0xffff;
        byte[] RecvBuff = new byte[8000];
        byte[] SendBuff = new byte[300];
        int RecvLength = 0;
        public string value = "";
        TcpClient client;
        Stream streamToTran;
        int COM_TYPE = -1;
        int inventoryScanTime = 0;
        public ReciveDataCallback ReceiveCallback;
        byte fComAddr = 0;
       // public bool isStopInventory=false;
        public Reader()
        {
            serialPort1 = new SerialPort();
            //serialPort1.ReadBufferSize = 50000;
        }
        /// <summary>
        /// GetCRC
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="ADataLen"></param>
        #region 
        private void GetCRC(byte[] pData, int ADataLen)
        {
            int i, j;
            ushort current_crc_value;
            current_crc_value = PRESET_VALUE;
            for (i = 0; i <= ADataLen - 1; i++)
            {
                current_crc_value = (ushort)(current_crc_value ^ (pData[i]));
                for (j = 0; j < 8; j++)
                {
                    if ((current_crc_value & 0x0001) != 0)
                    {
                        current_crc_value = (ushort)((current_crc_value >> 1) ^ POLYNOMIAL);
                    }
                    else
                    {
                        current_crc_value = (ushort)(current_crc_value >> 1);
                    }
                }
            }
            pData[i++] = (byte)(current_crc_value & 0x000000ff);
            pData[i] = (byte)((current_crc_value >> 8) & 0x000000ff);
        }

        #endregion
        /// <summary>
        /// CheckCRC
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        #region 
        private int CheckCRC(byte[] pData, int len)
        {
            GetCRC(pData, len);
            if ((pData[len + 1] == 0) && (pData[len] == 0))
                return 0;
            else
                return 0x31;
        }
        #endregion
        /// <summary>
        /// 16进制数组字符串转换
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        #region
        private byte[] HexStringToByteArray(string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;
        }

        private string ByteArrayToHexString(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            foreach (byte b in data)
                sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
            return sb.ToString().ToUpper();

        }
        #endregion

        /// <summary>
        /// OpenCom
        /// </summary>
        /// <param name="Port"></param>
        /// <param name="fbaud"></param>
        /// <returns></returns>
        #region 
        private int OpenCom(int Port, byte fbaud)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
            try
            {
                serialPort1.PortName = "com" + Port.ToString();
                int nBaudrate = 0;
                switch (fbaud)
                {
                    case 0:
                        nBaudrate = 9600;
                        break;
                    case 1:
                        nBaudrate = 19200;
                        break;
                    case 2:
                        nBaudrate = 38400;
                        break;
                    case 3:
                        nBaudrate = 57600;
                        break;
                    case 4:
                        nBaudrate = 115200;
                        break;
                    default:
                        nBaudrate = 57600;
                        break;
                }
                serialPort1.BaudRate = nBaudrate;
                serialPort1.ReadTimeout = 200;
                serialPort1.Open();
                return 0;
            }
            catch (System.Exception ex)
            {
                ex.ToString();
                return 0x30;
            }
        }
        #endregion

        /// <summary>
        /// OpenByCom
        /// </summary>
        /// <param name="Port"></param>
        /// <param name="ComAddr"></param>
        /// <param name="Baud"></param>
        /// <returns></returns>
        #region 
        public int OpenByCom(int Port, ref byte ComAddr, byte Baud)
        {
            if (OpenCom(Port, Baud) == 0)
            {
                COM_TYPE = 0;
                byte addr = ComAddr;
                byte[] Verion = new byte[2];
                byte Model = 0;
                byte SupProtocol = 0;
                byte dmaxfre = 0, dminfre = 0, power = 0, ScanTime = 0, Ant = 0, BeepEn = 0, OutputRep = 0, CheckAnt = 0;
                int result = GetReaderInformation(ref addr, Verion, ref Model, ref SupProtocol, ref dmaxfre, ref dminfre, ref power, ref ScanTime, ref Ant, ref BeepEn, ref OutputRep, ref CheckAnt);
                if (result == 0)
                {
                    ComAddr = addr;
                    fComAddr = addr;
                    return (0);
                }
                else
                {
                    serialPort1.Close();
                    COM_TYPE = -1;
                    return 0x30;
                }

            }
            else
            {
                return 0x30;
            }
        }

        #endregion

        /// <summary>
        /// CloseByCom
        /// </summary>
        /// <returns></returns>
        #region 
        public int CloseByCom()
        {
            try
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.Close();
                    COM_TYPE = -1;
                    return 0;
                }
                else
                {
                    return 0x30;
                }
            }
            catch (System.Exception ex)
            {
                ex.ToString();
                return 0x30;
            }
        }
        #endregion


        /// <summary>
        /// OpenNet
        /// </summary>
        /// <param name="ipAddr"></param>
        /// <param name="Port"></param>
        /// <returns></returns>
        #region 
        private int OpenNet(string ipAddr, int Port)
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse(ipAddr);
                client = new TcpClient();
                client.Connect(ipAddress, Port);
                streamToTran = client.GetStream();    // 获取连接至远程的
                streamToTran.ReadTimeout = 1000;
                return 0;
            }
            catch (System.Exception ex)
            {
                ex.ToString();
                return 0x30;
            }
        }
        #endregion

        /// <summary>
        /// OpenByTcp
        /// </summary>
        /// <param name="ipAddr"></param>
        /// <param name="Port"></param>
        /// <param name="ComAddr"></param>
        /// <returns></returns>
        #region 
        public int OpenByTcp(string ipAddr, int Port, ref byte ComAddr)
        {
            if (OpenNet(ipAddr, Port) == 0)
            {
                COM_TYPE = 1;
                byte addr =0;
                byte[] Verion = new byte[2];
                byte Model = 0;
                byte SupProtocol = 0;
                byte dmaxfre = 0, dminfre = 0, power = 0, ScanTime = 0, Ant = 0, BeepEn = 0, OutputRep = 0, CheckAnt = 0;
                int result = GetReaderInformation(ref addr, Verion, ref Model, ref SupProtocol, ref dmaxfre, ref dminfre, ref power, ref ScanTime, ref Ant, ref BeepEn, ref OutputRep, ref CheckAnt);
                if (result == 0)
                {
                   // addr = 119;
                    ComAddr = addr;
                    fComAddr = addr;
                    return (0);
                }
                else
                {
                    if (streamToTran != null)
                        streamToTran.Dispose();
                    if (client != null)
                        client.Close();
                    COM_TYPE = -1;
                    return 0x30;
                }

            }
            else
            {
                return 0x30;
            }
        }
        #endregion

        /// <summary>
        /// CloseByTcp
        /// </summary>
        /// <returns></returns>
        #region 
        public int CloseByTcp()
        {
            try
            {
                if (streamToTran != null)
                    streamToTran.Dispose();
                if (client != null)
                    client.Close();
                COM_TYPE = -1;
                return 0;
            }
            catch (System.Exception ex)
            {
                ex.ToString();
                return 0x30;
            }
        }
        #endregion


        /// <summary>
        /// SendDataToPort
        /// </summary>
        /// <param name="dataToSend"></param>
        /// <param name="BytesOfSend"></param>
        /// <returns></returns>
        #region 
        private int SendDataToPort(byte[] dataToSend, int BytesOfSend)
        {
            RecvLength = 0;
            Array.Clear(RecvBuff, 0, 8000);
            if (COM_TYPE == 0)
            {
                try
                {
                    serialPort1.DiscardInBuffer();//接受
                    serialPort1.DiscardOutBuffer();//发送
                    serialPort1.Write(dataToSend, 0, BytesOfSend);
                    return 0;
                }
                catch 
                {
                    return 0x30;
                }
            }
            else
            {
                try
                {
                    lock (streamToTran)
                    {
                        streamToTran.Flush();
                        streamToTran.Write(dataToSend, 0, BytesOfSend);
                        return 0;
                    }
                }
                catch
                {
                    return 0x30;
                }
            }

        }
        #endregion

        private long timeInitial = 0;
        /// <summary>
        /// GetDataFromPort
        /// </summary>
        /// <param name="TYPE_RECV"></param>
        /// <returns></returns>
        #region 
        private int GetDataFromPort(int TYPE_RECV)
        {
            int recvTimeOut;
            if (TYPE_RECV == 0)
                recvTimeOut = 1000;
            else
                recvTimeOut = 5000;
            timeInitial = System.Environment.TickCount;
            int temp = 0;
            int strlen = 0;
            byte[] inbuff_1 = new byte[300];
            if (COM_TYPE == 0)
            {
                while (System.Environment.TickCount - timeInitial < recvTimeOut)
                {
                    temp = serialPort1.BytesToRead;
                    if (temp > 0)
                    {
                        int count = serialPort1.Read(inbuff_1, 0, temp);
                        if (count > 0)
                        {
                            Array.Copy(inbuff_1, 0, RecvBuff, strlen, count);
                            strlen += count;
                            if ((RecvBuff[0] + 1 == strlen) && (RecvBuff[0] > 3))
                            {
                                RecvLength = strlen;
                                return 0;
                            }
                        }
                    }
                }
            }
            else
            {
                while (System.Environment.TickCount - timeInitial < recvTimeOut)
                {
                    try
                    {
                        int nLenRead = streamToTran.Read(inbuff_1, 0, inbuff_1.Length);
                        if (nLenRead == 0)
                        {
                            continue;
                        }
                        else
                        {
                            Array.Copy(inbuff_1, 0, RecvBuff, strlen, nLenRead);
                            strlen += nLenRead;
                            if ((RecvBuff[0] + 1 == strlen) && (RecvBuff[0] > 3))
                            {
                                RecvLength = strlen;
                                return 0;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                       Console.WriteLine(ex.StackTrace);
                        ex.ToString();
                        break;
                    }
                }
            }
            return (0x30);
        }
        #endregion
        /// <summary>
        /// /GetReaderInformation
        /// </summary>
        /// <param name="address"></param>
        /// <param name="VersionInfo"></param>
        /// <param name="ReaderType"></param>
        /// <param name="TrType"></param>
        /// <param name="dmaxfre"></param>
        /// <param name="dminfre"></param>
        /// <param name="powerdBm"></param>
        /// <param name="ScanTime"></param>
        /// <param name="Ant"></param>
        /// <param name="BeepEn"></param>
        /// <param name="OutputRep"></param>
        /// <param name="CheckAnt"></param>
        /// <returns></returns>
        #region 
        public int GetReaderInformation(ref byte address, byte[] VersionInfo, ref byte ReaderType, ref byte TrType, ref byte dmaxfre,
                                              ref byte dminfre, ref byte powerdBm, ref byte ScanTime, ref byte Ant, ref byte BeepEn,
                                              ref byte OutputRep, ref byte CheckAnt)//最大询查时间 
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = address;
            SendBuff[2] = 0x21;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x21)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            address = RecvBuff[1];
                            Array.Copy(RecvBuff, 4, VersionInfo, 0, 2);
                            ReaderType = RecvBuff[6];
                            TrType = RecvBuff[7];
                            dmaxfre = RecvBuff[8];
                            dminfre = RecvBuff[9];
                            powerdBm = RecvBuff[10];
                            ScanTime = RecvBuff[11];
                            inventoryScanTime = RecvBuff[11];
                            Ant = RecvBuff[12];
                            BeepEn = RecvBuff[13];
                            OutputRep = RecvBuff[14];
                            CheckAnt = RecvBuff[15];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
#endregion
        
        private int GetInventoryG2()
        {
            bool revFrameEnd = false;
            int BEGTime = System.Environment.TickCount;
            int cmdTime = 0;
            int nCount;
            bool flagheadok = false;
            byte revok = 0;         // 接收状态 0-未接收完成；1-已接收完成未通过校验；2-已接收完成并通过校验
            int revpointer;
            try
            {
                if (COM_TYPE == 0)
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 200 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    serialPort1.Read(btAryBuffer, 0, 1);
                                    revpointer = 1;////////////////////////////
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        serialPort1.Read(btAryBuffer, revpointer, nCount);
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        ////添加一个长度命令字的判断
                                        serialPort1.Read(btAryBuffer, revpointer, btAryBuffer[0] + 1 - revpointer);
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成
                        
                        if (revok == 2)      // 接收成功并crc正确
                        {
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03))
                            {
                                byte Ant = btAryBuffer[4];
                                int num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte epclen = epclist[m];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 1, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 1];
                                        m = m + epclen + 2;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "1:" + Ant.ToString() + "," + sEPC + "," + RSSI.ToString() + " ";
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }
                            }
                            else if (btAryBuffer[3] == 0x26)
                            {
                                int tagrate = btAryBuffer[5] * 256 + btAryBuffer[6];
                                byte[] timb = new byte[4];
                                Array.Copy(btAryBuffer, 7, timb, 0, 4);
                                string stim = ByteArrayToHexString(timb);
                                int totalnum = Convert.ToInt32(stim, 16);
                                cmdTime = System.Environment.TickCount - BEGTime;
                                string Temp = "0:" + tagrate.ToString() + "," + totalnum.ToString() + "," + cmdTime.ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                            if ((btAryBuffer[3] == 0xFB) || (btAryBuffer[3] == 0xF8) || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                        }
                        else if (revok == 0)
                        {
                            return 0x30;
                        }
                    }
                  //  MessageBox.Show("111111111");
                    #endregion
                }
                else
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = streamToTran.Read(btAryBuffer, 0, 1);
                                if (nCount > 0)
                                {
                                    revpointer = 1;
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                int len = btAryBuffer[0] + 1 - revpointer;
                                nCount = streamToTran.Read(btAryBuffer, revpointer, len);
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成

                        if (revok == 2)      // 接收成功并crc正确
                        {
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03))
                            {
                                byte Ant = btAryBuffer[4];
                                int num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte epclen = epclist[m];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 1, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 1];
                                        m = m + epclen + 2;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "1:" + Ant.ToString() + "," + sEPC + "," + RSSI.ToString() + " ";
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }

                            }
                            else if (btAryBuffer[3] == 0x26)
                            {
                                int tagrate = btAryBuffer[5] * 256 + btAryBuffer[6];
                                byte[] timb = new byte[4];
                                Array.Copy(btAryBuffer, 7, timb, 0, 4);
                                string stim = ByteArrayToHexString(timb);
                                int totalnum = Convert.ToInt32(stim, 16);
                                cmdTime = System.Environment.TickCount - BEGTime;
                                string Temp = "0:" + tagrate.ToString() + "," + totalnum.ToString() + "," + cmdTime.ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                            if ((btAryBuffer[3] == 0xF8) || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                        }
                    }
                    #endregion
                }
            }
            catch
            {
                ;
            }
            return 0x30;
        }
        private int GetInventoryG1()
        {
            try
            {
                bool revFrameEnd = false;
                int BEGTime = System.Environment.TickCount;
                //int cmdTime = 0;
                int nCount;
                bool flagheadok = false;
                byte revok = 0;         // 接收状态 0-未接收完成；1-已接收完成未通过校验；2-已接收完成并通过校验
                int revpointer;
                if (COM_TYPE == 0)
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    serialPort1.Read(btAryBuffer, 0, 1);
                                    revpointer = 1;
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        serialPort1.Read(btAryBuffer, revpointer, nCount);
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        serialPort1.Read(btAryBuffer, revpointer, btAryBuffer[0] + 1 - revpointer);
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成

                        if (revok == 2)      // 接收成功并crc正确
                        {
                            int num = 0;
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03) || (btAryBuffer[3] == 0x04))
                            {
                                byte Ant = btAryBuffer[4];
                                num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte epclen = epclist[m];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 1, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 1];
                                        m = m + epclen + 2;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "1:" + Ant.ToString() + "," + sEPC + "," + RSSI.ToString() + " ";
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }

                            }
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0xFB) || (btAryBuffer[3] == 0xF8)
                                || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                               
                                return 0;
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = streamToTran.Read(btAryBuffer, 0, 1);
                                if (nCount > 0)
                                {
                                    revpointer = 1;
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                int len = btAryBuffer[0] + 1 - revpointer;
                                nCount = streamToTran.Read(btAryBuffer, revpointer, len);
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成

                        if (revok == 2)      // 接收成功并crc正确
                        {
                            int num = 0;
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03) || (btAryBuffer[3] == 0x04))
                            {
                                byte Ant = btAryBuffer[4];
                                num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte epclen = epclist[m];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 1, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 1];
                                        m = m + epclen + 2;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "1:" + Ant.ToString() + "," + sEPC + "," + RSSI.ToString() + " ";
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }

                            }
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0xFB) || (btAryBuffer[3] == 0xF8)
                                || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                        }
                    }
                    #endregion
                }
            }
            catch 
            {
                ;
            }
            return 0x30;
        }
       // public event EventHandler OnScan;
        /// <summary>
        /// Inventory_G2
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="QValue"></param>
        /// <param name="Session"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="MaskFlag"></param>
        /// <param name="AdrTID"></param>
        /// <param name="LenTID"></param>
        /// <param name="TIDFlag"></param>
        /// <param name="Target"></param>
        /// <param name="InAnt"></param>
        /// <param name="Scantime"></param>
        /// <param name="FastFlag"></param>
        /// <returns></returns>
        #region 
        public int Inventory_G2(ref byte ComAdr, byte QValue, byte Session, byte MaskMem, byte[] MaskAdr, byte MaskLen,
                                byte[] MaskData, byte MaskFlag, byte AdrTID, byte LenTID, byte TIDFlag, byte Target, byte InAnt,
                                byte Scantime, byte FastFlag)//byte[] pEPCList,ref byte Ant,ref int Totallen,ref int CardNum
        {
            SendBuff[1] = ComAdr;
            SendBuff[2] = 0x01;
            SendBuff[3] = QValue;
            SendBuff[4] = Session;
            if (MaskFlag == 1)
            {
                int len = 0;
                SendBuff[5] = MaskMem;
                Array.Copy(MaskAdr, 0, SendBuff, 6, 2);
                SendBuff[8] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 9, len);
                if (TIDFlag == 1)
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[len + 9] = AdrTID;
                        SendBuff[len + 10] = LenTID;
                        SendBuff[len + 11] = Target;
                        SendBuff[len + 12] = InAnt;
                        SendBuff[len + 13] = Scantime;
                        SendBuff[0] = Convert.ToByte(15 + len);
                    }
                    else
                    {
                        SendBuff[len + 9] = AdrTID;
                        SendBuff[len + 10] = LenTID;
                        SendBuff[0] = Convert.ToByte(12 + len);
                    }
                }
                else
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[len + 9] = Target;
                        SendBuff[len + 10] = InAnt;
                        SendBuff[len + 11] = Scantime;
                        SendBuff[0] = Convert.ToByte(13 + len);
                    }
                    else
                    {
                        SendBuff[0] = Convert.ToByte(10 + len);
                    }
                }
            }
            else
            {
                if (TIDFlag == 1)
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[5] = AdrTID;
                        SendBuff[6] = LenTID;
                        SendBuff[7] = Target;
                        SendBuff[8] = InAnt;
                        SendBuff[9] = Scantime;
                        SendBuff[0] = 0x0B;
                    }
                    else
                    {
                        SendBuff[5] = AdrTID;
                        SendBuff[6] = LenTID;
                        SendBuff[0] = 0x08;
                    }
                }
                else
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[5] = Target;
                        SendBuff[6] = InAnt;
                        SendBuff[7] = Scantime;
                        SendBuff[0] = 0x09;
                    }
                    else
                    {
                        SendBuff[0] = 0x06;
                    }
                }
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            if ((QValue & 0x80) == 0x80)//带时间返回
            {
                return GetInventoryG2();
            }
            else
            {
                return GetInventoryG1();
            }
        }
        #endregion

        /// <summary>
        /// SetAddress
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="aNewComAdr"></param>
        /// <returns></returns>
        #region 
        public int SetAddress(ref byte fComAdr, byte aNewComAdr)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x24;
            SendBuff[3] = aNewComAdr;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x24)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// SetRegion
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="dmaxfre"></param>
        /// <param name="dminfreint"></param>
        /// <returns></returns>
        #region 
        public int SetRegion(ref byte fComAdr, byte dmaxfre, byte dminfreint)
        {
            int result = 0x30;
            SendBuff[0] = 0x06;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x22;
            SendBuff[3] = dmaxfre;
            SendBuff[4] = dminfreint;
            GetCRC(SendBuff, 5);
            SendDataToPort(SendBuff, 7);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x22)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// SetInventoryScanTime
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="ScanTime"></param>
        /// <returns></returns>
        #region 
        public int SetInventoryScanTime(ref byte fComAdr, byte ScanTime)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x25;
            SendBuff[3] = ScanTime;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x25)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetBaudRate
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="baud"></param>
        /// <returns></returns>
        #region 
        public int SetBaudRate(ref byte fComAdr, byte baud)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x28;
            SendBuff[3] = baud;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x28)
                    {
                        fComAdr = RecvBuff[1];
                        int nBaudrate = 0;
                        switch (baud)
                        {
                            case 0:
                                nBaudrate = 9600;
                                break;
                            case 1:
                                nBaudrate = 19200;
                                break;
                            case 2:
                                nBaudrate = 38400;
                                break;
                            case 5:
                                nBaudrate = 57600;
                                break;
                            case 6:
                                nBaudrate = 115200;
                                break;
                            default:
                                nBaudrate = 57600;
                                break;
                        }
                        serialPort1.BaudRate = nBaudrate;
                        serialPort1.Close();
                        serialPort1.Open();
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// SetRfPower
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="powerDbm"></param>
        /// <returns></returns>
        #region 
        public int SetRfPower(ref byte fComAdr, byte powerDbm)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x2F;
            SendBuff[3] = powerDbm;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x2F)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// BuzzerAndLEDControl
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="AvtiveTime"></param>
        /// <param name="SilentTime"></param>
        /// <param name="Times"></param>
        /// <returns></returns>
        #region 
        public int BuzzerAndLEDControl(ref byte fComAdr, byte AvtiveTime, byte SilentTime, byte Times)
        {
            int result = 0x30;
            SendBuff[0] = 0x07;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x33;
            SendBuff[3] = AvtiveTime;
            SendBuff[4] = SilentTime;
            SendBuff[5] = Times;
            GetCRC(SendBuff, 6);
            SendDataToPort(SendBuff, 8);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x33)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
        
        /// <summary>
        /// SetAntennaMultiplexing
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Ant"></param>
        /// <returns></returns>
        #region 
        public int SetAntennaMultiplexing(ref byte fComAdr, byte Ant)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x3F;
            SendBuff[3] = Ant;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x3F)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// SetBeepNotification
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="BeepEn"></param>
        /// <returns></returns>
        #region 
        public int SetBeepNotification(ref byte fComAdr, byte BeepEn)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x40;
            SendBuff[3] = BeepEn;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x40)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetGPIO
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="OutputPin"></param>
        /// <returns></returns>
        #region 
        public int SetGPIO(ref byte fComAdr, byte OutputPin)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x46;
            SendBuff[3] = OutputPin;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x46)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion
        
        /// <summary>
        /// GetGPIOStatus
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="OutputPin"></param>
        /// <returns></returns>
        #region 
        public int GetGPIOStatus(ref byte fComAdr, ref byte OutputPin)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x47;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x47)
                    {
                        fComAdr = RecvBuff[1];
                        OutputPin = RecvBuff[4];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
     
        /// <summary>
        /// GetSeriaNo
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="SeriaNo"></param>
        /// <returns></returns>
        #region 
        public int GetSeriaNo(ref byte fComAdr, byte[] SeriaNo)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x4C;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x4C)
                    {
                        fComAdr = RecvBuff[1];
                        Array.Copy(RecvBuff, 4, SeriaNo, 0, 4);
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
       
        /// <summary>
        /// SetCheckAnt
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="CheckAnt"></param>
        /// <returns></returns>
        #region 
        public int SetCheckAnt(ref byte fComAdr, byte CheckAnt)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x66;
            SendBuff[3] = CheckAnt;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x66)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// SetWorkMode
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Read_mode"></param>
        /// <returns></returns>
        #region 
        public int SetWorkMode(ref byte fComAdr, byte Read_mode)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x35;
            SendBuff[3] = Read_mode;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x35)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
       
        /// <summary>
        /// GetSystemParameter
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Read_mode"></param>
        /// <param name="Accuracy"></param>
        /// <param name="RepCondition"></param>
        /// <param name="RepPauseTime"></param>
        /// <param name="ReadPauseTim"></param>
        /// <param name="TagProtocol"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="TriggerTime"></param>
        /// <param name="AdrTID"></param>
        /// <param name="LenTID"></param>
        /// <returns></returns>
        #region 
        public int GetSystemParameter(ref byte fComAdr, ref byte Read_mode, ref byte Accuracy, ref byte RepCondition, ref byte RepPauseTime,
                                      ref byte ReadPauseTim, ref byte TagProtocol, ref byte MaskMem, byte[] MaskAdr, ref byte MaskLen, byte[] MaskData,
                                      ref byte TriggerTime, ref byte AdrTID, ref byte LenTID)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x36;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x36)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            Read_mode = RecvBuff[4];
                            Accuracy = RecvBuff[5];
                            RepCondition = RecvBuff[6];
                            RepPauseTime = RecvBuff[7];
                            ReadPauseTim = RecvBuff[8];
                            TagProtocol = RecvBuff[9];
                            MaskMem = RecvBuff[10];
                            MaskAdr[0] = RecvBuff[11];
                            MaskAdr[1] = RecvBuff[12];
                            MaskLen = RecvBuff[13];
                            Array.Copy(RecvBuff, 14, MaskData, 0, 32);
                            if (RecvBuff[0] == 0x2F)
                            {
                                TriggerTime = 0;//兼容1.2以下版本
                            }
                            else
                            {
                                TriggerTime = RecvBuff[46];
                                AdrTID = RecvBuff[47];
                                LenTID = RecvBuff[48];
                            }
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
        
        /// <summary>
        /// SetEASSensitivity
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Accuracy"></param>
        /// <returns></returns>
        #region 
        public int SetEASSensitivity(ref byte fComAdr, byte Accuracy)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x37;
            SendBuff[3] = Accuracy;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x37)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
        
        /// <summary>
        /// SetMask
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <returns></returns>
        #region 
        public int SetMask(ref byte fComAdr, byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData)
        {
            int result = 0x30;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x3B;
            SendBuff[3] = MaskMem;
            SendBuff[4] = MaskAdr[0];
            SendBuff[5] = MaskAdr[0];
            SendBuff[6] = MaskLen;
            int len = 0;
            if ((MaskLen) % 8 == 0)
            {
                len = (MaskLen) / 8;
            }
            else
            {
                len = (MaskLen) / 8 + 1;
            }
            Array.Copy(MaskData, 0, SendBuff, 7, len);
            SendBuff[0] = Convert.ToByte(8 + len);

            GetCRC(SendBuff, 7 + len);
            SendDataToPort(SendBuff, 9 + len);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x3B)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
        
        /// <summary>
        /// SetResponsePamametersofAuto_runningMode
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="RepCondition"></param>
        /// <param name="RepPauseTime"></param>
        /// <returns></returns>
        #region 
        public int SetResponsePamametersofAuto_runningMode(ref byte fComAdr, byte RepCondition, byte RepPauseTime)
        {
            int result = 0x30;
            SendBuff[0] = 0x06;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x3C;
            SendBuff[3] = RepCondition;
            SendBuff[4] = RepPauseTime;
            GetCRC(SendBuff, 5);
            SendDataToPort(SendBuff, 7);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x3C)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
        
        /// <summary>
        /// SetInventoryInterval
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="ReadPauseTim"></param>
        /// <returns></returns>
        #region 
        public int SetInventoryInterval(ref byte fComAdr, byte ReadPauseTim)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x3D;
            SendBuff[3] = ReadPauseTim;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x3D)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
        
        /// <summary>
        /// SelectTagType
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Protocol"></param>
        /// <returns></returns>
        #region 
        public int SelectTagType(ref byte fComAdr, byte Protocol)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x3E;
            SendBuff[3] = Protocol;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x3E)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
       
        /// <summary>
        /// SetReal_timeClock
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="paramer"></param>
        /// <returns></returns>
        #region 
        public int SetReal_timeClock(ref byte fComAdr, byte[] paramer)
        {
            int result = 0x30;
            SendBuff[0] = 0x0A;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x41;
            Array.Copy(paramer, 0, SendBuff, 3, 6);
            GetCRC(SendBuff, 9);
            SendDataToPort(SendBuff, 11);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x41)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
       
        /// <summary>
        /// GetTime
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="paramer"></param>
        /// <returns></returns>
        #region 
        public int GetTime(ref byte fComAdr, byte[] paramer)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x42;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x42)
                    {
                        fComAdr = RecvBuff[1];
                        Array.Copy(RecvBuff, 4, paramer, 0, 6);
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
        public int GetTagBufferInfo(ref byte fComAdr, byte[] Data,ref int dataLength)
        {
            dataLength = 0;
            return 0;
        }

        /// <summary>
        /// ClearTagBuffer
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <returns></returns>
        #region 
        public int ClearTagBuffer(ref byte fComAdr)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x44;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x44)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion
      
        /// <summary>
        /// SetRelay
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="RelayTime"></param>
        /// <returns></returns>
        #region      
        public int SetRelay(ref byte fComAdr, byte RelayTime)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x45;
            SendBuff[3] = RelayTime;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x45)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion
        
        /// <summary>
        /// SetNotificationPulseOutput
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="OutputRep"></param>
        /// <returns></returns>
        #region 
        public int SetNotificationPulseOutput(ref byte fComAdr, byte OutputRep)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x48;
            SendBuff[3] = OutputRep;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x48)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetTriggerTime
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="TriggerTime"></param>
        /// <returns></returns>
        #region 
        public int SetTriggerTime(ref byte fComAdr, byte TriggerTime)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x49;
            SendBuff[3] = TriggerTime;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x49)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetTIDParameter
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="AdrTID"></param>
        /// <param name="AdrTID"></param>
        /// <returns></returns>
        #region 
        public int SetTIDParameter(ref byte fComAdr, byte AdrTID, byte LenTID)
        {
            int result = 0x30;
            SendBuff[0] = 0x06;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x4A;
            SendBuff[3] = AdrTID;
            SendBuff[4] = LenTID;
            GetCRC(SendBuff, 5);
            SendDataToPort(SendBuff, 7);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x4A)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// ChangeATMode
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="ATMode"></param>
        /// <returns></returns>
        #region 
        public int ChangeATMode(ref byte fComAdr, byte ATMode)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x4D;
            SendBuff[3] = ATMode;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x4D)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// TransparentCMD
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="timeout"></param>
        /// <param name="cmdlen"></param>
        /// <param name="cmddata"></param>
        /// <param name="recvLen"></param>
        /// <param name="recvdata"></param>
        /// <returns></returns>
        #region 
        public int TransparentCMD(ref byte fComAdr, byte timeout, byte cmdlen, byte[] cmddata, ref byte recvLen, byte[] recvdata)
        {
            int result = 0x30;
            SendBuff[0] = Convert.ToByte(0x05 + cmdlen);
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x4E;
            SendBuff[3] = timeout;
            Array.Copy(cmddata, 0, SendBuff, 4, cmdlen);
            GetCRC(SendBuff, 4 + cmdlen);
            SendDataToPort(SendBuff, 6 + cmdlen);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x4E)
                    {
                        if (RecvBuff[3]==0)
                        {
                            fComAdr = RecvBuff[1];
                            recvLen = Convert.ToByte(RecvLength - 6);
                            if (recvLen>0)
                            {
                                Array.Copy(RecvBuff, 4, recvdata, 0, recvLen);
                            }
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetQS
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Qvalue"></param>
        /// <param name="Session"></param>
        /// <returns></returns>
        #region 
        public int SetQS(ref byte fComAdr, byte Qvalue, byte Session)
        {
            int result = 0x30;
            SendBuff[0] = 0x06;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x81;
            SendBuff[3] = Qvalue;
            SendBuff[4] = Session;
            GetCRC(SendBuff, 5);
            SendDataToPort(SendBuff, 7);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x81)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// GetQS
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Qvalue"></param>
        /// <param name="Session"></param>
        /// <returns></returns>
        #region
        public int GetQS(ref byte fComAdr,ref byte Qvalue,ref byte Session)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x82;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x82)
                    {
                        fComAdr = RecvBuff[1];
                        Qvalue = RecvBuff[4];
                        Session = RecvBuff[5];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// GetModuleVersion
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Version"></param>
        /// <returns></returns>
        #region
        public int GetModuleVersion(ref byte fComAdr, byte[] Version)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x83;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x83)
                    {
                        fComAdr = RecvBuff[1];
                        Version[0] = RecvBuff[4];
                        Version[1] = RecvBuff[5];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// SetFlashRom
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <returns></returns>
        #region
        public int SetFlashRom(ref byte fComAdr)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x84;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x84)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// ReadActiveModeData
        /// </summary>
        /// <param name="ScanModeData"></param>
        /// <param name="ValidDatalength"></param>
        /// <returns></returns>
        #region 
        public int ReadActiveModeData(byte[] ScanModeData, ref int ValidDatalength)
        {
            try
            {
                if (COM_TYPE == 0)
                {
                    int m = serialPort1.BytesToRead;
                    if (m > 0)
                    {
                        int count = serialPort1.Read(RecvBuff, 0, m);
                        if (count > 0)
                        { 
                            ValidDatalength = count;
                            Array.Copy(RecvBuff, ScanModeData, count);
                        }
                        return 0;
                    }
                    return 0x0E;
                }
                else
                {
                    int nCount = streamToTran.Read(RecvBuff, 0, 1000);
                    if (nCount > 0)
                    {
                        ValidDatalength = nCount;
                        Array.Copy(RecvBuff, ScanModeData, nCount);
                        return 0;
                    }
                    return 0x0E;
                }
                
            }
            catch (System.Exception ex)
            {
                ex.ToString();
                return 0x30;
            }
        }
        #endregion

        /// <summary>
        /// ReadData_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="ENum"></param>
        /// <param name="Mem"></param>
        /// <param name="WordPtr"></param>
        /// <param name="Num"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="Data"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int ReadData_G2(ref byte fComAdr, byte[] EPC, byte ENum, byte Mem, byte WordPtr, byte Num, byte[] Password,
                               byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, byte[] Data, ref int errorcode)
        {
            int result = 0x30;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x02;
            SendBuff[3] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[4] = Mem;
                SendBuff[5] = WordPtr;
                SendBuff[6] = Num;
                Array.Copy(Password, 0, SendBuff, 7, 4);

                SendBuff[11] = MaskMem;
                SendBuff[12] = MaskAdr[0];
                SendBuff[13] = MaskAdr[1];
                SendBuff[14] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 15, len);
                SendBuff[0] = Convert.ToByte(16 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 4, ENum * 2);
                SendBuff[ENum * 2 + 4] = Mem;
                SendBuff[ENum * 2 + 5] = WordPtr;
                SendBuff[ENum * 2 + 6] = Num;
                Array.Copy(Password, 0, SendBuff, ENum * 2 + 7, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + 12);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x02)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                            Array.Copy(RecvBuff, 4, Data, 0, RecvLength - 6);
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// ExtReadData_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="ENum"></param>
        /// <param name="Mem"></param>
        /// <param name="WordPtr"></param>
        /// <param name="Num"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="Data"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int ExtReadData_G2(ref byte fComAdr, byte[] EPC, byte ENum, byte Mem, byte[] WordPtr, byte Num, byte[] Password,
                               byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, byte[] Data, ref int errorcode)
        {
            int result = 0x30;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x15;
            SendBuff[3] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[4] = Mem;
                SendBuff[5] = WordPtr[0];
                SendBuff[6] = WordPtr[1];
                SendBuff[7] = Num;
                Array.Copy(Password, 0, SendBuff, 8, 4);

                SendBuff[12] = MaskMem;
                SendBuff[13] = MaskAdr[0];
                SendBuff[14] = MaskAdr[1];
                SendBuff[15] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 16, len);
                SendBuff[0] = Convert.ToByte(17 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 4, ENum * 2);
                SendBuff[ENum * 2 + 4] = Mem;
                SendBuff[ENum * 2 + 5] = WordPtr[0];
                SendBuff[ENum * 2 + 6] = WordPtr[1];
                SendBuff[ENum * 2 + 7] = Num;
                Array.Copy(Password, 0, SendBuff, ENum * 2 + 8, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + 13);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x15)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                            Array.Copy(RecvBuff, 4, Data, 0, RecvLength - 6);
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// WriteData_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="WNum"></param>
        /// <param name="ENum"></param>
        /// <param name="Mem"></param>
        /// <param name="WordPtr"></param>
        /// <param name="Wdt"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int WriteData_G2(ref byte fComAdr, byte[] EPC, byte WNum, byte ENum, byte Mem, byte WordPtr, byte[] Wdt, byte[] Password,
                                byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result = 0x30;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x03;
            SendBuff[3] = WNum;
            SendBuff[4] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[5] = Mem;
                SendBuff[6] = WordPtr;
                Array.Copy(Wdt, 0, SendBuff, 7, WNum * 2);
                Array.Copy(Password, 0, SendBuff, 7 + WNum * 2, 4);
                SendBuff[11 + WNum * 2] = MaskMem;
                SendBuff[12 + WNum * 2] = MaskAdr[0];
                SendBuff[13 + WNum * 2] = MaskAdr[1];
                SendBuff[14 + WNum * 2] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 15 + WNum * 2, len);
                SendBuff[0] = Convert.ToByte(16 + WNum * 2 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 5, ENum * 2);
                SendBuff[ENum * 2 + 5] = Mem;
                SendBuff[ENum * 2 + 6] = WordPtr;
                Array.Copy(Wdt, 0, SendBuff, ENum * 2 + 7, WNum * 2);
                Array.Copy(Password, 0, SendBuff, WNum * 2 + ENum * 2 + 7, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + WNum * 2 + 12);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x03)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// ExtWriteData_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="WNum"></param>
        /// <param name="ENum"></param>
        /// <param name="Mem"></param>
        /// <param name="WordPtr"></param>
        /// <param name="Wdt"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int ExtWriteData_G2(ref byte fComAdr, byte[] EPC, byte WNum, byte ENum, byte Mem, byte[] WordPtr, byte[] Wdt, byte[] Password,
                                byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result = 0x30;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x16;
            SendBuff[3] = WNum;
            SendBuff[4] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[5] = Mem;
                SendBuff[6] = WordPtr[0];
                SendBuff[7] = WordPtr[1];
                Array.Copy(Wdt, 0, SendBuff, 8, WNum * 2);
                Array.Copy(Password, 0, SendBuff, 8 + WNum * 2, 4);
                SendBuff[12 + WNum * 2] = MaskMem;
                SendBuff[13 + WNum * 2] = MaskAdr[0];
                SendBuff[14 + WNum * 2] = MaskAdr[1];
                SendBuff[15 + WNum * 2] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 16 + WNum * 2, len);
                SendBuff[0] = Convert.ToByte(17 + WNum * 2 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 5, ENum * 2);
                SendBuff[ENum * 2 + 5] = Mem;
                SendBuff[ENum * 2 + 6] = WordPtr[0];
                SendBuff[ENum * 2 + 7] = WordPtr[1];
                Array.Copy(Wdt, 0, SendBuff, ENum * 2 + 8, WNum * 2);
                Array.Copy(Password, 0, SendBuff, WNum * 2 + ENum * 2 + 8, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + WNum * 2 + 13);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x16)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// WriteEPC_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Password"></param>
        /// <param name="WriteEPC"></param>
        /// <param name="ENum"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int WriteEPC_G2(ref byte fComAdr, byte[] Password, byte[] WriteEPC, byte ENum, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x04;
            SendBuff[3] = ENum;
            Array.Copy(Password, 0, SendBuff,4,4);
            Array.Copy(WriteEPC, 0, SendBuff, 8, ENum*2);
            SendBuff[0] = Convert.ToByte(ENum * 2 + 9);
            GetCRC(SendBuff, ENum * 2 + 8);
            SendDataToPort(SendBuff, ENum * 2 + 10);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x04)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// KillTag_G2
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="ENum"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int KillTag_G2(ref byte fComAdr, byte[] EPC, byte ENum, byte[] Password, byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result = 0x30;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x05;
            SendBuff[3] = ENum;
            if (ENum == 0xFF)
            {
                Array.Copy(Password, 0, SendBuff,4,4);
                SendBuff[8] = MaskMem;
                SendBuff[9] = MaskAdr[0];
                SendBuff[10] = MaskAdr[1];
                SendBuff[11] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 12, len);
                SendBuff[0] = Convert.ToByte(13 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 4, ENum * 2);
                Array.Copy(Password, 0, SendBuff, ENum * 2 + 4, 4);
                SendBuff[0] = Convert.ToByte(9 + ENum * 2);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0]-1);
            SendDataToPort(SendBuff, SendBuff[0]+1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x05)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// Lock_G2
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="ENum"></param>
        /// <param name="select"></param>
        /// <param name="setprotect"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int Lock_G2(ref byte fComAdr, byte[] EPC, byte ENum, byte select, byte setprotect, byte[] Password, byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x06;
            SendBuff[3] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[4] = select;
                SendBuff[5] = setprotect;
                Array.Copy(Password, 0, SendBuff,6,4);
                SendBuff[10] = MaskMem;
                SendBuff[11] = MaskAdr[0];
                SendBuff[12] = MaskAdr[11];
                SendBuff[13] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff,14,len);
                SendBuff[0] = Convert.ToByte(15 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 4, ENum * 2);
                SendBuff[ENum * 2 + 4] = select;
                SendBuff[ENum * 2 + 5] = setprotect;
                Array.Copy(Password, 0, SendBuff, ENum * 2 + 6,4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + 11);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x06)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// BlockErase_G2
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="ENum"></param>
        /// <param name="Mem"></param>
        /// <param name="WordPtr"></param>
        /// <param name="Num"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int BlockErase_G2(ref byte fComAdr, byte[] EPC, byte ENum, byte Mem, byte WordPtr, byte Num, byte[] Password, byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result;
            int len = 0;

            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x07;
            SendBuff[3] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[4] = Mem;
                SendBuff[5] = WordPtr;
                SendBuff[6] = Num;
                Array.Copy(Password, 0, SendBuff, 7, 4);
                SendBuff[11] = MaskMem;
                SendBuff[12] = MaskAdr[0];
                SendBuff[13] = MaskAdr[1];
                SendBuff[14] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 15, len);
                SendBuff[0] = Convert.ToByte(16 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 4, ENum * 2);
                SendBuff[ENum * 2 + 4] = Mem;
                SendBuff[ENum * 2 + 5] = WordPtr;
                SendBuff[ENum * 2 + 6] = Num;
                Array.Copy(Password, 0, SendBuff, ENum * 2 + 7, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + 12);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x07)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetPrivacyByEPC_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="ENum"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int SetPrivacyByEPC_G2(ref byte fComAdr, byte[] EPC, byte ENum,byte[] Password, byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result;
            int len = 0;

            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x08;
            SendBuff[3] = ENum;
            if (ENum == 0xFF)
            {
                Array.Copy(Password, 0, SendBuff, 4, 4);
                SendBuff[8] = MaskMem;
                SendBuff[9] = MaskAdr[0];
                SendBuff[10] = MaskAdr[1];
                SendBuff[11] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 12, len);
                SendBuff[0] = Convert.ToByte(13 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 4, ENum * 2);
                Array.Copy(Password, 0, SendBuff, ENum * 2 + 4, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + 9);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x08)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetPrivacyWithoutEPC_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Password"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int SetPrivacyWithoutEPC_G2(ref byte fComAdr, byte[] Password, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = 0x08;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x09;
            Array.Copy(Password, 0, SendBuff, 3, 4);
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x09)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
       #endregion

        /// <summary>
        /// ResetPrivacy_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Password"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
       #region 
        public int ResetPrivacy_G2(ref byte fComAdr, byte[] Password, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = 0x08;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x0A;
            Array.Copy(Password, 0, SendBuff, 3, 4);
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x0A)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

       #endregion

        /// <summary>
        /// CheckPrivacy_G2
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="readpro"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int CheckPrivacy_G2(ref byte fComAdr, ref byte readpro, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x0B;
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x0B)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                            readpro = RecvBuff[4];
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// EASConfigure_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="ENum"></param>
        /// <param name="Password"></param>
        /// <param name="EAS"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int EASConfigure_G2(ref byte fComAdr, byte[] EPC, byte ENum, byte[] Password, byte EAS, byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x0C;
            SendBuff[3] = ENum;
            if (ENum == 0xFF)
            {
                Array.Copy(Password, 0, SendBuff, 4, 4);
                SendBuff[8] = EAS;
                SendBuff[9] = MaskMem;
                SendBuff[10] = MaskAdr[0];
                SendBuff[11] = MaskAdr[1];
                SendBuff[12] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 13, len);
                SendBuff[0] = Convert.ToByte(14 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 4, ENum * 2);
                Array.Copy(Password, 0, SendBuff, ENum * 2 + 4, 4);
                SendBuff[ENum * 2 + 8] = EAS;
                SendBuff[0] = Convert.ToByte(ENum * 2 + 10);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x0C)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// EASAlarm_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int EASAlarm_G2(ref byte fComAdr, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x0D;
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x0D)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return RecvBuff[3];
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion


        /// <summary>
        /// BlockLock_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="WNum"></param>
        /// <param name="ENum"></param>
        /// <param name="Mem"></param>
        /// <param name="WordPtr"></param>
        /// <param name="Wdt"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int BlockLock_G2(ref byte fComAdr, byte[] EPC, byte WNum, byte ENum, byte Mem, byte WordPtr, byte[] Wdt, byte[] Password,
                                byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result = 0x30;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x10;
            SendBuff[3] = WNum;
            SendBuff[4] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[5] = Mem;
                SendBuff[6] = WordPtr;
                Array.Copy(Wdt, 0, SendBuff, 7, WNum * 2);
                Array.Copy(Password, 0, SendBuff, 7 + WNum * 2, 4);
                SendBuff[11 + WNum * 2] = MaskMem;
                SendBuff[12 + WNum * 2] = MaskAdr[0];
                SendBuff[13 + WNum * 2] = MaskAdr[1];
                SendBuff[14 + WNum * 2] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 15 + WNum * 2, len);
                SendBuff[0] = Convert.ToByte(16 + WNum * 2 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 5, ENum * 2);
                SendBuff[ENum * 2 + 5] = Mem;
                SendBuff[ENum * 2 + 6] = WordPtr;
                Array.Copy(Wdt, 0, SendBuff, ENum * 2 + 7, WNum * 2);
                Array.Copy(Password, 0, SendBuff, WNum * 2 + ENum * 2 + 7, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + WNum * 2 + 12);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x10)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion


        /// <summary>
        /// WriteData_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="EPC"></param>
        /// <param name="WNum"></param>
        /// <param name="ENum"></param>
        /// <param name="Mem"></param>
        /// <param name="WordPtr"></param>
        /// <param name="Wdt"></param>
        /// <param name="Password"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int BlockWrite_G2(ref byte fComAdr, byte[] EPC, byte WNum, byte ENum, byte Mem, byte WordPtr, byte[] Wdt, byte[] Password,
                                byte MaskMem, byte[] MaskAdr, byte MaskLen, byte[] MaskData, ref int errorcode)
        {
            int result = 0x30;
            int len = 0;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x10;
            SendBuff[3] = WNum;
            SendBuff[4] = ENum;
            if (ENum == 0xFF)
            {
                SendBuff[5] = Mem;
                SendBuff[6] = WordPtr;
                Array.Copy(Wdt, 0, SendBuff, 7, WNum * 2);
                Array.Copy(Password, 0, SendBuff, 7 + WNum * 2, 4);
                SendBuff[11 + WNum * 2] = MaskMem;
                SendBuff[12 + WNum * 2] = MaskAdr[0];
                SendBuff[13 + WNum * 2] = MaskAdr[1];
                SendBuff[14 + WNum * 2] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 15 + WNum * 2, len);
                SendBuff[0] = Convert.ToByte(16 + WNum * 2 + len);
            }
            else if ((ENum >= 0) && (ENum < 32))
            {
                Array.Copy(EPC, 0, SendBuff, 5, ENum * 2);
                SendBuff[ENum * 2 + 5] = Mem;
                SendBuff[ENum * 2 + 6] = WordPtr;
                Array.Copy(Wdt, 0, SendBuff, ENum * 2 + 7, WNum * 2);
                Array.Copy(Password, 0, SendBuff, WNum * 2 + ENum * 2 + 7, 4);
                SendBuff[0] = Convert.ToByte(ENum * 2 + WNum * 2 + 12);
            }
            else
            {
                return 0xFF;
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x10)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            fComAdr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion


        /// <summary>
        /// InventorySingle_6B
        /// </summary>
        /// <param name="ConAddr"></param>
        /// <param name="ant"></param>
        /// <param name="ID_6B"></param>
        /// <returns></returns>
       #region 
        public int InventorySingle_6B(ref byte ConAddr, ref byte ant, byte[] ID_6B)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = ConAddr;
            SendBuff[2] = 0x50;
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x50)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            ant = RecvBuff[4];
                            Array.Copy(RecvBuff, 5, ID_6B, 0, 10);
                            ConAddr = RecvBuff[1];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
       #endregion

        /// <summary>
        /// InventoryMultiple_6B
        /// </summary>
        /// <param name="ConAddr"></param>
        /// <param name="Condition"></param>
        /// <param name="StartAddress"></param>
        /// <param name="mask"></param>
        /// <param name="ConditionContent"></param>
        /// <param name="ant"></param>
        /// <param name="ID_6B"></param>
        /// <param name="Cardnum"></param>
        /// <returns></returns>
        #region 
        public int InventoryMultiple_6B(ref byte ConAddr, byte Condition, byte StartAddress, byte mask, byte[] ConditionContent, ref byte ant, byte[] ID_6B, ref int Cardnum)
        {
            int result = 0x30;
            SendBuff[0] = 0x0F;
            SendBuff[1] = ConAddr;
            SendBuff[2] = 0x51;
            SendBuff[3] = Condition;
            SendBuff[4] = StartAddress;
            SendBuff[5] = mask;
            Array.Copy(ConditionContent, 0, SendBuff, 6, 8);
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x51)
                    {
                        if ((RecvBuff[3] == 0x15) || (RecvBuff[3] == 0x16) || (RecvBuff[3] == 0x17) || (RecvBuff[3] == 0x18))
                        {
                            ant = RecvBuff[4];
                            Cardnum = RecvBuff[5];
                            Array.Copy(RecvBuff, 6, ID_6B, 0, RecvLength - 8);
                            ConAddr = RecvBuff[1];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// ReadData_6B
        /// </summary>
        /// <param name="ConAddr"></param>
        /// <param name="ID_6B"></param>
        /// <param name="StartAddress"></param>
        /// <param name="Num"></param>
        /// <param name="Data"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int ReadData_6B(ref byte ConAddr, byte[] ID_6B, byte StartAddress, byte Num, byte[] Data, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = 0x0E;
            SendBuff[1] = ConAddr;
            SendBuff[2] = 0x52;
            SendBuff[3] = StartAddress;
            Array.Copy(ID_6B, 0, SendBuff, 4, 8);
            SendBuff[12] = Num;
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x52)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            Array.Copy(RecvBuff, 4, Data, 0, RecvLength - 6);
                            ConAddr = RecvBuff[1];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// WriteData_6B
        /// </summary>
        /// <param name="ConAddr"></param>
        /// <param name="ID_6B"></param>
        /// <param name="StartAddress"></param>
        /// <param name="Writedata"></param>
        /// <param name="Writedatalen"></param>
        /// <param name="writtenbyte"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region
        public int WriteData_6B(ref byte ConAddr,byte[] ID_6B, byte StartAddress, byte[] Writedata,byte Writedatalen,ref int writtenbyte, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = Convert.ToByte(13 + Writedatalen);
            SendBuff[1] = ConAddr;
            SendBuff[2] = 0x53;
            SendBuff[3] = StartAddress;
            Array.Copy(ID_6B, 0, SendBuff, 4, 8);
            Array.Copy(Writedata, 0, SendBuff, 12, Writedatalen);
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x53)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            errorcode = 0;
                        }
                        else if(RecvBuff[3]==0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// Lock_6B
        /// </summary>
        /// <param name="ConAddr"></param>
        /// <param name="ID_6B"></param>
        /// <param name="Address"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int Lock_6B(ref byte ConAddr, byte[] ID_6B, byte Address, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = 0x0D;
            SendBuff[1] = ConAddr;
            SendBuff[2] = 0x55;
            SendBuff[3] = Address;
            Array.Copy(ID_6B, 0, SendBuff, 4, 8);
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x55)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion
       
        /// <summary>
        /// CheckLock_6B
        /// </summary>
        /// <param name="ConAddr"></param>
        /// <param name="ID_6B"></param>
        /// <param name="Address"></param>
        /// <param name="ReLockState"></param>
        /// <param name="errorcode"></param>
        /// <returns></returns>
        #region 
        public int CheckLock_6B(ref byte ConAddr, byte[] ID_6B, byte Address, ref byte ReLockState, ref int errorcode)
        {
            int result = 0x30;
            SendBuff[0] = 0x0D;
            SendBuff[1] = ConAddr;
            SendBuff[2] = 0x54;
            SendBuff[3] = Address;
            Array.Copy(ID_6B, 0, SendBuff, 4, 8);
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x54)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            ReLockState = RecvBuff[4];
                            errorcode = 0;
                        }
                        else if (RecvBuff[3] == 0xFC)
                        {
                            errorcode = RecvBuff[4];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// InventoryBuffer_G2
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="QValue"></param>
        /// <param name="Session"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="MaskFlag"></param>
        /// <param name="AdrTID"></param>
        /// <param name="LenTID"></param>
        /// <param name="TIDFlag"></param>
        /// <param name="Target"></param>
        /// <param name="InAnt"></param>
        /// <param name="Scantime"></param>
        /// <param name="FastFlag"></param>
        /// <param name="BufferCount"></param>
        /// <param name="TagNum"></param>
        /// <returns></returns>
        #region 
        public int InventoryBuffer_G2(ref byte ComAdr, byte QValue, byte Session, byte MaskMem, byte[] MaskAdr, byte MaskLen,
                                byte[] MaskData, byte MaskFlag, byte AdrTID, byte LenTID, byte TIDFlag, byte Target, byte InAnt,
                                byte Scantime, byte FastFlag, ref int BufferCount,ref int TagNum)
        {
            SendBuff[1] = ComAdr;
            SendBuff[2] = 0x18;
            SendBuff[3] = QValue;
            SendBuff[4] = Session;
            if (MaskFlag == 1)
            {
                int len = 0;
                SendBuff[5] = MaskMem;
                Array.Copy(MaskAdr, 0, SendBuff, 6, 2);
                SendBuff[8] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 9, len);
                if (TIDFlag == 1)
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[len + 9] = AdrTID;
                        SendBuff[len + 10] = LenTID;
                        SendBuff[len + 11] = Target;
                        SendBuff[len + 12] = InAnt;
                        SendBuff[len + 13] = Scantime;
                        SendBuff[0] = Convert.ToByte(15 + len);
                    }
                    else
                    {
                        SendBuff[len + 9] = AdrTID;
                        SendBuff[len + 10] = LenTID;
                        SendBuff[0] = Convert.ToByte(12 + len);

                    }
                }
                else
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[len + 9] = Target;
                        SendBuff[len + 10] = InAnt;
                        SendBuff[len + 11] = Scantime;
                        SendBuff[0] = Convert.ToByte(13 + len);
                    }
                    else
                    {
                        SendBuff[0] = Convert.ToByte(10 + len);
                    }
                }
            }
            else
            {
                if (TIDFlag == 1)
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[5] = AdrTID;
                        SendBuff[6] = LenTID;
                        SendBuff[7] = Target;
                        SendBuff[8] = InAnt;
                        SendBuff[9] = Scantime;
                        SendBuff[0] = 0x0B;
                    }
                    else
                    {
                        SendBuff[5] = AdrTID;
                        SendBuff[6] = LenTID;
                        SendBuff[0] = 0x08;
                    }
                }
                else
                {
                    if (FastFlag == 1)
                    {
                        SendBuff[5] = Target;
                        SendBuff[6] = InAnt;
                        SendBuff[7] = Scantime;
                        SendBuff[0] = 0x09;
                    }
                    else
                    {
                        SendBuff[0] = 0x06;
                    }
                }
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            int result = GetDataFromPort(1);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x18)
                    {
                        if (RecvBuff[3] == 0)
                        {
                            BufferCount = RecvBuff[4] * 256 + RecvBuff[5];
                            TagNum = RecvBuff[6] * 256 + RecvBuff[7];
                        }
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// SetSaveLen
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="SaveLen"></param>
        /// <returns></returns>
        #region 
        public int SetSaveLen(ref byte fComAdr, byte SaveLen)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x70;
            SendBuff[3] = SaveLen;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x70)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// GetSaveLen
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="SaveLen"></param>
        /// <returns></returns>
        #region
        public int GetSaveLen(ref byte fComAdr, ref byte SaveLen)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x71;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x71)
                    {
                        fComAdr = RecvBuff[1];
                        SaveLen = RecvBuff[4];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// ClearBuffer_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <returns></returns>
        #region
        public int ClearBuffer_G2(ref byte fComAdr)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x73;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x73)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// GetBufferCnt_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Count"></param>
        /// <returns></returns>
        #region
        public int GetBufferCnt_G2(ref byte fComAdr, ref int Count)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x74;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x74)
                    {
                        fComAdr = RecvBuff[1];
                        Count = RecvBuff[4] * 256 + RecvBuff[5];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }

        #endregion

        /// <summary>
        /// GetDatafromBuff
        /// </summary>
        /// <returns></returns>
        #region 
        private int GetDatafromBuff()
        {
            bool revFrameEnd = false;
            int BEGTime = System.Environment.TickCount;
            int nCount;
            bool flagheadok = false;
            byte revok = 0;         // 接收状态 0-未接收完成；1-已接收完成未通过校验；2-已接收完成并通过校验
            int revpointer;
            if (COM_TYPE == 0)
            {
                #region
                while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                {
                    byte[] btAryBuffer = new byte[300];
                    nCount = 0;
                    flagheadok = false;
                    revok = 0;
                    revpointer = 0;
                    int roundBEGTime = System.Environment.TickCount;
                    do
                    {
                        if (flagheadok == false)
                        {
                            nCount = serialPort1.BytesToRead;
                            if (nCount > 0)
                            {
                                serialPort1.Read(btAryBuffer, 0, 1);
                                revpointer = 1;
                                flagheadok = true;
                            }
                        }
                        else
                        {
                            nCount = serialPort1.BytesToRead;
                            if (nCount > 0)
                            {
                                if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                {
                                    serialPort1.Read(btAryBuffer, revpointer, nCount);
                                    revpointer += nCount;
                                }
                                else
                                {
                                    serialPort1.Read(btAryBuffer, revpointer, btAryBuffer[0] + 1 - revpointer);
                                    revpointer = btAryBuffer[0] + 1;
                                    revok = 1;
                                    if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                    {
                                        revok = 2;
                                    }
                                }
                            }
                        }
                    }
                    while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成
                    if (revok == 2)      // 接收成功并crc正确
                    {
                        if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x03))
                        {
                            int num = btAryBuffer[4];
                            byte[] epclist = new byte[300];
                            Array.Copy(btAryBuffer, 5, epclist, 0, btAryBuffer[0] - 6);
                            int m = 0;
                            for (int i = 0; i < num; i++)
                            {
                                int epclen = epclist[m + 1];
                                int len = epclen + 4;
                                byte[] temp = new byte[len];
                                Array.Copy(epclist, m, temp, 0, len);
                                string str = "3:" + ByteArrayToHexString(temp);
                                value = str;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                m = m + epclen + 4;
                            }
                            if (btAryBuffer[3] == 0x01)
                            {
                                return 0;
                            }
                        }
                        else
                        {
                            return btAryBuffer[3];
                        }
                    }
                }
                #endregion
            }
            else
            {
                #region
                while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                {
                    byte[] btAryBuffer = new byte[300];
                    nCount = 0;
                    flagheadok = false;
                    revok = 0;
                    revpointer = 0;
                    int roundBEGTime = System.Environment.TickCount;
                    do
                    {
                        if (flagheadok == false)
                        {
                            nCount = streamToTran.Read(btAryBuffer, 0, 1);
                            if (nCount > 0)
                            {
                                revpointer = 1;
                                flagheadok = true;
                            }
                        }
                        else
                        {
                            int len = btAryBuffer[0] + 1 - revpointer;
                            nCount = streamToTran.Read(btAryBuffer, revpointer, len);
                            if (nCount > 0)
                            {
                                if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                {
                                    revpointer += nCount;
                                }
                                else
                                {
                                    revpointer = btAryBuffer[0] + 1;
                                    revok = 1;
                                    if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                    {
                                        revok = 2;
                                    }
                                }
                            }
                        }
                    }
                    while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成
                    if (revok == 2)      // 接收成功并crc正确
                    {
                        if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x03))
                        {
                            int num = btAryBuffer[4];
                            byte[] epclist = new byte[300];
                            Array.Copy(btAryBuffer, 5, epclist, 0, btAryBuffer[0] - 6);
                            int m = 0;
                            for (int i = 0; i < num; i++)
                            {
                                int epclen = epclist[m + 1];
                                int len = epclen + 4;
                                byte[] temp = new byte[len];
                                Array.Copy(epclist, m, temp, 0, len);
                                string str = "3:" + ByteArrayToHexString(temp);
                                value = str;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                m = m + epclen + 4;
                            }
                            if (btAryBuffer[3] == 0x01)
                            {
                                return 0;
                            }
                        }
                        else
                        {
                            return btAryBuffer[3];
                        }
                    }

                }
                #endregion
            }
            return 0x30;
        }
        #endregion
       
        /// <summary>
        /// ReadBuffer_G2
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <returns></returns>
        #region 
        public int ReadBuffer_G2(ref byte fComAdr)
        {
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x72;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            return GetDatafromBuff();
        }
        #endregion

        /// <summary>
        /// SetReadMode
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="ReadMode"></param>
        /// <returns></returns>
        #region
        public int SetReadMode(ref byte fComAdr, byte ReadMode)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x76;
            SendBuff[3] = ReadMode;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x76)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion


        /// <summary>
        /// SetReadParameter
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Parameter"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="MaskFlag"></param>
        /// <param name="AdrTID"></param>
        /// <param name="LenTID"></param>
        /// <param name="TIDFlag"></param>
        /// <returns></returns>
        #region 
        public int SetReadParameter(ref byte fComAdr, byte[] Parameter, byte MaskMem,byte[] MaskAdr, byte MaskLen, byte[]MaskData, byte MaskFlag, byte AdrTID, byte LenTID, byte TIDFlag)
        {
            int result=0x30;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x75;
            Array.Copy(Parameter,0,SendBuff,3,5);
            if(MaskFlag==1)
	        {
		        SendBuff[8]=MaskMem;
		        SendBuff[9]=MaskAdr[0];
		        SendBuff[10]=MaskAdr[1];
		        SendBuff[11]=MaskLen;
		        int len=0; 
		        if((MaskLen%8)==0)
		        {
			        len=MaskLen/8;
		        }
		        else
		        {
			        len=MaskLen/8 +1;
		        }
                Array.Copy(MaskData,0,SendBuff,12,len);
		        if(TIDFlag==1)
		        {

			        SendBuff[12+len]=AdrTID;
			        SendBuff[13+len]=LenTID;
                    SendBuff[0] = Convert.ToByte(len + 15);
		        }
		        else
		        {
                    SendBuff[0] = Convert.ToByte(len + 13);
		        }
	        }
	        else
	        {
		        if(TIDFlag==1)
		        {
			        SendBuff[8] = AdrTID;
			        SendBuff[9] = LenTID;
			        SendBuff[0] = 11;
		        }
		        else
		        {
			        SendBuff[0] = 9;
		        }
	        }
            GetCRC(SendBuff, SendBuff[0]-1);
            SendDataToPort(SendBuff, SendBuff[0]+1);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x75)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion



        #region
        public int GetReadParameter(ref byte fComAdr, byte[] Parameter)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x77;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x77)
                    {
                        fComAdr = RecvBuff[1];
                        Array.Copy(RecvBuff, 4, Parameter, 0, RecvLength - 6);
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion
        /// <summary>
        /// WriteRfPower
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="PowerDbm"></param>
        /// <returns></returns>
        #region

        public int WriteRfPower(ref byte fComAdr, byte PowerDbm)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x79;
            SendBuff[3] = PowerDbm;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x79)
                    {
                        fComAdr = RecvBuff[1];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// ReadRfPower
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="PowerDbm"></param>
        /// <returns></returns>
        #region
        public int ReadRfPower(ref byte fComAdr, ref byte PowerDbm)
        {
            int result = 0x30;
            SendBuff[0] = 0x04;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x7A;
            GetCRC(SendBuff, 3);
            SendDataToPort(SendBuff, 5);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x7A)
                    {
                        fComAdr = RecvBuff[1];
                        PowerDbm = RecvBuff[4];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion

        /// <summary>
        /// RetryTimes
        /// </summary>
        /// <param name="fComAdr"></param>
        /// <param name="Times"></param>
        /// <returns></returns>
        #region
        public int RetryTimes(ref byte fComAdr, ref byte Times)
        {
            int result = 0x30;
            SendBuff[0] = 0x05;
            SendBuff[1] = fComAdr;
            SendBuff[2] = 0x7B;
            SendBuff[3] = Times;
            GetCRC(SendBuff, 4);
            SendDataToPort(SendBuff, 6);
            result = GetDataFromPort(0);
            if (result == 0)
            {
                if (CheckCRC(RecvBuff, RecvLength) == 0)
                {
                    if (RecvBuff[2] == 0x7B)
                    {
                        fComAdr = RecvBuff[1];
                        Times = RecvBuff[4];
                        return (RecvBuff[3]);
                    }
                    else
                    {
                        return (0xEE);
                    }
                }
                else
                {
                    return (0x31);
                }
            }
            else
            {
                return (result);
            }
        }
        #endregion


        /// <summary>
        /// Inventory_G2
        /// </summary>
        /// <param name="ComAdr"></param>
        /// <param name="QValue"></param>
        /// <param name="Session"></param>
        /// <param name="MaskMem"></param>
        /// <param name="MaskAdr"></param>
        /// <param name="MaskLen"></param>
        /// <param name="MaskData"></param>
        /// <param name="MaskFlag"></param>
        /// <param name="AdrTID"></param>
        /// <param name="LenTID"></param>
        /// <param name="TIDFlag"></param>
        /// <param name="Target"></param>
        /// <param name="InAnt"></param>
        /// <param name="Scantime"></param>
        /// <param name="FastFlag"></param>
        /// <returns></returns>
        #region
        public int InventoryMix_G2(ref byte ComAdr, byte QValue, byte Session, byte MaskMem, byte[] MaskAdr, byte MaskLen,
                                byte[] MaskData, byte MaskFlag, byte ReadMem, byte[] ReadAdr, byte ReadLen, byte[]Psd,byte Target, byte InAnt,
                                byte Scantime, byte FastFlag)
        {
            SendBuff[1] = ComAdr;
            SendBuff[2] = 0x19;
            SendBuff[3] = QValue;
            SendBuff[4] = Session;
            if (MaskFlag == 1)
            {
                int len = 0;
                SendBuff[5] = MaskMem;
                Array.Copy(MaskAdr, 0, SendBuff, 6, 2);
                SendBuff[8] = MaskLen;
                if ((MaskLen) % 8 == 0)
                {
                    len = (MaskLen) / 8;
                }
                else
                {
                    len = (MaskLen) / 8 + 1;
                }
                Array.Copy(MaskData, 0, SendBuff, 9, len);
                SendBuff[len + 9] = ReadMem;
                SendBuff[len + 10] = ReadAdr[0];
                SendBuff[len + 11] = ReadAdr[1];
                SendBuff[len + 12] = ReadLen;
                Array.Copy(Psd, 0, SendBuff, 13, 4);
                if (FastFlag == 1)
                {
                    SendBuff[len + 17] = Target;
                    SendBuff[len + 18] = InAnt;
                    SendBuff[len + 19] = Scantime;
                    SendBuff[0] = Convert.ToByte(21 + len);
                }
                else
                {
                    SendBuff[0] = Convert.ToByte(18 + len);
                }
            }
            else
            {
                SendBuff[5] = ReadMem;
                SendBuff[6] = ReadAdr[0];
                SendBuff[7] = ReadAdr[1];
                SendBuff[8] = ReadLen;
                Array.Copy(Psd, 0, SendBuff, 9, 4);
                if (FastFlag == 1)
                {
                    SendBuff[13] = Target;
                    SendBuff[14] = InAnt;
                    SendBuff[15] = Scantime;
                    SendBuff[0] = 0x11;
                }
                else
                {
                    SendBuff[0] = 0x0E;
                }
            }
            GetCRC(SendBuff, SendBuff[0] - 1);
            SendDataToPort(SendBuff, SendBuff[0] + 1);
            if ((QValue & 0x80) == 0x80)//带时间返回
            {
                return GetInventoryMixG2();
            }
            else
            {
                return GetInventoryMixG1();
            }
        }
        #endregion

        private int GetInventoryMixG2()
        {
            bool revFrameEnd = false;
            int BEGTime = System.Environment.TickCount;
            int cmdTime = 0;
            int nCount;
            bool flagheadok = false;
            byte revok = 0;         // 接收状态 0-未接收完成；1-已接收完成未通过校验；2-已接收完成并通过校验
            int revpointer;
            try
            {
                if (COM_TYPE == 0)
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 200 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    serialPort1.Read(btAryBuffer, 0, 1);
                                    revpointer = 1;////////////////////////////
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        serialPort1.Read(btAryBuffer, revpointer, nCount);
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        ////添加一个长度命令字的判断
                                        serialPort1.Read(btAryBuffer, revpointer, btAryBuffer[0] + 1 - revpointer);
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成

                        if (revok == 2)      // 接收成功并crc正确
                        {
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03))
                            {
                                byte Ant = btAryBuffer[4];
                                int num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte M_type = epclist[m];
                                        string gunma = Convert.ToString(M_type, 16).PadLeft(2, '0');
                                        byte epclen = epclist[m + 1];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 2, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 2];
                                        string s_rssi = Convert.ToString(RSSI, 16).PadLeft(2, '0');
                                        m = m + epclen + 3;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "4:" + gunma + "," + sEPC + "," + s_rssi + "," + Ant.ToString();
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }
                            }
                            else if (btAryBuffer[3] == 0x26)
                            {
                                int tagrate = btAryBuffer[5] * 256 + btAryBuffer[6];
                                byte[] timb = new byte[4];
                                Array.Copy(btAryBuffer, 7, timb, 0, 4);
                                string stim = ByteArrayToHexString(timb);
                                int totalnum = Convert.ToInt32(stim, 16);
                                cmdTime = System.Environment.TickCount - BEGTime;
                                string Temp = "0:" + tagrate.ToString() + "," + totalnum.ToString() + "," + cmdTime.ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                            if ((btAryBuffer[3] == 0xFB) || (btAryBuffer[3] == 0xF8) || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                        }
                        else if (revok == 0)
                        {
                            return 0x30;
                        }
                    }
                    //  MessageBox.Show("111111111");
                    #endregion
                }
                else
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = streamToTran.Read(btAryBuffer, 0, 1);
                                if (nCount > 0)
                                {
                                    revpointer = 1;
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                int len = btAryBuffer[0] + 1 - revpointer;
                                nCount = streamToTran.Read(btAryBuffer, revpointer, len);
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成

                        if (revok == 2)      // 接收成功并crc正确
                        {
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03))
                            {
                                byte Ant = btAryBuffer[4];
                                int num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte M_type = epclist[m];
                                        string gunma = Convert.ToString(M_type, 16).PadLeft(2, '0');
                                        byte epclen = epclist[m + 1];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 2, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 2];
                                        string s_rssi = Convert.ToString(RSSI, 16).PadLeft(2, '0');
                                        m = m + epclen + 3;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "4:" + gunma + "," + sEPC + "," + s_rssi + "," + Ant.ToString();
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }

                            }
                            else if (btAryBuffer[3] == 0x26)
                            {
                                int tagrate = btAryBuffer[5] * 256 + btAryBuffer[6];
                                byte[] timb = new byte[4];
                                Array.Copy(btAryBuffer, 7, timb, 0, 4);
                                string stim = ByteArrayToHexString(timb);
                                int totalnum = Convert.ToInt32(stim, 16);
                                cmdTime = System.Environment.TickCount - BEGTime;
                                string Temp = "0:" + tagrate.ToString() + "," + totalnum.ToString() + "," + cmdTime.ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                            if ((btAryBuffer[3] == 0xF8) || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                        }
                    }
                    #endregion
                }
            }
            catch
            {
                ;
            }
            return 0x30;
        }
        private int GetInventoryMixG1()
        {
            try
            {
                bool revFrameEnd = false;
                int BEGTime = System.Environment.TickCount;
                //int cmdTime = 0;
                int nCount;
                bool flagheadok = false;
                byte revok = 0;         // 接收状态 0-未接收完成；1-已接收完成未通过校验；2-已接收完成并通过校验
                int revpointer;
                if (COM_TYPE == 0)
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    serialPort1.Read(btAryBuffer, 0, 1);
                                    revpointer = 1;
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                nCount = serialPort1.BytesToRead;
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        serialPort1.Read(btAryBuffer, revpointer, nCount);
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        serialPort1.Read(btAryBuffer, revpointer, btAryBuffer[0] + 1 - revpointer);
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成

                        if (revok == 2)      // 接收成功并crc正确
                        {
                            int num = 0;
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03) || (btAryBuffer[3] == 0x04))
                            {
                                byte Ant = btAryBuffer[4];
                                num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte M_type = epclist[m];
                                        string gunma = Convert.ToString(M_type, 16).PadLeft(2, '0');
                                        byte epclen = epclist[m+1];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 2, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 2];
                                        string s_rssi = Convert.ToString(RSSI, 16).PadLeft(2, '0');
                                        m = m + epclen + 3;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "4:" + gunma + "," + sEPC + "," + s_rssi + "," + Ant.ToString();
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }

                            }
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0xFB) || (btAryBuffer[3] == 0xF8)
                                || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }

                                return 0;
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    #region
                    while ((!revFrameEnd) && ((System.Environment.TickCount - BEGTime) < inventoryScanTime * 100 + 2000))
                    {
                        byte[] btAryBuffer = new byte[300];
                        nCount = 0;
                        flagheadok = false;
                        revok = 0;
                        revpointer = 0;
                        int roundBEGTime = System.Environment.TickCount;
                        do
                        {
                            if (flagheadok == false)
                            {
                                nCount = streamToTran.Read(btAryBuffer, 0, 1);
                                if (nCount > 0)
                                {
                                    revpointer = 1;
                                    flagheadok = true;
                                }
                            }
                            else
                            {
                                int len = btAryBuffer[0] + 1 - revpointer;
                                nCount = streamToTran.Read(btAryBuffer, revpointer, len);
                                if (nCount > 0)
                                {
                                    if (nCount < (btAryBuffer[0] + 1 - revpointer))
                                    {
                                        revpointer += nCount;
                                    }
                                    else
                                    {
                                        revpointer = btAryBuffer[0] + 1;
                                        revok = 1;
                                        if (CheckCRC(btAryBuffer, btAryBuffer[0] + 1) == 0)
                                        {
                                            revok = 2;
                                        }
                                    }
                                }
                            }
                        }
                        while ((revok == 0) && ((System.Environment.TickCount - roundBEGTime) < 1000));         // 一个数据包接收完成

                        if (revok == 2)      // 接收成功并crc正确
                        {
                            int num = 0;
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0x03) || (btAryBuffer[3] == 0x04))
                            {
                                byte Ant = btAryBuffer[4];
                                num = btAryBuffer[5];
                                if (num > 0)
                                {
                                    byte[] epclist = new byte[300];
                                    Array.Copy(btAryBuffer, 6, epclist, 0, btAryBuffer[0] - 7);
                                    int m = 0;
                                    for (int i = 0; i < num; i++)
                                    {
                                        byte M_type = epclist[m];
                                        string gunma = Convert.ToString(M_type, 16).PadLeft(2, '0');
                                        byte epclen = epclist[m + 1];
                                        byte[] epc = new byte[epclen];
                                        Array.Copy(epclist, m + 2, epc, 0, epclen);
                                        byte RSSI = epclist[epclen + 2];
                                        string s_rssi = Convert.ToString(RSSI, 16).PadLeft(2, '0');
                                        m = m + epclen + 3;
                                        string sEPC = ByteArrayToHexString(epc);
                                        string Temp = "4:" + gunma + "," + sEPC + "," + s_rssi + "," + Ant.ToString();
                                        value = Temp;
                                        if (ReceiveCallback != null)
                                        {
                                            ReceiveCallback(value);
                                        }
                                    }
                                }

                            }
                            if ((btAryBuffer[3] == 0x01) || (btAryBuffer[3] == 0x02) || (btAryBuffer[3] == 0xFB) || (btAryBuffer[3] == 0xF8)
                                || (btAryBuffer[3] == 0xF9) || (btAryBuffer[3] == 0xFE) || (btAryBuffer[3] == 0xFD) || (btAryBuffer[3] == 0xff))
                            {
                                revFrameEnd = true;
                                string Temp = "2:" + btAryBuffer[3].ToString();
                                value = Temp;
                                if (ReceiveCallback != null)
                                {
                                    ReceiveCallback(value);
                                }
                                return 0;
                            }
                        }
                    }
                    #endregion
                }
            }
            catch
            {
                ;
            }
            return 0x30;
        }
    }
}
