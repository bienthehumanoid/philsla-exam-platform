using System.Text;
using System.Text.Json;
using PhilSLA.ExamPlatform.Contracts.Admissions;
using PhilSLA.ExamPlatform.Contracts.Devices;
using PhilSLA.ExamPlatform.Contracts.Packages;
using PhilSLA.ExamPlatform.Contracts.Protocol;
using PhilSLA.ExamPlatform.Contracts.Security;
using PhilSLA.ExamPlatform.Contracts.Submissions;
using PhilSLA.ExamPlatform.Contracts.Synchronization;

namespace PhilSLA.ExamPlatform.Contracts.Tests;

[TestClass]
public sealed class ContractJsonTests
{
    private static readonly DateTimeOffset IssuedAt = new(
        2026,
        8,
        15,
        8,
        0,
        0,
        TimeSpan.Zero);

    private static readonly SignatureEnvelope Signature = new(
        "test-signature",
        "issuer-key-1",
        "c2lnbmF0dXJl");

    [TestMethod]
    public void PhaseOneContracts_RoundTripWithoutChangingWireRepresentation()
    {
        AssertRoundTrip(new DeviceCredential(
            ContractProtocol.CurrentVersion,
            Id(1),
            Id(2),
            "test-public-key",
            "cHVibGljLWtleQ==",
            "philsla-test",
            IssuedAt,
            IssuedAt.AddDays(30),
            Signature));
        AssertRoundTrip(new ExamPermit(
            ContractProtocol.CurrentVersion,
            Id(3),
            Id(4),
            Id(2),
            Id(5),
            Id(6),
            Id(7),
            "ROOM-101",
            IssuedAt,
            IssuedAt.AddHours(1),
            IssuedAt.AddHours(4),
            ["extended-time"],
            "philsla-test",
            Signature));
        AssertRoundTrip(new ExamPackageManifest(
            ContractProtocol.CurrentVersion,
            Id(6),
            Id(5),
            "2026.08.15.1",
            Id(4),
            "extended-time",
            "test-content-encryption",
            "test-key-wrapping",
            "d3JhcHBlZC1rZXk=",
            new string('A', 64),
            4096,
            IssuedAt,
            "philsla-test",
            Signature));
        AssertRoundTrip(new SynchronizationEnvelope(
            ContractProtocol.CurrentVersion,
            Id(8),
            Id(9),
            Id(4),
            1,
            "answer-revision",
            "application/json",
            Encoding.UTF8.GetBytes("{\"question\":1}"),
            new string('B', 64),
            IssuedAt));
        AssertRoundTrip(new ExamSubmissionManifest(
            ContractProtocol.CurrentVersion,
            Id(10),
            Id(3),
            Id(9),
            Id(4),
            Id(5),
            Id(6),
            SubmissionCompletionReason.Candidate,
            IssuedAt.AddHours(3),
            100,
            new string('C', 64),
            Signature));
        AssertRoundTrip(new SubmissionReceipt(
            ContractProtocol.CurrentVersion,
            Id(11),
            Id(10),
            Id(9),
            Id(5),
            Id(7),
            new string('D', 64),
            IssuedAt.AddHours(3).AddMinutes(1),
            "philsla-proctor-test",
            Signature));
    }

    [TestMethod]
    public void Serialize_UsesCamelCasePropertiesAndStringEnums()
    {
        var contract = new ExamSubmissionManifest(
            ContractProtocol.CurrentVersion,
            Id(10),
            Id(3),
            Id(9),
            Id(4),
            Id(5),
            Id(6),
            SubmissionCompletionReason.TimeExpired,
            IssuedAt,
            100,
            new string('C', 64),
            Signature);

        var json = Encoding.UTF8.GetString(ContractJson.Serialize(contract));

        StringAssert.Contains(json, "\"protocolVersion\":1");
        StringAssert.Contains(json, "\"completionReason\":\"timeExpired\"");
        Assert.DoesNotContain("ProtocolVersion", json);
    }

    [TestMethod]
    public void Deserialize_RejectsUnsupportedProtocolVersion()
    {
        var json =
            """
            {
              "protocolVersion": 2,
              "messageId": "00000008-0000-4000-8000-000000000001",
              "attemptId": "00000009-0000-4000-8000-000000000001",
              "candidateId": "00000004-0000-4000-8000-000000000001",
              "sequenceNumber": 1,
              "messageType": "answer-revision",
              "contentType": "application/json",
              "payload": "e30=",
              "payloadSha256Hex": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
              "createdAtUtc": "2026-08-15T08:00:00+00:00"
            }
            """u8.ToArray();

        var error = Assert.ThrowsExactly<UnsupportedProtocolVersionException>(
            () => ContractJson.Deserialize<SynchronizationEnvelope>(json));

        Assert.AreEqual(2, error.ActualVersion);
        Assert.AreEqual(ContractProtocol.CurrentVersion, error.SupportedVersion);
    }

    [TestMethod]
    public void Deserialize_RejectsUnknownProperties()
    {
        var contract = new DeviceCredential(
            ContractProtocol.CurrentVersion,
            Id(1),
            Id(2),
            "test-public-key",
            "cHVibGljLWtleQ==",
            "philsla-test",
            IssuedAt,
            IssuedAt.AddDays(30),
            Signature);
        var json = Encoding.UTF8.GetString(ContractJson.Serialize(contract));
        json = json.Insert(json.Length - 1, ",\"unexpected\":true");

        Assert.ThrowsExactly<JsonException>(() =>
            ContractJson.Deserialize<DeviceCredential>(
                Encoding.UTF8.GetBytes(json)));
    }

    [TestMethod]
    public void Deserialize_RejectsMissingRequiredConstructorProperties()
    {
        var json =
            """
            {
              "protocolVersion": 1,
              "credentialId": "00000001-0000-4000-8000-000000000001"
            }
            """u8.ToArray();

        Assert.ThrowsExactly<JsonException>(() =>
            ContractJson.Deserialize<DeviceCredential>(json));
    }

    private static void AssertRoundTrip<TContract>(TContract contract)
        where TContract : IProtocolContract
    {
        var firstJson = ContractJson.Serialize(contract);
        var restored = ContractJson.Deserialize<TContract>(firstJson);
        var secondJson = ContractJson.Serialize(restored);

        CollectionAssert.AreEqual(firstJson, secondJson);
    }

    private static Guid Id(int value)
    {
        return Guid.Parse($"{value:D8}-0000-4000-8000-000000000001");
    }
}
