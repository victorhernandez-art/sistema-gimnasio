using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gimnasio.Utilidades
{
    /// <summary>
    /// Motor de temas visuales modernos para toda la aplicación.
    /// Aplica una paleta oscura tipo Dark Mode con acentos en color cielo (sky-blue).
    /// Uso: clsTheme.Apply(this) en el evento Load de cualquier Form.
    /// </summary>
    public static class clsTheme
    {
        // ── Paleta de colores ─────────────────────────────────────────────────────

        /// <summary>Fondo principal de formularios y áreas de contenido</summary>
        public static readonly Color BgPrimary = Color.FromArgb(15, 23, 42);       // slate-900

        /// <summary>Fondos de superficies/tarjetas (paneles, DataGridView headers)</summary>
        public static readonly Color BgSurface = Color.FromArgb(30, 41, 59);       // slate-800

        /// <summary>Fondos de elementos elevados (barras de herramientas, filas alt.)</summary>
        public static readonly Color BgPanel = Color.FromArgb(51, 65, 85);         // slate-700

        /// <summary>Color de acento primario para botones principales y encabezados</summary>
        public static readonly Color Accent = Color.FromArgb(14, 165, 233);        // sky-500

        /// <summary>Acento en estado hover/pressed</summary>
        public static readonly Color AccentHover = Color.FromArgb(2, 132, 199);    // sky-600

        /// <summary>Texto principal (casi blanco)</summary>
        public static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);  // slate-100

        /// <summary>Texto secundario / placeholders</summary>
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);    // slate-400

        /// <summary>Fondo de inputs (TextBox, ComboBox, DateTimePicker)</summary>
        public static readonly Color InputBg = Color.FromArgb(30, 41, 59);         // slate-800

        /// <summary>Color de borde/línea separadora</summary>
        public static readonly Color Border = Color.FromArgb(71, 85, 105);         // slate-600

        // Botones de acción semántica
        public static readonly Color BtnDanger = Color.FromArgb(239, 68, 68);      // red-500
        public static readonly Color BtnSuccess = Color.FromArgb(34, 197, 94);     // green-500
        public static readonly Color BtnWarning = Color.FromArgb(245, 158, 11);    // amber-500

        // Colores para DataGridView
        public static readonly Color GridEven = Color.FromArgb(22, 33, 54);        // slightly lighter bg
        public static readonly Color GridOdd = Color.FromArgb(15, 23, 42);         // = BgPrimary
        public static readonly Color GridHeader = Color.FromArgb(30, 41, 59);      // = BgSurface
        public static readonly Color GridSelect = Color.FromArgb(14, 90, 155);     // dark blue selection

        // ── Tipografía ────────────────────────────────────────────────────────────

        private static readonly Font _fontTitle = new Font("Segoe UI", 20f, FontStyle.Bold);
        private static readonly Font _fontSubtitle = new Font("Segoe UI", 12f, FontStyle.Bold);
        private static readonly Font _fontBody = new Font("Segoe UI", 10f);
        private static readonly Font _fontBold = new Font("Segoe UI", 10f, FontStyle.Bold);
        private static readonly Font _fontSmall = new Font("Segoe UI", 9f);
        private static readonly Font _fontToolbar = new Font("Segoe UI", 9f, FontStyle.Bold);

        public static Font FontTitle    { get { return _fontTitle; } }
        public static Font FontSubtitle { get { return _fontSubtitle; } }
        public static Font FontBody     { get { return _fontBody; } }
        public static Font FontBold     { get { return _fontBold; } }
        public static Font FontSmall    { get { return _fontSmall; } }
        public static Font FontToolbar  { get { return _fontToolbar; } }

        // ── Punto de entrada principal ────────────────────────────────────────────

        /// <summary>
        /// Aplica el tema moderno a un formulario completo, incluyendo todos sus controles hijos.
        /// Llamar en el evento Load del formulario: clsTheme.Apply(this);
        /// </summary>
        // ── Dark title bar (Windows 10 1903+ / Windows 11) ─────────────────────
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static void ApplyDarkTitleBar(Form form)
        {
            try
            {
                int dark = 1;
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Win11/10 21H1+)
                if (DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)) != 0)
                    // fallback para builds anteriores de Win10
                    DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));
            }
            catch { }
        }

        public static void Apply(Form form)
        {
            // Deshabilitar AutoScaleMode.Font ANTES de cambiar la fuente para evitar
            // que WinForms reescale todos los controles con la nueva proporción de métricas.
            form.AutoScaleMode = AutoScaleMode.None;
            form.BackColor = BgPrimary;
            form.ForeColor = TextPrimary;
            form.Font = _fontBody;
            ApplyDarkTitleBar(form);
            ApplyMdiBackground(form);
            ApplyControls(form.Controls);
        }

        /// <summary>
        /// Aplica el tema a una colección de controles de forma recursiva.
        /// </summary>
        public static void ApplyControls(Control.ControlCollection controls)
        {
            foreach (Control ctrl in controls)
            {
                StyleControl(ctrl);
                // Recursión para contenedores
                if (ctrl.Controls.Count > 0 && !(ctrl is DataGridView))
                    ApplyControls(ctrl.Controls);
            }
        }

        // ── Lógica de estilos por tipo de control ─────────────────────────────────

        private static void StyleControl(Control ctrl)
        {
            // Actualiza la tipografía:
            // - Fuentes por defecto (Microsoft Sans Serif ≤ 12pt) → Segoe UI 10pt
            // - Fuentes grandes (> 12pt) → mantener tamaño, sólo cambiar familia a Segoe UI
            // - Fuentes ya personalizadas (no Sans Serif) → no tocar
            UpdateFont(ctrl);

            if (ctrl is Button)
            {
                StyleButton((Button)ctrl);
            }
            else if (ctrl is DataGridView)
            {
                StyleDataGridView((DataGridView)ctrl);
            }
            else if (ctrl is MenuStrip)
            {
                StyleMenuStrip((MenuStrip)ctrl);
            }
            else if (ctrl is SplitContainer)
            {
                SplitContainer spl = (SplitContainer)ctrl;
                spl.BackColor = BgPrimary;
                spl.Panel1.BackColor = BgSurface;
                spl.Panel2.BackColor = BgPrimary;
            }
            else if (ctrl is Panel)
            {
                Panel pan = (Panel)ctrl;
                if (!pan.Name.Equals("panelBotones", StringComparison.OrdinalIgnoreCase))
                    pan.BackColor = BgSurface;
                else
                    StyleToolbarPanel(pan);
            }
            else if (ctrl is TextBox)
            {
                TextBox txt = (TextBox)ctrl;
                txt.BackColor = InputBg;
                txt.ForeColor = TextPrimary;
                txt.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (ctrl is RichTextBox)
            {
                RichTextBox rtb = (RichTextBox)ctrl;
                rtb.BackColor = InputBg;
                rtb.ForeColor = TextPrimary;
                rtb.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (ctrl is Label)
            {
                ctrl.ForeColor = TextPrimary;
                ctrl.BackColor = Color.Transparent;
            }
            else if (ctrl is GroupBox)
            {
                ctrl.BackColor = BgSurface;
                ctrl.ForeColor = TextPrimary;
            }
            else if (ctrl is ComboBox)
            {
                ComboBox cmb = (ComboBox)ctrl;
                cmb.BackColor = InputBg;
                cmb.ForeColor = TextPrimary;
                cmb.FlatStyle = FlatStyle.Flat;
            }
            else if (ctrl is CheckBox)
            {
                ctrl.ForeColor = TextPrimary;
                ctrl.BackColor = Color.Transparent;
            }
            else if (ctrl is RadioButton)
            {
                ctrl.ForeColor = TextPrimary;
                ctrl.BackColor = Color.Transparent;
            }
            else if (ctrl is TabControl)
            {
                StyleTabControl((TabControl)ctrl);
            }
            else if (ctrl is TabPage)
            {
                ctrl.BackColor = BgSurface;
                ctrl.ForeColor = TextPrimary;
            }
            else if (ctrl is PictureBox)
            {
                ctrl.BackColor = BgSurface;
            }
            else if (ctrl is DateTimePicker)
            {
                ctrl.BackColor = InputBg;
                ctrl.ForeColor = TextPrimary;
            }
            else if (ctrl is NumericUpDown)
            {
                ctrl.BackColor = InputBg;
                ctrl.ForeColor = TextPrimary;
            }
            else if (ctrl is ListBox)
            {
                ctrl.BackColor = InputBg;
                ctrl.ForeColor = TextPrimary;
            }
            else if (ctrl is ToolStrip)
            {
                ToolStrip ts = (ToolStrip)ctrl;
                ts.BackColor = BgSurface;
                ts.ForeColor = TextPrimary;
                ts.Renderer = new ToolStripProfessionalRenderer(new GymColorTable());
            }
        }

        // ── Botones ───────────────────────────────────────────────────────────────

        // Hover state per button
        private static readonly System.Collections.Generic.Dictionary<Button, bool> _actionBtnHover =
            new System.Collections.Generic.Dictionary<Button, bool>();

        // Segoe MDL2 Assets glyph map for action buttons
        private static readonly System.Collections.Generic.Dictionary<string, string> _actionGlyphs =
            new System.Collections.Generic.Dictionary<string, string>()
            {
                { "cmdnuevo",       "\uE710" }, // Add
                { "cmdmodificar",   "\uE70F" }, // Edit / pencil
                { "cmddesabilitar", "\uEDB5" }, // Block / disable
                { "cmdabilitar",    "\uE73E" }, // Checkmark
                { "cmdeliminar",    "\uE74D" }, // Delete / trash
                { "cmdguardar",     "\uE74E" }, // Save
                { "cmdgrabar",      "\uE74E" }, // Save
                { "btnnuevo",       "\uE710" },
                { "btnmodificar",   "\uE70F" },
                { "btnaceptar",     "\uE73E" },
                { "btnguardar",     "\uE74E" },
                { "btncancelar",    "\uE8FB" }, // Cancel
            };

        /// <summary>
        /// Aplica estilo moderno con esquinas redondeadas, ícono MDL2 y efecto hover/press.
        /// </summary>
        public static void StyleButton(Button btn)
        {
            Color bg = GetButtonColor(btn.Name);

            btn.BackgroundImage = null;
            btn.Image = null;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.Font = _fontBold;
            btn.Cursor = Cursors.Hand;
            btn.UseVisualStyleBackColor = false;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.Tag = bg;

            // Aumentar alto de botones de acción para mejor proporción
            if (btn.Name.StartsWith("cmd", StringComparison.OrdinalIgnoreCase))
            {
                btn.Height = 36;
                if (btn.Parent != null && btn.Parent.ClientSize.Height > 0)
                    btn.Top = (btn.Parent.ClientSize.Height - 36) / 2;
            }

            btn.Paint      -= ActionBtn_Paint;
            btn.MouseEnter -= ActionBtn_MouseEnter;
            btn.MouseLeave -= ActionBtn_MouseLeave;
            btn.MouseDown  -= ActionBtn_MouseDown;
            btn.MouseUp    -= ActionBtn_MouseUp;

            _actionBtnHover[btn] = false;
            btn.Paint      += ActionBtn_Paint;
            btn.MouseEnter += ActionBtn_MouseEnter;
            btn.MouseLeave += ActionBtn_MouseLeave;
            btn.MouseDown  += ActionBtn_MouseDown;
            btn.MouseUp    += ActionBtn_MouseUp;
        }

        private static void ActionBtn_MouseEnter(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            if (_actionBtnHover.ContainsKey(btn)) _actionBtnHover[btn] = true;
            btn.Invalidate();
        }
        private static void ActionBtn_MouseLeave(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            if (_actionBtnHover.ContainsKey(btn)) _actionBtnHover[btn] = false;
            btn.Invalidate();
        }
        private static void ActionBtn_MouseDown(object sender, MouseEventArgs e) { ((Button)sender).Invalidate(); }
        private static void ActionBtn_MouseUp(object sender, MouseEventArgs e)   { ((Button)sender).Invalidate(); }

        private static void ActionBtn_Paint(object sender, PaintEventArgs e)
        {
            Button btn = (Button)sender;
            if (btn.IsDisposed) return;

            Color baseColor = (btn.Tag is Color) ? (Color)btn.Tag : Accent;
            bool hover   = _actionBtnHover.ContainsKey(btn) && _actionBtnHover[btn];
            bool pressed = hover && (System.Windows.Forms.Control.MouseButtons & MouseButtons.Left) != 0;

            Color bg = pressed ? DarkenColor(baseColor, 25)
                     : hover   ? LightenColor(baseColor, 22)
                               : baseColor;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Limpiar con color del padre para que las esquinas redondeadas "transparen"
            Color parentBg = (btn.Parent != null) ? btn.Parent.BackColor : BgSurface;
            g.Clear(parentBg);

            Rectangle rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
            int radius = 8;

            // Fondo redondeado
            using (GraphicsPath path = CreateRoundedPath(rect, radius))
            {
                using (SolidBrush fill = new SolidBrush(bg))
                    g.FillPath(fill, path);

                // Línea de brillo superior sutil
                using (Pen glow = new Pen(Color.FromArgb(55, Color.White), 1f))
                    g.DrawLine(glow, rect.X + radius, rect.Y + 1, rect.Right - radius, rect.Y + 1);

                // Borde interior muy tenue
                using (Pen border = new Pen(Color.FromArgb(35, Color.White), 1f))
                    g.DrawPath(border, path);
            }

            // Ícono Segoe MDL2 Assets
            string glyph = null;
            _actionGlyphs.TryGetValue((btn.Name ?? "").ToLower(), out glyph);

            float iconW = 0f;
            if (!string.IsNullOrEmpty(glyph))
            {
                using (Font iFont = new Font("Segoe MDL2 Assets", 10f))
                {
                    SizeF s = g.MeasureString(glyph, iFont);
                    iconW = s.Width;
                }
            }

            // Calcular posición centrada (ícono + espacio + texto)
            SizeF textSize = g.MeasureString(btn.Text, _fontBold);
            float gap     = string.IsNullOrEmpty(glyph) ? 0f : 3f;
            float totalW  = iconW + gap + textSize.Width;
            float startX  = (btn.Width - totalW) / 2f;
            float textX   = startX + iconW + gap;
            float textY   = (btn.Height - textSize.Height) / 2f;
            float iconY   = 0f;

            using (SolidBrush wb = new SolidBrush(Color.White))
            {
                if (!string.IsNullOrEmpty(glyph))
                {
                    using (Font iFont = new Font("Segoe MDL2 Assets", 10f))
                    {
                        SizeF s = g.MeasureString(glyph, iFont);
                        iconY = (btn.Height - s.Height) / 2f;
                        g.DrawString(glyph, iFont, wb, startX, iconY);
                    }
                }
                g.DrawString(btn.Text, _fontBold, wb, textX, textY);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X,            rect.Y,             d, d, 180, 90);
            path.AddArc(rect.Right - d,    rect.Y,             d, d, 270, 90);
            path.AddArc(rect.Right - d,    rect.Bottom - d,    d, d,   0, 90);
            path.AddArc(rect.X,            rect.Bottom - d,    d, d,  90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color GetButtonColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return Accent;
            string n = name.ToLower();
            if (n.Contains("nuevo") || (n.Contains("abilitar") && !n.Contains("des")))
                return BtnSuccess;
            if (n.Contains("eliminar") || n.Contains("borrar") || n.Contains("cancelar"))
                return BtnDanger;
            if (n.Contains("desabilitar") || n.Contains("deshabilitar"))
                return BtnWarning;
            if (n.Contains("guardar") || n.Contains("grabar") || n.Contains("save"))
                return BtnSuccess;
            return Accent;
        }
        // ── TabControl (owner-drawn) ───────────────────────────────────────

        private static void StyleTabControl(TabControl tc)
        {
            tc.DrawMode    = TabDrawMode.OwnerDrawFixed;
            tc.ItemSize    = new Size(tc.ItemSize.Width < 10 ? 120 : tc.ItemSize.Width, 34);
            tc.SizeMode    = TabSizeMode.Fixed;
            tc.Padding     = new Point(16, 6);
            tc.BackColor   = BgSurface;
            tc.ForeColor   = TextPrimary;

            tc.DrawItem -= TabControl_DrawItem;
            tc.DrawItem += TabControl_DrawItem;
        }

        private static void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tc = (TabControl)sender;
            TabPage page  = tc.TabPages[e.Index];
            bool selected = (tc.SelectedIndex == e.Index);
            bool hot      = (e.State & DrawItemState.Selected) == 0 && e.Bounds.Contains(tc.PointToClient(System.Windows.Forms.Cursor.Position));

            Graphics g = e.Graphics;
            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.TextRenderingHint  = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Fondo del tab
            Color bgTab = selected ? BgSurface
                        : hot      ? BgPanel
                                   : BgPrimary;
            using (SolidBrush bg = new SolidBrush(bgTab))
                g.FillRectangle(bg, e.Bounds);

            // Línea indicadora inferior en el tab seleccionado
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(Accent))
                    g.FillRectangle(accent,
                        new Rectangle(e.Bounds.X + 4, e.Bounds.Bottom - 3,
                                      e.Bounds.Width - 8, 3));
            }

            // Texto del tab
            Color fgTab = selected ? TextPrimary
                        : hot      ? TextPrimary
                                   : TextMuted;
            Font  font  = selected ? _fontBold : _fontBody;

            StringFormat sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            // Ajustar rect para dejar espacio al indicador
            Rectangle textRect = new Rectangle(
                e.Bounds.X, e.Bounds.Y,
                e.Bounds.Width, e.Bounds.Height - (selected ? 3 : 0));

            using (SolidBrush fg = new SolidBrush(fgTab))
                g.DrawString(page.Text, font, fg, textRect, sf);
        }
        // ── Panel de botones (toolbar interno) ───────────────────────────────────

        private static void StyleToolbarPanel(Panel panel)
        {
            panel.BackColor = BgSurface;
            panel.Padding = new Padding(4, 6, 4, 6);
        }

        // ── DataGridView ─────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica el tema oscuro completo al DataGridView de listado.
        /// </summary>
        public static void StyleDataGridView(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.BackgroundColor = BgPrimary;
            dgv.GridColor = BgPanel;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.RowHeadersVisible = false;

            // Encabezados de columna
            dgv.ColumnHeadersDefaultCellStyle.BackColor = GridHeader;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
            dgv.ColumnHeadersDefaultCellStyle.Font = _fontBold;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersHeight = 36;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Celdas por defecto (fila impar)
            dgv.DefaultCellStyle.BackColor = GridOdd;
            dgv.DefaultCellStyle.ForeColor = TextPrimary;
            dgv.DefaultCellStyle.Font = _fontBody;
            dgv.DefaultCellStyle.SelectionBackColor = GridSelect;
            dgv.DefaultCellStyle.SelectionForeColor = TextPrimary;
            dgv.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);

            // Filas alternas (par)
            dgv.AlternatingRowsDefaultCellStyle.BackColor = GridEven;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = GridSelect;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;

            // Estilo base de fila
            dgv.RowsDefaultCellStyle.BackColor = GridOdd;
            dgv.RowsDefaultCellStyle.ForeColor = TextPrimary;
            dgv.RowsDefaultCellStyle.SelectionBackColor = GridSelect;
            dgv.RowsDefaultCellStyle.SelectionForeColor = TextPrimary;

            dgv.RowTemplate.Height = 34;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        }

        // ── MenuStrip ─────────────────────────────────────────────────────────────

        private static void StyleMenuStrip(MenuStrip ms)
        {
            ms.BackColor = BgSurface;
            ms.ForeColor = TextPrimary;
            ms.Renderer = new ToolStripProfessionalRenderer(new GymColorTable());
            ms.Padding = new Padding(8, 2, 0, 2);

            foreach (ToolStripItem item in ms.Items)
            {
                item.ForeColor = TextPrimary;
                item.BackColor = BgSurface;
                item.Font = _fontBold;
                if (item is ToolStripMenuItem)
                    StyleMenuItems(((ToolStripMenuItem)item).DropDownItems);
            }
        }

        private static void StyleMenuItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.ForeColor = TextPrimary;
                item.BackColor = BgSurface;
                if (item is ToolStripMenuItem)
                    StyleMenuItems(((ToolStripMenuItem)item).DropDownItems);
            }
        }

        // ── MDI Background ────────────────────────────────────────────────────────

        /// <summary>
        /// Busca el control MdiClient interno y le aplica el color de fondo oscuro.
        /// </summary>
        public static void ApplyMdiBackground(Form form)
        {
            foreach (Control ctrl in form.Controls)
            {
                if (ctrl.GetType().Name == "MdiClient")
                {
                    ctrl.BackColor = BgPrimary;
                    break;
                }
            }
        }

        // ── Método especial para la barra de herramientas principal ───────────────

        /// <summary>
        /// Aplica estilos especiales a los botones de la barra de herramientas principal
        /// del MDI (panel1 de FrmMain), que tienen imágenes de fondo (iconos).
        /// </summary>
        // ══════════════════════════════════════════════════════════════════════════
        //  TOOLBAR: Iconos Segoe MDL2 Assets + efecto hover con transición suave
        // ══════════════════════════════════════════════════════════════════════════

        // Mapa nombre-botón → glifo Segoe MDL2 Assets
        private static readonly System.Collections.Generic.Dictionary<string, string> _toolbarGlyphs =
            new System.Collections.Generic.Dictionary<string, string>()
            {
                { "button1",  "\uE716" }, // Usuarios       — persona
                { "button2",  "\uE902" }, // Socios         — grupo personas
                { "button3",  "\uE8EC" }, // Membresías     — tarjeta contacto
                { "button4",  "\uE7B8" }, // Productos      — paquete
                { "button5",  "\uE8BA" }, // Salidas        — salir
                { "button6",  "\uE8BF" }, // Entradas       — entrar
                { "button7",  "\uE9F9" }, // Reportes       — gráfica barras
                { "button8",  "\uE9D5" }, // Registro       — reloj / historial
                { "button9",  "\uE713" }, // Configuración  — engranaje
                { "button12", "\uE74E" }, // Respaldar      — guardar
                { "button13", "\uE72C" }, // Restaurar      — actualizar circular
            };

        // Estado de animación hover por botón (0.0 = reposo → 1.0 = hover completo)
        private static readonly System.Collections.Generic.Dictionary<Button, float> _btnHoverAmt =
            new System.Collections.Generic.Dictionary<Button, float>();
        private static readonly System.Collections.Generic.Dictionary<Button, int> _btnHoverDir =
            new System.Collections.Generic.Dictionary<Button, int>(); // +1 entrar, -1 salir, 0 estático
        private static System.Windows.Forms.Timer _hoverTimer;

        public static void StyleMainToolbarPanel(Panel toolbar)
        {
            toolbar.BackColor = BgSurface;
            toolbar.Height = 90;

            // Temporizador compartido ~66 fps para transiciones
            if (_hoverTimer == null)
            {
                _hoverTimer = new System.Windows.Forms.Timer();
                _hoverTimer.Interval = 15;
                _hoverTimer.Tick += HoverTimer_Tick;
                _hoverTimer.Start();
            }

            foreach (Control ctrl in toolbar.Controls)
            {
                if (ctrl is Button)
                    ApplyToolbarButton((Button)ctrl);
                else if (ctrl is Label)
                {
                    ctrl.ForeColor = TextMuted;
                    ctrl.BackColor = Color.Transparent;
                    ctrl.Font = _fontSmall;
                }
            }
        }

        private static void ApplyToolbarButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.BackColor = BgSurface;
            btn.Text = "";
            btn.Image = null;
            btn.BackgroundImage = null;
            btn.Cursor = Cursors.Hand;
            btn.UseVisualStyleBackColor = false;

            // Guardar glifo en Tag para Paint
            string glyph;
            _toolbarGlyphs.TryGetValue(btn.Name, out glyph);
            btn.Tag = glyph;

            _btnHoverAmt[btn] = 0f;
            _btnHoverDir[btn] = 0;

            // Suscribir (evitar duplicados)
            btn.Paint -= ToolbarBtn_Paint;
            btn.MouseEnter -= ToolbarBtn_MouseEnter;
            btn.MouseLeave -= ToolbarBtn_MouseLeave;
            btn.Paint += ToolbarBtn_Paint;
            btn.MouseEnter += ToolbarBtn_MouseEnter;
            btn.MouseLeave += ToolbarBtn_MouseLeave;
        }

        private static void HoverTimer_Tick(object sender, EventArgs e)
        {
            System.Collections.Generic.List<Button> keys =
                new System.Collections.Generic.List<Button>(_btnHoverAmt.Keys);
            foreach (Button btn in keys)
            {
                if (btn.IsDisposed) continue;
                int dir;
                _btnHoverDir.TryGetValue(btn, out dir);
                if (dir == 0) continue;

                float cur = _btnHoverAmt[btn];
                float next = cur + dir * 0.10f;
                if (next >= 1f) { next = 1f; _btnHoverDir[btn] = 0; }
                else if (next <= 0f) { next = 0f; _btnHoverDir[btn] = 0; }

                _btnHoverAmt[btn] = next;
                btn.Invalidate();
            }
        }

        private static void ToolbarBtn_MouseEnter(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            if (_btnHoverDir.ContainsKey(btn)) _btnHoverDir[btn] = 1;
        }

        private static void ToolbarBtn_MouseLeave(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            if (_btnHoverDir.ContainsKey(btn)) _btnHoverDir[btn] = -1;
        }

        private static void ToolbarBtn_Paint(object sender, PaintEventArgs e)
        {
            Button btn = (Button)sender;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            float hover = 0f;
            _btnHoverAmt.TryGetValue(btn, out hover);

            bool isPressed = (System.Windows.Forms.Control.MouseButtons == MouseButtons.Left)
                          && btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position));

            // ── 1. Fondo con tinte suave al hacer hover ──────────────────────────
            Color bg = isPressed
                ? BlendColor(BgSurface, Accent, 0.28f)
                : BlendColor(BgSurface, BgPanel, hover * 0.80f);
            using (SolidBrush bgBrush = new SolidBrush(bg))
                g.FillRectangle(bgBrush, 0, 0, btn.Width, btn.Height);

            // ── 2. Línea de acento inferior que crece con el hover ───────────────
            int lineH = (int)Math.Round(4f * hover);
            if (lineH > 0)
            {
                Color lineColor = BlendColor(AccentHover, Color.White, hover * 0.15f);
                using (SolidBrush lineBrush = new SolidBrush(lineColor))
                    g.FillRectangle(lineBrush, 2, btn.Height - lineH, btn.Width - 4, lineH);
            }

            // ── 3. Icono (Segoe MDL2 Assets) — se ilumina en hover ───────────────
            string glyph = btn.Tag as string;
            if (!string.IsNullOrEmpty(glyph))
            {
                Color iconColor = isPressed
                    ? Color.White
                    : BlendColor(Accent, Color.White, hover * 0.40f);

                float iconPx = btn.Height * 0.52f;
                Font iconFont = null;
                try
                {
                    iconFont = new Font("Segoe MDL2 Assets", iconPx, FontStyle.Regular, GraphicsUnit.Pixel);
                }
                catch
                {
                    try { iconFont = new Font("Segoe UI Symbol", iconPx, FontStyle.Regular, GraphicsUnit.Pixel); }
                    catch { iconFont = new Font("Arial", iconPx); }
                }

                using (iconFont)
                using (SolidBrush iconBrush = new SolidBrush(iconColor))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(glyph, iconFont, iconBrush,
                        new RectangleF(0, 0, btn.Width, btn.Height), sf);
                }
            }

            // ── 4. Borde sutil en hover ──────────────────────────────────────────
            if (hover > 0f)
            {
                int alpha = (int)(40 * hover);
                using (Pen borderPen = new Pen(Color.FromArgb(alpha, Accent)))
                    g.DrawRectangle(borderPen, 1, 1, btn.Width - 3, btn.Height - 3);
            }
        }

        /// <summary>Interpola linealmente entre dos colores. t=0 devuelve a, t=1 devuelve b.</summary>
        private static Color BlendColor(Color a, Color b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // ── Utilidades de color ───────────────────────────────────────────────────

        /// <summary>
        /// Actualiza el font de un control de forma inteligente:
        /// - Si usa "Microsoft Sans Serif" y tamaño ≤ 12pt → sustituye por Segoe UI 10pt.
        /// - Si usa "Microsoft Sans Serif" y tamaño > 12pt → mantiene tamaño, cambia a Segoe UI.
        /// - Cualquier otra fuente → no se toca.
        /// </summary>
        private static void UpdateFont(Control ctrl)
        {
            string name = ctrl.Font.Name;
            if (!name.Equals("Microsoft Sans Serif", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("MS Sans Serif", StringComparison.OrdinalIgnoreCase))
                return; // fuente personalizada, no modificar

            if (ctrl.Font.Size <= 12f)
            {
                // Sustituir por fuente de cuerpo del tema
                bool isBold = ctrl.Font.Bold;
                ctrl.Font = isBold ? _fontBold : _fontBody;
            }
            else
            {
                // Título grande: mantener tamaño, sólo cambiar a Segoe UI
                ctrl.Font = new Font("Segoe UI", ctrl.Font.Size, ctrl.Font.Style);
            }
        }

        /// <summary>Aclara un color en la cantidad de pasos indicada (0-255).</summary>
        public static Color LightenColor(Color color, int amount)
        {
            return Color.FromArgb(
                Math.Min(255, color.R + amount),
                Math.Min(255, color.G + amount),
                Math.Min(255, color.B + amount));
        }

        /// <summary>Oscurece un color en la cantidad de pasos indicada (0-255).</summary>
        public static Color DarkenColor(Color color, int amount)
        {
            return Color.FromArgb(
                Math.Max(0, color.R - amount),
                Math.Max(0, color.G - amount),
                Math.Max(0, color.B - amount));
        }

        // ── Tabla de colores personalizada para MenuStrip/ToolStrip ──────────────

        private class GymColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected              { get { return BgPanel; } }
            public override Color MenuItemBorder                { get { return Accent; } }
            public override Color MenuBorder                    { get { return Border; } }
            public override Color MenuItemSelectedGradientBegin { get { return BgPanel; } }
            public override Color MenuItemSelectedGradientEnd   { get { return BgPanel; } }
            public override Color MenuItemPressedGradientBegin  { get { return AccentHover; } }
            public override Color MenuItemPressedGradientEnd    { get { return AccentHover; } }
            public override Color MenuStripGradientBegin        { get { return BgSurface; } }
            public override Color MenuStripGradientEnd          { get { return BgSurface; } }
            public override Color ToolStripDropDownBackground   { get { return BgSurface; } }
            public override Color ImageMarginGradientBegin      { get { return BgSurface; } }
            public override Color ImageMarginGradientMiddle     { get { return BgSurface; } }
            public override Color ImageMarginGradientEnd        { get { return BgSurface; } }
            public override Color SeparatorDark                 { get { return BgPanel; } }
            public override Color SeparatorLight                { get { return Border; } }
            public override Color ToolStripContentPanelGradientBegin { get { return BgPrimary; } }
            public override Color ToolStripContentPanelGradientEnd   { get { return BgPrimary; } }
            public override Color ToolStripBorder               { get { return Border; } }
        }
    }
}
