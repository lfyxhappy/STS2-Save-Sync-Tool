using System.Windows;
using System.Windows.Controls;
using Sts2SaveSyncTool.Models;
using Sts2SaveSyncTool.Services;
using Sts2SaveSyncTool.ViewModels;

namespace Sts2SaveSyncTool;

public partial class MainWindow : Window
{
    private bool _ignoreAccountSelectionChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _ignoreAccountSelectionChanged = true;
        try
        {
            await ViewModel.InitializeAsync();
        }
        finally
        {
            _ignoreAccountSelectionChanged = false;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _ignoreAccountSelectionChanged = true;
        try
        {
            await ViewModel.RefreshAccountsAsync();
        }
        finally
        {
            _ignoreAccountSelectionChanged = false;
        }
    }

    private async void ApplySteamRootOverride_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplySteamRootOverrideAsync();
    }

    private async void ClearSteamRootOverride_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearSteamRootOverrideAsync();
    }

    private async void AccountSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _ignoreAccountSelectionChanged)
        {
            return;
        }

        if (AccountSelector.SelectedItem is SteamAccountState account)
        {
            await ViewModel.SelectAccountAsync(account);
        }
    }

    private async void SyncToModded_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ProfilePairState pair)
        {
            await ViewModel.SyncAsync(pair, SyncDirection.NormalToModded);
        }
    }

    private async void SyncToNormal_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ProfilePairState pair)
        {
            await ViewModel.SyncAsync(pair, SyncDirection.ModdedToNormal);
        }
    }
}
