using Esk8_Tracker.Views;

namespace Esk8_Tracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(RideDetailPage), typeof(RideDetailPage));
        }
    }
}
