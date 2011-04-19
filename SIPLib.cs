using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Xml;

//github.com    �����������
//rfc4317       SDP
//pjsip.org ����������

namespace SIPLib
{
    public delegate void Del(string str);
    public delegate bool DelRequest(string str);
    


    public class Session    //���� ������
    {
        Del DelOutput;
        string ToIP;        //IP �������
        string ToUser;      //��� �������
        string MyName;    //���� ���
        System.Net.IPAddress myIP;  //��� IP
        int n = 0;  //������� �������
        int port;   //����
        bool SessionConfirmed = false; //���� ��������������� ������ (�� ��� ������ ��������)
        string SessionID;
        Thread WaitForAnswer;


        //==============�����������==================
        public Session(System.Net.IPAddress myIP, int myPort, string ToIP, string ToUser, string FromUser, Del d, string ID)    //����������� ��� ������
        {
            DelOutput = d;
            this.ToIP = ToIP;
            this.ToUser = ToUser;
            this.MyName = FromUser;
            this.myIP = myIP;
            this.port = myPort;
            this.SessionID = ID;
            n++;
        }

        //==============������� �������==============
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

        /*public bool CheckSessionByFrom(string name,string ip)   //������� �������� �� ����� � IP 
        {
            if (this.ToUser == name )
            {
                return true;
            }
            return false;
        }*/

        public bool CheckSessionByID(string ID)
        {
            if (this.SessionID == ID) return true;
            else return false;
        }

        public bool WatchInfo(string Info)
        {
            //DelOutput(Info);

            if (Info.Contains("200 OK"))    //�����
            {
                DelOutput("������� ACK");
                this.ACKDecompile();
                return true;
            }

            if (Info.Contains("BYE"))    //����
            {

                return true;
            }

            if (Info.Contains("CANCEL"))    //������
            {

                return true;
            }

            if (Info.Contains("REGISTER"))    //�����������
            {

                return true;
            }

            if (Info.Contains("OPTIONS"))    //������ �������
            {

                return true;
            }


            return false;
        }

        //==============���������� �������==============


        void WaitForAnswerFunc()    //������� �������� ���������� ������
        {
            for (int i = 0; i < 40; i++)    //��������� � ������� 4� ������
            {
                Thread.Sleep(100);
                if (_SessionConfirmed == true) return;// return true;
            }
            

            //������� �������� ������ !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //return false;
            
        }

        bool SendInfo(string Info)
        {
            System.Net.IPAddress ipAddress;         //IP ����, ���� ��������
            UdpClient udpClient = new UdpClient();  //������ UDP ������

            Byte[] sendBytes = Encoding.ASCII.GetBytes(Info);       //����������� ������ �������

            if (System.Net.IPAddress.TryParse(ToIP, out ipAddress))    //�������� ����� �����. out - ���������� �� ������
            {
                System.Net.IPEndPoint ipEndPoint = new System.Net.IPEndPoint(ipAddress, port); //������ ����� ����������

                try
                {
                    udpClient.Send(sendBytes, sendBytes.Length, ipEndPoint); //�������� ����������
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                IPAddress[] ips;
                ips = Dns.GetHostAddresses(ToIP);   //���� ��� ������� � ��
                foreach (IPAddress ip in ips)
                {
                    System.Net.IPEndPoint ipEndPoint = new System.Net.IPEndPoint(ip, port); //������ ����� ����������

                    try
                    {
                        udpClient.Send(sendBytes, sendBytes.Length, ipEndPoint); //�������� ����������
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


                //==�������� � ������ ������������� ��������==
        public void Invite()
        {
            string Request = "";

            Request += "INVITE sip:" + this.ToUser + "@" + this.ToIP + " SIP/2.0 " + "\n";
            Request += "Record-Route: <sip:" + this.ToUser + "@" + this.myIP.ToString() + ";lr>" + "\n";
            Request += "From: " + "\"" + this.MyName + "\"" + "<sip: " + this.MyName + "@" + this.myIP.ToString() + "> " + "\n";
            Request += "To: " + "<sip: " + this.ToUser + "@" + this.ToIP + "> " + "\n";
            Request += "Call-ID: " + SessionID + "@" + this.myIP + "\n";
            Request += "CSeq:" + (++this.n).ToString() + " INVITE" + "\n";

            Request += "Date: " + DateTime.Now.ToString() + "\n";   //���� � �����
            Request += "Allow: INVITE, ACK, CANCEL, BYE" + "\n";

            Request += "\n" + SDP();

            SendInfo(Request);
            WaitForAnswer = new Thread(WaitForAnswerFunc);
            WaitForAnswer.Start();

        }

        string InviteDeCompile(string FromIP, string ToIP, string ToUser, string FromUser)
        {
            string Request = "";
            return Request;
        }

        public void ACK()
        {
            string Request = "";
            Request += "SIP/2.0 200 OK" + "\n";
            Request += "From: " + this.MyName + " <sip:" + this.MyName + "@" + this.myIP.ToString() + ">" + "\n";
            Request += "To: <sip: " + this.ToUser + "@" + this.ToIP + ">" + "\n";
            Request += this.SessionID.ToString() + "\n";
            Request += "Cseq: " + (++this.n).ToString() + " OK" + "\n";
            Request += "Date: " + DateTime.Now.ToString() + "\n";
            //????????
            Request += "\n" + SDP();

            SendInfo(Request);
            DelOutput(Request);
            SessionConfirmed = true;

        }
        int ACKDecompile() { return 0; }
        string BYECompile()
        {
            string Request = "";
            Request += "BYE sip:" + ToUser + " SIP/2.0 " + "\n";


            return Request;
        }
        int BYEDecompile() { return 0; }
        string REGISTERCompile() { return null; }
        int REGISTERDecompile() { return 0; }

        string SDP()    //<============================================= ����������� �����������
        {
            return null;
        }
    }



    //=======================================================================================================================
    //=======================================================================================================================
    //=======================================================================================================================


    public class Listener   //��������������
    {
        
        //==============����������==============
        static Del DelOutput;   //������� �� ����� ��������� ����������
        static DelRequest DelRequest1;  //������� �� ������ �������� �����������
        String host = System.Net.Dns.GetHostName();
        System.Net.IPAddress myIP;  //��� IP

        static System.Threading.Mutex Mut = new Mutex();
        Thread ThreadListen;    //����� ��� ���������
        static int port;        //����� ������������� �����

        static double LastSessionID = 0;
        static string myName;   //��� ������������

        static List<Session> Sessions = new List<Session>();
        //==============������������==============
        public Listener(int newport, Del d,DelRequest d1, string name) 
        {
            DelOutput = d;
            DelRequest1 = d1;

            myName = name;

            myIP = System.Net.Dns.GetHostByName(host).AddressList[0];    //�������� ���� IP
            port = newport;   //������������� ����� �����
            ThreadListen = new Thread(ListenSockets);   //����������� ����� �� ������� ���������
            ThreadListen.Start();
        }

        

        //==============������� �������==============
        public void MakeCall( string ToIP, string ToUser, string FromUser)
        {
            Sessions.Add(new Session(myIP, port, ToIP, ToUser, FromUser, DelOutput, (LastSessionID++).ToString()));
            Sessions.Last().Invite();
        }

        public bool CheckSessionExistance(string str)
        {
            foreach (Session s in Sessions)
            {
                if (str == s._ToUser) return true;
            }
            return false;
        }

        //==============���������� �������==============
        public void StopPhone()
        {
            ThreadListen.Abort();
            SendSocket("127.0.0.1", port, "quit");
        }
        static void ListenSockets()  //������������ �������� �������
        {
            UdpClient receivingUdpClient = new UdpClient(port);    //������ ������ � ����� ����

            try
            {
                System.Net.IPEndPoint RemoteIpEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);    //��������� ���������� �� ���� IP �� ��������� ������ �����

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
        static bool WatchInfo(Byte[] receiveBytes)  //���������� ��� ���������� ���������� � ������� � � ������ ������� �������
        {
            Mut.WaitOne();  //<=====����� �������������� ��� �������������� �����
            string Info = Encoding.ASCII.GetString(receiveBytes);

            string From = Info.Substring(Info.IndexOf("From: "), Info.IndexOf('\n', Info.IndexOf("From: ")) - Info.IndexOf("From: "));    //�������� ������ From
            string tmp, tmp1, tmp2, tmp3;

            if (From.Length <= 0)
            {
                return false;   //���� ��������� ������ (�� �������� From)
            }

            From = From.Remove(0, From.IndexOf("sip: ") + "sip: ".Length);
            From = From.Remove(From.IndexOf('>'));

            tmp = Info.Remove(0, Info.IndexOf("To"));
            tmp = tmp.Remove(tmp.IndexOf('@'));
            tmp = tmp.Remove(0, tmp.IndexOf("sip: ") + "sip: ".Length);

            //DelOutput("�������� ������ ���: " + tmp);

            if (tmp == myName)  //���������: ��� �� ����������
            {
                if (Info.Contains("INVITE "))   //��� ������� �������
                {
                    if (DelRequest1(From))  //���������� �� �������� ����� ������
                    {
                        tmp = Info.Remove(0, Info.IndexOf("To: <sip: ") + "To: <sip: ".Length);
                        tmp = tmp.Remove(tmp.IndexOf('>'));
                        tmp = tmp.Remove(0, tmp.IndexOf('@') + 1);
                       // DelOutput("1"+tmp+"1"); //ToIp
                        tmp2 = Info.Remove(0, Info.IndexOf("To: <sip: ") + "To: <sip: ".Length);
                        tmp2 = tmp2.Remove(tmp2.IndexOf('>'));
                        tmp2 = tmp2.Remove(tmp2.IndexOf('@'));
                        //DelOutput("1" + tmp2 + "1"); //ToUser
                        //DelOutput(From.Remove(From.IndexOf('@'))); //FromUser
                        //DelOutput(From.Remove(0,From.IndexOf('@')+1));  //FromIp

                        tmp3 = Info.Remove(0, Info.IndexOf("Call-ID"));
                        tmp3 = tmp3.Remove(tmp3.IndexOf('\n'));

                        Sessions.Add(new Session(System.Net.Dns.GetHostByName(System.Net.Dns.GetHostName()).AddressList[0], port, tmp, tmp2, From.Remove(From.IndexOf('@')), DelOutput, tmp3));
                        Sessions.Last().ACK();  //����� �������������
                    }
                    else
                    {
                        return false;
                    }

                    //������� �������������� ����� �� ������������� ����� <=============================!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //����� ������������� � ������ ��� ��


                }
                else    //����� ��������� �������������� � ��������� �������� ������� � ������ ������
                {
                    tmp = Info.Remove(0, Info.IndexOf("Call-ID"));
                    tmp = tmp.Remove(tmp.IndexOf('\n'));

                    //DelOutput("���ب� �����");

                    //DelOutput("����� ������:" + Sessions.Count.ToString() + "\n" + "1" + From + "1");
                    foreach (Session s in Sessions)
                    {
                        //if (s.CheckSessionByFrom(tmp, From.Remove(0, From.IndexOf('@') + 1))) s.WatchInfo(Info);  //�������� �� From (��� � IP) � ������ ������� �������
                        if (s.CheckSessionByID(tmp))
                        {
                            s.WatchInfo(Info);
                            //DelOutput("����� ����� �� ��������: " + tmp);
                        }
                    }
                }
            }

            Mut.ReleaseMutex();
            return true;
        }
        bool SendSocket(string ToIP, int port, string Info)  //������� �������� �� ������ IP, � ����� ����, ����� ����
        {
            System.Net.IPAddress ipAddress;         //IP ����, ���� ��������
            UdpClient udpClient = new UdpClient();  //������ UDP ������
            Byte[] sendBytes = Encoding.ASCII.GetBytes(Info);       //����������� ������ �������

            if (!System.Net.IPAddress.TryParse(ToIP, out ipAddress))    //�������� ����� �����. out - ���������� �� ������
            {
                return false;
            };

            System.Net.IPEndPoint ipEndPoint = new System.Net.IPEndPoint(ipAddress, port); //������ ����� ����������

            try
            {
                udpClient.Send(sendBytes, sendBytes.Length, ipEndPoint); //�������� ����������
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

    }
}
