namespace PhilSLA.ExamPlatform.Proctor;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "PhilSLA.ExamPlatform.Proctor" };
	}
}
