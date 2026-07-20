using CvarcCellLog.Pages;

namespace CvarcCellLog;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(QsoEntryPage), typeof(QsoEntryPage));
	}
}
