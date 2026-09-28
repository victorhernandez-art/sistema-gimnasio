import os
from PIL import Image, ImageDraw

def create_dumbbell_icon(size=512):
    # Escala para antialiasing óptimo
    scale = 4
    canvas_size = size * scale
    img = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Colores
    CYAN = (0, 242, 255, 255)       # #00f2ff cian neón
    BG_DARK = (15, 20, 28, 255)     # #0f141c fondo oscuro premium
    BORDER_CYAN = (0, 242, 255, 75) # Borde cian sutil

    # Squircle redondeado
    pad = int(16 * scale)
    corner_radius = int(105 * scale)
    draw.rounded_rectangle(
        [pad, pad, canvas_size - pad, canvas_size - pad],
        radius=corner_radius,
        fill=BG_DARK,
        outline=BORDER_CYAN,
        width=int(5 * scale)
    )

    cx = canvas_size / 2
    cy = canvas_size / 2

    # Proporciones precisas de la pesa
    bar_w = int(115 * scale)
    bar_h = int(36 * scale)
    bar_radius = int(8 * scale)

    # Mango central
    draw.rounded_rectangle(
        [cx - bar_w / 2, cy - bar_h / 2, cx + bar_w / 2, cy + bar_h / 2],
        radius=bar_radius,
        fill=CYAN
    )

    # Placas interiores
    inner_plate_w = int(40 * scale)
    inner_plate_h = int(245 * scale)
    inner_plate_r = int(18 * scale)

    # Placas exteriores
    outer_plate_w = int(36 * scale)
    outer_plate_h = int(185 * scale)
    outer_plate_r = int(16 * scale)

    # Extremos exteriores
    end_cap_w = int(24 * scale)
    end_cap_h = int(85 * scale)
    end_cap_r = int(10 * scale)

    # Lado izquierdo
    d_inner_x = cx - bar_w / 2 - inner_plate_w
    d_outer_x = d_inner_x - outer_plate_w - int(8 * scale)
    d_end_x = d_outer_x - end_cap_w - int(4 * scale)

    draw.rounded_rectangle(
        [d_inner_x, cy - inner_plate_h / 2, d_inner_x + inner_plate_w, cy + inner_plate_h / 2],
        radius=inner_plate_r,
        fill=CYAN
    )
    draw.rounded_rectangle(
        [d_outer_x, cy - outer_plate_h / 2, d_outer_x + outer_plate_w, cy + outer_plate_h / 2],
        radius=outer_plate_r,
        fill=CYAN
    )
    draw.rounded_rectangle(
        [d_end_x, cy - end_cap_h / 2, d_end_x + end_cap_w, cy + end_cap_h / 2],
        radius=end_cap_r,
        fill=CYAN
    )

    # Lado derecho
    d_inner_rx = cx + bar_w / 2
    d_outer_rx = d_inner_rx + inner_plate_w + int(8 * scale)
    d_end_rx = d_outer_rx + outer_plate_w + int(4 * scale)

    draw.rounded_rectangle(
        [d_inner_rx, cy - inner_plate_h / 2, d_inner_rx + inner_plate_w, cy + inner_plate_h / 2],
        radius=inner_plate_r,
        fill=CYAN
    )
    draw.rounded_rectangle(
        [d_outer_rx, cy - outer_plate_h / 2, d_outer_rx + outer_plate_w, cy + outer_plate_h / 2],
        radius=outer_plate_r,
        fill=CYAN
    )
    draw.rounded_rectangle(
        [d_end_rx, cy - end_cap_h / 2, d_end_rx + end_cap_w, cy + end_cap_h / 2],
        radius=end_cap_r,
        fill=CYAN
    )

    return img.resize((size, size), Image.Resampling.LANCZOS)

def generate_all():
    base_dir = os.path.dirname(os.path.abspath(__file__))
    wwwroot_img = os.path.join(base_dir, "GymWeb", "wwwroot", "img")
    os.makedirs(wwwroot_img, exist_ok=True)

    img512 = create_dumbbell_icon(512)
    img192 = img512.resize((192, 192), Image.Resampling.LANCZOS)
    img180 = img512.resize((180, 180), Image.Resampling.LANCZOS)
    img48  = img512.resize((48, 48), Image.Resampling.LANCZOS)
    img32  = img512.resize((32, 32), Image.Resampling.LANCZOS)
    img16  = img512.resize((16, 16), Image.Resampling.LANCZOS)

    # 1. Guardar PNGs en wwwroot/img
    img512.save(os.path.join(wwwroot_img, "icon-512.png"))
    img192.save(os.path.join(wwwroot_img, "icon-192.png"))
    img180.save(os.path.join(wwwroot_img, "apple-touch-icon.png"))
    img32.save(os.path.join(wwwroot_img, "favicon-32x32.png"))
    img16.save(os.path.join(wwwroot_img, "favicon-16x16.png"))
    print("PNGs guardados en GymWeb/wwwroot/img/")

    # 2. Guardar favicon.ico multi-resolución en GymWeb/wwwroot/
    ico_path = os.path.join(base_dir, "GymWeb", "wwwroot", "favicon.ico")
    img512.save(
        ico_path,
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    )
    print(f"ICO guardado en: {ico_path}")

    # 3. Guardar app.ico en raíz de Gym y en Publish
    app_ico_root = os.path.join(base_dir, "app.ico")
    img512.save(
        app_ico_root,
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    )
    print(f"app.ico guardado en: {app_ico_root}")

    publish_dir = os.path.join(base_dir, "Publish")
    if os.path.exists(publish_dir):
        app_ico_pub = os.path.join(publish_dir, "app.ico")
        img512.save(
            app_ico_pub,
            format="ICO",
            sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
        )
        print(f"app.ico guardado en: {app_ico_pub}")

    # Limpiar archivo temporal si existe
    temp_file = os.path.join(base_dir, "temp_test_dumbbell.png")
    if os.path.exists(temp_file):
        os.remove(temp_file)

if __name__ == "__main__":
    generate_all()
