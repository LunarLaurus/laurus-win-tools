using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WindowsTrayCore;

namespace ClipTray;

internal sealed class PickerForm : Form
{
    private const int Gap = 6;
    private const int SearchHeight = 28;
    private const int CloseButtonWidth = 28;
    private const int RowHeight = 36;
    private const int ThumbW = 32;
    private const int ThumbH = 24;
    private const int MaxTextPreview = 80;

    private readonly ClipboardHistory _history;
    private readonly AppSettings _settings;
    private readonly ImageStore _images;

    private readonly TextBox _searchBox;
    private readonly Button _closeButton;
    private readonly ListBox _listBox;

    private IntPtr _targetWindow;
    private HistoryItem? _pendingReveal;

    private readonly Dictionary<string, Image> _thumbCache = new();

    private readonly EventHandler _themeChangedHandler;

    public PickerForm(ClipboardHistory history, AppSettings settings, ImageStore images)
    {
        _history = history;
        _settings = settings;
        _images = images;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;

        int w = settings.PickerWidth;
        int h = settings.PickerHeight;
        ClientSize = new Size(w, h);

        _searchBox = new TextBox
        {
            Left = Gap,
            Top = Gap,
            Width = w - Gap * 2 - CloseButtonWidth - Gap,
            Height = SearchHeight,
            Font = new Font("Segoe UI", 9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Search (Esc to close)...",
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _pendingReveal = null;
            RebuildList();
        };
        _searchBox.KeyDown += OnKeyDown;

        _closeButton = new Button
        {
            Left = w - Gap - CloseButtonWidth,
            Top = Gap,
            Width = CloseButtonWidth,
            Height = SearchHeight,
            Text = "✕",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
        };
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Click += (_, _) => Close();

        _listBox = new ListBox
        {
            Left = Gap,
            Top = Gap + SearchHeight + Gap,
            Width = w - Gap * 2,
            Height = h - (Gap + SearchHeight + Gap) - Gap,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = RowHeight,
            BorderStyle = BorderStyle.None,
            HorizontalScrollbar = false,
            Font = new Font("Segoe UI", 9.5f),
            SelectionMode = SelectionMode.One,
        };
        _listBox.DrawItem += OnDrawItem;
        _listBox.KeyDown += OnKeyDown;
        _listBox.MouseClick += OnListMouseClick;
        _listBox.MouseUp += OnListMouseUp;

        Controls.Add(_searchBox);
        Controls.Add(_closeButton);
        Controls.Add(_listBox);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Opening += OnContextMenuOpening;

        var pinItem = new ToolStripMenuItem("Pin / Unpin");
        pinItem.Click += (_, _) => TogglePinOnSelected();

        var revealItem = new ToolStripMenuItem("Reveal and paste");
        revealItem.Click += (_, _) => RevealAndPasteSelected();

        var copyItem = new ToolStripMenuItem("Copy without paste");
        copyItem.Click += (_, _) => CopyWithoutPaste();

        var deleteItem = new ToolStripMenuItem("Delete");
        deleteItem.Click += (_, _) => DeleteSelected();

        var markNotSensitiveItem = new ToolStripMenuItem("Mark not sensitive");
        markNotSensitiveItem.Click += (_, _) => MarkNotSensitiveSelected();

        contextMenu.Items.Add(pinItem);
        contextMenu.Items.Add(revealItem);
        contextMenu.Items.Add(copyItem);
        contextMenu.Items.Add(deleteItem);
        contextMenu.Items.Add(markNotSensitiveItem);

        _listBox.ContextMenuStrip = contextMenu;

        _themeChangedHandler = (_, _) => ThemeApplier.ApplyTo(this, TrayTheme.Current);
        TrayTheme.Current.Changed += _themeChangedHandler;

        Disposed += (_, _) =>
        {
            TrayTheme.Current.Changed -= _themeChangedHandler;
            DisposeThumbCache();
        };

        ThemeApplier.ApplyTo(this, TrayTheme.Current);
    }

    public void ShowAtCursor(IntPtr targetWindow)
    {
        _targetWindow = targetWindow;
        _pendingReveal = null;
        _searchBox.Text = string.Empty;

        RebuildList();

        int w = ClientSize.Width;
        int h = ClientSize.Height;

        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor).WorkingArea;

        int x = cursor.X;
        int y = cursor.Y;

        if (x + w > screen.Right)  x = screen.Right - w;
        if (y + h > screen.Bottom) y = screen.Bottom - h;
        if (x < screen.Left)       x = screen.Left;
        if (y < screen.Top)        y = screen.Top;

        Location = new Point(x, y);

        Show();
        // A borderless TopMost form does not reliably activate from Show()
        // alone; the resulting "ghost" state means Deactivate never fires
        // when the user clicks outside. Force activation so OnDeactivate
        // becomes a real dismiss path.
        Activate();
        BringToFront();
        _searchBox.Focus();
    }

    private void RebuildList()
    {
        var filter = _searchBox.Text;
        var items = FilterItems(filter);

        _listBox.BeginUpdate();
        _listBox.Items.Clear();

        var pinned = items.Where(i => i.IsPinned)
                         .OrderByDescending(i => i.CapturedUtc)
                         .ToList();
        var recent = items.Where(i => !i.IsPinned)
                          .OrderByDescending(i => i.CapturedUtc)
                          .ToList();

        foreach (var item in pinned)
            _listBox.Items.Add(item);

        if (pinned.Count > 0 && recent.Count > 0)
        {
            var sep = new SeparatorRow();
            _listBox.Items.Add(sep);
        }

        foreach (var item in recent)
            _listBox.Items.Add(item);

        if (_listBox.Items.Count > 0)
            _listBox.SelectedIndex = 0;

        _listBox.EndUpdate();
    }

    private IEnumerable<HistoryItem> FilterItems(string filter)
    {
        var all = _history.Items;

        if (string.IsNullOrEmpty(filter))
            return all;

        if (filter.StartsWith('/'))
        {
            var pattern = filter[1..];
            if (string.IsNullOrEmpty(pattern))
                return Array.Empty<HistoryItem>();

            Regex? rx;
            try
            {
                rx = new Regex(pattern,
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                return Array.Empty<HistoryItem>();
            }

            var matched = new List<HistoryItem>();
            foreach (var item in all)
            {
                if (item.Kind == HistoryKind.Image) continue;
                if (item.Text is null) continue;
                bool hit;
                try   { hit = rx.IsMatch(item.Text); }
                catch (RegexMatchTimeoutException) { return Array.Empty<HistoryItem>(); }
                if (hit) matched.Add(item);
            }
            return matched;
        }

        return all.Where(item =>
        {
            if (item.Kind == HistoryKind.Image) return false;
            return item.Text?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        });
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_pendingReveal is not null)
        {
            if (e.KeyCode == Keys.Y)
            {
                e.Handled = true;
                var item = _pendingReveal;
                _pendingReveal = null;
                ExecutePaste(item);
            }
            else if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.N)
            {
                e.Handled = true;
                _pendingReveal = null;
                _listBox.Invalidate();
                if (e.KeyCode == Keys.Escape) Close();
            }
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                e.Handled = true;
                Close();
                break;

            case Keys.Up:
                e.Handled = true;
                NavigateList(-1);
                break;

            case Keys.Down:
                e.Handled = true;
                NavigateList(+1);
                break;

            case Keys.Enter:
                e.Handled = true;
                e.SuppressKeyPress = true;
                ActivateSelected();
                break;

            case Keys.P when e.Control:
                e.Handled = true;
                TogglePinOnSelected();
                break;

            case Keys.Delete:
                e.Handled = true;
                DeleteSelected();
                break;

            case Keys.R when e.Control:
                e.Handled = true;
                MarkNotSensitiveSelected();
                break;
        }
    }

    private void NavigateList(int delta)
    {
        if (_listBox.Items.Count == 0) return;
        int next = _listBox.SelectedIndex + delta;
        if (next < 0) next = 0;
        if (next >= _listBox.Items.Count) next = _listBox.Items.Count - 1;

        if (_listBox.Items[next] is SeparatorRow)
            next += delta;

        if (next < 0) next = 0;
        if (next >= _listBox.Items.Count) next = _listBox.Items.Count - 1;

        _listBox.SelectedIndex = next;
    }

    private void ActivateSelected()
    {
        var item = SelectedHistoryItem();
        if (item is null) return;

        if (item.IsSensitive && _pendingReveal is null)
        {
            _pendingReveal = item;
            _listBox.Invalidate();
            return;
        }

        ExecutePaste(item);
    }

    private void ExecutePaste(HistoryItem item)
    {
        if (item.Kind == HistoryKind.Text && item.Text is not null)
            ClipboardWriter.SetText(item.Text);
        else if (item.Kind == HistoryKind.Image && item.ImagePath is not null)
            ClipboardWriter.SetImage(item.ImagePath);

        bool ok = ClipboardWriter.SendCtrlV(_targetWindow);
        if (!ok)
        {
            MessageBox.Show(
                "Couldn't auto-paste. The item is on the clipboard; press Ctrl+V in your target window.",
                "ClipTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Close();
    }

    private void CopyWithoutPaste()
    {
        var item = SelectedHistoryItem();
        if (item is null) return;

        if (item.Kind == HistoryKind.Text && item.Text is not null)
            ClipboardWriter.SetText(item.Text);
        else if (item.Kind == HistoryKind.Image && item.ImagePath is not null)
            ClipboardWriter.SetImage(item.ImagePath);
    }

    private void RevealAndPasteSelected()
    {
        var item = SelectedHistoryItem();
        if (item is null || !item.IsSensitive) return;
        ExecutePaste(item);
    }

    private void TogglePinOnSelected()
    {
        var item = SelectedHistoryItem();
        if (item is null) return;
        _history.SetPinned(item.Hash, !item.IsPinned);
        RebuildList();
    }

    private void DeleteSelected()
    {
        var item = SelectedHistoryItem();
        if (item is null) return;
        _history.Delete(item.Hash);
        _pendingReveal = null;
        RebuildList();
    }

    private void MarkNotSensitiveSelected()
    {
        var item = SelectedHistoryItem();
        if (item is null || !item.IsSensitive) return;
        var idx = _history.Items.ToList().FindIndex(i => i.Hash == item.Hash);
        if (idx < 0) return;
        _history.Delete(item.Hash);
        var updated = item with { IsSensitive = false };
        _history.Add(updated);
        _pendingReveal = null;
        RebuildList();
    }

    private HistoryItem? SelectedHistoryItem()
    {
        var sel = _listBox.SelectedItem;
        return sel as HistoryItem;
    }

    private void OnListMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        int idx = _listBox.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _listBox.Items.Count) return;
        if (_listBox.Items[idx] is SeparatorRow) return;
        _listBox.SelectedIndex = idx;
        ActivateSelected();
    }

    private void OnListMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        int idx = _listBox.IndexFromPoint(e.Location);
        if (idx >= 0 && idx < _listBox.Items.Count && _listBox.Items[idx] is not SeparatorRow)
            _listBox.SelectedIndex = idx;
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var item = SelectedHistoryItem();
        if (item is null)
        {
            e.Cancel = true;
            return;
        }

        var cm = _listBox.ContextMenuStrip!;
        var pinItem = (ToolStripMenuItem)cm.Items[0];
        var revealItem = (ToolStripMenuItem)cm.Items[1];
        var markNotSensitiveItem = (ToolStripMenuItem)cm.Items[4];

        pinItem.Text = item.IsPinned ? "Unpin" : "Pin";
        revealItem.Visible = item.IsSensitive;
        markNotSensitiveItem.Visible = item.IsSensitive;
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _listBox.Items.Count) return;

        var obj = _listBox.Items[e.Index];
        var theme = TrayTheme.Current;

        if (obj is SeparatorRow)
        {
            using var bg = new SolidBrush(theme.SurfaceAlt);
            e.Graphics.FillRectangle(bg, e.Bounds);
            int midY = e.Bounds.Y + e.Bounds.Height / 2;
            using var pen = new Pen(theme.SurfaceStroke);
            e.Graphics.DrawLine(pen, e.Bounds.Left + 4, midY, e.Bounds.Right - 4, midY);
            return;
        }

        if (obj is not HistoryItem item) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool isPendingReveal = ReferenceEquals(item, _pendingReveal);

        Color bgColor = selected ? theme.AccentSubtle : theme.SurfaceAlt;
        Color fgColor = selected ? theme.AccentOn : theme.Foreground;

        using var bgBrush = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        var r = e.Bounds;
        int x = r.Left + 4;
        int textTop = r.Top + (r.Height - e.Font!.Height) / 2;

        if (item.Kind == HistoryKind.Image)
        {
            var thumb = LoadThumb(item);
            if (thumb is not null)
            {
                var destRect = new Rectangle(x, r.Top + (r.Height - ThumbH) / 2, ThumbW, ThumbH);
                e.Graphics.DrawImage(thumb, destRect);
                x += ThumbW + 4;
            }

            string imgLabel = thumb is not null
                ? $"Image {thumb.Width}x{thumb.Height}"
                : "Image (unavailable)";

            using var fgBrush = new SolidBrush(fgColor);
            e.Graphics.DrawString(imgLabel, e.Font, fgBrush, x, textTop);
        }
        else
        {
            string displayText;
            if (isPendingReveal)
            {
                displayText = "Reveal and paste? [Y/N]";
                fgColor = theme.Warning;
            }
            else if (item.IsSensitive)
            {
                displayText = "••••••••";
            }
            else
            {
                var txt = item.Text ?? string.Empty;
                displayText = txt.Length > MaxTextPreview
                    ? txt[..MaxTextPreview] + "..."
                    : txt;
                displayText = displayText.Replace('\n', ' ').Replace('\r', ' ');
            }

            using var fgBrush = new SolidBrush(fgColor);
            e.Graphics.DrawString(displayText, e.Font, fgBrush, x, textTop);
        }

        string age = FormatAge(item.CapturedUtc);
        using var dimBrush = new SolidBrush(theme.ForegroundDim);
        var ageSize = e.Graphics.MeasureString(age, e.Font);

        int ageRight = r.Right - 4;
        if (item.IsPinned)
            ageRight -= 14;

        e.Graphics.DrawString(age, e.Font, dimBrush,
            ageRight - ageSize.Width, textTop);

        if (item.IsPinned)
        {
            using var pinBrush = new SolidBrush(theme.Accent);
            e.Graphics.FillEllipse(pinBrush,
                r.Right - 12, r.Top + (r.Height - 8) / 2, 8, 8);
        }
    }

    private Image? LoadThumb(HistoryItem item)
    {
        if (item.ImagePath is null) return null;
        if (_thumbCache.TryGetValue(item.Hash, out var cached)) return cached;

        try
        {
            var img = Image.FromFile(item.ImagePath);
            _thumbCache[item.Hash] = img;
            return img;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatAge(DateTime capturedUtc)
    {
        var diff = DateTime.UtcNow - capturedUtc;
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h";
        return $"{(int)diff.TotalDays}d";
    }

    private void DisposeThumbCache()
    {
        foreach (var img in _thumbCache.Values)
            img.Dispose();
        _thumbCache.Clear();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(TrayTheme.Current.Accent, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Standard popup-picker behaviour: dismiss on focus loss so users
        // who don't know Esc closes it aren't stuck with a floating window.
        // Pending reveal prompts are abandoned (state is local to this lifecycle).
        if (Visible) Close();
    }

    private sealed class SeparatorRow { }
}
