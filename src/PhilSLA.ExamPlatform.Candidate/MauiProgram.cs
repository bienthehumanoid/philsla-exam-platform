using Microsoft.Extensions.Logging;

using Microsoft.Maui.Networking;
using PhilSLA.ExamPlatform.Candidate.Authentication;
using PhilSLA.ExamPlatform.Candidate.Examination;
using PhilSLA.ExamPlatform.Candidate.Persistence;
using PhilSLA.ExamPlatform.Candidate.Readiness;
using PhilSLA.ExamPlatform.Core.Examinations;
using PhilSLA.ExamPlatform.Infrastructure.Examinations;

namespace PhilSLA.ExamPlatform.Candidate;

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
		var candidateDatabasePath = Path.Combine(
			FileSystem.AppDataDirectory,
			"philsla-candidate-mvp.db");
		var candidateRepository = new TemporaryCandidateRepository(
			candidateDatabasePath,
			passwordHasher);

		builder.Services.AddSingleton(passwordHasher);
		builder.Services.AddSingleton(candidateRepository);
		builder.Services.AddSingleton<IAuthenticationService, TemporaryAuthenticationService>();
		builder.Services.AddSingleton<CandidateSessionState>();
		builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
		builder.Services.AddSingleton<IExamAssignmentProvider, SeededExamAssignmentProvider>();
		builder.Services.AddSingleton<IExamDefinitionProvider, SeededExamDefinitionProvider>();
		builder.Services.AddSingleton<IExamAuthorizationService>(
			new TimedExamAuthorizationService(TimeSpan.FromSeconds(5)));
		builder.Services.AddSingleton<IExamAttemptStore>(
			new SqliteExamAttemptStore(candidateDatabasePath));
		builder.Services.AddSingleton(TimeProvider.System);
		builder.Services.AddSingleton<ExamSessionService>();

#if WINDOWS
		builder.Services.AddSingleton<IDeviceReadinessService, WindowsDeviceReadinessService>();
#else
		builder.Services.AddSingleton<IDeviceReadinessService, UnsupportedDeviceReadinessService>();
#endif

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
