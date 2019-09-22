using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO.Ports;
using System.Net.NetworkInformation;

/*Алгоритм:
 * 1. Пришел пакет, записали его в массив байт
 * 2. Массив отправили в метод интерпретации, который нам должен вернуть экземпляр класса или ошибку (nuull)
 *    А в мэйне мы проверяем, если null, то игнорируем этот пакет
 *    В классе:
 *    - адрес
 *    - фуннкция
 *    - лист с адресами 
 *    - причём проверка сразу должна произойти, можем ли мы выдать ответ по этому запросу. Если нет, то null
 * 3. Если класс вернулся, то обращаемся к карте и через метод построения ответа вытаскиваем регистры и строим чексумму
 *    Метод возвращает готовый пакет ответа
 * 4. Записываем ответ в порт
 */

/*На будущее:
 * 1. Сделать проверку пришедшего пакета чексуммой, её анализ и сравнение с пакетом          
 */


namespace ModbusSlaveTest1
{
    class Program
    {
        //Все данные приходят не в хексе! Они приходят в byte
        class DataPacket 
        {
            public byte Address; // адрес в модбасе
            public byte Function; // функция модбаса
            public byte ByteCount; //колиичество байт в ответе 
            public int FirstRegister; //первый регистр в запросе
            public int CountRegister; //кол-во региистров в запросе
            public byte CheckSumHI; //старший байт чексуммы
            public byte CheckSumLO; //младший байт чексуммы
            public List<int> Registers = new List<int>(); //лист с перечнем адресов требуемых регистров
            public List<int> ResultRegisters = new List<int>(); //лист со значениями реггистров согласно адресам

        }

        //массив с картой регистров, адрес регистра = индекс в массиве.
        public static int[] HoldingRegistersMap = {3000,3001,3002,3003,3004,3005,3006,3007,3008,3009,3010,3011,3012,3013,3014,3015,3016,3017,3018,3019,3020}; 


        //метод поиска значений регистров по карте
        public static List<int> ReadRegisters(List<int> Addresses)
        {
            List<int> Result = new List<int>();
            if ((HoldingRegistersMap.Length - Addresses[0]) >= Addresses.Count)
            {
                foreach (int index in Addresses)
                {
                    Result.Add(HoldingRegistersMap[index]);
                }
                return Result;
            }
            else { Console.WriteLine("Запрос выходит за карту регистров"); return null; }
        }


        private static DataPacket ReadDataPacket(byte[] Packet, byte Address, byte Function)
        {
            //Стандартная длина пакета запроса 8 байт
            if (Packet.Length == 8)
            {
                DataPacket UnpackedPacket = new DataPacket();
                UnpackedPacket.Address = Packet[0];
                //адрес должен соответствовать нашему
                if (UnpackedPacket.Address != Address) { Console.WriteLine("Адрес не совпадает"); return null; }                
                UnpackedPacket.Function = Packet[1];
                //функция должна соответствовать ожидаемой
                if (UnpackedPacket.Function != Function) { Console.WriteLine("Функция не совпадает"); return null; }
                UnpackedPacket.FirstRegister = (Packet[2] << 8) + Packet[3];
                UnpackedPacket.CountRegister = (Packet[4] << 8) + Packet[5];
                //получим список адресов регистров
                for (int i = 0; i < UnpackedPacket.CountRegister; i++)
                {
                    UnpackedPacket.Registers.Add(UnpackedPacket.FirstRegister + i);
                }
                //заполним лист с значениями регистров
                UnpackedPacket.ResultRegisters = ReadRegisters(UnpackedPacket.Registers);

                //Проверка на null,
                if (UnpackedPacket.ResultRegisters == null)
                {
                    Console.WriteLine("Регистры не найдены ");
                    return null;
                }
                else { return UnpackedPacket; }
            }
            else { Console.WriteLine("Длина пакета не равна 8"); return null; }
        }

        private static byte[] FormPacket(DataPacket Packet)
        {
            List<byte> frameList = new List<byte>(); //лист с пакетом из байт
            frameList.Add(Packet.Address); //фрейм с адресом
            frameList.Add(Packet.Function); // фрейм с фйнкцией
            //количество байт = кол-во регистров * 2, так как регистр = 2 байта.
            byte ByteCount = Convert.ToByte(Packet.ResultRegisters.Count);
            ByteCount *= 2;
            frameList.Add(ByteCount); // фрейм с колиичеством байт
            foreach (int reg in Packet.ResultRegisters)
            {
                //реггистр разделяем на два байта
                frameList.Add((byte)(reg >> 8));
                frameList.Add((byte)reg);
            }

            byte[] crc = CalculateCRC(frameList);
            frameList.Add(crc[0]);
            frameList.Add(crc[1]);

            //Перевод листа в массив byte
            byte[] result = new byte[frameList.Count];
            for (int i = 0; i < frameList.Count; i++)
            {
                result[i] = frameList[i];
            }
            return result;
        }

        private static byte[] CalculateCRC(List<byte> data)
        {
            ushort CRCFull = 0xFFFF; // Set the 16-bit register (CRC register) = FFFFH.
            byte CRCHigh = 0xFF, CRCLow = 0xFF;
            char CRCLSB;
            byte[] CRC = new byte[2];
            for (int i = 0; i < (data.Count); i++)
            {
                CRCFull = (ushort)(CRCFull ^ data[i]); // 

                for (int j = 0; j < 8; j++)
                {
                    CRCLSB = (char)(CRCFull & 0x0001);
                    CRCFull = (ushort)((CRCFull >> 1) & 0x7FFF);

                    if (CRCLSB == 1)
                        CRCFull = (ushort)(CRCFull ^ 0xA001);
                }
            }
            CRC[1] = CRCHigh = (byte)((CRCFull >> 8) & 0xFF);
            CRC[0] = CRCLow = (byte)(CRCFull & 0xFF);
            return CRC;
        }



        public static UInt16 FromBytes(byte LoVal, byte HiVal)
        {
            return (UInt16)(HiVal * 256 + LoVal);
        }

        public static UInt16 FromByteArray(byte[] bytes)
        {
            // bytes[0] -> HighByte
            // bytes[1] -> LowByte
            return FromBytes(bytes[1], bytes[0]);
        }

        public static UInt16[] ByteToUInt16(byte[] bytes)
        {
            UInt16[] values = new UInt16[bytes.Length / 2];
            int counter = 0;
            for (int cnt = 0; cnt < bytes.Length / 2; cnt++)
                values[cnt] = FromByteArray(new byte[] { bytes[counter++], bytes[counter++] });
            return values;
        }

        //пинг в отдельном потоке
        public static void Ping()
        {
            
            List<string> serversList = new List<string>();
            serversList.Add("microsoft.com");
            serversList.Add("google.com");
            serversList.Add("10.9.119.207");
            serversList.Add("192.168.1.1");

            

            Ping ping = new System.Net.NetworkInformation.Ping();
            PingReply pingReply = null;
            while (true)
            {
                for (int i = 0; i < serversList.Count; i++ )
                    {
                        pingReply = ping.Send(serversList[i]);

                        if (pingReply.Status != IPStatus.TimedOut)
                        {
                            /* Console.WriteLine("------ПИНГ-------");
                             Console.WriteLine(server);
                             Console.WriteLine(pingReply.Address);
                             Console.WriteLine(pingReply.Status);
                             Console.WriteLine(pingReply.RoundtripTime);
                             Console.WriteLine(pingReply.Options.Ttl);
                             Console.WriteLine(pingReply.Buffer.Length);
                             Console.WriteLine("------КОНЕЦ ПИНГА-------");*/

                            HoldingRegistersMap[i] = 1;
                            Console.WriteLine("Пинг сервера: " + serversList[i] + " прошёл");

                        }
                        else
                        {
                            // Console.WriteLine(server); //server
                            // Console.WriteLine(pingReply.Status);

                            HoldingRegistersMap[i] = 0;
                            Console.WriteLine("Сервер: " + serversList[i] + " не ответил");
                        }
                    }
                Thread.Sleep(1000);
            }

        }


        static void Main(string[] args)
        {
           // Program prg = new Program();
            SerialPort serialPort1 = null;
            int speed = 9600;   
            string comport = "COM8";
            int interval = 100;
            byte slaveAddress = 16; //наш адрес
            byte Function = 3; //функция, на которую мы можем ответить
            
            //объявление еще одного потока
            Thread myThread = new Thread(new ThreadStart(Ping));
            myThread.Start(); // запускаем поток

            //Попытка открыть порт
            try
            {
                serialPort1 = new SerialPort(comport, speed, Parity.None, 8, StopBits.One); // настройки как на панели
                serialPort1.Open(); // Занимаем порт
                Console.WriteLine("Порт открыт");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            //сделать условие, что делать, если порт не откроется
            while (true)
            {
                if (serialPort1.IsOpen)
                {
                    if (serialPort1.BytesToRead >= 5)   // если пришел не какой то левый шум, то распакуем пакет
                    {
                        byte[] bufferReceiver = new byte[serialPort1.BytesToRead];
                        serialPort1.Read(bufferReceiver, 0, serialPort1.BytesToRead);
                        serialPort1.DiscardInBuffer();

                        //конвертируем массив байт и отображаем
                        string hex1 = BitConverter.ToString(bufferReceiver);
                        Console.WriteLine("Получен пакет: " + hex1);

                        DataPacket Packet = new DataPacket();
                        Packet = ReadDataPacket(bufferReceiver, slaveAddress, Function);
                        if (Packet != null)
                        {
                            //вычислим пакет для отправки
                            byte[] Answer = FormPacket(Packet); 
                            //отправим пакет
                            string hex = BitConverter.ToString(Answer);
                            serialPort1.Write(Answer, 0, Answer.Length);
                            Console.WriteLine("Отправлен пакет: " + hex);
                        }
                        else { Console.WriteLine("Согласно запросу данных нет!"); }

                    }
                }
                Thread.Sleep(interval);
            }    
        }
    }
}


/*


//ЗДЕСЬ БУДЕ ПОСТРОЕНИЕ ОТВЕТА
byte[] frame0 = { 1, 3, 2, 0, 7 };  //фрейм без контрольной суммы

List<byte> frameList = new List<byte>();

foreach (byte t in frame0)
{
    frameList.Add(t);
}
                        
//отправляем кадр на вычисление контрольной суммы
byte[] crc = prg.CalculateCRC(frame0); 
frameList.Add(crc[0]);
frameList.Add(crc[1]);

//преобразуем лист в массив                       
byte[] result = new byte[frameList.Count];
for (int i = 0; i < frameList.Count; i++)
{
    result[i] = frameList[i];
}

string hex3 = BitConverter.ToString(result);

serialPort1.Write(result, 0, result.Length);

Console.WriteLine("Отправлен пакет: " + hex3);*/

/*     private byte[] WriteResponce(byte slaveAddress, int startAddress, byte function, uint numberOfPoints)  //объявление функции чтения регистра
     {
         byte[] frame = new byte[8];
         frame[0] = slaveAddress;			    // Slave Address
         frame[1] = function;				    // Function             
         frame[2] = (byte)(startAddress >> 8);	// Starting Address High
         frame[3] = (byte)startAddress;		    // Starting Address Low            
         frame[4] = (byte)(numberOfPoints >> 8);	// Quantity of Registers High
         frame[5] = (byte)numberOfPoints;		// Quantity of Registers Low
         byte[] crc = this.CalculateCRC(frame);  // Calculate CRC.
         frame[frame.Length - 2] = crc[0];       // Error Check Low
         frame[frame.Length - 1] = crc[1];       // Error Check High
         return frame;
     }*/

/*
using (TextWriter tw = new StreamWriter("d:\\MyLog.txt"))
            {
                Ping ping = new System.Net.NetworkInformation.Ping();
                PingReply pingReply = null;

                foreach (string server in serversList)
                {
                    pingReply = ping.Send(server);

                    if (pingReply.Status != IPStatus.TimedOut)
                    {
                        tw.WriteLine(server); //server
                        tw.WriteLine(pingReply.Address); //IP
                        tw.WriteLine(pingReply.Status); //Статус
                        tw.WriteLine(pingReply.RoundtripTime); //Время ответа
                        tw.WriteLine(pingReply.Options.Ttl); //TTL
                        tw.WriteLine(pingReply.Options.DontFragment); //Фрагментирование
                        tw.WriteLine(pingReply.Buffer.Length); //Размер буфера
                        tw.WriteLine();
                    }
                    else
                    {
                        tw.WriteLine(server); //server
                        tw.WriteLine(pingReply.Status);
                        tw.WriteLine();
                    }
                }
            }*/