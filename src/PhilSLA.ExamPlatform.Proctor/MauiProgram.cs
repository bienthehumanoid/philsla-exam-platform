using Microsoft.Extensions.Logging;
using PhilSLA.ExamPlatform.Core.Attendance;
using PhilSLA.ExamPlatform.Infrastructure.Attendance;
using PhilSLA.ExamPlatform.Proctor.Attendance;
using PhilSLA.ExamPlatform.Proctor.Authentication;
using PhilSLA.ExamPlatform.Proctor.Persistence;

namespace PhilSLA.ExamPlatform.Proctor;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		var passwordHasher = new PasswordHasher();
		var proctorDatabasePath = Path.Combine(
			FileSystem.AppDataDirectory,
			"philsla-proctor-mvp.db");
		var proctorRepository = new TemporaryProctorRepository(
			proctorDatabasePath,
			passwordHasher);

		builder.Services.AddSingleton(passwordHasher);
		builder.Services.AddSingleton(proctorRepository);
		builder.Services.AddSingleton<IAuthenticationService, TemporaryAuthenticationService>();
		builder.Services.AddSingleton<ProctorSessionState>();
		builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
		builder.Services.AddSingleton<IAttendanceSessionProvider, SeededAttendanceSessionProvider>();
		builder.Services.AddSingleton<IAttendanceStore>(_ =>
			new SqliteAttendanceStore(proctorDatabasePath));
		builder.Services.AddSingleton<AttendanceService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
