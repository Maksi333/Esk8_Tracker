using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker
{
    public partial class App : Application
    {
        private readonly Esk8Database _database;

        public App(Esk8Database database)
        {
            InitializeComponent();
            _database = database;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override void OnStart()
        {
            base.OnStart();
            _ = RecoverAsync();
        }

        private async Task RecoverAsync()
        {
            try
            {
                await _database.RecoverUnfinishedRidesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ride recovery failed: {ex}");
            }
        }
    }
}
