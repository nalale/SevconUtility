using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using System.Timers;

using DEService;

namespace S10_Software
{
    enum SdoCommand { WRITE = 0x20, TX_4_BYTES = 0x23, TX_3_BYTES = 0x27, TX_2_BYTES = 0x2B, TX_1_BYTE = 0x2F, READ = 0x40, TX_ACK = 0x60, RX_ERROR = 0x80 };

    enum IndexSDO { IND_LM_MAP = 0x4614, FLUX_MAP = 0x4610, POWER_MAP = 0x4611, MOTOR_DATA = 0x4641, SPEED_CONTROL_GAINS = 0x4651, ACCESS_LEVEL = 0x5000, PHYSICAL_LAYER = 0x5900, MAX_TORQUE = 0x6072, CURRENT_LIMIT = 0x6075, PEAK_TORQUE = 0x6076, TORQUE_SLOPE = 0x6087, };

    enum MotorDataSubIndex { MAX_ST_CURRENT = 0x02, MIN_MAG_CURRENT = 0x03, MAX_MAG_CURRENT = 0x04, ST_RESISTANCE = 0x06, RATED_ST_CURRENT = 0x07, RT_RESISTANCE = 0x08, MAG_INDUCTANCE = 0x09, ST_LEAK_IND = 0x0A,
                            RT_LEAK_IND = 0x0B, CURRENT_KP = 0x0D, CURRENT_KI = 0x0F, RATED_MAG_CURRENT = 0x13, MOD_INDEX_KP = 0x19, MOD_INDEX_KI = 0x1A, LS_LS = 0x20, ID_RECOVERY_RATE = 0x25};

    enum SpeedControlGainsSubIndex { SPEED_KP = 0x01, SPEED_KI = 0x02 };


    struct Disp
    {
        public int Index;
        public int SubIndex;
        public int Data;
        public bool DataIsAvailable;
        public bool RequestIsAborted;
        public uint AbortCode;
        public uint AdditionalInfo;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ICanDriver CanDriver;
        System.Timers.Timer timer;
        uint NodeId, timerDelay;
        int[] temp_array;
        Disp dispatcher = new Disp();
        List<uint> nodes = new List<uint>();
        CAN_Message message = new CAN_Message();

        byte[] MotorDataSubIndArray = new byte[] {(byte)MotorDataSubIndex.MAX_ST_CURRENT, (byte)MotorDataSubIndex.MIN_MAG_CURRENT, (byte)MotorDataSubIndex.MAX_MAG_CURRENT, (byte)MotorDataSubIndex.ST_RESISTANCE,
                                            (byte)MotorDataSubIndex.RATED_ST_CURRENT, (byte)MotorDataSubIndex.RT_RESISTANCE, (byte)MotorDataSubIndex.MAG_INDUCTANCE, (byte)MotorDataSubIndex.ST_LEAK_IND,
                                             (byte)MotorDataSubIndex.RT_LEAK_IND, (byte)MotorDataSubIndex.RATED_MAG_CURRENT, (byte)MotorDataSubIndex.LS_LS, (byte)MotorDataSubIndex.ID_RECOVERY_RATE,
                                            (byte)MotorDataSubIndex.CURRENT_KP, (byte)MotorDataSubIndex.CURRENT_KI, (byte)MotorDataSubIndex.MOD_INDEX_KP, (byte)MotorDataSubIndex.MOD_INDEX_KI};

        object locker = new object();

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += FrmMain_Loaded;
            this.Closed += FrmMain_Closed;
        }

        private void FrmMain_Loaded(object sender, RoutedEventArgs e)
        {
            CanDriver = new UCan();
            CanDriver.SetSpeed(250);
            CanDriver.Start(0, 250);
            CanDriver.OnReadMessage += ReadMessage_Handler;

            timer = new System.Timers.Timer(100);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            message.dlc = 8;
            message.Ext = false;
        }

        private void FrmMain_Closed(object sender, EventArgs e)
        {
            timer.Stop();
            CanDriver.Stop();            
        }

        private void btnRead_ClickHandler(object sender, RoutedEventArgs e)
        {
            if (!CanDriver.IsConnected)
            {
                e.Handled = true;
                return;
            }


            int selected_tab = tc_Tabs.SelectedIndex;
            message.id = 0x600 + NodeId;

            switch (selected_tab)
            {
                case 0:
                    if (temp_array == null)
                        temp_array = new int[18];
                    else if (temp_array.Length != 18)
                        Array.Resize<int>(ref temp_array, 18);
                    LmMapRead();
                    break;
                case 1:
                    if (temp_array == null)
                        temp_array = new int[18];
                    else if (temp_array.Length != 18)
                        Array.Resize<int>(ref temp_array, 18);
                    FluxMapRead();
                    break;
                case 2:
                    if (temp_array == null)
                        temp_array = new int[18];
                    else if (temp_array.Length != 18)
                        Array.Resize<int>(ref temp_array, 18);
                    PowerMapRead();
                    break;
                case 3:
                    if (temp_array == null)
                        temp_array = new int[16];
                    else if (temp_array.Length != 16)
                        Array.Resize<int>(ref temp_array, 16);
                    MotorDataRead();
                    break;
                case 4:
                    if (temp_array == null)
                        temp_array = new int[6];
                    else if (temp_array.Length != 6)
                        Array.Resize<int>(ref temp_array, 6);
                    GainsRead();
                    break;
            }
        }


        private void btnLoad_ClickHandler(object sender, RoutedEventArgs e)
        {
            if (!CanDriver.IsConnected)
            {
                e.Handled = true;
                return;
            }

            int selected_tab = tc_Tabs.SelectedIndex;
            message.id = 0x600 + NodeId;

            switch (selected_tab)
            {
                // Lm Map
                case 0:
                    if (temp_array == null)
                        temp_array = new int[18];
                    else if (temp_array.Length != 18)
                        Array.Resize<int>(ref temp_array, 18);
                    LmMapFromWindow();
                    LmMapLoad();
                    break;
                // Flux map
                case 1:
                    if (temp_array == null)
                        temp_array = new int[18];
                    else if (temp_array.Length != 18)
                        Array.Resize<int>(ref temp_array, 18);
                    FluxMapFromWindow();
                    FluxMapLoad();
                    break;
                // Power Map
                case 2:
                    if (temp_array == null)
                        temp_array = new int[18];
                    else if (temp_array.Length != 18)
                        Array.Resize<int>(ref temp_array, 18);
                    PowerMapFromWindow();
                    PowerMapLoad();
                    break;
                case 3:
                    if (temp_array == null)
                        temp_array = new int[16];
                    else if (temp_array.Length != 16)
                        Array.Resize<int>(ref temp_array, 16);
                    MotorDataFromWindow();
                    MotorDataLoad();
                    break;
                case 4:
                    if (temp_array == null)
                        temp_array = new int[6];
                    else if (temp_array.Length != 6)
                        Array.Resize<int>(ref temp_array, 6);
                    GainsFromWindow();
                    GainsLoad();
                    break;
            }
        }


        private void ReadMessage_Handler(CAN_Message msg)
        {
            if (((msg.id & 0xf00) == 0x700) && !nodes.Contains(msg.id & 0xf))
            {
                nodes.Add(msg.id & 0xf);
                return;
            }
            else if (msg.id != 0x580 + NodeId)
                return;

            lock (locker)
            {
                if ((msg.data[2] << 8) + msg.data[1] == dispatcher.Index)
                {
                    if (msg.data[3] == dispatcher.SubIndex)
                    {
                        dispatcher.DataIsAvailable = true;
                        dispatcher.RequestIsAborted = false;
                        switch ((SdoCommand)(msg.data[0] & 0xE0))
                        {
                            case SdoCommand.READ:
                                dispatcher.Data = (msg.data[7] << 24) + (msg.data[6] << 16) + (msg.data[5] << 8) + msg.data[4];
                                break;
                            case SdoCommand.WRITE:
                                break;
                            case SdoCommand.TX_ACK:
                                break;
                            case SdoCommand.RX_ERROR:
                                dispatcher.DataIsAvailable = false;
                                dispatcher.RequestIsAborted = true;
                                dispatcher.AbortCode = msg.data[6];
                                dispatcher.AdditionalInfo = msg.data[7];
                                break;
                            default:
                                dispatcher.DataIsAvailable = false;
                                break;
                        }
                    }
                }
            }
        }

        #region ChangeID

        private void IdLoad()
        {
            bool isFinished = false;
            message.id = 0x600 + NodeId;

            message.data[0] = (byte)SdoCommand.TX_1_BYTE;
            message.data[1] = (byte)((int)IndexSDO.PHYSICAL_LAYER & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.PHYSICAL_LAYER & 0xFF00) >> 8);

            message.data[3] = 1;

            message.data[4] = Convert.ToByte(tbNewNodeId.Text);            


            dispatcher.SubIndex = 1;
            dispatcher.Index = (int)IndexSDO.PHYSICAL_LAYER;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {                    
                    if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;                        
                        MessageBox.Show("Error\n Index: " + dispatcher.SubIndex.ToString() + "\nCode: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                    }
                }
            }
        }

        #endregion

        #region Gains

        private void GainsRead()
        {
            bool isFinished = false;
            byte subindex = 0;
            short index = 0, s_index = 0;

            message.data[0] = (byte)SdoCommand.READ;
            message.data[1] = (byte)((int)IndexSDO.SPEED_CONTROL_GAINS & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.SPEED_CONTROL_GAINS & 0xFF00) >> 8);

            message.data[3] = (byte)SpeedControlGainsSubIndex.SPEED_KP;
            dispatcher.SubIndex = (byte)SpeedControlGainsSubIndex.SPEED_KP;
            dispatcher.Index = (int)IndexSDO.SPEED_CONTROL_GAINS;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        temp_array[subindex] = dispatcher.Data;
                        dispatcher.DataIsAvailable = false;

                        if (subindex == temp_array.Length - 1)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;

                        switch (subindex)
                        {
                            case 1:
                                index = (short)IndexSDO.SPEED_CONTROL_GAINS;
                                s_index = (short)SpeedControlGainsSubIndex.SPEED_KI;
                                break;
                            case 2:
                                index = (short)IndexSDO.PEAK_TORQUE;
                                s_index = 0;
                                break;
                            case 3:
                                index = (short)IndexSDO.MAX_TORQUE;
                                s_index = 0;
                                break;
                            case 4:
                                index = (short)IndexSDO.CURRENT_LIMIT;
                                s_index = 0;
                                break;
                            case 5:
                                index = (short)IndexSDO.TORQUE_SLOPE;
                                s_index = 0;
                                break;
                        }
                        message.data[1] = (byte)(index & 0xFF);
                        message.data[2] = (byte)((index & 0xFF00) >> 8);

                        message.data[3] = (byte)s_index;
                        dispatcher.SubIndex = (byte)s_index;
                        dispatcher.Index = (int)index;

                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }
            GainsToWindow();
        }

        private void GainsLoad()
        {
            bool isFinished = false;
            byte subindex = 0;
            short index = 0, s_index = 0;

            message.data[0] = (byte)SdoCommand.TX_2_BYTES;
            message.data[1] = (byte)((int)IndexSDO.SPEED_CONTROL_GAINS & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.SPEED_CONTROL_GAINS & 0xFF00) >> 8);

            message.data[3] = (byte)SpeedControlGainsSubIndex.SPEED_KP;

            message.data[4] = (byte)(temp_array[subindex] & 0xff);
            message.data[5] = (byte)((temp_array[subindex] >> 8) & 0xff);


            dispatcher.SubIndex = (byte)SpeedControlGainsSubIndex.SPEED_KP;
            dispatcher.Index = (int)IndexSDO.SPEED_CONTROL_GAINS;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                message.data[0] = (byte)SdoCommand.TX_2_BYTES;
                lock (locker)
                {
                    // Если данные доступны для считываения
                    if (dispatcher.DataIsAvailable)
                    {
                        dispatcher.DataIsAvailable = false;

                        if (subindex == temp_array.Length - 1)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        switch (subindex)
                        {
                            case 1:
                                index = (short)IndexSDO.SPEED_CONTROL_GAINS;
                                s_index = (short)SpeedControlGainsSubIndex.SPEED_KI;
                                break;
                            case 2:
                                message.data[0] = (byte)SdoCommand.TX_4_BYTES;
                                index = (short)IndexSDO.PEAK_TORQUE;
                                s_index = 0;
                                break;
                            case 3:
                                index = (short)IndexSDO.MAX_TORQUE;
                                s_index = 0;
                                break;
                            case 4:
                                message.data[0] = (byte)SdoCommand.TX_4_BYTES;
                                index = (short)IndexSDO.CURRENT_LIMIT;
                                s_index = 0;
                                break;
                            case 5:
                                message.data[0] = (byte)SdoCommand.TX_4_BYTES;
                                index = (short)IndexSDO.TORQUE_SLOPE;
                                s_index = 0;
                                break;
                        }



                        message.data[1] = (byte)((int)index & 0xFF);
                        message.data[2] = (byte)(((int)index & 0xFF00) >> 8);

                        message.data[3] = (byte)s_index;
                        message.data[4] = (byte)(temp_array[subindex] & 0xff);
                        message.data[5] = (byte)((temp_array[subindex] >> 8) & 0xff);
                        message.data[6] = (byte)((temp_array[subindex] >> 16) & 0xff);
                        message.data[7] = (byte)((temp_array[subindex] >> 24) & 0xff);

                        dispatcher.SubIndex = (byte)s_index;
                        dispatcher.Index = (int)index;

                        CanDriver.SendMessage(message);
                    }
                    // если S10 прислал отклонение запроса
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        // Sub-index does not exist
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];

                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }

        }

        private void GainsToWindow()
        {
            tbSpeedKp.Text = (temp_array[0] * Math.Pow(2, -12)).ToString();
            tbSpeedKi.Text = (temp_array[1] * Math.Pow(2, -8)).ToString();
            tbPeakTorque.Text = (temp_array[2] / 1000).ToString();
            tbMaxTorque.Text = (temp_array[3] / 10).ToString();
            tbCurrentLimit.Text = (temp_array[4] / 1000).ToString();
            tbTorqueSlope.Text = (temp_array[5] / 10).ToString();
        }

        private void GainsFromWindow()
        {
            temp_array[0] = Convert.ToUInt16(Convert.ToSingle(tbSpeedKp.Text) / Math.Pow(2, -12));
            temp_array[1] = Convert.ToUInt16(Convert.ToSingle(tbSpeedKi.Text) / Math.Pow(2, -8));
            temp_array[2] = Convert.ToInt32(tbPeakTorque.Text) * 1000;
            temp_array[3] = Convert.ToUInt16(Convert.ToSingle(tbMaxTorque.Text) * 10);
            temp_array[4] = Convert.ToInt32(tbCurrentLimit.Text) * 1000;
            temp_array[5] = Convert.ToInt32(Convert.ToSingle(tbTorqueSlope.Text) * 10);
        }

        #endregion

        #region Motor Data

        private void MotorDataRead()
        {
            bool isFinished = false;
            byte subindex = 0;

            message.data[0] = (byte)SdoCommand.READ;
            message.data[1] = (byte)((int)IndexSDO.MOTOR_DATA & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.MOTOR_DATA & 0xFF00) >> 8);

            message.data[3] = MotorDataSubIndArray[subindex];
            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
            dispatcher.Index = (int)IndexSDO.MOTOR_DATA;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        temp_array[subindex] = dispatcher.Data;
                        dispatcher.DataIsAvailable = false;

                        if (subindex == temp_array.Length - 1)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                        message.data[3] = MotorDataSubIndArray[subindex];
                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Index: " + dispatcher.SubIndex.ToString() + "\nCode: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }
            MotorDataToWindow();
        }

        private void MotorDataLoad()
        {
            bool isFinished = false;
            byte subindex = 0;

            message.data[0] = (byte)SdoCommand.TX_2_BYTES;
            message.data[1] = (byte)((int)IndexSDO.MOTOR_DATA & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.MOTOR_DATA & 0xFF00) >> 8);

            message.data[3] = MotorDataSubIndArray[subindex];

            message.data[4] = (byte)(temp_array[subindex] & 0xff);
            message.data[5] = (byte)((temp_array[subindex] >> 8) & 0xff);


            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
            dispatcher.Index = (int)IndexSDO.MOTOR_DATA;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    // Если данные доступны для считываения
                    if (dispatcher.DataIsAvailable)
                    {
                        dispatcher.DataIsAvailable = false;

                        if (subindex == temp_array.Length - 1)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                        message.data[3] = MotorDataSubIndArray[subindex];
                        message.data[4] = (byte)(temp_array[subindex] & 0xff);
                        message.data[5] = (byte)((temp_array[subindex] & 0xff00) >> 8);
                        CanDriver.SendMessage(message);
                    }
                    // если S10 прислал отклонение запроса
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        // Sub-index does not exist
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Index: " + dispatcher.SubIndex.ToString() + "\nCode: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }

        }

        private void MotorDataToWindow()
        {
            tbMaxStCurrent.Text = (temp_array[0]).ToString();
            tbMinMagCurrent.Text = (temp_array[1] >> 8).ToString();
            tbMaxMagCurrent.Text = (temp_array[2] >> 6).ToString();
            tbStResistance.Text = (temp_array[3] * Math.Pow(2, -12) * 1000).ToString();
            tbRatedCurrent.Text = temp_array[4].ToString();
            tbRtResistance.Text = (temp_array[5] * Math.Pow(2, -12) * 1000).ToString();
            tbMagInductance.Text = (temp_array[6] * Math.Pow(2, -16) * 1000000).ToString();
            tbStLeakInductance.Text = (temp_array[7] * 0.953674316).ToString();
            tbRtLeakInductance.Text = (temp_array[8] * 0.953674316).ToString();
            tbImRated.Text = (temp_array[9] >> 4).ToString();
            tbLsLs.Text = (temp_array[10] >> 8).ToString();
            tbIdRecoveryRate.Text = (temp_array[11]).ToString();
            tbCurrenKp.Text = (temp_array[12] * Math.Pow(0.5, 15)).ToString();
            tbCurrentKi.Text = (temp_array[13] * Math.Pow(0.5, 15)).ToString();
            tbModIndexKp.Text = (temp_array[14] / 32768F).ToString();
            tbModIndexKi.Text = (temp_array[15] / 32768F).ToString();
        }

        private void MotorDataFromWindow()
        {
            temp_array[0] = Convert.ToUInt16(tbMaxStCurrent.Text);
            temp_array[1] = Convert.ToUInt16(tbMinMagCurrent.Text) << 8;
            temp_array[2] = Convert.ToUInt16(tbMaxMagCurrent.Text) << 6;
            temp_array[3] = Convert.ToUInt16(Convert.ToSingle(tbStResistance.Text) / (Math.Pow(2, -12) * 1000));
            temp_array[4] = Convert.ToUInt16(tbRatedCurrent.Text);
            temp_array[5] = Convert.ToUInt16(Convert.ToSingle(tbRtResistance.Text) / (Math.Pow(2, -12) * 1000));
            temp_array[6] = Convert.ToUInt16(Convert.ToSingle(tbMagInductance.Text) / (Math.Pow(2, -16) * 1000000));
            temp_array[7] = Convert.ToUInt16(Convert.ToSingle(tbStLeakInductance.Text) / 0.953674316);
            temp_array[8] = Convert.ToUInt16(Convert.ToSingle(tbRtLeakInductance.Text) / 0.953674316);
            temp_array[9] = Convert.ToUInt16(tbImRated.Text) << 4;
            temp_array[10] = Convert.ToUInt16(tbLsLs.Text) << 8;
            temp_array[11] = Convert.ToUInt16(tbIdRecoveryRate.Text);
            temp_array[12] = Convert.ToUInt16(Convert.ToSingle(tbCurrenKp.Text) / Math.Pow(0.5, 15));
            temp_array[13] = Convert.ToUInt16(Convert.ToSingle(tbCurrentKi.Text) / Math.Pow(0.5, 15));
            temp_array[14] = Convert.ToUInt16(Convert.ToSingle(tbModIndexKp.Text) * 32768);
            temp_array[15] = Convert.ToUInt16(Convert.ToSingle(tbModIndexKi.Text) * 32768);
        }

        #endregion

        #region Power Map

        private void PowerMapRead()
        {
            bool isFinished = false;
            byte subindex = 1;

            message.data[0] = (byte)SdoCommand.READ;
            message.data[1] = (byte)((int)IndexSDO.POWER_MAP & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.POWER_MAP & 0xFF00) >> 8);

            message.data[3] = subindex;
            dispatcher.SubIndex = subindex;
            dispatcher.Index = (int)IndexSDO.POWER_MAP;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        temp_array[subindex - 1] = dispatcher.Data;
                        dispatcher.DataIsAvailable = false;


                        if (subindex == 18)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = subindex;
                        message.data[3] = subindex;
                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }
            PowerMapToWindow();
        }

        private void PowerMapLoad()
        {
            bool isFinished = false;
            byte subindex = 1;

            message.data[0] = (byte)SdoCommand.TX_2_BYTES;
            message.data[1] = (byte)((int)IndexSDO.POWER_MAP & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.POWER_MAP & 0xFF00) >> 8);

            message.data[3] = subindex;

            message.data[4] = (byte)(temp_array[subindex - 1] & 0xff);
            message.data[5] = (byte)((temp_array[subindex - 1] >> 8) & 0xff);


            dispatcher.SubIndex = subindex;
            dispatcher.Index = (int)IndexSDO.POWER_MAP;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        dispatcher.DataIsAvailable = false;

                        if (subindex == 18)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = subindex;
                        message.data[3] = subindex;
                        message.data[4] = (byte)(temp_array[subindex - 1] & 0xff);
                        message.data[5] = (byte)((temp_array[subindex - 1] & 0xff00) >> 8);
                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }

        }

        private void PowerMapToWindow()
        {
            tbPt1MaxTorque.Text = (temp_array[0] >> 4).ToString();
            tbPt1Speed.Text = (temp_array[1]).ToString();
            tbPt2MaxTorque.Text = (temp_array[2] >> 4).ToString();
            tbPt2Speed.Text = (temp_array[3]).ToString();
            tbPt3MaxTorque.Text = (temp_array[4] >> 4).ToString();
            tbPt3Speed.Text = (temp_array[5]).ToString();
            tbPt4MaxTorque.Text = (temp_array[6] >> 4).ToString();
            tbPt4Speed.Text = (temp_array[7]).ToString();
            tbPt5MaxTorque.Text = (temp_array[8] >> 4).ToString();
            tbPt5Speed.Text = (temp_array[9]).ToString();
            tbPt6MaxTorque.Text = (temp_array[10] >> 4).ToString();
            tbPt6Speed.Text = (temp_array[11]).ToString();
            tbPt7MaxTorque.Text = (temp_array[12] >> 4).ToString();
            tbPt7Speed.Text = (temp_array[13]).ToString();
            tbPt8MaxTorque.Text = (temp_array[14] >> 4).ToString();
            tbPt8Speed.Text = (temp_array[15]).ToString();
            tbPt9MaxTorque.Text = (temp_array[16] >> 4).ToString();
            tbPt9Speed.Text = (temp_array[17]).ToString();
        }

        private void PowerMapFromWindow()
        {
            temp_array[0] = Convert.ToUInt16(tbPt1MaxTorque.Text) << 4;
            temp_array[1] = Convert.ToUInt16(tbPt1Speed.Text);
            temp_array[2] = Convert.ToUInt16(tbPt2MaxTorque.Text) << 4;
            temp_array[3] = Convert.ToUInt16(tbPt2Speed.Text);
            temp_array[4] = Convert.ToUInt16(tbPt3MaxTorque.Text) << 4;
            temp_array[5] = Convert.ToUInt16(tbPt3Speed.Text);
            temp_array[6] = Convert.ToUInt16(tbPt4MaxTorque.Text) << 4;
            temp_array[7] = Convert.ToUInt16(tbPt4Speed.Text);
            temp_array[8] = Convert.ToUInt16(tbPt5MaxTorque.Text) << 4;
            temp_array[9] = Convert.ToUInt16(tbPt5Speed.Text);
            temp_array[10] = Convert.ToUInt16(tbPt6MaxTorque.Text) << 4;
            temp_array[11] = Convert.ToUInt16(tbPt6Speed.Text);
            temp_array[12] = Convert.ToUInt16(tbPt7MaxTorque.Text) << 4;
            temp_array[13] = Convert.ToUInt16(tbPt7Speed.Text);
            temp_array[14] = Convert.ToUInt16(tbPt8MaxTorque.Text) << 4;
            temp_array[15] = Convert.ToUInt16(tbPt8Speed.Text);
            temp_array[16] = Convert.ToUInt16(tbPt9MaxTorque.Text) << 4;
            temp_array[17] = Convert.ToUInt16(tbPt9Speed.Text);
        }

        #endregion

        #region Flux map

        private void FluxMapRead()
        {
            bool isFinished = false;
            byte subindex = 1;

            message.data[0] = (byte)SdoCommand.READ;
            message.data[1] = (byte)((int)IndexSDO.FLUX_MAP & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.FLUX_MAP & 0xFF00) >> 8);

            message.data[3] = subindex;
            dispatcher.SubIndex = subindex;
            dispatcher.Index = (int)IndexSDO.FLUX_MAP;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        temp_array[subindex - 1] = dispatcher.Data;
                        dispatcher.DataIsAvailable = false;


                        if (subindex == temp_array.Length)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = subindex;
                        message.data[3] = subindex;
                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }
            FluxMapToWindow();
        }

        private void FluxMapLoad()
        {
            bool isFinished = false;
            byte subindex = 1;

            message.data[0] = (byte)SdoCommand.TX_2_BYTES;
            message.data[1] = (byte)((int)IndexSDO.FLUX_MAP & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.FLUX_MAP & 0xFF00) >> 8);

            message.data[3] = subindex;

            message.data[4] = (byte)(temp_array[subindex - 1] & 0xff);
            message.data[5] = (byte)((temp_array[subindex - 1] >> 8) & 0xff);


            dispatcher.SubIndex = subindex;
            dispatcher.Index = (int)IndexSDO.FLUX_MAP;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        dispatcher.DataIsAvailable = false;

                        if (subindex == temp_array.Length)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = subindex;
                        message.data[3] = subindex;
                        message.data[4] = (byte)(temp_array[subindex - 1] & 0xff);
                        message.data[5] = (byte)((temp_array[subindex - 1] & 0xff00) >> 8);
                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }

        }

        private void FluxMapToWindow()
        {
            tbPt1Torque.Text = (temp_array[0] >> 4).ToString();
            tbPt1FluxCurrent.Text = (temp_array[1] >> 6).ToString();
            tbPt2Torque.Text = (temp_array[2] >> 4).ToString();
            tbPt2FluxCurrent.Text = (temp_array[3] >> 6).ToString();
            tbPt3Torque.Text = (temp_array[4] >> 4).ToString();
            tbPt3FluxCurrent.Text = (temp_array[5] >> 6).ToString();
            tbPt4Torque.Text = (temp_array[6] >> 4).ToString();
            tbPt4FluxCurrent.Text = (temp_array[7] >> 6).ToString();
            tbPt5Torque.Text = (temp_array[8] >> 4).ToString();
            tbPt5FluxCurrent.Text = (temp_array[9] >> 6).ToString();
            tbPt6Torque.Text = (temp_array[10] >> 4).ToString();
            tbPt6FluxCurrent.Text = (temp_array[11] >> 6).ToString();
            tbPt7Torque.Text = (temp_array[12] >> 4).ToString();
            tbPt7FluxCurrent.Text = (temp_array[13] >> 6).ToString();
            tbPt8Torque.Text = (temp_array[14] >> 4).ToString();
            tbPt8FluxCurrent.Text = (temp_array[15] >> 6).ToString();
            tbPt9Torque.Text = (temp_array[16] >> 4).ToString();
            tbPt9FluxCurrent.Text = (temp_array[17] >> 6).ToString();
        }

        private void FluxMapFromWindow()
        {
            temp_array[0] = Convert.ToUInt16(tbPt1Torque.Text) << 4;
            temp_array[1] = Convert.ToUInt16(tbPt1FluxCurrent.Text) << 6;
            temp_array[2] = Convert.ToUInt16(tbPt2Torque.Text) << 4;
            temp_array[3] = Convert.ToUInt16(tbPt2FluxCurrent.Text) << 6;
            temp_array[4] = Convert.ToUInt16(tbPt3Torque.Text) << 4;
            temp_array[5] = Convert.ToUInt16(tbPt3FluxCurrent.Text) << 6;
            temp_array[6] = Convert.ToUInt16(tbPt4Torque.Text) << 4;
            temp_array[7] = Convert.ToUInt16(tbPt4FluxCurrent.Text) << 6;
            temp_array[8] = Convert.ToUInt16(tbPt5Torque.Text) << 4;
            temp_array[9] = Convert.ToUInt16(tbPt5FluxCurrent.Text) << 6;
            temp_array[10] = Convert.ToUInt16(tbPt6Torque.Text) << 4;
            temp_array[11] = Convert.ToUInt16(tbPt6FluxCurrent.Text) << 6;
            temp_array[12] = Convert.ToUInt16(tbPt7Torque.Text) << 4;
            temp_array[13] = Convert.ToUInt16(tbPt7FluxCurrent.Text) << 6;
            temp_array[14] = Convert.ToUInt16(tbPt8Torque.Text) << 4;
            temp_array[15] = Convert.ToUInt16(tbPt8FluxCurrent.Text) << 6;
            temp_array[16] = Convert.ToUInt16(tbPt9Torque.Text) << 4;
            temp_array[17] = Convert.ToUInt16(tbPt9FluxCurrent.Text) << 6;
        }

        #endregion

        #region LmMap
        private void LmMapRead()
        {
            bool isFinished = false;
            byte subindex = 1;

            message.data[0] = (byte)SdoCommand.READ;
            message.data[1] = (byte)((int)IndexSDO.IND_LM_MAP & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.IND_LM_MAP & 0xFF00) >> 8);

            message.data[3] = subindex;
            dispatcher.SubIndex = subindex;
            dispatcher.Index = (int)IndexSDO.IND_LM_MAP;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        temp_array[subindex - 1] = dispatcher.Data;
                        dispatcher.DataIsAvailable = false;


                        if (subindex == temp_array.Length)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = subindex;
                        message.data[3] = subindex;
                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }
            LmMapToWindow();
        }

        private void LmMapLoad()
        {
            bool isFinished = false;
            byte subindex = 1;

            message.data[0] = (byte)SdoCommand.TX_2_BYTES;
            message.data[1] = (byte)((int)IndexSDO.IND_LM_MAP & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.IND_LM_MAP & 0xFF00) >> 8);

            message.data[3] = subindex;

            message.data[4] = (byte)(temp_array[subindex - 1] & 0xff);
            message.data[5] = (byte)((temp_array[subindex - 1] >> 8) & 0xff);


            dispatcher.SubIndex = subindex;
            dispatcher.Index = (int)IndexSDO.IND_LM_MAP;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        dispatcher.DataIsAvailable = false;

                        if (subindex == temp_array.Length)
                        {
                            isFinished = true;
                            continue;
                        }

                        subindex++;
                        dispatcher.SubIndex = subindex;
                        message.data[3] = subindex;
                        message.data[4] = (byte)(temp_array[subindex - 1] & 0xff);
                        message.data[5] = (byte)((temp_array[subindex - 1] & 0xff00) >> 8);
                        CanDriver.SendMessage(message);
                    }
                    else if (dispatcher.RequestIsAborted)
                    {
                        dispatcher.RequestIsAborted = false;
                        if (dispatcher.AdditionalInfo == 0x11)
                        {
                            dispatcher.AdditionalInfo = 0x00;
                            if (subindex == temp_array.Length)
                            {
                                isFinished = true;
                                continue;
                            }
                            subindex++;
                            dispatcher.SubIndex = MotorDataSubIndArray[subindex];
                            message.data[3] = MotorDataSubIndArray[subindex];
                            CanDriver.SendMessage(message);
                        }
                        else
                        {
                            MessageBox.Show("Error\n Code: " + dispatcher.AbortCode.ToString() + "\nAdditional info: " + dispatcher.AdditionalInfo.ToString());
                            break;
                        }
                    }
                }
            }

        }

        private void LmMapToWindow()
        {
            tbPt1Current.Text = (temp_array[0] >> 4).ToString();
            tbPt1Inductance.Text = (temp_array[1] * Math.Pow(2, -12)).ToString();
            tbPt2Current.Text = (temp_array[2] >> 4).ToString();
            tbPt2Inductance.Text = (temp_array[3] * Math.Pow(2, -12)).ToString();
            tbPt3Current.Text = (temp_array[4] >> 4).ToString();
            tbPt3Inductance.Text = (temp_array[5] * Math.Pow(2, -12)).ToString();
            tbPt4Current.Text = (temp_array[6] >> 4).ToString();
            tbPt4Inductance.Text = (temp_array[7] * Math.Pow(2, -12)).ToString();
            tbPt5Current.Text = (temp_array[8] >> 4).ToString();
            tbPt5Inductance.Text = (temp_array[9] * Math.Pow(2, -12)).ToString();
            tbPt6Current.Text = (temp_array[10] >> 4).ToString();
            tbPt6Inductance.Text = (temp_array[11] * Math.Pow(2, -12)).ToString();
            tbPt7Current.Text = (temp_array[12] >> 4).ToString();
            tbPt7Inductance.Text = (temp_array[13] * Math.Pow(2, -12)).ToString();
            tbPt8Current.Text = (temp_array[14] >> 4).ToString();
            tbPt8Inductance.Text = (temp_array[15] * Math.Pow(2, -12)).ToString();
            tbPt9Current.Text = (temp_array[16] >> 4).ToString();
            tbPt9Inductance.Text = (temp_array[17] * Math.Pow(2, -12)).ToString();
        }

        private void LmMapFromWindow()
        {
            temp_array[0] = Convert.ToUInt16(tbPt1Current.Text) << 4;
            temp_array[1] = (Int32)(Convert.ToSingle(tbPt1Inductance.Text) / Math.Pow(2, -12));
            temp_array[2] = Convert.ToUInt16(tbPt2Current.Text) << 4;
            temp_array[3] = (Int32)(Convert.ToSingle(tbPt2Inductance.Text) / Math.Pow(2, -12));
            temp_array[4] = Convert.ToUInt16(tbPt3Current.Text) << 4;
            temp_array[5] = (Int32)(Convert.ToSingle(tbPt3Inductance.Text) / Math.Pow(2, -12));
            temp_array[6] = Convert.ToUInt16(tbPt4Current.Text) << 4;
            temp_array[7] = (Int32)(Convert.ToSingle(tbPt4Inductance.Text) / Math.Pow(2, -12));
            temp_array[8] = Convert.ToUInt16(tbPt5Current.Text) << 4;
            temp_array[9] = (Int32)(Convert.ToSingle(tbPt5Inductance.Text) / Math.Pow(2, -12));
            temp_array[10] = Convert.ToUInt16(tbPt6Current.Text) << 4;
            temp_array[11] = (Int32)(Convert.ToSingle(tbPt6Inductance.Text) / Math.Pow(2, -12));
            temp_array[12] = Convert.ToUInt16(tbPt7Current.Text) << 4;
            temp_array[13] = (Int32)(Convert.ToSingle(tbPt7Inductance.Text) / Math.Pow(2, -12));
            temp_array[14] = Convert.ToUInt16(tbPt8Current.Text) << 4;
            temp_array[15] = (Int32)(Convert.ToSingle(tbPt8Inductance.Text) / Math.Pow(2, -12));
            temp_array[16] = Convert.ToUInt16(tbPt9Current.Text) << 4;
            temp_array[17] = (Int32)(Convert.ToSingle(tbPt9Inductance.Text) / Math.Pow(2, -12));
        }

        #endregion

        private void SetAccessLevel()
        {
            message.id = 0x600 + NodeId;
            bool isFinished = false;
            byte subindex = 3;


            message.data[0] = (byte)SdoCommand.TX_2_BYTES;
            message.data[1] = (byte)((int)IndexSDO.ACCESS_LEVEL & 0xFF);
            message.data[2] = (byte)(((int)IndexSDO.ACCESS_LEVEL & 0xFF00) >> 8);

            message.data[3] = subindex;
            dispatcher.SubIndex = subindex;
            dispatcher.Index = (int)IndexSDO.ACCESS_LEVEL;
            CanDriver.SendMessage(message);

            while (!isFinished)
            {
                lock (locker)
                {
                    if (dispatcher.DataIsAvailable)
                    {
                        dispatcher.DataIsAvailable = false;

                        subindex--;

                        if (dispatcher.SubIndex == 1)
                        {
                            dispatcher.Data = 0x04;
                            isFinished = true;
                            continue;
                        }
                        if (subindex == 2)
                        {
                            message.data[4] = 0xDF;
                            message.data[5] = 0x4B;
                        }
                        if (subindex == 1)
                        {
                            message.data[0] = (byte)SdoCommand.READ;
                            message.data[4] = 0x0;
                            message.data[5] = 0x0;
                        }


                        dispatcher.SubIndex = subindex;
                        message.data[3] = subindex;
                        CanDriver.SendMessage(message);
                    }
                }
            }

        }

        int ReadSdo(uint index, uint subindex)
        {
            int result = 0;


            return result;
        }

        #region NodeId Select
        private void cbNodeListDrop_Handle(object sender, EventArgs e)
        {
            ComboBox nodeslist = sender as ComboBox;
            nodeslist.Items.Clear();
            foreach (int i in nodes)
                nodeslist.Items.Add(i);
        }

        private void cbNodeSelected_Handle(object sender, SelectionChangedEventArgs e)
        {
            ComboBox nodeslist = sender as ComboBox;
            NodeId = Convert.ToUInt32(nodeslist.SelectedValue);            
            SetAccessLevel();
            btnRead_ClickHandler(tc_Tabs, null);
            tbNewNodeId.Text = NodeId.ToString();
        }
        #endregion

        private void ValueIsChanged_Handler(object sender, KeyEventArgs e)
        {
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || e.Key == Key.OemComma)
            {
                e.Handled = false;
            }
            else
                e.Handled = true;
        }

        private void tc_Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(CanDriver != null && NodeId != 0)
                btnRead_ClickHandler(sender, null);
        }

        private void btnChangeID_ClickHandler(object sender, RoutedEventArgs e)
        {
            IdLoad();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                timerDelay++;

                if (timerDelay >= 10)   // 1 раз в секунду
                {
                    timerDelay = 0;
                    if (CanDriver.IsConnected)
                    {
                        tbCanAdapter.Text = "Connected.\tSpeed: " + CanDriver.BitRate + " kBit/s";
                    }
                    else
                    {
                        tbCanAdapter.Text = "Not connected.\tSpeed: -";
                    }
                }
            }));
        }
    }
}
