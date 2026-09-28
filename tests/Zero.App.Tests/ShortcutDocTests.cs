using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    /// <summary>
    /// The README's shortcut table drifted away from the code once already. These read both and
    /// compare them, so the next shortcut that moves takes the documentation with it.
    /// </summary>
    public class ShortcutDocTests
    {
        private static string Readme => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "README.md"));

        private static string HandleShortcutBody()
        {
            string source = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "src", "Zero.App", "MainWindow.axaml.cs"));
            int start = source.IndexOf("private bool HandleShortcut(", StringComparison.Ordinal);
            Assert.True(start >= 0, "HandleShortcut has been renamed; this test needs updating with it.");
            int end = source.IndexOf("\n        }", start, StringComparison.Ordinal);
            Assert.True(end > start, "could not find the end of HandleShortcut");
            return source.Substring(start, end - start);
        }

        /// <summary>The rows of the one table under "The following key combinations".</summary>
        private static IEnumerable<string> DocumentedRows()
        {
            string readme = Readme;
            int start = readme.IndexOf("The following key combinations", StringComparison.Ordinal);
            Assert.True(start >= 0, "the README no longer introduces a shortcut table");
            int table = readme.IndexOf("----------------|", start, StringComparison.Ordinal);
            Assert.True(table > start, "the shortcut table is missing its header rule");
            foreach (string line in readme.Substring(table).Split('\n').Skip(1))
            {
                if (!line.Contains('|')) yield break;
                yield return line.Split('|')[0].Trim();
            }
        }

        /// <summary>Every row of the shortcut table, whole, prose excluded.</summary>
        private static IEnumerable<string> ReadmeTableLines()
        {
            string readme = Readme;
            int start = readme.IndexOf("The following key combinations", StringComparison.Ordinal);
            int table = readme.IndexOf("----------------|", start, StringComparison.Ordinal);
            foreach (string line in readme.Substring(table).Split('\n').Skip(1))
            {
                if (!line.Contains('|')) yield break;
                yield return line;
            }
        }

        /// <summary>"Shift+F9" -> F9, "Esc" -> Escape, "Cmd+," -> OemComma.</summary>
        private static string KeyName(string label)
        {
            string key = label.Split('+').Last().Trim();
            return key switch { "Esc" => "Escape", "," => "OemComma", _ => key };
        }

        private static HashSet<string> CodedKeys(bool commandModified)
        {
            string body = HandleShortcutBody();
            int cmd = body.IndexOf("if (cmd)", StringComparison.Ordinal);
            Assert.True(cmd > 0, "HandleShortcut no longer splits plain keys from Cmd/Ctrl ones");
            string half = commandModified ? body.Substring(cmd) : body.Substring(0, cmd);
            return Regex.Matches(half, @"case Key\.(\w+):").Select(m => m.Groups[1].Value).ToHashSet();
        }

        [Fact]
        public void Every_plain_shortcut_key_the_window_handles_is_in_the_readme()
        {
            HashSet<string> documented = DocumentedRows()
                .Where(r => !r.StartsWith("Cmd", StringComparison.Ordinal))
                .Select(KeyName).ToHashSet();
            // Escape is handled before the switch, so it is not one of the cases.
            HashSet<string> coded = CodedKeys(false);
            coded.Add("Escape");
            Assert.Equal(coded.OrderBy(k => k), documented.OrderBy(k => k));
        }

        [Fact]
        public void Every_command_modified_shortcut_the_window_handles_is_in_the_readme()
        {
            HashSet<string> documented = DocumentedRows()
                .Where(r => r.StartsWith("Cmd", StringComparison.Ordinal))
                .Select(KeyName).ToHashSet();
            Assert.Equal(CodedKeys(true).OrderBy(k => k), documented.OrderBy(k => k));
        }

        [Fact]
        public void Key_forwarding_has_no_platform_branch_left_in_it()
        {
            string source = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "src", "Zero.App", "MainWindow.axaml.cs"));
            int start = source.IndexOf("private void OnWindowKeyDown(", StringComparison.Ordinal);
            Assert.True(start >= 0, "OnWindowKeyDown has been renamed; this test needs updating with it.");
            int end = source.IndexOf("\n        }", start, StringComparison.Ordinal);
            string body = source.Substring(start, end - start);
            // A platform test here is what used to swallow Ctrl+P on Windows and Linux, taking
            // Symbol Shift with it. Which keys are shortcuts is HandleShortcut's business now.
            Assert.DoesNotContain("IsMac", body, StringComparison.Ordinal);
            Assert.DoesNotContain("CommandModifier", body, StringComparison.Ordinal);
        }

        [Fact]
        public void The_readme_does_not_promise_features_this_build_dropped()
        {
            string table = string.Join("\n", ReadmeTableLines());
            foreach (string gone in new[] { "RZX Recording", "RZX Bookmark", "Step Over", "Step In", "Step Out", "Breakpoints", "Tape Browser" })
                Assert.DoesNotContain(gone, table, StringComparison.OrdinalIgnoreCase);
        }

    }
}
