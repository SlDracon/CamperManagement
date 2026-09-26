import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from release_version import parse_tag


class ReleaseVersionTests(unittest.TestCase):
    def test_versions_and_android_update_order(self):
        self.assertEqual(("1.2.3", 1_002_003), parse_tag("v1.2.3"))
        tags = ["v0.0.1", "v0.999.999", "v1.0.0", "v1.0.999", "v1.1.0", "v2099.999.999"]
        codes = [parse_tag(tag)[1] for tag in tags]
        self.assertEqual(sorted(set(codes)), codes)
        self.assertLessEqual(codes[-1], 2_100_000_000)

    def test_invalid_unsafe_and_out_of_range_tags(self):
        for tag in ["main", "1.0.0", "v1.0", "v01.0.0", "v1.0.0-rc.1", "v1.0.0+build", "v0.0.0", "v2100.0.0", "v1.1000.0", "v1.0.1000", "v1.0.0\n", "v1.0.0/../../x", "v1.0.0;echo x"]:
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                parse_tag(tag)


if __name__ == "__main__":
    unittest.main()
