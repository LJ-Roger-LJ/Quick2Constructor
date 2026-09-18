using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Quick2Constructor
{
    internal static class ConstructorNavigator
    {
        public static async Task NavigateAsync(ConstructorLocation location)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DocumentView opened = await VS.Documents.OpenAsync(location.FilePath);
            if (opened?.TextView == null || opened.TextBuffer == null)
            {
                await VS.StatusBar.ShowMessageAsync(Strings.CannotOpenFile);
                return;
            }

            ITextSnapshot snapshot = opened.TextBuffer.CurrentSnapshot;
            if (location.Line < 0 || location.Line >= snapshot.LineCount)
            {
                await VS.StatusBar.ShowMessageAsync(Strings.LocationStale);
                return;
            }

            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(location.Line);
            int column = Math.Max(0, Math.Min(location.Character, line.Length));
            SnapshotPoint point = line.Start + column;
            int length = Math.Max(1, Math.Min(location.Length, snapshot.Length - point.Position));
            SnapshotSpan span = new SnapshotSpan(point, length);

            opened.TextView.Caret.MoveTo(point);
            opened.TextView.Selection.Select(span, false);
            opened.TextView.ViewScroller.EnsureSpanVisible(span, EnsureSpanVisibleOptions.AlwaysCenter);
            opened.TextView.VisualElement.Focus();
        }
    }
}
