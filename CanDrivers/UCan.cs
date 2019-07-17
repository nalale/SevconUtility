using FTD2XX_NET;
using RDeviceControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


namespace DEService
{
	public delegate void dCANDataRead(CAN_Message msg);

	public class UCan : ICanDriver
	{
		public event dCANDataRead OnReadMessage;

		// Статус девайса
		FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
		// Create new instance of the FTDI device class
		static FTDI ftdiDevice = new FTDI();


		// Список для отправляемых сообщений
		static List<string> FTDI_send_msg_list = new List<string>();
		// Список для принимаемых сообщений
		static List<CAN_Message> FTDI_recieve_msg_list = new List<CAN_Message>();
		//Поток отпраки сообщений
		Thread TransmitThread;
		// Поток принятия сообщений
		Thread ReceiveThread;
		bool rxThreadAlive = true;
		bool txThreadAlive = true;
		// локер для отправляемых сообщений
		object txlocker = new object();
		EventWaitHandle txWait = new AutoResetEvent(false);
		// локер для принимаемых сообщений
		object rxlocker = new object();
		EventWaitHandle rxWait = new AutoResetEvent(false);

		Thread checkThread;
		bool isCheckThreadAlive =true;

		uint numBytesWritten;

		int loadCnt;
		int loadCntTx;
		int loadCntRx;
		int loadExtra = 36;

		public int BitRate { get; set; }
		public int CanLoadPercent { get; set; }
		public int CanLoadTx { get; set; }
		public int CanLoadRx { get; set; }


		Config cfg;

		public UCan()
		{

		}

		~UCan()
		{

		}


		#region Свойства

		public int Model { get; set; }
		public UInt16 HW_Version { get; set; }
		public UInt16 SW_Version { get; set; }
		public bool IsConnected { get; set; }
		public UInt32 TxCount { get; set; }
		public UInt32 RxCount { get; set; }

		#endregion


		#region General

		public bool Start(int channel, int bitRate)
		{
			this.BitRate = bitRate;
			if (checkThread == null)
			{
				checkThread = new Thread(check_thread);
				checkThread.Start();
			}

			return true;
		}

		void Connect()
		{
			UInt32 ftdiDeviceCount = 0;

			if (ftdiDevice.IsOpen == true)
			{
				DisconnectFromAdapter();
				ftdiDevice.Close();
				TransmitThread.Abort();
				ReceiveThread.Abort();
				TransmitThread = null;
				ReceiveThread = null;
				FTDI_send_msg_list.Clear();
				FTDI_recieve_msg_list.Clear();
			}

			ftStatus = ftdiDevice.GetNumberOfDevices(ref ftdiDeviceCount);
			if (ftdiDeviceCount == 0)
				return;


			FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];
			ftStatus = ftdiDevice.GetDeviceList(ftdiDeviceList);
			ftStatus = ftdiDevice.OpenBySerialNumber(ftdiDeviceList[0].SerialNumber);

			if (ftStatus == FTDI.FT_STATUS.FT_OK)
			{
				ftStatus = ftdiDevice.SetBaudRate(3000000);
				ftStatus = ftdiDevice.SetEventNotification(FTDI.FT_EVENTS.FT_EVENT_RXCHAR, rxWait);
				//ftStatus = ftdiDevice.SetLatency(16);
			}
			else
			{   //throw new Exception(string.Format("Error to set Baud rate, ftStatus: {0}", ftStatus));
				return;
			}

			if (TransmitThread == null)
				TransmitThread = new Thread(new ThreadStart(send_thread));


			TransmitThread.IsBackground = true;
			if (TransmitThread.IsAlive == false)
				TransmitThread.Start();

			if (ReceiveThread == null)
				ReceiveThread = new Thread(new ThreadStart(recieve_thread));

			ReceiveThread.IsBackground = true;
			if (ReceiveThread.IsAlive == false)
				ReceiveThread.Start();

			// Set data characteristics - Data bits, Stop bits, Parity
			ftStatus = ftdiDevice.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_1, FTDI.FT_PARITY.FT_PARITY_NONE);
			if (ftStatus != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception(string.Format("Error to set data characteristics , ftStatus: {0}", ftStatus));
			}

			// Set flow control - set RTS/CTS flow control
			ftStatus = ftdiDevice.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_NONE, 0x11, 0x13);
			if (ftStatus != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception(string.Format("Error to set flow control , ftStatus: {0}", ftStatus));
			}

			// Set read timeout to 5 seconds, write timeout to infinite
			ftStatus = ftdiDevice.SetTimeouts(5000, 0);
			if (ftStatus != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception(string.Format("Error to set timeouts, ftStatus: {0}", ftStatus));
			}

			IsConnected = true;
			SetSpeed(BitRate);
			TxCount = 0;
			RxCount = 0;

			ConnectToAdapter();
			// Запрос версии CAN-адаптера
			string s = "I\r";
			ftdiDevice.Write(s, s.Length, ref numBytesWritten);
		}

		public void Stop()
		{
			txWait.Set();
			rxThreadAlive = false;
			txThreadAlive = false;
			isCheckThreadAlive = false;

			DisconnectFromAdapter();
			ftdiDevice.Close();
		}

		public bool SetSpeed(int bitRate)
		{
			this.BitRate = bitRate;
			if (IsConnected == false)
				return false;

			string CanSpeedSetting;

			switch (bitRate)
			{
				case 100:
					CanSpeedSetting = "\rC\r\rA0\r\rS3\r\r\r\rO\r";
					break;
				case 125:
					CanSpeedSetting = "\rC\r\rA0\r\rS4\r\r\r\rO\r";
					break;
				case 250:
					CanSpeedSetting = "\rC\r\rA0\r\rS5\r\r\r\rO\r";
					break;
				case 500:
					CanSpeedSetting = "\rC\r\rA0\r\rS6\r\r\r\rO\r";
					break;
				case 1000:
					CanSpeedSetting = "\rC\r\rA0\r\rS7\r\r\r\rO\r";
					break;
				default:
					CanSpeedSetting = "\rC\r\rA0\r\rS5\r\r\r\rO\r";
					break;
			}
			UInt32 numBytesWritten = 0;


			ftStatus = ftdiDevice.Write(CanSpeedSetting, CanSpeedSetting.Length, ref numBytesWritten);

			if (ftStatus != FTDI.FT_STATUS.FT_OK)
			{
				WPFMessageBox.Show("Failed to write to ftdi", "Error", CommitButtonType.Ok, WPFMessageBoxImage.Error);
				return false;
			}

			return true;
		}


		void ConnectToAdapter()
		{
			cfg.Connected = true;
			SendConfig();
		}

		void DisconnectFromAdapter()
		{
			cfg.Connected = false;
			SendConfig();
		}

		public void SetDiagnosticMode(bool val)
		{
			if (cfg.UseUDS == val)
				return;

			cfg.UseUDS = val;
			SendConfig();
		}

		void SendConfig()
		{
			int v = 0;
			if (cfg.Connected)
				v |= 1;
			if (cfg.UseUDS)
				v |= 2;

			string s = "C" + v.ToString("X1") + "\r";
			ftdiDevice.Write(s, s.Length, ref numBytesWritten);
		}

		#endregion


		#region Check

		private void check_thread()
		{
			bool div = false;

			while (isCheckThreadAlive)
			{
				div ^= true;
				string s = "";

				if (div)	// 1 раз в секунду
				{
					FTDI.FT_STATUS st = ftdiDevice.GetDescription(out s);
					if (s == "")
					{
						IsConnected = false;
						Connect();
					}
				}

				//CanLoadPercent = loadCnt * 100 * 2 / (BitRate * 1000);
				// Упрощение выражения выше
				CanLoadPercent = loadCnt / (BitRate * 5);
				CanLoadTx = loadCntTx * 2;
				CanLoadRx = loadCntRx * 2;

				loadCnt = 0;
				loadCntTx = 0;
				loadCntRx = 0;
				Thread.Sleep(500);
			}
		}

		#endregion


		#region Send


		public bool SendMessage(CAN_Message msg)
		{
			string s = "";

			if (!IsConnected)
				return false;

			// Стандартные сообщения
			if (msg.Ext == false)
			{
				s = "t";
				s += (msg.id & 0x7FF).ToString("X3");
				s += msg.dlc.ToString("x");
				for (int i = 0; i < msg.dlc; i++)
				{
					string sub_str = msg.data[i].ToString("X2");
					s += sub_str;
				}
				s += "\r";
				loadCnt += 11 + 8 * msg.dlc + loadExtra;
				loadCntTx++;
			}
			// Расширенные сообщения
			else
			{
				s = "x";
				s += msg.id.ToString("X8");
				s += msg.dlc.ToString("x");
				for (int i = 0; i < msg.dlc; i++)
				{
					string sub_str = msg.data[i].ToString("X2");
					s += sub_str;
				}
				s += "\r";
				s = s.Replace("X", "");
				loadCnt += 29 + 8 * msg.dlc + loadExtra;
				loadCntTx++;
                /*
				if (msg.id == 0x18DA17F1 || msg.id == 0x18DAF117)
					Global.OutputAll(msg.ToString(), TextColor.Tx);
                */
            }

            //ftdiDevice.Write(s, s.Length, ref numBytesWritten);
            lock (txlocker)
			{
				FTDI_send_msg_list.Add(s);
				txWait.Set();
			}

			TxCount++;

			return true;
		}

		private void send_thread()
		{
			while (txThreadAlive)
			{
				txWait.WaitOne();
				while (FTDI_send_msg_list.Count > 0)
				{
					lock (txlocker)
					{
						if (FTDI_send_msg_list[0] != null)
						{
							FTDI.FT_STATUS st = ftdiDevice.Write(FTDI_send_msg_list[0], FTDI_send_msg_list[0].Length, ref numBytesWritten);
						}
						FTDI_send_msg_list.RemoveAt(0);
					}
				}
			}
		}

		#endregion


		#region Recieve


		public void recieve_thread()
		{
			uint numBytesAvailable = 0;
			uint numBytesRead = 0;
			int end_mes;
			string inBuffer;
			CAN_Message msg = new CAN_Message();

			string buf = "";
			string s;

			while (rxThreadAlive)
			{
				rxWait.WaitOne(100);

				ftStatus = ftdiDevice.GetRxBytesAvailable(ref numBytesAvailable);
				if (numBytesAvailable > 1)
				{
					s = "";
					ftdiDevice.Read(out s, numBytesAvailable, ref numBytesRead);
					buf += s;

					int end = buf.IndexOf("\r");
					while (end != -1)
					{
						inBuffer = buf.Substring(0, end + 1);
						buf = buf.Substring(end + 1);

						// Парсинг стандартных ID
						if (inBuffer.StartsWith("t"))
						{
							inBuffer = inBuffer.Substring(1);
							end_mes = inBuffer.IndexOf("\r");
							if (end_mes >= 4)
							{
								msg = new CAN_Message();
								msg.id = uint.Parse(inBuffer.Substring(0, 3), System.Globalization.NumberStyles.HexNumber);
								msg.dlc = byte.Parse(inBuffer[3].ToString(), System.Globalization.NumberStyles.HexNumber);
								int str_ptr = 4;
								for (int byte_cnt = 0; byte_cnt < msg.dlc; byte_cnt++)
								{
									if ((inBuffer.Length) < str_ptr + 2)
									{
										break;
									}

									string sub_str = inBuffer.Substring(str_ptr, 2);
									msg.data[byte_cnt] = byte.Parse(sub_str, System.Globalization.NumberStyles.HexNumber);
									str_ptr += 2;
								}
								lock (rxlocker)
								{
									loadCnt += 11 + 8 * msg.dlc + loadExtra;
									loadCntRx++;
									OnReadMessage(msg);
									RxCount++;
								}
								inBuffer = "";
							}
						}

						else if (inBuffer.StartsWith("x"))  // Парсинг расширенных ID
						{
							end_mes = inBuffer.IndexOf("\r");
							msg.id = 0x1101;
							msg.Ext = true;
							if (end_mes >= 0)
							{
								msg = new CAN_Message();
								msg.id = UInt32.Parse(inBuffer.Substring(1, 8), System.Globalization.NumberStyles.HexNumber);
								msg.Ext = true;
								msg.dlc = byte.Parse(inBuffer[9].ToString());
								int str_ptr = 10;
								for (int byte_cnt = 0; byte_cnt < msg.dlc; byte_cnt++)
								{
									string sub_str = inBuffer.Substring(str_ptr, 2);
									msg.data[byte_cnt] = byte.Parse(sub_str, System.Globalization.NumberStyles.HexNumber);
									str_ptr += 2;
								}

								lock (rxlocker)
								{
                                    /*
									if (msg.id == 0x18DA17F1 || msg.id == 0x18DAF117)
										Global.OutputAll(msg.ToString(), TextColor.Rx);
                                    */
									loadCnt += 29 + 8 * msg.dlc + loadExtra;
									loadCntRx++;
									OnReadMessage(msg);
									RxCount++;
								}
								inBuffer = "";
							}
						}
						else if (inBuffer.StartsWith("I"))
						{
							int n = 1;
							Model = Int32.Parse(inBuffer.Substring(n, 2), System.Globalization.NumberStyles.HexNumber);
							n += 2;
							HW_Version = (UInt16)(Int32.Parse(inBuffer.Substring(n, 2), System.Globalization.NumberStyles.HexNumber) << 8);
							n += 2;
							HW_Version += UInt16.Parse(inBuffer.Substring(n, 2), System.Globalization.NumberStyles.HexNumber);
							n += 2;
							SW_Version = (UInt16)(Int32.Parse(inBuffer.Substring(n, 2), System.Globalization.NumberStyles.HexNumber) << 8);
							n += 2;
							SW_Version += UInt16.Parse(inBuffer.Substring(n, 2), System.Globalization.NumberStyles.HexNumber);
						}

						end = buf.IndexOf("\r");
					}
				}				
			}

		}

		#endregion


		struct Config
		{
			public bool Connected;
			public bool UseUDS;
		}
	}
}
