import contextlib
import importlib.machinery
import importlib.util
import io
import shutil
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "scripts" / "cleanup-test-artifacts"
LOADER = importlib.machinery.SourceFileLoader("cleanup_test_artifacts", str(SCRIPT_PATH))
SPEC = importlib.util.spec_from_loader(LOADER.name, LOADER)
assert SPEC is not None
cleanup = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = cleanup
LOADER.exec_module(cleanup)

NOW = "2026-07-23T12:00:00Z"
OLD_RUN = "20260720T110000Z-package-1234567890abcdef"
RECENT_RUN = "20260722T130000Z-runtime-smoke-1234567890abcdef"


class CleanupTestArtifactsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.main = self.make_repository("main")
        self.beta = self.make_repository("beta")

    def tearDown(self):
        self.temp.cleanup()

    def make_repository(self, name):
        repository = self.root / name
        (repository / "artifacts" / "tests").mkdir(parents=True)
        return repository

    def make_run(self, repository, run_id):
        run = repository / "artifacts" / "tests" / run_id
        run.mkdir()
        (run / "evidence.log").write_bytes(b"evidence")
        return run

    def execute(self, *arguments):
        args = cleanup.parse_args([*arguments, "--now", NOW])
        stdout = io.StringIO()
        stderr = io.StringIO()
        exit_code = cleanup.run_cleanup(
            args,
            roots={"main": self.main, "beta": self.beta},
            stdout=stdout,
            stderr=stderr,
        )
        return exit_code, stdout.getvalue(), stderr.getvalue()

    def test_dry_run_preserves_old_run_and_reports_it(self):
        run = self.make_run(self.main, OLD_RUN)

        exit_code, stdout, _ = self.execute("--target", "main")

        self.assertEqual(0, exit_code)
        self.assertTrue(run.exists())
        self.assertIn("would delete", stdout)
        self.assertIn("eligible=1", stdout)
        self.assertIn("deleted=0", stdout)

    def test_delete_removes_only_old_runs(self):
        old = self.make_run(self.main, OLD_RUN)
        recent = self.make_run(self.main, RECENT_RUN)

        exit_code, stdout, _ = self.execute("--target", "main", "--delete")

        self.assertEqual(0, exit_code)
        self.assertFalse(old.exists())
        self.assertTrue(recent.exists())
        self.assertIn("recent=1", stdout)
        self.assertIn("deleted=1", stdout)
        self.assertIn("reclaimed_bytes=", stdout)

    def test_symlink_and_invalid_run_name_cannot_escape_artifacts_root(self):
        outside = self.root / "outside"
        outside.mkdir()
        (outside / "sentinel").write_text("keep", encoding="utf-8")
        unsafe_link = self.main / "artifacts" / "tests" / OLD_RUN
        unsafe_link.symlink_to(outside, target_is_directory=True)
        invalid = self.make_run(self.main, "not-a-run-id")

        exit_code, stdout, stderr = self.execute("--target", "main", "--delete")

        self.assertEqual(0, exit_code)
        self.assertTrue(outside.exists())
        self.assertTrue((outside / "sentinel").exists())
        self.assertTrue(invalid.exists())
        self.assertIn("unsafe=2", stdout)
        self.assertIn("unsafe=2", stdout)
        self.assertIn("unsafe symlink", stderr)

    def test_all_targets_handles_missing_beta_without_blocking_main(self):
        run = self.make_run(self.main, OLD_RUN)
        shutil.rmtree(self.beta)

        exit_code, stdout, _ = self.execute("--target", "all")

        self.assertEqual(0, exit_code)
        self.assertTrue(run.exists())
        self.assertIn("[beta] skipped: repository root is absent", stdout)
        self.assertIn("[main]", stdout)

    def test_all_targets_uses_same_cleanup_logic_for_main_and_beta(self):
        main_run = self.make_run(self.main, OLD_RUN)
        beta_run = self.make_run(self.beta, OLD_RUN)

        exit_code, stdout, _ = self.execute("--target", "all", "--delete")

        self.assertEqual(0, exit_code)
        self.assertFalse(main_run.exists())
        self.assertFalse(beta_run.exists())
        self.assertIn("[main] delete:", stdout)
        self.assertIn("[beta] delete:", stdout)


if __name__ == "__main__":
    unittest.main()
