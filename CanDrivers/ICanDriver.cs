using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DEService
{
	public interface ICanDriver
	{
		bool Start(int channel, int bitRate);
		void Stop();
		bool SetSpeed(int bitRate);
		void SetDiagnosticMode(bool val);

		bool SendMessage(CAN_Message msg);

		event dCANDataRead OnReadMessage;

		int BitRate { get; set; }
		int CanLoadPercent { get; set; }
		int CanLoadTx { get; set; }
		int CanLoadRx { get; set; }

		int Model { get; set; }
		UInt16 HW_Version { get; set; }
		UInt16 SW_Version { get; set; }
		bool IsConnected { get; set; }

		UInt32 TxCount { get; set; }
		UInt32 RxCount { get; set; }

	}


	public class CAN_Message
	{
		public CAN_Message()
		{
			id = 0;
			data = new byte[8];
			dlc = 8;
			Ext = false;
		}

		public CAN_Message(UInt32 id, byte[] data, int dlc, int flags, long time)
		{
			this.id = id;
			this.data = data;
			this.dlc = dlc;
			this.flags = flags;
			this.time = time;
		}


		#region Свойства

		public UInt32 id { get; set; }
		public bool Ext { get; set; }
		public byte[] data { get; set; }
		public int dlc { get; set; }
		public int flags { get; set; }
		public long time { get; set; }

		public bool Handled { get; set; }

		#endregion


		public override string ToString()
		{
			//string pre = "0x";
			string pre = "";
			bool decodeUDS = true;

			string s;
			s = pre + id.ToString("X") + " " + (Ext ? "X " : "  ");
			for (int i = 0; i < dlc; i++)
			{
				s += pre + data[i].ToString("X2") + " ";
			}
            /*
                        if (decodeUDS)
                        {
                            UDS.PCI_SF f = new UDS.PCI_SF();
                            f.Value = data[0];
                            s += f.FrameType;
                        }
            */
            return s;
		}

	}
}
