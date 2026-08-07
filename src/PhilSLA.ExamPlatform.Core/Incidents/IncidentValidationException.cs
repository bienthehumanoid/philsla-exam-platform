namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed class IncidentValidationException(string message) : Exception(message);
