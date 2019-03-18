using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Net.Sockets;
using System.Net;

namespace wuzq
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>

    public partial class MainWindow : Window
    {
        enum GameType { People, Computer };
        enum GameTurn { Black, White };

        Thread aigameThread;
        bool isPlaying = false;

        Dictionary<GameTurn, GameType> type = new Dictionary<GameTurn, GameType>();
        Dictionary<GameTurn, AIInterface> ai = new Dictionary<GameTurn, AIInterface>();

        GameTurn gameTurn = GameTurn.Black;
        bool isEnd = false;

        int lastX = 15, lastY = 15;
        double step = 612.5 / 14.0;
        double Top = 8.75, Left = 8.75;
        double R = 18.75;

        Brush halfBlack = new SolidColorBrush(Color.FromArgb(127, 0, 0, 0));
        Brush halfWhite = new SolidColorBrush(Color.FromArgb(127, 255, 255, 255));
        Brush black = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
        Brush white = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

        int[,] board = new int[16, 16];
        int[,] _board = new int[16, 16];

        Ellipse[,] whitePieces = new Ellipse[32, 32];
        Ellipse[,] blackPieces = new Ellipse[32, 32];

        //public NetworkInterface Network;
        public MainWindow()
        {
            InitializeComponent();
            type[GameTurn.Black] = GameType.People;
            type[GameTurn.White] = GameType.People;
            for (int i = 0; i < 15; i++)    //初始化棋盘
            {
                for (int j = 0; j < 15; j++)
                {
                    double x = Top + step * i;
                    double y = Left + step * j;

                    whitePieces[i, j] = MakeVirtualPieces(x, y, halfWhite);
                    blackPieces[i, j] = MakeVirtualPieces(x, y, halfBlack);
                }
            }

            whitePieces[15, 15] = blackPieces[15, 15] = new Ellipse();
            people_people.IsChecked = true;
            groupBox.Visibility = Visibility.Collapsed;
            groupBox1.Visibility = Visibility.Collapsed;
            groupBox2.Visibility = Visibility.Collapsed;
        }

        public Ellipse MakeVirtualPieces(double x, double y, Brush brush)  //制作虚拟棋子
        {
            Ellipse result = new Ellipse();
            result.Fill = brush;
            result.Width = R * 2;
            result.Height = R * 2;
            chessBoard.Children.Add(result);
            Canvas.SetLeft(result, x);
            Canvas.SetTop(result, y);
            result.Visibility = Visibility.Hidden;
            return result;
        }
        private void HideLastVirtualPiece()     //隐藏虚拟棋子
        {
            if (board[lastX, lastY] != 0)
                return;
            if (gameTurn == GameTurn.Black)
                blackPieces[lastX, lastY].Visibility = Visibility.Hidden;
            else
                whitePieces[lastX, lastY].Visibility = Visibility.Hidden;
        }
        private void ShowVirtualPiece(int x, int y)    //显示虚拟棋子
        {
            if (board[x, y] != 0)
                return;
            if (gameTurn == GameTurn.Black)
                blackPieces[x, y].Visibility = Visibility.Visible;
            else
                whitePieces[x, y].Visibility = Visibility.Visible;
        }
        private void PutPiece(int x, int y)    //落子及判断胜负函数
        {
            if (board[x, y] != 0)
                return;
            if (gameTurn == GameTurn.Black)   //落黑子
            {
                board[x, y] = 1;
                HideLastVirtualPiece();
                blackPieces[x, y].Visibility = Visibility.Visible;
                blackPieces[x, y].Fill = black;
                gameTurn = GameTurn.White;
            }
            else    //落白子
            {
                board[x, y] = 2;
                whitePieces[lastX, lastY].Visibility = Visibility.Hidden;
                whitePieces[x, y].Visibility = Visibility.Visible;
                whitePieces[x, y].Fill = white;
                gameTurn = GameTurn.Black;
            }

            lastX = lastY = 15;
            int result = Util.CheckBoardResult(board, x, y);  //调用Until类中判断函数
            if (result == 1)
            {
                MessageBox.Show("黑棋获胜，游戏结束");
                isEnd = true;
            }

            if (result == 2)
            {
                MessageBox.Show("白棋获胜，游戏结束");
                isEnd = true;
            }
            if (!isEnd && type[gameTurn] == GameType.Computer)   //若轮到电脑，使用AI落子
                UseAI(ai[gameTurn] as AIInterface);

        }
        /// <summary>
        /// 判断最终结果类
        /// </summary>
        public class Util
        {
            static int[] dx = { 0, 1, 1, 1 };
            static int[] dy = { 1, -1, 0, 1 };
            private static bool InRange(int x, int y)
            {
                return x >= 0 && x < 15 && y >= 0 && y < 15;
            }

            public static int CheckBoardResult(int[,] board, int x, int y)  
                //黑棋胜返回1，白棋胜返回2，平局返回3，不分胜负返回0
            {
                int now = board[x, y];
                for (int d = 0; d < 4; d++)
                {
                    int s = 1;
                    for (int i = 1; true; i++)
                    {
                        int nowX = x + dx[d] * i, nowY = y + dy[d] * i;
                        if (!InRange(nowX, nowY) || board[nowX, nowY] != now)
                            break;
                        s++;
                    }

                    for (int i = 1; true; i++)
                    {
                        int nowX = x - dx[d] * i, nowY = y - dy[d] * i;
                        if (!InRange(nowX, nowY) || board[nowX, nowY] != now)
                            break;
                        s++;
                    }

                    if (s > 4)
                        return now;
                }
                for(int i=0;i<board.Rank;i++)
                    for(int j=0;j<board.GetLength(0);j++)
                    {
                        if(board[i,j]==0)
                        {
                            return 0;
                        }
                    }
                return 3;
            }
        }
        private void CopyBoard()   //复制棋盘，用于AI运算
        {
            for (int i = 0; i < 15; i++)
                for (int j = 0; j < 15; j++)
                    _board[i, j] = board[i, j];
        }
        private void UseAI(AIInterface ai)   //AI操作函数
        {
            int now = 1;
            if (gameTurn != GameTurn.Black)
                now = 2;
            CopyBoard();
            ai.Running(_board, now);
            int x, y;
            ai.GetNextStep(out x, out y);
            PutPiece(x, y);
        }
        private void restart()  //重置棋盘函数
        {
            if (aigameThread != null)
            {
                aigameThread.Abort();
                aigameThread.Join();
                isPlaying = false;
            }

            isEnd = false;
            for (int i = 0; i < 15; i++)
                for (int j = 0; j < 15; j++)
                {
                    blackPieces[i, j].Fill = halfBlack;
                    whitePieces[i, j].Fill = halfWhite;
                    blackPieces[i, j].Visibility = Visibility.Hidden;
                    whitePieces[i, j].Visibility = Visibility.Hidden;
                    board[i, j] = 0;
                    gameTurn = GameTurn.Black;
                }
        }
        //后续代码均为事件处理
        private void chessBoard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (type[gameTurn] == GameType.Computer || isEnd || isPlaying)
                return;
            double xPos = e.GetPosition(chessBoard).X;
            double yPos = e.GetPosition(chessBoard).Y;
            int x = Convert.ToInt32((xPos) / step - 0.5);
            int y = Convert.ToInt32((yPos) / step - 0.5);
            if (x >= 0 && x < 15 && y >= 0 && y < 15)
                PutPiece(x, y);
        }

        private void chessBoard_MouseMove(object sender, MouseEventArgs e)
        {
            if (type[gameTurn] == GameType.Computer || isEnd || isPlaying)
                return;
            double xPos = e.GetPosition(chessBoard).X;
            double yPos = e.GetPosition(chessBoard).Y;
            int x = Convert.ToInt32((xPos) / step - 0.5);
            int y = Convert.ToInt32((yPos) / step - 0.5);
            if (x >= 0 && x < 15 && y >= 0 && y < 15)
            {
                HideLastVirtualPiece();
                ShowVirtualPiece(x, y);
                lastX = x;
                lastY = y;
            }
        }

        private void 人机对战_Click(object sender, RoutedEventArgs e)
        {
            restart();
            groupBox.Visibility = Visibility.Collapsed;
            groupBox1.Visibility = Visibility.Collapsed;
            groupBox2.Visibility = Visibility.Collapsed;
            MessageBoxResult result = MessageBox.Show(this, "AI是否先行?", "" ,MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                type[GameTurn.Black] = GameType.Computer;
                ai[GameTurn.Black] = new ZtxzAI();
                type[GameTurn.White] = GameType.People;
            }
            else
            {
                type[GameTurn.Black] = GameType.People;
                ai[GameTurn.White] = new ZtxzAI();
                type[GameTurn.White] = GameType.Computer;
            }
            if(type[GameTurn.Black]==GameType.Computer)
                UseAI(ai[GameTurn.Black]);
        }

        private void people_people_Click(object sender, RoutedEventArgs e)
        {
            groupBox.Visibility = Visibility.Collapsed;
            groupBox1.Visibility = Visibility.Collapsed;
            groupBox2.Visibility = Visibility.Collapsed;
            type[GameTurn.Black] = GameType.People;
            type[GameTurn.White] = GameType.People;
            restart();
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            restart();
            if (type[GameTurn.Black] == GameType.Computer)
                UseAI(ai[GameTurn.Black]);
        }

        private void internet_Click(object sender, RoutedEventArgs e)
        {
            //restart();
            //radioButton.IsChecked = true;
            //groupBox.Visibility = Visibility.Visible;
            //groupBox1.Visibility = Visibility.Visible;
            //groupBox2.Visibility = Visibility.Collapsed;
        }

        private void radioButton_Click(object sender, RoutedEventArgs e)
        {
            groupBox1.Visibility = Visibility.Visible;
            groupBox2.Visibility = Visibility.Collapsed;
        }

        private void radioButton1_Click(object sender, RoutedEventArgs e)
        {
            groupBox2.Visibility = Visibility.Visible;
            groupBox.Visibility = Visibility.Collapsed;
            groupBox1.Visibility = Visibility.Visible;
        }

        private void found_Click(object sender, RoutedEventArgs e)
        {
            //try
            //{
            //    int port = int.Parse(textBox1.Text);
            //    this.Network.Start(port);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(this, ex.Message, string.Empty, MessageBoxButton.OK);
            //    return;
            //}
        }

        private void connect_Click(object sender, RoutedEventArgs e)
        {
            //try
            //{
            //    IPAddress ip = IPAddress.Parse(textBox1.Text);
            //    int port = int.Parse(textBox2.Text);

            //    IPEndPoint end = new IPEndPoint(ip, port);
            //    Network.Connect(end);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(this, ex.Message, string.Empty, MessageBoxButton.OK);
            //    return;
            //}
        }

        private void quit_Click_1(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }
    }
}
