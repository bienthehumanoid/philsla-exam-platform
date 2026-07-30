using Microsoft.Extensions.Logging;

using PhilSLA.ExamPlatform.Candidate.Authentication;
using PhilSLA.ExamPlatform.Candidate.Persistence;

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

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
