namespace PhilSLA.ExamPlatform.Core.Incidents;

public interface IIncidentAssignmentProvider
{
    Task<IReadOnlyList<IncidentAssignment>> GetAssignedAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default);
}
