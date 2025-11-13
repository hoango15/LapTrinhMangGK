using System;
using System.Drawing;
using System.Windows.Forms;

namespace Client
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private Panel pnlConnect;
        private Panel pnlMain;
        private Panel pnlBoard;
        private TextBox txtName;
        private TextBox txtHost;
        private TextBox txtPort;
        private Button btnConnect;
        private Label lblStatus;
        private Label lblTurn;
        private Button btnRematch;
        private Button btnExitMatch;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlConnect = new Panel();
            this.txtName = new TextBox();
            this.txtHost = new TextBox();
            this.txtPort = new TextBox();
            this.btnConnect = new Button();

            this.pnlMain = new Panel();
            this.pnlBoard = new Panel();
            this.lblStatus = new Label();
            this.lblTurn = new Label();
            this.btnRematch = new Button();
            this.btnExitMatch = new Button();

            this.SuspendLayout();

            // === Form settings ===
            this.Text = "🎮 Caro Online Client";
            this.BackColor = Color.FromArgb(248, 249, 252);
            this.ClientSize = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            this.MinimumSize = new Size(700, 500);

            // === pnlConnect ===
            this.pnlConnect.Dock = DockStyle.Fill;
            this.pnlConnect.BackColor = Color.White;
            this.pnlConnect.Padding = new Padding(10);

            Label lblTitle = new Label();
            lblTitle.Text = "🧩 CARO ONLINE";
            lblTitle.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            lblTitle.AutoSize = false;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 100;

            // === Center container ===
            Panel pnlLoginCenter = new Panel();
            pnlLoginCenter.Dock = DockStyle.Fill;
            pnlLoginCenter.BackColor = Color.White;
            pnlLoginCenter.Resize += (s, e) =>
            {
                CenterLoginControls(pnlLoginCenter, txtName, txtHost, txtPort, btnConnect);
            };

            // === Input fields ===
            txtName.PlaceholderText = "Tên người chơi";
            txtHost.PlaceholderText = "Địa chỉ server (vd: 127.0.0.1)";
            txtPort.PlaceholderText = "Port (vd: 5001)";

            foreach (var t in new[] { txtName, txtHost, txtPort })
            {
                t.Width = 280;
                t.Height = 40;
                t.Font = new Font("Segoe UI", 12F);
                t.TextAlign = HorizontalAlignment.Center;
                t.BorderStyle = BorderStyle.FixedSingle;
            }

            // === Connect Button ===
            btnConnect.Text = "Kết nối";
            btnConnect.Width = 280;
            btnConnect.Height = 45;
            btnConnect.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            btnConnect.BackColor = Color.FromArgb(52, 152, 219);
            btnConnect.ForeColor = Color.White;
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.FlatAppearance.BorderSize = 0;
            btnConnect.Cursor = Cursors.Hand;
            btnConnect.Click += new EventHandler(this.BtnConnect_Click);

            pnlLoginCenter.Controls.AddRange(new Control[] { txtName, txtHost, txtPort, btnConnect });

            pnlConnect.Controls.Add(pnlLoginCenter);
            pnlConnect.Controls.Add(lblTitle);

            // === pnlMain (board container) ===
            this.pnlMain.Dock = DockStyle.Fill;
            this.pnlMain.Visible = false;
            this.pnlMain.BackColor = Color.WhiteSmoke;
            this.pnlMain.Resize += (s, e) => CenterBoard();

            // === pnlBoard ===
            this.pnlBoard.BackColor = Color.White;
            this.pnlBoard.BorderStyle = BorderStyle.FixedSingle;
            this.pnlBoard.Size = new Size(600, 600);
            this.pnlBoard.Paint += new PaintEventHandler(this.PnlBoard_Paint);
            this.pnlBoard.MouseClick += new MouseEventHandler(this.PnlBoard_MouseClick);
            this.pnlBoard.Anchor = AnchorStyles.None;

            // === lblStatus & lblTurn ===
            this.lblStatus.Dock = DockStyle.Top;
            this.lblStatus.Height = 30;
            this.lblStatus.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.lblStatus.TextAlign = ContentAlignment.MiddleCenter;

            this.lblTurn.Dock = DockStyle.Top;
            this.lblTurn.Height = 30;
            this.lblTurn.Font = new Font("Segoe UI", 10, FontStyle.Italic);
            this.lblTurn.TextAlign = ContentAlignment.MiddleCenter;

            // === Buttons bottom ===
            FlowLayoutPanel bottomPanel = new FlowLayoutPanel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.FlowDirection = FlowDirection.RightToLeft;
            bottomPanel.Padding = new Padding(10);
            bottomPanel.Height = 60;
            bottomPanel.BackColor = Color.White;

            btnRematch.Text = "Chơi lại";
            btnRematch.Size = new Size(120, 35);
            btnRematch.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnRematch.BackColor = Color.MediumSeaGreen;
            btnRematch.ForeColor = Color.White;
            btnRematch.FlatStyle = FlatStyle.Flat;
            btnRematch.Visible = false;
            btnRematch.Click += new EventHandler(this.BtnRematch_Click);

            btnExitMatch.Text = "Thoát trận";
            btnExitMatch.Size = new Size(120, 35);
            btnExitMatch.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnExitMatch.BackColor = Color.IndianRed;
            btnExitMatch.ForeColor = Color.White;
            btnExitMatch.FlatStyle = FlatStyle.Flat;
            btnExitMatch.Visible = false;
            btnExitMatch.Click += new EventHandler(this.BtnExitMatch_Click);

            bottomPanel.Controls.Add(btnExitMatch);
            bottomPanel.Controls.Add(btnRematch);

            pnlMain.Controls.Add(pnlBoard);
            pnlMain.Controls.Add(bottomPanel);
            pnlMain.Controls.Add(lblTurn);
            pnlMain.Controls.Add(lblStatus);

            // === Form Controls ===
            this.Controls.Add(pnlMain);
            this.Controls.Add(pnlConnect);

            this.ResumeLayout(false);
        }

    
        private void CenterLoginControls(Panel container, TextBox name, TextBox host, TextBox port, Button connect)
        {
            int spacing = 15;
            int totalHeight = name.Height + host.Height + port.Height + connect.Height + spacing * 3;
            int startY = (container.Height - totalHeight) / 2;
            int centerX = (container.Width - name.Width) / 2;

            name.Location = new Point(centerX, startY);
            host.Location = new Point(centerX, name.Bottom + spacing);
            port.Location = new Point(centerX, host.Bottom + spacing);
            connect.Location = new Point(centerX, port.Bottom + spacing);
        }
    }
}
