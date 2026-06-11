import struct, sys

sources = [
    ('Assets/anytray-16x16.ico', 16, 16),
    ('Assets/anytray-32x32.ico', 32, 32),
]

output = 'Assets/anytray.ico'

entries = []
image_data = []

for path, w, h in sources:
    with open(path, 'rb') as f:
        data = f.read()
    # Skip ICONDIR (6 bytes) + ICONDIRENTRY (16 bytes) = 22 bytes
    img = data[22:]
    entries.append((w, h, len(img)))
    image_data.append(img)

count = len(entries)
header_size = 6 + 16 * count

with open(output, 'wb') as f:
    # ICONDIR
    f.write(struct.pack('<HHH', 0, 1, count))
    
    # ICONDIRENTRY
    offset = header_size
    for w, h, size in entries:
        f.write(struct.pack('<BBBBHHII', w, h, 0, 0, 1, 32, size, offset))
        offset += size
    
    # Image data
    for img in image_data:
        f.write(img)

print(f'Created {output} with {count} sizes:')
for e in entries:
    print(f'  {e[0]}x{e[1]} ({e[2]} bytes)')
