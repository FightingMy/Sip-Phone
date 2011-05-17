﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Threading;
using System.Net.Sockets;

using System.Media;

//github.com    репозиторий
//rfc4317       SDP
//pjsip.org библиотека    pjmedia

namespace SIPLib
{
    public delegate void Del(string Info, string Caption);
    public delegate bool DelRequest(string str);
    public delegate void DelCloseSession(string Name);

    public class Session    //сама сессия
    {
        Del DelOutput;
        DelCloseSession DelClosesession;
        string ToIP;        //IP клиента
        string ToUser;      //имя клиента
        string MyName;    //наше имя
        System.Net.IPAddress myIP;  //наш IP
        int n = 0;  //порядок запроса
        int port, myaudioport, toaudioport;   //порт
        bool SessionConfirmed = false; //флаг подтверждённости сессии (на наш запрос ответили)
        string SessionID;
        string _SDP;
        Thread WaitForAnswer;

        //==============конструктор==================
        public Session(System.Net.IPAddress myIP, int myPort, string ToIP, string ToUser, string FromUser, Del d, DelCloseSession d1, string ID, string SDPfunc)    //конструктор при звонке
        {
            DelOutput = d;
            this.ToIP = ToIP;
            this.ToUser = ToUser;
            this.MyName = FromUser;
            this.myIP = myIP;
            this.port = myPort;
            this.myaudioport = 11010;
            this.SessionID = ID;
            DelClosesession = d1;
            n++;

            if (SDPfunc.Length != 0)
            {
                this._SDP = SDPcombine(SDPfunc);
            }
            else
            {
                this._SDP = SDP();
            }
        }

        //==============внешние функции==============
        public string _ToUser
        {
            get
            {
                return ToUser;
            }
        }

        public void CloseSession()
        {


        }

        public bool _SessionConfirmed
        {
            get
            {
                return SessionConfirmed;
            }
        }

        public string _SessionID
        {
            get
            {
                return this.SessionID;
            }
        }

        public bool CheckSessionByID(string ID)
        {
            if (this.SessionID == ID) return true;
            else return false;
        }

        public bool WatchInfo(string Info)
        {
            //DelOutput(Info);
            this.n++;

            if (Info.Contains("BYE"))    //пока
            {
                BYEDecompile(Info);
                return true;
            }

            if (Info.Contains("CANCEL"))    //отмена
            {

                return true;
            }

            if (Info.Contains("REGISTER"))    //регистрация
            {

                return true;
            }

            if (Info.Contains("OPTIONS"))    //запрос свойств
            {
                _2XXCompile("00", true, false);  //вызов подтверждения
                return true;
            }

            if (Info.Contains("SIP/2.0 1"))    //ответ    OK
            {
                DelOutput("Получено 1XX", "Получен ответ");
                return true;
            }

            if (Info.Contains("SIP/2.0 2"))    //ответ    OK
            {
                DelOutput("Получено 2XX (согласие)", "Получен ответ");
                this._2XXDecompile(Info);
                return true;
            }

            if (Info.Contains("SIP/2.0 3"))    //ответ    OK
            {
                DelOutput("Получено 3XX", "Получен ответ");
                this._3XXDecompile(Info);
                return true;
            }

            /*if (Info.Contains("SIP/2.0 4"))    //ответ    OK
            {
                DelOutput("Получено 4XX","Получен ответ");
                this._4XXDecompile(Info);
                return true;
            }*/

            if (Info.Contains("SIP/2.0 5"))    //ответ    OK
            {
                DelOutput("Получено 5XX", "Получен ответ");
                this._5XXDecompile(Info);
                return true;
            }

            if (Info.Contains("SIP/2.0 6"))    //ответ      DECLINE
            {
                DelOutput("Получен 6XX (отказ)", "Получен ответ");
                this._6XXDecompile(Info);
                return true;
            }

            return false;
        }

        //==============внутренние функции==============

        bool SendInfo(string Info)      //отправка информации
        {
            System.Net.IPAddress ipAddress;         //IP того, кому посылаем
            UdpClient udpClient = new UdpClient();  //создаём UDP клиент

            Byte[] sendBytes = Encoding.ASCII.GetBytes(Info);       //преобразуем строку запроса

            if (System.Net.IPAddress.TryParse(ToIP, out ipAddress))    //получаем адрес компа. out - возвращаем по ссылке
            {
                System.Net.IPEndPoint ipEndPoint = new System.Net.IPEndPoint(ipAddress, port); //создаём точку назначения

                try
                {
                    udpClient.Send(sendBytes, sendBytes.Length, ipEndPoint); //посылаем информацию
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                IPAddress[] ips;
                ips = Dns.GetHostAddresses(ToIP);   //если для сервера и тп
                foreach (IPAddress ip in ips)
                {
                    System.Net.IPEndPoint ipEndPoint = new System.Net.IPEndPoint(ip, port); //создаём точку назначения

                    try
                    {
                        udpClient.Send(sendBytes, sendBytes.Length, ipEndPoint); //посылаем информацию
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                if (ips.Length == 0) return false;
            };

            return true;
        }

        void WaitForAnswerFunc()    //функция проверки активности сессии
        {
            for (int i = 0; i < 300; i++)    //проверяем в течение 30и секунд
            {
                Thread.Sleep(100);
                if (_SessionConfirmed == true) return;// return true;
            }
            this.CloseSession();
        }

        //==создание и разбор внутринностей запросов==
        public void Invite()
        {
            string Request = "";

            Request += "INVITE sip: " + this.ToUser + "@" + this.ToIP + " SIP/2.0 " + "\n";
            Request += "Record-Route: <sip:" + this.ToUser + "@" + this.myIP.ToString() + ";lr>" + "\n";
            Request += "From: " + "\"" + this.MyName + "\"" + "<sip: " + this.MyName + "@" + this.myIP.ToString() + "> " + "\n";
            Request += "To: " + "<sip: " + this.ToUser + "@" + this.ToIP + "> " + "\n";
            Request += "Call-ID: " + SessionID + "@" + this.myIP + "\n";
            Request += "CSeq: " + (this.n).ToString() + " INVITE" + "\n";

            Request += "Date: " + DateTime.Now.ToString() + "\n";   //дата и время
            Request += "Allow: INVITE, ACK, CANCEL, BYE" + "\n";

            Request += _SDP;

            DelOutput(Request, "Из Invite");

            if (!SendInfo(Request)) DelOutput("Invite failed", "Внутри Invite");
            WaitForAnswer = new Thread(WaitForAnswerFunc);
            WaitForAnswer.Start();

        }

        public void BYECompile()
        {
            string Request = "";

            Request += "BYE sip: " + this.ToUser + "@" + this.ToIP + " SIP/2.0 " + "\n";
            Request += "Record-Route: <sip:" + this.ToUser + "@" + this.myIP.ToString() + ";lr>" + "\n";
            Request += "From: " + "\"" + this.MyName + "\"" + "<sip: " + this.MyName + "@" + this.myIP.ToString() + "> " + "\n";
            Request += "To: " + "<sip: " + this.ToUser + "@" + this.ToIP + "> " + "\n";
            Request += "Call-ID: " + SessionID + "@" + this.myIP + "\n";
            Request += "CSeq:" + (++this.n).ToString() + " BYE" + "\n";

            Request += "Date: " + DateTime.Now.ToString() + "\n";   //дата и время

            SendInfo(Request);
            DelClosesession(ToUser);
            DelOutput(Request, "Внутри BYE");

        }
        void BYEDecompile(string Info)
        {
            this._2XXCompile("00", false, true);
            this.CloseSession();
        }

        /*string REGISTER() { return null; }
        int REGISTERDecompile(string Info) { return 0; }*/





        //==создание и разбор внутринностей ответов==

        public void _1XXCompile(string _XX)
        {
            string Request = "";
            Request += "SIP/2.0 1";
            switch (_XX)
            {
                case "80": Request += _XX + " Ringing\n"; break;
                default: return;
            }

            Request += "From: " + this.MyName + " <sip:" + this.MyName + "@" + this.myIP.ToString() + ">" + "\n";
            Request += "To: <sip: " + this.ToUser + "@" + this.ToIP + ">" + "\n";
            Request += this.SessionID.ToString() + "\n";
            Request += "Cseq: " + (++this.n).ToString();
            switch (_XX)
            {
                case "80": Request += _XX + " Ringing\n"; break;
                default: return;
            }

            Request += "Date: " + DateTime.Now.ToString();
            SendInfo(Request);
        }

        public void _2XXCompile(string _XX, bool SDPRequired, bool EndSession)
        {
            string Request = "";
            Request += "SIP/2.0 2" + _XX + " OK" + "\n";
            Request += "From: " + this.MyName + " <sip:" + this.MyName + "@" + this.myIP.ToString() + ">" + "\n";
            Request += "To: <sip: " + this.ToUser + "@" + this.ToIP + ">" + "\n";
            Request += this.SessionID.ToString() + "\n";
            Request += "Cseq: " + (++this.n).ToString() + " OK" + "\n";
            Request += "Date: " + DateTime.Now.ToString() + "\n";
            if (SDPRequired)
            {
                Request += _SDP;
            }
            SendInfo(Request);

            if (EndSession)
                this.CloseSession();
        }
        void _2XXDecompile(string Info)
        {

        }


        public void _3XXCompile(string _XX, bool SDPRequired, bool EndSession)
        {
            string Request = "";
            Request += "SIP/2.0 3";
            switch (_XX)
            {
                case "00": Request += _XX + " Multiple Choices\n"; break;
                case "01": Request += _XX + " Moved Permanently\n"; break;
                case "02": Request += _XX + " Moved Temporary\n"; break;
                default: return;
            }

            Request += "From: " + this.MyName + " <sip:" + this.MyName + "@" + this.myIP.ToString() + ">" + "\n";
            Request += "To: <sip: " + this.ToUser + "@" + this.ToIP + ">" + "\n";
            Request += this.SessionID.ToString() + "\n";
            Request += "Cseq: " + (++this.n).ToString();// " Decline" + "\n";
            switch (_XX)
            {
                case "00": Request += _XX + " Multiple Choices\n"; break;
                case "01": Request += _XX + " Moved Permanently\n"; break;
                case "02": Request += _XX + " Moved Temporary\n"; break;
                default: return;
            }

            Request += "Date: " + DateTime.Now.ToString() + "\n";
            if (SDPRequired)
                Request += "\n" + SDP();

            SendInfo(Request);
            DelOutput(Request, "Внутри 3XX");

            if (EndSession)
                this.CloseSession();
        }
        public void _3XXDecompile(string Info)
        {
        }

        /*
        public void _4XXCompile(string _XX, bool SDPRequired, bool EndSession)
        {
        }
        public void _4XXDecompile(string _XX, bool SDPRequired, bool EndSession)
        {
        }
        */

        public void _5XXCompile(string _XX, bool SDPRequired, bool EndSession)
        {
            string Request = "";
            Request += "SIP/2.0 5";
            switch (_XX)
            {
                case "00": Request += _XX + " Server Internal Error\n"; break;
                case "01": Request += _XX + " Not Implemented\n"; break;
                case "02": Request += _XX + " Bad Gateway\n"; break;
                case "03": Request += _XX + " Service Unavailable\n"; break;
                default: Request += "01 Not Implemented\n"; break;
            }

            Request += "From: " + this.MyName + " <sip:" + this.MyName + "@" + this.myIP.ToString() + ">" + "\n";
            Request += "To: <sip: " + this.ToUser + "@" + this.ToIP + ">" + "\n";
            Request += this.SessionID.ToString() + "\n";
            Request += "Cseq: " + (++this.n).ToString();// " Decline" + "\n";
            switch (_XX)
            {
                case "00": Request += _XX + " Server Internal Error\n"; break;
                case "01": Request += _XX + " Not Implemented\n"; break;
                case "02": Request += _XX + " Bad Gateway\n"; break;
                case "03": Request += _XX + " Service Unavailable\n"; break;
                default: Request += "01 Not Implemented\n"; break;
            }

            Request += "Date: " + DateTime.Now.ToString() + "\n";
            if (SDPRequired)
                Request += "\n" + SDP();

            SendInfo(Request);
            DelOutput(Request, "Внутри 5XX");

            if (EndSession)
                this.CloseSession();

        }
        public void _5XXDecompile(string Info)
        {
        }

        public void _6XXCompile(string _XX, bool SDPRequired, bool EndSession)
        {
            string Request = "";
            Request += "SIP/2.0 6";
            switch (_XX)
            {
                case "00": Request += _XX + " Busy Everywhere\n"; break;
                case "03": Request += _XX + " Decline\n"; break;
                case "04": Request += _XX + " Does Not Exist Anywhere\n"; break;
                case "06": Request += _XX + " Not Acceptable\n"; break;
                default: Request += "03 Decline\n"; break;
            }

            Request += "From: " + this.MyName + " <sip:" + this.MyName + "@" + this.myIP.ToString() + ">" + "\n";
            Request += "To: <sip: " + this.ToUser + "@" + this.ToIP + ">" + "\n";
            Request += this.SessionID.ToString() + "\n";
            Request += "Cseq: " + (++this.n).ToString();// " Decline" + "\n";
            switch (_XX)
            {
                case "00": Request += _XX + " Busy Everywhere\n"; break;
                case "03": Request += _XX + " Decline\n"; break;
                case "04": Request += _XX + " Does Not Exist Anywhere\n"; break;
                case "06": Request += _XX + " Not Acceptable\n"; break;
                default: Request += "03 Decline\n"; break;
            }

            Request += "Date: " + DateTime.Now.ToString() + "\n";

            if (SDPRequired)
                Request += "\n" + SDP();

            SendInfo(Request);
            DelOutput(Request, "Внутри 6XX");

            if (EndSession)
                this.CloseSession();

        }
        public void _6XXDecompile(string Info)
        {
            this._2XXCompile("00", false, true);
            this.CloseSession();
        }


        //==================================================\\
        //                      SDP                         \\
        //==================================================\\

        string SDP()
        {
            string CodecInfo = "", tmp = "";
            CodecInfo += "Content-Type: application/sdp\n";

            tmp += "v=0\n";
            tmp += "o=" + MyName + "5648733" + "247594567" + "IN IP4" + myIP + "\n";   //???????????????????????
            tmp += "c=IN IP4 " + myIP + "\n";
            tmp += "m=audio " + this.myaudioport.ToString() + " RTP/AVP 0 8\n";
            tmp += "a=rtpmap:0 PCMU/8000\n";
            tmp += "a=rtpmap:8 PCMA/8000\n";

            CodecInfo += "Content-Length: " + tmp.Length + "\n\n" + tmp;

            return CodecInfo;
        }

        string SDPcombine(string str)
        {
            string CodecInfo = "", tmp = "", tmp1 = "";
            CodecInfo += "Content-Type: application/sdp\n";
            tmp += "v=0\n";
            tmp += "o=" + MyName + "??????" + "??????" + myIP + "\n";   //???????????????????????
            tmp += "c=IN IP4 " + myIP + "\n";

            string[] ms = str.Split('\n');
            foreach (string str1 in ms)
            {
                if (str1.Contains("m=audio"))
                {
                    tmp += str1 + "\n";
                    tmp1 = str1.Remove(0, str1.IndexOf("audio ") + "audio ".Length);
                    tmp1 = tmp1.Remove(tmp1.IndexOf(" RTP"));
                    this.toaudioport = Convert.ToInt32(tmp1);   //записываем аудиопорт получателя
                }
                if (str1.Contains("PCMU/8000")) tmp += str1 + "\n";
                if (str1.Contains("PCMA/8000")) tmp += str1 + "\n";
            }

            CodecInfo += "Content-Length: " + tmp.Length + "\n\n" + tmp;
            return CodecInfo;
        }
    }



    //=======================================================================================================================
    //=======================================================================================================================
    //=======================================================================================================================


    public class Listener   //прослушиватель
    {

        //==============переменные==============
        static Del DelOutput;   //делегат на вывод пришедшей информации
        static DelRequest DelRequest1;  //делегат на запрос принятия приглашения
        static DelCloseSession DelClosesession;
        String host = System.Net.Dns.GetHostName();
        System.Net.IPAddress myIP;  //наш IP

        static System.Threading.Mutex Mut = new Mutex();
        Thread ThreadListen;    //поток для прослушки
        static int port;        //номер используемого порта

        static double LastSessionID = 0;
        static string myName;   //имя пользователя

        static List<Session> Sessions = new List<Session>();
        //==============конструкторы==============
        public Listener(int newport, Del d, DelRequest d1, string name, DelCloseSession d2)
        {
            DelOutput = d;
            DelRequest1 = d1;
            DelClosesession = d2;

            myName = name;

            myIP = System.Net.Dns.GetHostByName(host).AddressList[0];    //получаем свой IP
            port = newport;   //устанавливаем номер порта
            ThreadListen = new Thread(ListenSockets);   //настраиваем поток на функцию прослушки
            ThreadListen.Start();
        }



        //==============внешние функции==============
        /// <summary>
        /// Отправка звонка пользователю
        /// </summary>
        /// <param name="ToIP">IP получателя</param>
        /// <param name="ToUser">Имя получателя</param>
        /// <param name="FromUser">Имя отправителя</param>
        public void MakeCall(string ToIP, string ToUser, string FromUser)
        {
            Sessions.Add(new Session(myIP, port, ToIP, ToUser, FromUser, DelOutput, DelClosesession, (LastSessionID++).ToString(), ""));
            Sessions.Last().Invite();
        }
        /// <summary>
        /// Завершение разговора
        /// </summary>
        /// <param name="ToUser">Имя собеседника</param>
        /// <param name="FromUser">Имя завершающего</param>
        public void EndCall(string ToUser, string FromUser)
        {
            foreach (Session s in Sessions)
            {
                if (s._ToUser == ToUser)
                {
                    s.BYECompile();
                    Thread.Sleep(100);
                    Sessions.Remove(s);
                    break;
                }
            }
        }
        /// <summary>
        /// Проверка на наличие подобной сессии
        /// </summary>
        /// <param name="str">Имя собеседника</param>
        /// <returns>Если такая сессия существует - вернёт true, иначе - false</returns>
        public bool CheckSessionExistance(string str)
        {
            foreach (Session s in Sessions)
            {
                if (str == s._ToUser) return true;
            }
            return false;
        }

        //==============внутренние функции==============
        /// <summary>
        /// Отключение телефона
        /// </summary>
        public void StopPhone()
        {
            ThreadListen.Abort();
            SendSocket("127.0.0.1", port, "quit");
            Sessions.Last().BYECompile();
            Sessions.Clear();
        }
        /// <summary>
        /// Прослушивание входящих запросов
        /// </summary>
        static void ListenSockets()  //прослушиваем входящие запросы
        {
            UdpClient receivingUdpClient = new UdpClient(port);    //создаём клиент и задаём порт

            try
            {
                System.Net.IPEndPoint RemoteIpEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);    //принимаем информацию со всех IP по заданному нашему порту

                while (true)
                {
                    Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);
                    WatchInfo(receiveBytes);
                }
            }
            catch (Exception)
            {
                return;
            }

        }
        /// <summary>
        /// Разборка приходящих запросов
        /// </summary>
        /// <param name="receiveBytes">Содержание запроса в виде потока байт</param>
        /// <returns>true если запрос правильный и адресован нам, иначе - false</returns>
        static bool WatchInfo(Byte[] receiveBytes)  //определяем тип полученной информации и передаём её в нужную функцию разбора
        {
            Mut.WaitOne();  //<=====будет использоваться при многоканальной связи
            string Info = Encoding.ASCII.GetString(receiveBytes);

            string From = Info.Substring(Info.IndexOf("From: "), Info.IndexOf('\n', Info.IndexOf("From: ")) - Info.IndexOf("From: "));    //выделяем строку From
            string tmp = "", tmp2 = "", tmp3 = "", tmp4 = "", SDP = "";

            if (From.Length <= 0)
            {
                return false;   //если ошибочный запрос (не содержит From)
            }

            From = From.Remove(0, From.IndexOf("sip: ") + "sip: ".Length);
            From = From.Remove(From.IndexOf('>'));

            tmp = Info.Remove(0, Info.IndexOf("To"));
            tmp = tmp.Remove(tmp.IndexOf('@'));
            tmp = tmp.Remove(0, tmp.IndexOf("sip: ") + "sip: ".Length);

            //DelOutput("Получили запрос для: " + tmp);

            if (tmp == myName)  //проверяем: нам ли адресовано
            {
                DelOutput(Info, "Получили такой запрос");
                if (Info.Contains("INVITE "))   //при приходе инвайта
                {
                    tmp4 = Info.Remove(0, Info.IndexOf("From:"));
                    tmp4 = tmp4.Remove(tmp4.IndexOf('>'));
                    tmp4 = tmp4.Remove(0, tmp4.IndexOf('@') + 1);

                    tmp2 = Info.Remove(0, Info.IndexOf("To: <sip: ") + "To: <sip: ".Length);
                    tmp2 = tmp2.Remove(tmp2.IndexOf('>'));
                    tmp2 = tmp2.Remove(tmp2.IndexOf('@'));

                    tmp3 = Info.Remove(0, Info.IndexOf("Call-ID"));
                    tmp3 = tmp3.Remove(tmp3.IndexOf('\n'));



                    SDP = Info.Remove(0, Info.IndexOf("Content-Length"));
                    SDP = SDP.Remove(0, SDP.IndexOf("\n\n") + 2);

                    Sessions.Add(new Session(System.Net.Dns.GetHostByName(System.Net.Dns.GetHostName()).AddressList[0], port, tmp4, From.Remove(From.IndexOf('@')), tmp2, DelOutput, DelClosesession, tmp3, SDP));
                    Sessions.Last()._1XXCompile("01");

                    if (DelRequest1(From) == true)  //спрашиваем об открытии новой сессии
                    {
                        Sessions.Last()._2XXCompile("00", true, false);  //вызов подтверждения
                    }
                    else
                    {
                        Sessions.Add(new Session(System.Net.Dns.GetHostByName(System.Net.Dns.GetHostName()).AddressList[0], port, tmp, tmp2, From.Remove(From.IndexOf('@')), DelOutput, DelClosesession, tmp3, ""));
                        Sessions.Last()._6XXCompile("03", false, true);
                        Sessions.Remove(Sessions.Last());
                    }


                }
                else    //иначе проверяем принадлежность и запускаем разборку запроса в нужной сессии
                {
                    tmp = Info.Remove(0, Info.IndexOf("Call-ID"));
                    tmp = tmp.Remove(tmp.IndexOf('\n'));

                    foreach (Session s in Sessions)
                    {
                        //if (s.CheckSessionByFrom(tmp, From.Remove(0, From.IndexOf('@') + 1))) s.WatchInfo(Info);  //проверка по From (имя и IP) и запуск разбора запроса
                        if (s.CheckSessionByID(tmp))    //проверка по ID
                        {
                            s.WatchInfo(Info);
                            //DelOutput("Ответ дошёл до адресата: " + tmp);
                        }
                    }
                }
            }

            Mut.ReleaseMutex();
            return true;
        }
        bool SendSocket(string ToIP, int port, string Info)  //функция отправки по такому IP, в такой порт, такой инфы
        {
            System.Net.IPAddress ipAddress;         //IP того, кому посылаем
            UdpClient udpClient = new UdpClient();  //создаём UDP клиент
            Byte[] sendBytes = Encoding.ASCII.GetBytes(Info);       //преобразуем строку запроса

            if (!System.Net.IPAddress.TryParse(ToIP, out ipAddress))    //получаем адрес компа. out - возвращаем по ссылке
            {
                return false;
            };

            System.Net.IPEndPoint ipEndPoint = new System.Net.IPEndPoint(ipAddress, port); //создаём точку назначения

            try
            {
                udpClient.Send(sendBytes, sendBytes.Length, ipEndPoint); //посылаем информацию
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

    }


    public class Player
    {
        private WaveLib.WaveOutPlayer m_Player;         //проигрыватель
        private WaveLib.WaveInRecorder m_Recorder;      //записыватель
        private WaveLib.FifoStream m_Fifo = new WaveLib.FifoStream();   //буфер

        private byte[] m_PlayBuffer;
        private byte[] m_RecBuffer;
        private int _portReceive;
        private string _ToIP;

        Object LockSend = new Object();
        Object LockReceive = new Object();
        Thread ThreadListen;    //поток для прослушки

        /// <summary>
        /// Конструктор объекта работы со звуком
        /// </summary>
        public Player(string toip, int pr)
        {

            _portReceive = pr;
            _ToIP = toip;

            ThreadListen = new Thread(DataReceive);   //настраиваем поток на функцию прослушки
            ThreadListen.Start();
            ThreadListen.Suspend();
        }

        /// <summary>
        /// Функция воспроизведения звука
        /// </summary>
        /// <param name="data">Информация</param>
        /// <param name="size">Размер информации</param>
        private void Filler(IntPtr data, int size)
        {
            if (m_PlayBuffer == null || m_PlayBuffer.Length < size)
                m_PlayBuffer = new byte[size];
            if (m_Fifo.Length >= size)
                m_Fifo.Read(m_PlayBuffer, 0, size);
            else
                for (int i = 0; i < m_PlayBuffer.Length; i++)
                    m_PlayBuffer[i] = 0;
            System.Runtime.InteropServices.Marshal.Copy(m_PlayBuffer, 0, data, size);
        }




        /// <summary>
        /// Функция записи звука
        /// </summary>
        /// <param name="data">Информация</param>
        /// <param name="size">Размер информации</param>
        private void DataArrived(Byte[] data, int size)
        {
            if (m_RecBuffer == null || m_RecBuffer.Length < size)
                m_RecBuffer = new byte[size];
            data.CopyTo(m_RecBuffer, 0);
            //System.Runtime.InteropServices.Marshal.Copy(data, m_RecBuffer, 0, size);

            m_Fifo.Write(m_RecBuffer, 0, m_RecBuffer.Length);
        }

        private void DataSend(IntPtr data, int size)    //???
        {
            lock (LockSend)
            {
                byte[] tmpBuffer = new byte[size];
                System.Runtime.InteropServices.Marshal.Copy(data, tmpBuffer, 0, size);
                SendSocket(_ToIP, _portReceive, tmpBuffer);
            }
        }

        /// <summary>
        /// Получение и запись звука
        /// </summary>
        private void DataReceive()
        {
            lock (LockReceive)
            {
                UdpClient receivingUdpClient = new UdpClient(_portReceive);    //создаём клиент и задаём порт
                System.Net.IPAddress ipAddress;
                System.Net.IPAddress.TryParse(_ToIP, out ipAddress);
                try
                {
                    System.Net.IPEndPoint RemoteIpEndPoint = new System.Net.IPEndPoint(ipAddress, _portReceive);

                    while (true)
                    {
                        Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);
                        DataArrived(receiveBytes, receiveBytes.Length);
                    }
                }
                catch (Exception)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Конец записи/воспроизведения звука
        /// </summary>
        public void Stop()
        {
            ThreadListen.Suspend();
            if (m_Player != null)
                try
                {
                    m_Player.Dispose();
                }
                finally
                {
                    m_Player = null;
                }
            if (m_Recorder != null)
                try
                {
                    m_Recorder.Dispose();
                }
                finally
                {
                    m_Recorder = null;
                }
            m_Fifo.Flush(); // clear all pending data
        }
        /// <summary>
        /// Начало записи/воспроизведения звука
        /// </summary>
        public void Start()
        {
            Stop();
            ThreadListen.Resume();
            try
            {
                WaveLib.WaveFormat fmt = new WaveLib.WaveFormat(22050, 16, 2);
                m_Player = new WaveLib.WaveOutPlayer(-1, fmt, 32000, 5, new WaveLib.BufferFillEventHandler(Filler));
                m_Recorder = new WaveLib.WaveInRecorder(-1, fmt, 32000, 5, new WaveLib.BufferDoneEventHandler(DataSend));
            }
            catch
            {
                Stop();
                throw;
            }
        }
        /// <summary>
        /// Отправка информации
        /// </summary>
        /// <param name="ToIP">IP получателя</param>
        /// <param name="port">Порт получателя</param>
        /// <param name="Info">Информация</param>
        /// <returns>Если отправка успешна - вернёт true, иначе - false</returns>
        bool SendSocket(string ToIP, int port, Byte[] sendBytes)  //функция отправки по такому IP, в такой порт, такой инфы
        {
            System.Net.IPAddress ipAddress;         //IP того, кому посылаем
            UdpClient udpClient = new UdpClient();  //создаём UDP клиент

            if (!System.Net.IPAddress.TryParse(ToIP, out ipAddress))    //получаем адрес компа. out - возвращаем по ссылке
            {
                return false;
            };

            System.Net.IPEndPoint ipEndPoint = new System.Net.IPEndPoint(ipAddress, port); //создаём точку назначения

            try
            {
                udpClient.Send(sendBytes, sendBytes.Length, ipEndPoint); //посылаем информацию
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public void SetOptions(string toip, int pr)
        {
            _portReceive = pr;
            _ToIP = toip;
        }

        public string Options
        {
            get
            {
                return _ToIP + " " + _portReceive.ToString();
            }
        }
    }
}
