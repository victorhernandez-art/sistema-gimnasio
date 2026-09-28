# 📌 Guía Universal de Lectores de Huella Dactilar — GymWeb

Esta guía está diseñada para que puedas conectar **cualquier lector de huella del mercado** en tus gimnasios clientes sin complicaciones técnicas.

---

## 🚀 1. ¿Cómo funciona la detección de huellas en GymWeb?

GymWeb ahora cuenta con **Detección Dinámica en Caliente (Hot-Plug)** y **Auto-Diagnóstico de Hardware**:
1. **Conexión en Caliente:** Si el cliente conecta o desconecta el lector USB mientras GymWeb está abierto, el sistema lo detecta automáticamente sin necesidad de reiniciar la computadora ni el sistema.
2. **Semáforo en la pantalla de Socios:**
   - 🟢 **Verde:** Lector reconocido y listo para capturar huellas.
   - 🟡 **Amarillo:** Lector físico detectado en el puerto USB, pero requiere su controlador oficial de Windows (WBF).
   - ⚪ **Gris:** No hay ningún cable USB conectado (el sistema trabaja en modo Clave PIN / Teclado o Tarjeta).
3. **Herramienta de Diagnóstico en 1 Clic:**
   En la carpeta del sistema se incluye el archivo `PROBAR-LECTOR-USB.bat`. Al hacerle doble clic, te dice en 2 segundos qué marca y modelo de lector detecta Windows.

---

## 🛒 2. Los 3 tipos de lectores del mercado y cómo configurarlos

Debido a que cada cliente puede comprar una marca diferente, aquí te detallamos cómo hacer funcionar cada uno:

### Opción A: Mini Lectores USB Windows Hello (Recomendada — 100% Plug & Play)
* **Modelos:** Mini llaves USB para Windows 10/11 (Kensington VeriMark, PQI My Lockey, Benss, Arcanite o lectores genéricos chinos de Amazon / Mercado Libre de $15 a $25 USD).
* **¿Cómo se instala?**
  1. Conéctalo a cualquier puerto USB.
  2. Windows Update descarga e instala el controlador automáticamente en menos de 1 minuto.
  3. GymWeb lo detectará de inmediato con el semáforo en **Verde 🟢**.

---

### Opción B: DigitalPersona U.are.U 4500 / 5100 (El más popular en gimnasios)
* **Modelos:** DigitalPersona 4500, U.are.U 5100 (lector óptico clásico con luz azul).
* **¿Por qué suele fallar con el CD original?**
  El disco de fábrica antiguo instala un driver privativo de desarrollo ("OneTouch SDK"), el cual **no permite** que Windows lo use como lector del sistema.
* **Solución de 1 Clic:**
  1. Descarga o instala el controlador oficial: **DigitalPersona WBF Driver** (*Windows Biometric Framework Driver*).
  2. Al instalar ese driver oficial, Windows convierte el lector en un dispositivo biométrico estándar de Windows.
  3. GymWeb lo detectará de inmediato con el semáforo en **Verde 🟢**.

---

### Opción C: ZKTeco USB (ZK4500, ZK9500, SLK20R)
* **Modelos:** ZK4500, ZK9500 (luz verde).
* **¿Cómo se instala?**
  1. Instala el instalador de ZKTeco que viene con el lector (`zkusb.sys`).
  2. Verifica en el *Administrador de Dispositivos* de Windows que el dispositivo no tenga un triángulo amarillo de advertencia.

---

## 🛡️ 3. Plan de Contingencia Universal: Clave PIN / Código de Barras / Tarjeta RFID

Si un cliente:
- Aún no ha comprado su lector de huella.
- Se le descompuso el cable USB.
- O tiene un socio con las huellas desgastadas (muy común en personas mayores o deportistas con callos):

**El sistema nunca se detiene:**
* Cada socio tiene una **Clave PIN** (por defecto es su número de socio único).
* En la pantalla de registro de asistencia ([`Registro/Index`](file:///c:/xampp/htdocs/Gym%20(2)%20-%20copia/Gym/GymWeb/Views/Registro/Index.cshtml)), el socio puede simplemente teclear su PIN o pasar una tarjeta con código de barras / RFID y el sistema le da acceso inmediatamente.

---

## 🔧 4. Solución rápida cuando un cliente dice "Instalé el driver y no hace nada"

Sigue estos 3 sencillos pasos:

1. **Ejecuta `PROBAR-LECTOR-USB.bat`:**
   Te dirá exactamente si Windows está detectando la corriente y datos del cable USB.
2. **Revisa el Administrador de Dispositivos de Windows (`devmgmt.msc`):**
   - Si aparece la categoría **"Dispositivos biométricos"**, el lector está 100% listo.
   - Si aparece bajo **"Controladores de bus serie universal"** con un signo de admiración amarillo ⚠️, el puerto USB no tiene energía suficiente o el driver requiere reiniciar la PC una vez.
3. **Prueba desde GymWeb:**
   Ve a **Socios** -> Clic en el icono de **Huella** -> Presiona **Escanear**. La barra te confirmará el estado en tiempo real.
