using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace wuzq
{

    public enum MessageType
    {
        None = 0,
        Sucess,
        Error,

        Reset = 8,
        Piece,
        Set,
        Pass,
    }

    public static class StreamHead
    {
        const int HeadLengthIndex = 0;
        const int MessageTypeIndex = 4;
        const int PointXIndex = 8;
        const int PointYIndex = 12;
        const int ChatIndex = 16;

        public static void WriteHead(byte[] array, MessageType type, int x, int y)
        {
            int HeadLength = 16 ;
            Array.Copy(BitConverter.GetBytes(HeadLength), 0, array, HeadLengthIndex, 4);
            Array.Copy(BitConverter.GetBytes((int)type), 0, array, MessageTypeIndex, 4);
            Array.Copy(BitConverter.GetBytes(x), 0, array, PointXIndex, 4);
            Array.Copy(BitConverter.GetBytes(y), 0, array, PointYIndex, 4);
        }

        public static void Read(byte[] array, out MessageType type, out int x, out int y)
        {
            type = (MessageType)BitConverter.ToInt32(array, MessageTypeIndex);
            x = BitConverter.ToInt32(array, PointXIndex);
            y = BitConverter.ToInt32(array, PointYIndex);
            int length = BitConverter.ToInt32(array, HeadLengthIndex);
        }
    }
    public class NetworkInterface
    {
        public delegate void Act();

        public MainWindow Owner { get; private set; }

        public event EventHandler ClientConnected;
        public event EventHandler Connected;
        public event EventHandler AbortedByException;

        public bool Running
        {
            get
            {
                return server != null || socket != null || acceptThread != null || listenThread != null || sendThread != null;
            }
        }

        List<byte[]> messages;

        Random random;
        Socket server;
        Socket socket;
        Thread acceptThread;
        Thread listenThread;
        Thread sendThread;

        public NetworkInterface(object obj)    //网络接口
        {
            if (obj == null || obj.GetType() != typeof(MainWindow))
            {
                throw new Exception("无效的父对象!");
            }

            Owner = (MainWindow)obj;
            messages = new List<byte[]>();
        }

        public void Start(int port)    //建立服务器
        {
            if (Running)
            {
                throw new Exception("对象非空!");
            }
            try
            {
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);   //建立接口
                server.Bind(new IPEndPoint(IPAddress.Any, port));
                server.Listen(1);   //最大允许一个连入
            }
            catch
            {
                if (server != null)
                {
                    server.Close();
                    server = null;
                }
                throw;
            }

            acceptThread = new Thread(accept);
            acceptThread.Start();   //开始监听
        }

        void accept()    //收到信息
        {
            socket = server.Accept();    //为连接创建新的socket
            server.Close();
            server = null;    //关闭服务器

            messages.Clear();

            listenThread = new Thread(receiver);    //创建接受流和发送流
            listenThread.Start();

            sendThread = new Thread(sender);
            sendThread.Start();

            if (ClientConnected != null)
            {
                ClientConnected(this, new EventArgs());
            }
        }

        public void Connect(IPEndPoint ip)    //客户端连接
        {
            if (Running)
            {
                throw new Exception("对象非空!");
            }

            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(ip);
            }
            catch
            {
                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }
                throw;
            }

            messages.Clear();

            listenThread = new Thread(receiver);
            listenThread.Start();

            sendThread = new Thread(sender);
            sendThread.Start();

            if (Connected != null)
            {
                Connected(this, new EventArgs());
            }
        }

        public void Send(MessageType type, int x = 0, int y = 0)   //发送信息
        {
            int length = 16 ;
            byte[] head = new byte[length];
            StreamHead.WriteHead(head, type, x, y);

            lock (messages)   //线程锁
            {
                messages.Add(head);
            }
        }

        void receiver()    //接收端
        {
            while (true)
            {
                MessageType type;
                int x, y;

                byte[] getLength = new byte[4];
                int alllength;
                try
                {
                    alllength = socket.Receive(getLength, 4, SocketFlags.None);  //接受数据
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    goto End;
                }
                alllength = BitConverter.ToInt32(getLength, 0);
                byte[] head = new byte[alllength];
                byte[] temp = new byte[alllength];
                Array.Copy(getLength, head, 4);

                try
                {
                    int sub = alllength - 4;
                    int length;
                    while (true)
                    {
                        int l = (temp.Length < sub) ? temp.Length : sub;
                        length = socket.Receive(temp, l, SocketFlags.None);
                        if (length <= 0)
                        {
                            goto End;
                        }
                        if (length == sub)
                        {
                            Array.Copy(temp, 0, head, head.Length - sub, length);
                            break;
                        }
                        else // if (length < sub)
                        {
                            Array.Copy(temp, 0, head, head.Length - sub, length);
                            sub -= length;
                        }
                    }
                    StreamHead.Read(head, out type, out x, out y);

                    if (type == MessageType.Reset)
                    {
                        MainWindow.Piece self = random.Next(0, 2) == 0 ? MainWindow.Piece.Black : MainWindow.Piece.White;
                        MainWindow.Piece now = MainWindow.Piece.Black;

                        // 先设置棋子 再请求刷新 否则可能不一致
                        Owner.RefreshText(self, now);
                        Owner.Reset();

                        Send(MessageType.Piece, (int)MainWindow.Reserve(self), (int)now);
                    }
                    if (type == MessageType.Piece)
                    {
                        

                        Owner.RefreshText(self, now);
                        Owner.Reset();
                    }
                    if (type == MessageType.Set)
                    {
                        Owner.Set(new Point(x, y), false);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    goto End;
                }
            }

        End:
            new Thread(abort).Start();
        }

        void sender()     //发送端
        {
            while (true)
            {
                byte[] temp = null;

                lock (messages)
                {
                    if (messages.Count > 0)
                    {
                        temp = messages[0];
                        messages.RemoveAt(0);
                    }
                }

                if (temp != null)
                {
                    try
                    {
                        socket.Send(temp);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        goto End;
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }

        End:
            new Thread(abort).Start();
        }

        public void Abort()         //终止进程
        {
            if (server != null)
            {
                server.Close();
                server = null;
            }
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
            if (acceptThread != null)
            {
                acceptThread.Abort();
                acceptThread = null;
            }
            if (listenThread != null)
            {
                listenThread.Abort();
                listenThread = null;
            }
            if (sendThread != null)
            {
                sendThread.Abort();
                sendThread = null;
            }
        }

        void abort()
        {
            Abort();

            if (AbortedByException != null)
            {
                AbortedByException(this, new EventArgs());
            }
        }
    }

}
