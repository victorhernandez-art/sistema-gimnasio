using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace Gimnasio.Socios
{
    public partial class frmFoto : Form
    {
        private bool existenDispositivos = false;
        private bool fotografiaHecha = false;
        private FilterInfoCollection dispositivosDeVideo;
        private VideoCaptureDevice fuenteDeVideo = null;
        public PictureBox pbFotoSocio = null;
        public frmFoto()
        {
            InitializeComponent();
            BuscarDispositivos();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void frmFoto_Load(object sender, EventArgs e)
        {
            Utilidades.clsTheme.Apply(this);
            if (existenDispositivos)
            {
        	    fuenteDeVideo = new VideoCaptureDevice(dispositivosDeVideo[0].MonikerString);
        	    fuenteDeVideo.NewFrame += new NewFrameEventHandler(MostrarImagen);
        	    fuenteDeVideo.Start();
        	}
        	else
        	{
        	    MessageBox.Show("No se encuentra ningún dispositivo de vídeo en el sistema", "Información", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                this.Close();
             }
        }

        /*
     *  Identifica los dispositivos disponibles
     */
        private void BuscarDispositivos()
        {
            dispositivosDeVideo = new FilterInfoCollection(FilterCategory.VideoInputDevice); 

            if (dispositivosDeVideo.Count == 0)
                existenDispositivos = false;
            else
                existenDispositivos = true;
        }

        /*
         *  Muestra imagen en el PictureBox
         */
        private void MostrarImagen(object sender, NewFrameEventArgs eventArgs)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            int w = eventArgs.Frame.Width;
            int h = eventArgs.Frame.Height;
            Bitmap imagen = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(imagen))
            {
                g.DrawImage(eventArgs.Frame, 0, 0, w, h);
            }

            // BeginInvoke (no bloqueante) evita el deadlock entre el hilo de cámara y el UI
            pbFoto.BeginInvoke(new Action(delegate()
            {
                if (this.IsDisposed) { imagen.Dispose(); return; }
                Image vieja = pbFoto.Image;
                pbFoto.Image = imagen;
                if (vieja != null) vieja.Dispose();
            }));
        }

        /*
         *  Deja de capturar imágenes, obteniendo la última capturada
         */
        private void Capturar()
        {
            if (fuenteDeVideo != null && fuenteDeVideo.IsRunning)
            {
                // Guardar imagen ANTES de detener la cámara
                if (pbFoto.Image != null)
                    pbFotoSocio.Image = new Bitmap(pbFoto.Image);

                // Señalar stop sin bloquear — el hilo UI queda libre
                fuenteDeVideo.SignalToStop();
                fotografiaHecha = true;
                this.Close();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Capturar();
            fotografiaHecha = true;
        }

        private void frmFoto_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (fuenteDeVideo!=null)
            fuenteDeVideo.Stop();
        }

    }
}
