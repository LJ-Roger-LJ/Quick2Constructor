using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Quick2Constructor
{
    internal sealed class ConstructorPickWindow : DialogWindow
    {
        private const double TitleBarHeight = 32;

        private readonly IReadOnlyList<ConstructorLocation> _allItems;
        private readonly TextBox _filter;
        private readonly TextBlock _placeholder;
        private readonly ListBox _list;
        private readonly TextBlock _emptyState;

        public ConstructorLocation Selected { get; private set; }

        public ConstructorPickWindow(IReadOnlyList<ConstructorLocation> items)
        {
            _allItems = items ?? Array.Empty<ConstructorLocation>();

            Title = Strings.CommandTitle;
            Width = 560;
            Height = 380;
            MinWidth = 420;
            MinHeight = 240;
            HasMaximizeButton = false;
            HasMinimizeButton = false;
            HasHelpButton = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(0),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });

            SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            SetResourceReference(ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            SetResourceReference(BorderBrushProperty, EnvironmentColors.SearchBoxBorderBrushKey);
            BorderThickness = new Thickness(1);

            _filter = CreateFilterBox();
            _placeholder = CreatePlaceholder();
            _list = CreateList();
            _emptyState = CreateEmptyState();

            Content = BuildLayout();
            ApplyFilter();

            PreviewKeyDown += OnWindowPreviewKeyDown;
            Loaded += OnLoaded;
        }

        private Grid BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleBarHeight) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = CreateTitleBar();
            Grid.SetRow(titleBar, 0);

            var body = new Grid { Margin = new Thickness(12, 4, 12, 12) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var searchHost = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            searchHost.Children.Add(_filter);
            searchHost.Children.Add(_placeholder);
            Grid.SetRow(searchHost, 0);

            var listHost = new Grid();
            listHost.Children.Add(_list);
            listHost.Children.Add(_emptyState);
            Grid.SetRow(listHost, 1);

            var hint = new TextBlock
            {
                Text = Strings.PickerHint,
                Margin = new Thickness(2, 8, 2, 0),
                FontSize = 11
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.CommandBarTextInactiveBrushKey);
            Grid.SetRow(hint, 2);

            var cancel = new Button
            {
                IsCancel = true,
                Width = 0,
                Height = 0,
                Opacity = 0,
                IsTabStop = false
            };
            cancel.Click += (_, __) => Dismiss();

            body.Children.Add(searchHost);
            body.Children.Add(listHost);
            body.Children.Add(hint);
            body.Children.Add(cancel);
            Grid.SetRow(body, 1);

            root.Children.Add(titleBar);
            root.Children.Add(body);
            return root;
        }

        private FrameworkElement CreateTitleBar()
        {
            var bar = new Grid
            {
                Height = TitleBarHeight,
                Background = Brushes.Transparent
            };
            bar.SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = Strings.CommandTitle,
                Margin = new Thickness(12, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            Grid.SetColumn(title, 0);

            var close = new Button
            {
                Content = "\u00D7",
                Width = TitleBarHeight,
                Height = TitleBarHeight,
                FontSize = 16,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Arrow,
                IsTabStop = false,
                ToolTip = "Esc"
            };
            close.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            close.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            close.Click += (_, __) => Dismiss();
            Grid.SetColumn(close, 1);
            WindowChrome.SetIsHitTestVisibleInChrome(close, true);

            bar.Children.Add(title);
            bar.Children.Add(close);
            bar.MouseLeftButtonDown += OnTitleBarMouseLeftButtonDown;
            return bar;
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            for (DependencyObject source = e.OriginalSource as DependencyObject; source != null; source = VisualTreeHelper.GetParent(source))
            {
                if (source is Button)
                {
                    return;
                }

                if (ReferenceEquals(source, sender))
                {
                    break;
                }
            }

            DragMove();
        }

        private TextBox CreateFilterBox()
        {
            var box = new TextBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                BorderThickness = new Thickness(1)
            };
            box.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.SearchBoxBackgroundBrushKey);
            box.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            box.SetResourceReference(Control.BorderBrushProperty, EnvironmentColors.SearchBoxBorderBrushKey);
            box.SetResourceReference(TextBox.CaretBrushProperty, EnvironmentColors.ToolWindowTextBrushKey);
            box.TextChanged += (_, __) =>
            {
                _placeholder.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
                ApplyFilter();
            };
            box.PreviewKeyDown += OnFilterPreviewKeyDown;
            return box;
        }

        private TextBlock CreatePlaceholder()
        {
            var placeholder = new TextBlock
            {
                Text = Strings.FilterPlaceholder,
                Margin = new Thickness(10, 0, 8, 0),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            placeholder.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.CommandBarTextInactiveBrushKey);
            return placeholder;
        }

        private ListBox CreateList()
        {
            var list = new ListBox
            {
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                ItemTemplate = CreateItemTemplate(),
                ItemContainerStyle = CreateItemContainerStyle()
            };
            list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            list.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            list.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            list.MouseDoubleClick += (_, __) => AcceptSelection();
            list.KeyDown += OnListKeyDown;
            return list;
        }

        private TextBlock CreateEmptyState()
        {
            var empty = new TextBlock
            {
                Text = Strings.NoMatchingConstructors,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                Visibility = Visibility.Collapsed
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.CommandBarTextInactiveBrushKey);
            return empty;
        }

        private static DataTemplate CreateItemTemplate()
        {
            var stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.MarginProperty, new Thickness(10, 6, 10, 6));

            var signature = new FrameworkElementFactory(typeof(TextBlock));
            signature.SetBinding(TextBlock.TextProperty, new Binding(nameof(ConstructorLocation.Signature)));
            signature.SetValue(TextBlock.FontSizeProperty, 13.0);
            signature.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Cascadia Code, Consolas, Courier New"));
            signature.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            signature.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);

            var location = new FrameworkElementFactory(typeof(TextBlock));
            location.SetBinding(TextBlock.TextProperty, new Binding(nameof(ConstructorLocation.LocationText)));
            location.SetValue(TextBlock.FontSizeProperty, 11.0);
            location.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));
            location.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            location.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.CommandBarTextInactiveBrushKey);

            stack.AppendChild(signature);
            stack.AppendChild(location);

            return new DataTemplate(typeof(ConstructorLocation)) { VisualTree = stack };
        }

        private static Style CreateItemContainerStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(EnvironmentColors.ToolWindowTextBrushKey)));

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(EnvironmentColors.CommandBarMenuItemMouseOverBrushKey)));
            style.Triggers.Add(hover);

            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(EnvironmentColors.CommandBarMenuItemMouseOverBrushKey)));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(EnvironmentColors.ToolWindowTextBrushKey)));
            style.Triggers.Add(selected);

            return style;
        }

        private void ApplyFilter()
        {
            string text = _filter.Text?.Trim() ?? string.Empty;
            IEnumerable<ConstructorLocation> view = _allItems;
            if (!string.IsNullOrEmpty(text))
            {
                view = _allItems.Where(item =>
                    (item.Signature?.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    || (item.LocationText?.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0));
            }

            var filtered = view.ToList();
            _list.ItemsSource = filtered;
            _emptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _list.Visibility = filtered.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (filtered.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _filter.Focus();
            Keyboard.Focus(_filter);
            if (_list.SelectedItem != null)
            {
                _list.ScrollIntoView(_list.SelectedItem);
            }
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Dismiss();
                e.Handled = true;
            }
        }

        private void OnFilterPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _list.Items.Count > 0)
            {
                _list.Focus();
                if (_list.SelectedIndex < 0)
                {
                    _list.SelectedIndex = 0;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                AcceptSelection();
                e.Handled = true;
            }
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AcceptSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _list.SelectedIndex <= 0)
            {
                _filter.Focus();
                e.Handled = true;
            }
        }

        private void AcceptSelection()
        {
            if (_list.SelectedItem is ConstructorLocation location)
            {
                Selected = location;
                DialogResult = true;
            }
        }

        private void Dismiss()
        {
            try
            {
                DialogResult = false;
            }
            catch (InvalidOperationException)
            {
                Close();
            }
        }
    }
}
