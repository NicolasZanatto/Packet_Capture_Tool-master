﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpPcap;
using PacketDotNet;
using Packet_Capture_Tool;
using System.Net;
using SnmpSharpNet;

namespace Packet_Capture_Tool
{
    public partial class Form1 : Form
    {
        List<string> captureDeviceList;
        int deviceIndex;
        bool isPromisc;
        CaptureDeviceList devices;
        int typeOfDecode = 0;
        string writeLine;
        bool stopCapture = false;
        bool decodeMode;

        List<PackageDetail> packageDetailList;

        public Form1()
        {
            InitializeComponent();
            button1.Click += new EventHandler(button1_Click);
            button2.Click += new EventHandler(button2_Click);
            button3.Click += new EventHandler(button3_Click);
            button4.Click += new EventHandler(button4_Click);
            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
            radioButton2.CheckedChanged += new EventHandler(radioButton2_CheckedChanged);
            radioButton3.CheckedChanged += new EventHandler(radioButton3_CheckedChanged);
            string displayText = get_Device_List();
            textBox1.Text = displayText;
            comboBox1.DataSource = captureDeviceList;
            button3.Enabled = false;
            button4.Enabled = false;
            packageDetailList = new List<PackageDetail>();
            new PackageDetail();
        }

        private string get_Device_List()
        {
            captureDeviceList = new List<string>();
            var ver = SharpPcap.Version.VersionString;
            var stringDevices = "SharpPcap {0}, Example1.IfList.cs" + ver;
            devices = CaptureDeviceList.Instance;
            if (devices.Count < 1)
            {
                stringDevices = stringDevices + Environment.NewLine + "No devices were found on this machine";
                return stringDevices;
            }
            stringDevices = stringDevices + Environment.NewLine + "The following devices are available on this machine: ";
            int count = 0;
            foreach (ICaptureDevice dev in devices)
            {
                var device = count.ToString();
                stringDevices = stringDevices + Environment.NewLine + "----------------------------------------------------------------------" + Environment.NewLine + "Device #" + device + ": " + dev.ToString();
                captureDeviceList.Add(device);
                count++;
            }

            return stringDevices;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            deviceIndex = int.Parse(comboBox1.Text);
            isPromisc = checkBox1.Checked;
            decodeMode = false;
            Thread l = new Thread(new ThreadStart(listen_Start))
            {
                IsBackground = true
            };

            l.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            stopCapture = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            deviceIndex = comboBox1.SelectedIndex;
            isPromisc = checkBox1.Checked;
            decodeMode = true;
            Thread l = new Thread(new ThreadStart(listen_Start))
            {
                IsBackground = true
            };

            l.Start();
        }
        private void button4_Click(object sender, EventArgs e)
        {
            stopCapture = true;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            typeOfDecode = 0;
            button3.Enabled = false;
            button4.Enabled = false;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            typeOfDecode = 1;
            button3.Enabled = true;
            button4.Enabled = true;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            typeOfDecode = 2;
            button3.Enabled = false;
            button4.Enabled = false;
        }

        private void updateLog()
        {
            textBox1.AppendText(Environment.NewLine + writeLine);
        }

        private void listen_Start()
        {
            ICaptureDevice device = devices[deviceIndex];

            device.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);

            int readTimeoutMilliseconds = 1000;
            if (isPromisc == true)
            {
                device.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
            }
            else
            {
                device.Open(DeviceMode.Normal, readTimeoutMilliseconds);
            }

            string filter;

            if (decodeMode == false)
            {
                switch (typeOfDecode)
                {
                    case 0:
                        break;

                    case 1:
                        filter = "ip and udp";
                        device.Filter = filter;
                        break;

                    case 2:
                        filter = "ip and tcp";
                        device.Filter = filter;
                        break;
                }
            }
            else
            {
                filter = "udp port 161 or udp port 162";
                device.Filter = filter;
            }

            device.StartCapture();
            writeLine = "--- Listening For Packets ---";
            Invoke(new MethodInvoker(updateLog));
            while (!stopCapture)
            {

            }

            device.StopCapture();
            device.Close();

            writeLine = " -- Capture stopped, device closed. --";
            Invoke(new MethodInvoker(updateLog));
            stopCapture = false;
        }

        private void device_OnPacketArrival(object sender, CaptureEventArgs packet)
        {
            Packet pack = Packet.ParsePacket(packet.Packet.LinkLayerType, packet.Packet.Data);
            DateTime time = packet.Packet.Timeval.Date;
            int len = packet.Packet.Data.Length;

            if (IsTCPPacket(pack))
            {
                ShowTCPPacket(pack, time, len);
            }
            else if (IsUDPPacket(pack))
            {
                ShowUDPPacket(pack, time, len);
            }
            else if (IsICMPv4Packet(pack))
            {
                ShowICMPv4Packet(pack, time, len);
            }
            else if (IsICMPv6Packet(pack))
            {

            }
        }


        #region TCP
        private bool IsTCPPacket(Packet pack) => (TcpPacket)pack.Extract(typeof(TcpPacket)) != null;
        private void ShowTCPPacket(Packet pack, DateTime time, int len)
        {
            var tcpPacket = (TcpPacket)pack.Extract(typeof(TcpPacket));
            IpPacket ipPacket = (IpPacket)tcpPacket.ParentPacket;

            var packageDetail = new PackageDetail(tcpPacket, null, ipPacket);
            packageDetailList.Add(packageDetail);

            var srcIp = ipPacket.SourceAddress;
            var dstIp = ipPacket.DestinationAddress;
            var srcPort = tcpPacket.SourcePort;
            var dstPort = tcpPacket.DestinationPort;
            writeLine = string.Format("ID: {9} - {0}:{1}:{2},{3} - TCP Packet: {5}:{6}  -> {7}:{8}\n\n",
                                time.Hour, time.Minute, time.Second, time.Millisecond, len,
                                srcIp, srcPort, dstIp, dstPort, packageDetail.Id);
            Invoke(new MethodInvoker(updateLog));
        }
        #endregion

        #region UDP
        private bool IsUDPPacket(Packet pack) => (UdpPacket)pack.Extract(typeof(UdpPacket)) != null;
        private void ShowUDPPacket(Packet pack, DateTime time, int len)
        {
            UdpPacket udpPacket = (UdpPacket)pack.Extract(typeof(UdpPacket));
            IpPacket ipPacket = (IpPacket)udpPacket.ParentPacket;

            var packageDetail = new PackageDetail(null, udpPacket, ipPacket);
            packageDetailList.Add(packageDetail);

            IPAddress srcIp = ipPacket.SourceAddress;
            IPAddress dstIp = ipPacket.DestinationAddress;
            ushort srcPort = udpPacket.SourcePort;
            ushort dstPort = udpPacket.DestinationPort;
            writeLine = (string.Format("ID: {9} - {0}:{1}:{2},{3} - UDP Packet: {5}:{6} -> {7}:{8}\n",
                            time.Hour, time.Minute, time.Second, time.Millisecond, len,
                            srcIp, srcPort, dstIp, dstPort, packageDetail.Id));
            Invoke(new MethodInvoker(updateLog));
            if (decodeMode == true)
            {
                byte[] packetBytes = udpPacket.PayloadData;
                int version = SnmpPacket.GetProtocolVersion(packetBytes, packetBytes.Length);
                switch (version)
                {
                    case (int)SnmpVersion.Ver1:
                        SnmpV1Packet snmpPacket = new SnmpV1Packet();
                        try
                        {
                            snmpPacket.decode(packetBytes, packetBytes.Length);
                            writeLine = "SNMP.V1 Packet: " + snmpPacket.ToString();
                        }
                        catch (Exception e)
                        {
                            writeLine = e.ToString();
                        }
                        break;
                    case (int)SnmpVersion.Ver2:
                        SnmpV2Packet snmp2Packet = new SnmpV2Packet();
                        try
                        {
                            snmp2Packet.decode(packetBytes, packetBytes.Length);
                            writeLine = "SNMP.V2 Packet: " + snmp2Packet.ToString();
                        }
                        catch (Exception e)
                        {
                            writeLine = e.ToString();
                        }
                        break;
                    case (int)SnmpVersion.Ver3:
                        SnmpV3Packet snmp3Packet = new SnmpV3Packet();
                        try
                        {
                            snmp3Packet.decode(packetBytes, packetBytes.Length);
                            writeLine = "SNMP.V3 Packet: " + snmp3Packet.ToString();
                        }
                        catch (Exception e)
                        {
                            writeLine = e.ToString();
                        }
                        break;
                }
                Invoke(new MethodInvoker(updateLog));
            }
        }
        #endregion

        private bool IsICMPv4Packet(Packet pack) => (ICMPv4Packet)pack.Extract(typeof(ICMPv4Packet)) != null;

        private void ShowICMPv4Packet(Packet pack, DateTime time, int len)
        {
            var icmpv4Packet = (ICMPv4Packet)pack.Extract(typeof(ICMPv4Packet));
            IpPacket ipPacket = (IpPacket)icmpv4Packet.ParentPacket;

            var packageDetail = new PackageDetail(icmpv4Packet, ipPacket);
            packageDetailList.Add(packageDetail);

            var srcIp = ipPacket.SourceAddress;
            var dstIp = ipPacket.DestinationAddress;
            writeLine = string.Format("ID: {7} - {0}:{1}:{2},{3} - ICMPv4 Packet: {5} -> {6}\n\n",
                                time.Hour, time.Minute, time.Second, time.Millisecond, len,
                                srcIp, dstIp, packageDetail.Id);
            Invoke(new MethodInvoker(updateLog));
        }

        private bool IsICMPv6Packet(Packet pack) => (ICMPv6Packet)pack.Extract(typeof(ICMPv6Packet)) != null;

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {
            new PackageDetailForm(packageDetailList).Show();
        }
    }
}
