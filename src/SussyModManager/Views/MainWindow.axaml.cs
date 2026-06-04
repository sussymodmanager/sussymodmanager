using System;
using Avalonia.Controls;
using SussyModManager.ViewModels;

namespace SussyModManager.Views
{
    public partial class MainWindow : Window
    {
        private bool _wizardShown;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (_wizardShown || DataContext is not MainWindowViewModel vm || !vm.NeedsWizard)
                return;
            _wizardShown = true;

            var wizard = new WizardWindow { DataContext = vm.CreateWizard() };
            await wizard.ShowDialog(this);
            vm.OnWizardCompleted();
        }
    }
}
