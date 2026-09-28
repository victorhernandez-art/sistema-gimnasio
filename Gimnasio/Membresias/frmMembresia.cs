using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gimnasio.Membresias
{
    public partial class frmMembresia : Form
    {
        public int idMembresia = 0;
        clsMembresia oMembresia = new clsMembresia();

        // Días reales que se guardan en el campo `meses` de la BD.
        // El índice corresponde exactamente al ítem del cboMeses.
        private static readonly int[] _diasPeriodo = { 1, 7, 15, 30, 60, 90, 120, 180, 270, 365 };
        public frmMembresia()
        {
            InitializeComponent();
        }

        private void frmMembresia_Load(object sender, EventArgs e)
        {
            Utilidades.clsTheme.Apply(this);
            cboMeses.SelectedIndex = 0;

            if (idMembresia > 0)
            {
                cargaDatos();
            }
        }

        private void cargaDatos()
        {
            if (oMembresia.getDatos(idMembresia))
            {
                txtNombre.Text = oMembresia.datos.Nombre;
                txtPrecio.Text = oMembresia.datos.Precio.ToString();
                // Buscar el índice cuyo valor de días sea el más cercano al guardado
                cboMeses.SelectedIndex = IndicePorDias(oMembresia.datos.meses);
                dpInicio.Text = oMembresia.datos.horaInicio.ToString();
                dpFinal.Text = oMembresia.datos.horaFinal.ToString();
            }
            else
            {
                MessageBox.Show("Ocurrio un problema al cargar los datos " + oMembresia.getError());
                this.Close();
            }
        }

        // Devuelve el índice del combo cuyo valor de días es más cercano a 'dias'.
        private int IndicePorDias(int dias)
        {
            int idx = 0;
            int menorDif = Math.Abs(_diasPeriodo[0] - dias);
            for (int i = 1; i < _diasPeriodo.Length; i++)
            {
                int dif = Math.Abs(_diasPeriodo[i] - dias);
                if (dif < menorDif) { menorDif = dif; idx = i; }
            }
            return idx;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (idMembresia <= 0)
            {
                agrega();
            }
            else
            {
                modifica();
            }
        }

        private void agrega()
        {
             try
            {
                //validaciones
                if (txtNombre.Text.Trim().Equals("") || txtPrecio.Text.Trim().Equals("") || cboMeses.Text.Equals(""))
                {
                    MessageBox.Show("Nombre, Precio y Meses son obligatorios");
                    return;
                }
                if (!ExpresionesRegulares.RegEX.isDecimal(txtPrecio.Text.Trim()))
                {
                    MessageBox.Show("El precio debe ser un numero valido, no se permiten letras ni caracteres que no sean numeros");
                    return;
                }

                //asignacion de datos
                oMembresia.Nombre = txtNombre.Text.Trim();
                oMembresia.Precio = decimal.Parse(txtPrecio.Text.Trim());
                oMembresia.meses = _diasPeriodo[cboMeses.SelectedIndex];
                oMembresia.horaInicio = dpInicio.Value.TimeOfDay;
                oMembresia.horaFinal = dpFinal.Value.TimeOfDay;
                oMembresia.idUsuarioLog = Utilidades.clsUsuario.idUsuario;
                if (oMembresia.add())
                {
                    MessageBox.Show("Registro agregado con exito");
                    this.Close();
                }
                else
                    MessageBox.Show(oMembresia.getError());

             }
              catch (Exception EX)
              {
                  MessageBox.Show("Ocurrio un error de sistema " + EX.Message);
              }
        }

        private void modifica()
        {

            try
            {
                //validaciones
                if (txtNombre.Text.Trim().Equals("") || txtPrecio.Text.Trim().Equals("") || cboMeses.Text.Equals(""))
                {
                    MessageBox.Show("Nombre, Precio y Meses son obligatorios");
                    return;
                }
                if (!ExpresionesRegulares.RegEX.isDecimal(txtPrecio.Text.Trim()))
                {
                    MessageBox.Show("El precio debe ser un numero valido, no se permiten letras ni caracteres que no sean numeros");
                    return;
                }

                //asignacion de datos
                oMembresia.Nombre = txtNombre.Text.Trim();
                oMembresia.Precio = decimal.Parse(txtPrecio.Text.Trim());
                oMembresia.meses = _diasPeriodo[cboMeses.SelectedIndex];
                oMembresia.horaInicio= dpInicio.Value.TimeOfDay;
                oMembresia.horaFinal = dpFinal.Value.TimeOfDay;
                oMembresia.idUsuarioLog = Utilidades.clsUsuario.idUsuario;
                if (oMembresia.edit(idMembresia))
                {
                    MessageBox.Show("Registro modificado con exito");
                    this.Close();
                }
                else
                    MessageBox.Show(oMembresia.getError());

            }
            catch (Exception EX)
            {
                MessageBox.Show("Ocurrio un error de sistema "+EX.Message);
            }
        }
    }
}
