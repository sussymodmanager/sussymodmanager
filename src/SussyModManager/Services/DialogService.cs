using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Services
{
    /// <summary>
    /// Thin wrapper over MsBox.Avalonia for showing info/confirm dialogs and surfacing
    /// <see cref="InstallResult"/> warnings/errors that were previously dropped silently.
    /// </summary>
    public static class DialogService
    {
        private static Window Owner =>
            Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
                ? d.MainWindow
                : null;

        public static async Task ShowInfoAsync(string title, string message) =>
            await ShowAsync(title, message, ButtonEnum.Ok, Icon.Info).ConfigureAwait(true);

        public static async Task ShowErrorAsync(string title, string message) =>
            await ShowAsync(title, message, ButtonEnum.Ok, Icon.Error).ConfigureAwait(true);

        public static Task<bool> ConfirmAsync(string title, string message,
            string yes = "Yes", string no = "Cancel", bool danger = false) =>
            ShowCustomYesNoAsync(title, message, yes, no, danger);

        /// <summary>
        /// Prompts the user for a line of text. Returns the trimmed value, or null if cancelled
        /// or left empty.
        /// </summary>
        public static async Task<string> PickThemeFileAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var topLevel = Owner != null ? TopLevel.GetTopLevel(Owner) : null;
                if (topLevel == null)
                    return null;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import theme",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Theme file") { Patterns = new[] { "*.json" } }
                    }
                }).ConfigureAwait(true);

                return files?.FirstOrDefault()?.Path.LocalPath;
            });
        }

        public static async Task<string> SaveThemeFileAsync(string suggestedName)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var topLevel = Owner != null ? TopLevel.GetTopLevel(Owner) : null;
                if (topLevel == null)
                    return null;

                var safe = string.IsNullOrWhiteSpace(suggestedName) ? "theme" : suggestedName.Trim();
                foreach (var c in Path.GetInvalidFileNameChars())
                    safe = safe.Replace(c, '-');

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export theme",
                    SuggestedFileName = safe + ".json",
                    DefaultExtension = "json",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Theme file") { Patterns = new[] { "*.json" } }
                    }
                }).ConfigureAwait(true);

                return file?.Path.LocalPath;
            });
        }

        public static async Task<string> PickDllAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = Owner;
                var topLevel = owner != null ? TopLevel.GetTopLevel(owner) : null;
                if (topLevel == null)
                    return null;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select a BepInEx plugin (.dll)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("BepInEx plugin") { Patterns = new[] { "*.dll" } }
                    }
                }).ConfigureAwait(true);

                var file = files?.FirstOrDefault();
                return file?.Path.LocalPath;
            });
        }

        public static async Task<string> PromptAsync(string title, string message, string initialValue = "")
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var win = Views.PromptWindow.Create(title, message, initialValue);
                var owner = Owner;
                if (owner != null)
                    return await win.ShowDialog<string>(owner);

                var tcs = new TaskCompletionSource<string>();
                win.Closed += (_, _) => tcs.TrySetResult(null);
                win.Show();
                return await tcs.Task;
            });
        }

        /// <summary>
        /// Shows a results dialog for an install/preset/update operation. Returns nothing; only
        /// pops up when there are warnings or the operation failed (success-with-no-warnings is silent).
        /// </summary>
        public static async Task ShowResultAsync(string title, InstallResult result)
        {
            if (result == null)
                return;
            if (result.Success && (result.Warnings == null || result.Warnings.Count == 0))
                return;

            var lines = new List<string>();
            if (!result.Success)
                lines.Add(result.Message ?? "The operation failed.");
            else if (!string.IsNullOrWhiteSpace(result.Message))
                lines.Add(result.Message);

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                lines.Add("");
                lines.Add(result.Success ? "Some items need attention:" : "Details:");
                lines.AddRange(result.Warnings.Select(w => "  • " + w));
            }

            await ShowAsync(title, string.Join(Environment.NewLine, lines),
                ButtonEnum.Ok, result.Success ? Icon.Warning : Icon.Error).ConfigureAwait(true);
        }

        private static async Task<ButtonResult> ShowAsync(string title, string message, ButtonEnum buttons, Icon icon)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MessageBoxManager.GetMessageBoxStandard(title, message, buttons, icon);
                var owner = Owner;
                if (owner != null)
                    return await box.ShowWindowDialogAsync(owner);
                return await box.ShowWindowAsync();
            });
        }

        private static async Task<bool> ShowCustomYesNoAsync(string title, string message, string yes, string no, bool danger)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
                {
                    ContentTitle = title,
                    ContentMessage = message,
                    Icon = danger ? Icon.Warning : Icon.Question,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ButtonDefinitions = new List<ButtonDefinition>
                    {
                        new ButtonDefinition { Name = yes },
                        new ButtonDefinition { Name = no, IsCancel = true }
                    }
                });
                var owner = Owner;
                var result = owner != null
                    ? await box.ShowWindowDialogAsync(owner)
                    : await box.ShowWindowAsync();
                return string.Equals(result, yes, StringComparison.Ordinal);
            });
        }
    }
}
