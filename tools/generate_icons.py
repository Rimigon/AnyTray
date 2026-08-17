#!/usr/bin/env python3
"""Генерирует PNG-логотипы и мультиразмерную ICO для AnyTray из дизайна anytray.svg.

Дизайн (viewBox 64x64):
  - круг радиуса 30 с центром в (32,32), заливка #0894B2;
  - две параллельные стрелки вниз белым цветом (stroke #FFFFFF, width 4),
    левая стрелка по x=20, правая по x=44.
"""

import io
import os
import struct
from PIL import Image, ImageDraw

PROJECT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS_DIR = os.path.join(PROJECT_DIR, 'Assets')

SIZES = [16, 24, 32, 48, 64, 128]
SIZES_FOR_ICO = [(s, s) for s in SIZES] + [(256, 256)]

# SVG-координаты в viewBox 64x64
BACKGROUND = '#1E3A8A'
FOREGROUND = '#FFFFFF'

CIRCLE = {'cx': 32, 'cy': 32, 'r': 30}

# Линии: (x1, y1, x2, y2). Стрелки опущены ниже и немного короче,
# чтобы в круге было больше воздуха сверху и акцент снизу.
LINES = [
    # Левая стрелка
    (20, 16, 20, 42),
    (12, 34, 20, 42),
    (20, 42, 28, 34),
    # Правая стрелка
    (44, 16, 44, 42),
    (36, 34, 44, 42),
    (44, 42, 52, 34),
]


def draw_logo(size: int) -> Image.Image:
    """Рисует логотип заданного размера на прозрачном фоне."""
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    scale = size / 64.0

    cx = CIRCLE['cx'] * scale
    cy = CIRCLE['cy'] * scale
    r = CIRCLE['r'] * scale

    # Круг-фон
    draw.ellipse(
        [(cx - r, cy - r), (cx + r, cy + r)],
        fill=BACKGROUND
    )

    # Для мелких размеров используем упрощённые, но чёткие стрелки;
    # для больших — плавные линии с округлыми концами из SVG-геометрии.
    if size <= 16:
        # В 16x16 две отдельные стрелки нечитаемы — рисуем одну широкую
        # «двойную» стрелку: два близких ствола, сходящиеся в общий наконечник.
        stroke = 1
        shaft_top = 5
        shaft_bottom = size - 4
        left_x = size // 2 - 1
        right_x = size // 2 + 1

        # Стволы
        draw.line([(left_x, shaft_top), (left_x, shaft_bottom)], fill=FOREGROUND, width=stroke)
        draw.line([(right_x, shaft_top), (right_x, shaft_bottom)], fill=FOREGROUND, width=stroke)
        # Общий наконечник
        draw.polygon(
            [(left_x - 2, shaft_bottom - 3),
             (right_x + 2, shaft_bottom - 3),
             (size // 2, shaft_bottom + 3)],
            fill=FOREGROUND
        )
        return img

    if size == 24:
        stroke = 2
        gap = 4
        arrow_width = 5
        shaft_top = 8
        shaft_bottom = size - 7

        left_x = round(size / 2 - gap / 2)
        right_x = round(size / 2 + gap / 2)

        def draw_simple_arrow(x: int):
            draw.line([(x, shaft_top), (x, shaft_bottom)], fill=FOREGROUND, width=stroke)
            draw.polygon(
                [(x - arrow_width, shaft_bottom - arrow_width),
                 (x + arrow_width, shaft_bottom - arrow_width),
                 (x, shaft_bottom + arrow_width)],
                fill=FOREGROUND
            )
            radius = stroke / 2.0
            for px, py in [(x, shaft_top), (x, shaft_bottom)]:
                draw.ellipse(
                    [(px - radius, py - radius), (px + radius, py + radius)],
                    fill=FOREGROUND
                )

        draw_simple_arrow(left_x)
        draw_simple_arrow(right_x)
        return img

    # Большие размеры: масштабируем SVG-линии.
    stroke = max(2, round(4 * scale))

    for x1, y1, x2, y2 in LINES:
        draw.line(
            [(x1 * scale, y1 * scale), (x2 * scale, y2 * scale)],
            fill=FOREGROUND,
            width=stroke,
            joint='curve'
        )
        # Округлые концы линии
        radius = stroke / 2.0
        for x, y in [(x1 * scale, y1 * scale), (x2 * scale, y2 * scale)]:
            draw.ellipse(
                [(x - radius, y - radius), (x + radius, y + radius)],
                fill=FOREGROUND
            )

    return img


def _write_ico(frames, path):
    """Записывает мультиразмерную ICO, где каждый фрейм хранится как PNG."""
    # ICONDIR
    count = len(frames)
    data = struct.pack('<HHH', 0, 1, count)

    # ICONDIRENTRY
    offset = 6 + 16 * count
    png_chunks = []
    for size, img in frames:
        buf = io.BytesIO()
        img.save(buf, format='PNG')
        png_bytes = buf.getvalue()
        png_chunks.append(png_bytes)
        # 0 означает 256 в спецификации ICO
        w = 0 if size >= 256 else size
        h = 0 if size >= 256 else size
        data += struct.pack('<BBBBHHII', w, h, 0, 0, 1, 32, len(png_bytes), offset)
        offset += len(png_bytes)

    # Image data
    for png_bytes in png_chunks:
        data += png_bytes

    with open(path, 'wb') as f:
        f.write(data)


def main():
    os.makedirs(ASSETS_DIR, exist_ok=True)

    # PNG разных размеров
    for size in SIZES:
        img = draw_logo(size)
        path = os.path.join(ASSETS_DIR, f'anytray-{size}x{size}.png')
        img.save(path, 'PNG')
        print(f'  saved {path}')

    # Большой PNG для WPF/документации
    large = draw_logo(256)
    large_path = os.path.join(ASSETS_DIR, 'anytray.png')
    large.save(large_path, 'PNG')
    print(f'  saved {large_path}')

    # Мультиразмерная ICO: собираем из 16…128 + 256 в формате PNG-фреймов.
    # Ручная запись нужна, т.к. Pillow.Image.save для ICO игнорирует
    # дополнительные кадры и оставляет только первый размер.
    ico_frames = [(w, draw_logo(w)) for w, _ in SIZES_FOR_ICO]

    ico_path = os.path.join(ASSETS_DIR, 'anytray.ico')
    _write_ico(ico_frames, ico_path)
    print(f'  saved {ico_path}')

    print(f'\nDone. Generated {len(SIZES)} PNG sizes + anytray.png + anytray.ico.')


if __name__ == '__main__':
    main()
