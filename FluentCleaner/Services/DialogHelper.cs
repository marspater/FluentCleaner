using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace FluentCleaner.Services;

public static class DialogHelper
{
    private static readonly SemaphoreSlim _dialogLock = new(1, 1);

    public static async Task<ContentDialogResult> ShowSafeAsync(ContentDialog dialog)
    {
        await _dialogLock.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DialogHelper] ContentDialog error: {ex.Message}");
            return ContentDialogResult.None;
        }
        finally
        {
            _dialogLock.Release();
        }
    }
}
