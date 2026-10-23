"""Each guard catches its mistake and accepts the correct pattern."""

import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_rules  # noqa: E402


class GuardTest(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def write(self, rel: str, text: str) -> None:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(textwrap.dedent(text), encoding="utf-8")

    def rules(self) -> list[str]:
        return [f.rule for f in check_rules.run(self.root)]

    def test_clean_project_passes(self):
        self.write("scenes/menu/Menu.cs", """
            public partial class Menu : Control
            {
                public override void _EnterTree() => SettingsService.Instance.LocaleChanged += OnLocale;
                public override void _ExitTree() => SettingsService.Instance.LocaleChanged -= OnLocale;
                public override async void _Ready() { await Task.Yield(); }
            }
            """)
        self.assertEqual(self.rules(), [])

    def test_gdscript_is_rejected(self):
        self.write("core/old.gd", "extends Node\n")
        self.assertIn("G001", self.rules())

    def test_class_must_match_file(self):
        self.write("core/Wrong.cs", "public partial class Right : Node {}\n")
        self.assertIn("G002", self.rules())

    def test_autoload_subscription_without_unsubscribe(self):
        self.write("scenes/Menu.cs", """
            public partial class Menu : Control
            {
                public override void _Ready() { SceneRouter.Instance.RouteFailed += OnFailed; }
            }
            """)
        self.assertIn("G003", self.rules())

    def test_autoload_lambda_subscription(self):
        self.write("scenes/Menu.cs", """
            public partial class Menu : Control
            {
                public override void _Ready() { SettingsService.Instance.LocaleChanged += (l) => Refresh(); }
            }
            """)
        self.assertIn("G003", self.rules())

    def test_node_path_climbing(self):
        self.write("features/Thing.cs", 'public partial class Thing : Node { void F() { GetNode("../Other"); } }\n')
        self.assertIn("G004", self.rules())

    def test_scene_change_outside_router(self):
        self.write("scenes/Menu.cs", "public partial class Menu : Node { void F() { GetTree().ChangeSceneToFile(x); } }\n")
        self.assertIn("G005", self.rules())

    def test_async_void_helper(self):
        self.write("scenes/Menu.cs", "public partial class Menu : Node { async void Save() { } }\n")
        self.assertIn("G006", self.rules())

    def test_scene_path_in_game_code(self):
        self.write("scenes/Menu.cs", 'public partial class Menu : Node { string p = "res://scenes/x.tscn"; }\n')
        self.assertIn("G007", self.rules())

    def test_repository_is_clean(self):
        repo = Path(__file__).resolve().parents[3]
        findings = check_rules.run(repo)
        self.assertEqual([str(f) for f in findings], [])


if __name__ == "__main__":
    unittest.main()
