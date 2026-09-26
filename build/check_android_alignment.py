"""Fail CI if any 64-bit native library or stored ZIP entry is not 16-KB aligned.

Usage: python3 build/check_android_alignment.py path/to/app.apk
See https://developer.android.com/guide/practices/page-sizes
"""
import struct
import sys
import zipfile

PAGE_SIZE = 16384


def check_elf(data):
    if data[:6] != b'\x7fELF\x02\x01':
        raise ValueError('Expected a little-endian 64-bit ELF library')
    offset = struct.unpack_from('<Q', data, 32)[0]
    entry_size, count = struct.unpack_from('<HH', data, 54)
    if entry_size < 56 or offset + entry_size * count > len(data):
        raise ValueError('Invalid ELF program header table')
    loads = 0
    headers = []
    for i in range(count):
        kind, flags, file_offset, address, _, _, size, alignment = struct.unpack_from('<IIQQQQQQ', data, offset + i * entry_size)
        headers.append((kind, flags, address, size))
        if kind == 1:  # PT_LOAD
            loads += 1
            if alignment < PAGE_SIZE or alignment & (alignment - 1) or (address - file_offset) % PAGE_SIZE:
                raise ValueError('LOAD segment is not 16-KB aligned')
    # RELRO may end at the end of its LOAD segment; rounding then protects only
    # padding. Reject a rounded RELRO page that also contains writable data.
    for kind, _, address, size in headers:
        if kind != 0x6474e552:
            continue
        end = address + size
        rounded_end = (end + PAGE_SIZE - 1) // PAGE_SIZE * PAGE_SIZE
        for load_kind, flags, load_address, load_size in headers:
            if load_kind == 1 and flags & 2 and max(end, load_address) < min(rounded_end, load_address + load_size):
                raise ValueError('RELRO end shares a 16-KB page with writable data')
    if not loads:
        raise ValueError('ELF has no LOAD segments')


def check_apk(path):
    count = 0
    errors = []
    architectures = set()
    with zipfile.ZipFile(path) as apk, open(path, 'rb') as raw:
        for item in apk.infolist():
            parts = item.filename.split('/')
            if len(parts) != 3 or parts[0] != 'lib' or parts[1] not in ('arm64-v8a', 'x86_64') or not parts[2].endswith('.so'):
                continue
            count += 1
            architectures.add(parts[1])
            try:
                check_elf(apk.read(item))
                if item.compress_type == zipfile.ZIP_STORED:
                    raw.seek(item.header_offset)
                    header = raw.read(30)
                    name_size, extra_size = struct.unpack_from('<HH', header, 26)
                    if (item.header_offset + 30 + name_size + extra_size) % PAGE_SIZE:
                        raise ValueError('Uncompressed ZIP library is not 16-KB aligned')
            except (ValueError, struct.error) as error:
                errors.append(f'{item.filename}: {error}')
    if not count:
        errors.append('No 64-bit native libraries found')
    if errors:
        raise ValueError('\n'.join(errors))
    print(f'16-KB alignment verified: {count} native libraries ({", ".join(sorted(architectures))})')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit('Usage: check_android_alignment.py app.apk')
    try:
        check_apk(sys.argv[1])
    except (ValueError, OSError, zipfile.BadZipFile) as error:
        sys.exit(str(error))
