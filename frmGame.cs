using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace minesweeper
{
    public partial class frmGame : Form
    {
        // ====== 1. CẤU TRÚC DỮ LIỆU ======
        const int MINE = -1;          // map[r,c] == -1: ô có mìn; 0..8: số mìn xung quanh
        const int CELL = 30;          // kích thước 1 ô (px)

        int rows = 9, cols = 9, mines = 10;

        int[,] map;                   // mảng ngầm lưu mìn + số
        bool[,] opened;               // ô đã mở chưa
        bool[,] flagged;              // ô đã cắm cờ chưa
        Button[,] cells;              // các nút hiển thị

        bool firstClick, gameOver, chordUsed;
        int flagCount, openedCount, seconds;

        Bitmap imgMine, imgMineWrong, imgFlag;

        static readonly Color[] NumColors =
        {
            Color.Black, Color.Blue, Color.Green, Color.Red,
            Color.DarkBlue, Color.Brown, Color.Teal, Color.Black, Color.Gray
        };

        readonly Random rnd = new Random();

        public frmGame()
        {
            InitializeComponent();

            int s = CELL - 8;
            imgMine = DrawMine(s, false);
            imgMineWrong = DrawMine(s, true);
            imgFlag = DrawFlag(s);

            SetupEvents();
            NewGame();
        }

        // ====== GẮN SỰ KIỆN CHO CONTROL TẠO TRONG DESIGNER ======
        void SetupEvents()
        {
            cboLevel.SelectedIndex = 0;
            cboLevel.SelectedIndexChanged += (s, e) =>
            {
                switch (cboLevel.SelectedIndex)
                {
                    case 0: rows = 9; cols = 9; mines = 10; break;
                    case 1: rows = 16; cols = 16; mines = 40; break;
                    default: rows = 16; cols = 30; mines = 99; break;
                }
                NewGame();
            };

            btnReset.Click += (s, e) => NewGame();

            timer.Tick += (s, e) =>
            {
                seconds++;
                lblTime.Text = "⏱ " + seconds.ToString("000");
            };
        }

        // ====== VÁN MỚI ======
        void NewGame()
        {
            timer.Stop();
            seconds = 0; flagCount = 0; openedCount = 0;
            firstClick = true; gameOver = false; chordUsed = false;

            map = new int[rows, cols];
            opened = new bool[rows, cols];
            flagged = new bool[rows, cols];
            cells = new Button[rows, cols];

            pnlBoard.SuspendLayout();
            pnlBoard.Controls.Clear();
            pnlBoard.Size = new Size(cols * CELL, rows * CELL);

            int formW = Math.Max(cols * CELL, 340);
            ClientSize = new Size(formW, pnlTop.Height + rows * CELL);
            pnlBoard.Location = new Point((formW - pnlBoard.Width) / 2, pnlTop.Height);

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var b = new Button();
                    b.SetBounds(c * CELL, r * CELL, CELL, CELL);
                    b.FlatStyle = FlatStyle.Standard;
                    b.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                    b.ImageAlign = ContentAlignment.MiddleCenter;
                    b.Margin = Padding.Empty;
                    b.TabStop = false;
                    b.Tag = new Point(c, r);          
                    b.MouseUp += Cell_MouseUp;
                    cells[r, c] = b;
                    pnlBoard.Controls.Add(b);
                }
            pnlBoard.ResumeLayout();

            btnReset.Text = "🙂";
            lblTime.Text = "⏱ 000";
            UpdateFlagLabel();
        }

        void UpdateFlagLabel() => lblFlag.Text = "🚩 " + (mines - flagCount);

        // ====== 2. RẢI MÌN (FIRST-CLICK SAFE) ======
        void PlaceMines(int safeR, int safeC)
        {
            var candidates = new List<Point>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (Math.Abs(r - safeR) > 1 || Math.Abs(c - safeC) > 1)
                        candidates.Add(new Point(c, r));

            // Xáo trộn (Fisher-Yates) rồi lấy 'mines' ô đầu
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
            }
            for (int i = 0; i < mines; i++)
                map[candidates[i].Y, candidates[i].X] = MINE;

            // Tính số mìn xung quanh cho các ô còn lại
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (map[r, c] != MINE)
                        map[r, c] = CountAround(r, c, (rr, cc) => map[rr, cc] == MINE);
        }

        int CountAround(int r, int c, Func<int, int, bool> cond)
        {
            int n = 0;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int rr = r + dr, cc = c + dc;
                    if (InRange(rr, cc) && cond(rr, cc)) n++;
                }
            return n;
        }

        bool InRange(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;

        // ====== XỬ LÝ CHUỘT ======
        void Cell_MouseUp(object sender, MouseEventArgs e)
        {
            if (gameOver) return;
            var p = (Point)((Button)sender).Tag;
            int r = p.Y, c = p.X;

            // Nhả 1 nút trong khi nút kia vẫn đang giữ -> chording bằng 2 nút chuột
            bool otherDown =
                (e.Button == MouseButtons.Left && (Control.MouseButtons & MouseButtons.Right) != 0) ||
                (e.Button == MouseButtons.Right && (Control.MouseButtons & MouseButtons.Left) != 0);

            if (otherDown)
            {
                chordUsed = true;
                Chord(r, c);
                return;
            }
            if (chordUsed) { chordUsed = false; return; }   // bỏ qua lần nhả nút còn lại

            if (e.Button == MouseButtons.Left) LeftClick(r, c);
            else if (e.Button == MouseButtons.Right) RightClick(r, c);
            else if (e.Button == MouseButtons.Middle) Chord(r, c);
        }

        void LeftClick(int r, int c)
        {
            if (flagged[r, c]) return;

            // Click vào ô số đã mở -> Chording
            if (opened[r, c])
            {
                Chord(r, c);
                return;
            }

            // First-click safe: rải mìn SAU khi biết ô đầu tiên
            if (firstClick)
            {
                PlaceMines(r, c);
                firstClick = false;
                timer.Start();
            }

            if (map[r, c] == MINE)
            {
                GameOver(false, r, c);
                return;
            }

            OpenCell(r, c);
            CheckWin();
        }

        void RightClick(int r, int c)
        {
            if (opened[r, c]) return;

            if (flagged[r, c])
            {
                flagged[r, c] = false; flagCount--;
                cells[r, c].Image = null;
            }
            else
            {
                flagged[r, c] = true; flagCount++;
                cells[r, c].Image = imgFlag;
            }
            UpdateFlagLabel();
        }

        // ====== 3. FLOOD FILL ĐỆ QUY ======
        void OpenCell(int r, int c)
        {
            if (!InRange(r, c) || opened[r, c] || flagged[r, c]) return;

            opened[r, c] = true;
            openedCount++;
            ShowOpened(r, c);

            if (map[r, c] != 0) return;   

            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    if (dr != 0 || dc != 0)
                        OpenCell(r + dr, c + dc);
        }

        void ShowOpened(int r, int c)
        {
            var b = cells[r, c];
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.Gainsboro;
            b.FlatAppearance.BorderColor = Color.Silver;
            if (map[r, c] > 0)
            {
                b.Text = map[r, c].ToString();
                b.ForeColor = NumColors[map[r, c]];
            }
            else b.Text = "";
        }

        // ====== 4. CHORDING ======
        void Chord(int r, int c)
        {
            if (!opened[r, c] || map[r, c] <= 0) return;
            if (CountAround(r, c, (rr, cc) => flagged[rr, cc]) != map[r, c]) return;

            bool hitMine = false;
            int hitR = -1, hitC = -1;

            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int rr = r + dr, cc = c + dc;
                    if (!InRange(rr, cc) || opened[rr, cc] || flagged[rr, cc]) continue;

                    if (map[rr, cc] == MINE) { hitMine = true; hitR = rr; hitC = cc; }
                    else OpenCell(rr, cc);
                }

            if (hitMine) GameOver(false, hitR, hitC);
            else CheckWin();
        }

        // 5. THẮNG / THUA 
        void CheckWin()
        {
            if (openedCount == rows * cols - mines)
                GameOver(true, -1, -1);
        }

        void GameOver(bool win, int hitR, int hitC)
        {
            gameOver = true;
            timer.Stop();

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var b = cells[r, c];
                    if (map[r, c] == MINE)
                    {
                        if (win)
                        {
                            b.Image = imgFlag;                 
                        }
                        else if (!flagged[r, c])
                        {
                            b.Image = imgMine;                 
                            b.FlatStyle = FlatStyle.Flat;
                            b.BackColor = (r == hitR && c == hitC) ? Color.Red : Color.Gainsboro;
                        }
                    }
                    else if (!win && flagged[r, c])
                    {
                        b.Image = imgMineWrong;                // cắm cờ sai: mìn gạch chéo đỏ
                    }
                }

            if (win)
            {
                flagCount = mines; UpdateFlagLabel();
                btnReset.Text = "😎";
                MessageBox.Show($"Bạn đã thắng!\nThời gian: {seconds} giây", "Chiến thắng",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                btnReset.Text = "😵";
                MessageBox.Show("Bạn đã dính mìn!", "Thua rồi",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ====== VẼ HÌNH MÌN VÀ CỜ (GDI+) ======
        static Bitmap DrawMine(int size, bool wrong)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = size / 2f, cy = size / 2f;
                float r = size * 0.26f;      // bán kính thân
                float L = size * 0.42f;      // độ dài gai
                float d = L * 0.72f;

                using (var pen = new Pen(Color.Black, size * 0.09f))
                {
                    g.DrawLine(pen, cx - L, cy, cx + L, cy);
                    g.DrawLine(pen, cx, cy - L, cx, cy + L);
                    g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
                    g.DrawLine(pen, cx - d, cy + d, cx + d, cy - d);
                }
                g.FillEllipse(Brushes.Black, cx - r, cy - r, 2 * r, 2 * r);
                g.FillEllipse(Brushes.White, cx - r * 0.65f, cy - r * 0.65f, r * 0.55f, r * 0.55f);

                if (wrong)
                    using (var red = new Pen(Color.Red, size * 0.12f))
                    {
                        g.DrawLine(red, 2, 2, size - 2, size - 2);
                        g.DrawLine(red, 2, size - 2, size - 2, 2);
                    }
            }
            return bmp;
        }

        // Lá cờ đỏ cắm trên cột đen
        static Bitmap DrawFlag(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float px = size * 0.55f;
                var tri = new PointF[]
                {
                    new PointF(px, size * 0.12f),
                    new PointF(px, size * 0.52f),
                    new PointF(size * 0.18f, size * 0.32f)
                };
                g.FillPolygon(Brushes.Red, tri);
                using (var pen = new Pen(Color.Black, size * 0.08f))
                    g.DrawLine(pen, px, size * 0.12f, px, size * 0.78f);
                g.FillRectangle(Brushes.Black, size * 0.28f, size * 0.78f, size * 0.54f, size * 0.12f);
            }
            return bmp;
        }
    }
}