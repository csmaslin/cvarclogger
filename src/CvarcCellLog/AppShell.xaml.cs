using CvarcCellLog.Pages;

namespace CvarcCellLog;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(QsoLogPage), typeof(QsoLogPage));
		Routing.RegisterRoute(nameof(QsoEntryPage), typeof(QsoEntryPage));
		Routing.RegisterRoute(nameof(QsoEditPage), typeof(QsoEditPage));
		Routing.RegisterRoute(nameof(FileMenuPage), typeof(FileMenuPage));
		Routing.RegisterRoute(nameof(StationProfilesPage), typeof(StationProfilesPage));
		Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
		Routing.RegisterRoute(nameof(LogColumnsPage), typeof(LogColumnsPage));
		Routing.RegisterRoute(nameof(AwardsPage), typeof(AwardsPage));
	}
}
