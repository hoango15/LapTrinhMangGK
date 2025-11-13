using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class MainForm : Form
    {
        const int CellSize = 40;
        private char[,] board = new char[15, 15];
        private char myMark = '?';
        private char turn = 'X';
        private int size = 15;
        private bool myTurn = false;
        private bool gameOver = false;
        private string opponentName = "";

        private StreamReader? reader;
        private StreamWriter? writer;
        private TcpClient? client;

        public MainForm()
        {
            InitializeComponent();
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            string nick = txtName.Text.Trim();
            if (string.IsNullOrEmpty(nick))
            {
                MessageBox.Show("Vui lòng nhập tên!");
                return;
            }

            if (!int.TryParse(txtPort.Text.Trim(), out int port))
            {
                MessageBox.Show("Port không hợp lệ!");
                return;
            }

            client = new TcpClient();
            try
            {
                btnConnect.Enabled = false;
                await client.ConnectAsync(txtHost.Text.Trim(), port);
                var stream = client.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                await writer.WriteLineAsync($"NICK:{nick}");

                pnlConnect.Visible = false;
                pnlMain.Visible = true;
                lblStatus.Visible = true;
                lblTurn.Visible = true;

                _ = ListenServer();
            }
            catch
            {
                MessageBox.Show("Không thể kết nối server!");
                btnConnect.Enabled = true;
            }
        }

        private async Task ListenServer()
        {
            if (reader == null) return;
            string? line;

            try
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("ROLE:"))
                    {
                        myMark = line[^1];
                        lblStatus.Text = $"Bạn là {myMark}";
                    }
                    else if (line.StartsWith("OPPONENT:"))
                    {
                        opponentName = line["OPPONENT:".Length..];
                        lblTurn.Text = $"Đang chơi với: {opponentName}";
                    }
                    else if (line.StartsWith("START:SIZE="))
                    {
                        size = int.Parse(line["START:SIZE=".Length..]);
                        board = new char[size, size];
                        gameOver = false;
                        pnlBoard.Size = new Size(size * CellSize, size * CellSize);
                        pnlBoard.Invalidate();
                        CenterBoard();

                        btnRematch.Visible = false;
                        btnExitMatch.Visible = true;
                    }
                    else if (line.StartsWith("BOARD:"))
                    {
                        var parts = line["BOARD:".Length..].Split(',');
                        int x = int.Parse(parts[0]);
                        int y = int.Parse(parts[1]);
                        char mark = parts[2][0];
                        board[y, x] = mark;
                        pnlBoard.Invalidate();
                    }
                    else if (line.StartsWith("TURN:"))
                    {
                        turn = line[^1];
                        myTurn = (turn == myMark);
                        lblTurn.Text = myTurn ? "👉 Tới lượt bạn!" : $"⏳ Đợi {opponentName}...";
                    }
                    else if (line.StartsWith("WIN:"))
                    {
                        gameOver = true;
                        pnlBoard.Invalidate();
                        char winner = line[^1];
                        MessageBox.Show(winner == myMark ? "🎉 Bạn thắng!" : $"💀 {opponentName} thắng!", "Kết quả");
                        btnRematch.Visible = true;
                        btnExitMatch.Visible = true;
                    }
                    else if (line == "DRAW")
                    {
                        gameOver = true;
                        MessageBox.Show("🤝 Hòa!", "Kết quả");
                        btnRematch.Visible = true;
                        btnExitMatch.Visible = true;
                    }
                    else if (line.StartsWith("INFO:"))
                    {
                        lblStatus.Text = line[5..];
                    }
                }
            }
            catch
            {
                MessageBox.Show("Mất kết nối server!");
                ResetUI();
            }
        }

        private async void PnlBoard_MouseClick(object? sender, MouseEventArgs e)
        {
            if (!myTurn || writer == null || gameOver) return;

            int x = e.X / CellSize;
            int y = e.Y / CellSize;
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            if (board[y, x] != '\0') return;

            await writer.WriteLineAsync($"MOVE:{x},{y}");
        }

        private void PnlBoard_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.Black, 1.5f);

            for (int i = 0; i <= size; i++)
            {
                g.DrawLine(pen, i * CellSize, 0, i * CellSize, size * CellSize);
                g.DrawLine(pen, 0, i * CellSize, size * CellSize, i * CellSize);
            }

            using var font = new Font("Segoe UI", 20, FontStyle.Bold);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    char c = board[y, x];
                    if (c == '\0') continue;

                    Brush b = c == 'X' ? Brushes.Red : Brushes.Blue;
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(c.ToString(), font, b, new RectangleF(x * CellSize, y * CellSize, CellSize, CellSize), sf);
                }
            }
        }

        private async void BtnRematch_Click(object sender, EventArgs e)
        {
            if (writer != null)
            {
                await writer.WriteLineAsync("REMATCH");
                btnRematch.Visible = false;
                lblStatus.Text = "Đang chuẩn bị ván mới...";
            }
        }

        private async void BtnExitMatch_Click(object sender, EventArgs e)
        {
            if (writer != null)
            {
                await writer.WriteLineAsync("EXIT");
            }
            ResetUI();
        }

        private void ResetUI()
        {
            pnlConnect.Visible = true;
            pnlMain.Visible = false;
            lblStatus.Visible = false;
            lblTurn.Visible = false;
            btnConnect.Enabled = true;
            btnRematch.Visible = false;
            btnExitMatch.Visible = false;
            txtName.Clear();
            board = new char[15, 15];
            gameOver = false;
            opponentName = "";
        }

        private void CenterBoard()
        {
            pnlBoard.Location = new Point(
                (pnlMain.ClientSize.Width - pnlBoard.Width) / 2,
                (pnlMain.ClientSize.Height - pnlBoard.Height) / 2
            );
        }
    }
}
