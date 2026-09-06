using FluentCleaner.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FluentCleaner.Views;

public sealed partial class TerminalPage : Page
{
    public CliViewModel ViewModel { get; } = new();

    private bool _initialized;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    public TerminalPage()
    {
        InitializeComponent();
        ViewModel.Output.CollectionChanged += (_, _) =>
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                OutputScroller.ChangeView(null, double.MaxValue, null, disableAnimation: false);
            });
        };

        Loaded += async (_, _) =>
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                await ViewModel.InitAsync();
            }
            catch (Exception ex)
            {
                ViewModel.Output.Add($"Initialization error: {ex.Message}");
            }
        };
    }

    private void Input_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs e)
    {
        if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            sender.ItemsSource = ViewModel.GetSuggestions(sender.Text);
    }

    private void InputBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_history.Count == 0) return;

        if (e.Key == VirtualKey.Up)
        {
            if (_historyIndex == -1)
                _historyIndex = _history.Count - 1;
            else if (_historyIndex > 0)
                _historyIndex--;

            InputBox.Text = _history[_historyIndex];
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                InputBox.Text = _history[_historyIndex];
            }
            else
            {
                _historyIndex = -1;
                InputBox.Text = "";
            }
            e.Handled = true;
        }
    }

    private async void Input_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs e)
    {
        var text = e.ChosenSuggestion as string ?? e.QueryText;
        if (string.IsNullOrWhiteSpace(text)) return;

        _history.Add(text);
        _historyIndex = -1;

        sender.Text = "";
        sender.ItemsSource = null;  // force suggestion popup closed

        try
        {
            await ViewModel.ExecuteAsync(text);
        }
        catch (Exception ex)
        {
            ViewModel.Output.Add($"Error: {ex.Message}");
        }
    }
}
