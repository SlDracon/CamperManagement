import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('alignment', Path(__file__).parents[1] / 'check_android_alignment.py')
alignment = importlib.util.module_from_spec(spec)
spec.loader.exec_module(alignment)


def elf(page=16384, relro_end=32768):
    data = bytearray(64 + 56 * 2)
    data[:6] = b'\x7fELF\x02\x01'
    struct.pack_into('<Q', data, 32, 64)
    struct.pack_into('<HH', data, 54, 56, 2)
    struct.pack_into('<IIQQQQQQ', data, 64, 1, 6, 0, 0, 0, 32768, 32768, page)
    struct.pack_into('<IIQQQQQQ', data, 120, 0x6474e552, 4, 8192, 16384, 0, 8192, relro_end - 16384, 1)
    return data


class AlignmentTests(unittest.TestCase):
    def test_valid_elf(self):
        alignment.check_elf(elf())

    def test_old_4k_library_rejected(self):
        with self.assertRaisesRegex(ValueError, 'LOAD'):
            alignment.check_elf(elf(page=4096))

    def test_relro_rejected(self):
        with self.assertRaisesRegex(ValueError, 'RELRO'):
            alignment.check_elf(elf(relro_end=20480))

    def test_empty_apk_rejected(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / 'empty.apk'
            with zipfile.ZipFile(path, 'w'):
                pass
            with self.assertRaisesRegex(ValueError, 'No 64-bit'):
                alignment.check_apk(path)

    def test_unaligned_zip_rejected(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / 'app.apk'
            with zipfile.ZipFile(path, 'w') as apk:
                apk.writestr('lib/arm64-v8a/libtest.so', elf())
            with self.assertRaisesRegex(ValueError, 'ZIP'):
                alignment.check_apk(path)
