using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gimnasio.Utilidades
{
    public partial class frmPadre : Form
    {
        public frmPadre()
        {
            InitializeComponent();
        }

        private void frmPadre_Load(object sender, EventArgs e)
        {
            // Aplica el tema moderno a este formulario y todos sus controles hijos.
            // Al estar en frmPadre_Load (suscrito en Designer.cs), se ejecuta
            // automáticamente para todos los formularios que heredan de frmPadre.
            clsTheme.Apply(this);
            EstilizarEncabezado();
        }

        /// <summary>
        /// Personaliza el lblTitle para que se vea como un encabezado moderno.
        /// </summary>
        private void EstilizarEncabezado()
        {
            lblTitle.Font = clsTheme.FontTitle;
            lblTitle.ForeColor = clsTheme.Accent;
            lblTitle.BackColor = Color.Transparent;

            // Línea decorativa inferior en el panel superior
            spContenedor.Panel1.Paint -= Panel1_Paint; // evitar duplicado
            spContenedor.Panel1.Paint += Panel1_Paint;
        }

        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            // Dibuja una línea de acento en la parte inferior del panel de cabecera
            using (var pen = new System.Drawing.Pen(clsTheme.Accent, 2))
            {
                int y = spContenedor.Panel1.Height - 3;
                e.Graphics.DrawLine(pen, 0, y, spContenedor.Panel1.Width, y);
            }
        }

        private void spContenedor_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void cmdModificar_Click(object sender, EventArgs e)
        {

        }
    }
}
