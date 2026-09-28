using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gimnasio.Sesion
{
    public partial class frmLogin : Form
    {
        public frmLogin()
        {
            InitializeComponent();
        }

        private void frmLogin_Load(object sender, EventArgs e)
        {
            // Aplica el tema global y luego personaliza el panel de tarjeta
            Utilidades.clsTheme.Apply(this);

            // Pintado custom del panel card (sombra/línea de acento superior)
            panelCard.Paint += PanelCard_Paint;

            // Hover effects en el botón de login
            button1.MouseEnter += (s, ev) => button1.BackColor = Utilidades.clsTheme.AccentHover;
            button1.MouseLeave += (s, ev) => button1.BackColor = Utilidades.clsTheme.Accent;
        }

        private void PanelCard_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            // Borde de acento en la parte superior de la tarjeta
            using (var pen = new System.Drawing.Pen(Utilidades.clsTheme.Accent, 3))
                e.Graphics.DrawLine(pen, 0, 0, panelCard.Width, 0);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Text.Trim();
            //VALIDACIONES
            if (usuario.Equals("") || password.Equals(""))
            {
                MessageBox.Show("Usuario y password son obligatorios");
                return;
            }
            //PROCESO
            if (Utilidades.clsUsuario.login(usuario, password))
            {
                this.Close();
            }
            else
            {
                MessageBox.Show(Utilidades.clsUsuario.error);
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            
            
        }
    }
}
