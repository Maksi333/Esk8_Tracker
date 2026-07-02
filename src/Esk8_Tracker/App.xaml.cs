using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker
{
    public partial class App : Application
    {
        private readonly Esk8Database _database;
        private readonly RideRecorder _recorder;

        public App(Esk8Database database, RideRecorder recorder)
        {
            InitializeComponent();
            _database = database;
            _recorder = recorder;
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
                if (_recorder.State != RecorderState.Idle) return; // never touch the live ride
                await _database.RecoverUnfinishedRidesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ride recovery failed: {ex}");
            }
        }
    }
}
