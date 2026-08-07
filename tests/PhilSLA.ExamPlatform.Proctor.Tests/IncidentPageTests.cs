using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

using PhilSLA.ExamPlatform.Core.Incidents;
using PhilSLA.ExamPlatform.Proctor.Authentication;
using PhilSLA.ExamPlatform.Proctor.Tests.Attendance;
using PhilSLA.ExamPlatform.Proctor.Tests.Incidents;

using IncidentsComponent = PhilSLA.ExamPlatform.Proctor.Components.Pages.Incidents;
using NavMenuComponent = PhilSLA.ExamPlatform.Proctor.Components.Layout.NavMenu;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class IncidentPageTests
{
    [TestMethod]
    public void IncidentNavigation_IsEnabledAndRoutesToIncidentRecords()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton(new ProctorSessionState());

        var component = context.Render<NavMenuComponent>();
        var link = component.Find(".incident-navigation");

        Assert.AreEqual("incidents", link.GetAttribute("href"));
        Assert.IsFalse(link.HasAttribute("disabled"));
        Assert.AreEqual("Incident Records", link.TextContent.Trim());
    }

    [TestMethod]
    public void UnauthenticatedVisitor_IsRedirectedToLogin()
    {
        using var context = CreateContext(authenticated: false);

        context.Render<IncidentsComponent>();

        Assert.AreEqual("http://localhost/", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [TestMethod]
    public void LoadFailure_ShowsSafeRetryableError()
    {
        var store = new InMemoryIncidentStore { FailLoad = true };
        using var context = CreateContext(store: store);

        var component = context.Render<IncidentsComponent>();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Incident records could not be loaded.", component.Find(".incidents-error p").TextContent.Trim());
            Assert.HasCount(1, component.FindAll(".retry-incidents"));
        });
    }

    [TestMethod]
    public void PopulatedPage_MatchesIncidentTableAndNewestFirstOrder()
    {
        var older = IncidentTestData.CreateRecord() with
        {
            Id = Guid.NewGuid(),
            DisplayId = "INC-2026-001",
            CreatedAtUtc = IncidentTestData.CreatedAtUtc.AddMinutes(-10)
        };
        var newer = IncidentTestData.CreateRecord() with
        {
            Id = Guid.NewGuid(),
            DisplayId = "INC-2026-002",
            Severity = IncidentSeverity.Critical
        };
        using var context = CreateContext(records: [older, newer]);

        var component = context.Render<IncidentsComponent>();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Incident Records", component.Find("h1").TextContent.Trim());
            Assert.AreEqual("Create Incident", component.Find(".create-incident").TextContent.Trim());
            Assert.HasCount(2, component.FindAll("[data-incident-id]"));
            Assert.AreEqual("INC-2026-002", component.FindAll("[data-incident-id]")[0].QuerySelector(".incident-id")!.TextContent.Trim());
            CollectionAssert.AreEqual(
                new[] { "Incident ID", "Student Information", "Incident Category", "Severity", "Review Status", "Actions" },
                component.FindAll("thead th").Select(header => header.TextContent.Trim()).ToArray());
            Assert.HasCount(1, component.FindAll(".severity-critical"));
        });
    }

    [TestMethod]
    public void SearchSeverityAndStatusFilters_Combine()
    {
        var matching = IncidentTestData.CreateRecord() with
        {
            Id = Guid.NewGuid(),
            DisplayId = "INC-2026-010",
            CandidateName = "Maria Cristina Santos",
            Severity = IncidentSeverity.Medium,
            ReviewStatus = IncidentReviewStatus.Resolved
        };
        var other = IncidentTestData.CreateRecord() with
        {
            Id = Guid.NewGuid(),
            DisplayId = "INC-2026-011",
            CandidateName = "Juan Carlos Villanueva",
            Severity = IncidentSeverity.High,
            ReviewStatus = IncidentReviewStatus.Pending
        };
        using var context = CreateContext(records: [matching, other]);
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement("[data-incident-id]");

        component.Find("#incident-search").Input("Maria");
        component.Find("#severity-filter").Click();
        component.Find("#severity-filter-options [data-filter-value='Medium']").Click();
        component.Find("#review-status-filter").Click();
        component.Find("#review-status-filter-options [data-filter-value='Resolved']").Click();

        Assert.HasCount(1, component.FindAll("[data-incident-id]"));
        Assert.AreEqual("INC-2026-010", component.Find(".incident-id").TextContent.Trim());
    }

    [TestMethod]
    public void FilterMenus_AlignTheirOptionsWithTheTriggerButtons()
    {
        using var context = CreateContext();
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement("#severity-filter").Click();

        var severityMenu = component.Find("#severity-filter-options");
        Assert.IsTrue(severityMenu.ParentElement!.ClassList.Contains("incident-filter"));
        StringAssert.Contains(severityMenu.GetAttribute("style") ?? string.Empty, "width:100%");

        component.Find("#review-status-filter").Click();

        Assert.HasCount(0, component.FindAll("#severity-filter-options"));
        var statusMenu = component.Find("#review-status-filter-options");
        Assert.IsTrue(statusMenu.ParentElement!.ClassList.Contains("incident-filter"));
        StringAssert.Contains(statusMenu.GetAttribute("style") ?? string.Empty, "width:100%");
    }

    [TestMethod]
    public void FilterMenu_SupportsKeyboardSelection()
    {
        using var context = CreateContext();
        var component = context.Render<IncidentsComponent>();
        var trigger = component.WaitForElement("#review-status-filter");

        trigger.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown" });
        trigger.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown" });
        trigger.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        StringAssert.Contains(trigger.TextContent, "Pending");
        Assert.HasCount(0, component.FindAll("#review-status-filter-options"));
    }

    [TestMethod]
    public void DetailDialog_ShowsCompleteReadOnlyRecordAndEvidencePreview()
    {
        var attachment = new IncidentAttachment(
            Guid.NewGuid(),
            "evidence.png",
            "image/png",
            8,
            "evidence.png",
            new string('A', 64));
        var record = IncidentTestData.CreateRecord(attachments: [attachment]);
        var store = new InMemoryIncidentStore([record]);
        using var context = CreateContext(store: store);
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".view-incident").Click();

        component.WaitForAssertion(() =>
        {
            var dialog = component.Find(".incident-detail-dialog");
            StringAssert.Contains(dialog.TextContent, record.Description);
            StringAssert.Contains(dialog.TextContent, record.SessionTitle);
            StringAssert.Contains(dialog.TextContent, record.ReportedByProctorName);
            Assert.AreEqual(0, store.ReadEvidenceCalls);
            Assert.HasCount(0, component.FindAll(".edit-incident, .delete-incident, .resolve-incident"));
        });

        component.Find(".evidence-thumbnail").Click();
        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(1, store.ReadEvidenceCalls);
            Assert.HasCount(1, component.FindAll(".evidence-preview-dialog"));
        });

        component.Find(".evidence-preview-dialog button").Click();
        component.Find(".evidence-thumbnail").Click();
        component.WaitForAssertion(() => Assert.AreEqual(2, store.ReadEvidenceCalls));
    }

    [TestMethod]
    public void CreateIncidentModal_FollowsProvidedFieldStructure()
    {
        using var context = CreateContext();
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".create-incident").Click();

        var dialog = component.Find(".create-incident-dialog");
        Assert.AreEqual("Register New Incident", dialog.QuerySelector("h2")!.TextContent.Trim());
        CollectionAssert.AreEqual(
            new[] { "Assigned Candidate *", "Incident Category *", "Severity Level *", "Reason / Description of Breach *", "Screenshot Evidence" },
            dialog.QuerySelectorAll("label").Select(label => label.TextContent.Trim()).ToArray());
        Assert.HasCount(1, component.FindAll("input[type='file'][multiple][accept='image/jpeg,image/png']"));
        var fileInputStyle = component.Find("input[type='file']").GetAttribute("style") ?? string.Empty;
        StringAssert.Contains(fileInputStyle, "position:absolute");
        StringAssert.Contains(fileInputStyle, "opacity:0");
    }

    [TestMethod]
    public void CandidateCombobox_FiltersSelectsAndContainsItsOptions()
    {
        var maria = IncidentTestData.Assignment with
        {
            CandidateId = Guid.NewGuid(),
            StudentNumber = "2026-0099",
            CandidateName = "Maria Cristina Santos",
            Room = "SEC Lecture Hall 1"
        };
        using var context = CreateContext(assignments: [IncidentTestData.Assignment, maria]);
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".create-incident").Click();

        var input = component.Find("#incident-candidate-search");
        input.TriggerEvent("onfocus", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        input.Input("Maria");

        var listbox = component.Find("#incident-candidate-options");
        Assert.IsTrue(listbox.ParentElement!.ClassList.Contains("candidate-combobox"));
        StringAssert.Contains(listbox.GetAttribute("style") ?? string.Empty, "width:100%");
        StringAssert.Contains(listbox.GetAttribute("style") ?? string.Empty, "max-height:14rem");
        Assert.HasCount(1, component.FindAll("[role='option']"));
        StringAssert.Contains(component.Find(".candidate-option-primary").TextContent, "Maria Cristina Santos");
        StringAssert.Contains(component.Find(".candidate-option-secondary").TextContent, "SEC Lecture Hall 1");

        component.Find("[role='option']").Click();

        Assert.AreEqual("Maria Cristina Santos · 2026-0099", input.GetAttribute("value"));
        Assert.HasCount(0, component.FindAll("#incident-candidate-options"));
    }

    [TestMethod]
    public void CandidateCombobox_EnterSelectsTheHighlightedCandidate()
    {
        using var context = CreateContext();
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".create-incident").Click();
        var input = component.Find("#incident-candidate-search");

        input.Input("Juan");
        input.TriggerEvent(
            "onkeydown",
            new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.AreEqual("Juan Carlos Villanueva · 2026-0001", input.GetAttribute("value"));
        Assert.HasCount(0, component.FindAll("#incident-candidate-options"));
    }

    [TestMethod]
    public void CreateIncident_WithRequiredFields_AddsPendingRecordAndClosesDialog()
    {
        var store = new InMemoryIncidentStore();
        using var context = CreateContext(store: store);
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".create-incident").Click();

        CompleteRequiredFields(component, "Observed repeated tab switching.");
        component.Find(".submit-incident").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(1, store.CreateCalls);
            Assert.HasCount(0, component.FindAll(".create-incident-dialog"));
            Assert.HasCount(1, component.FindAll("[data-incident-id]"));
            Assert.AreEqual("Incident INC-2026-001 was created and is pending review.", component.Find(".incident-created-message").TextContent.Trim());
        });
    }

    [TestMethod]
    public void CreateIncident_SaveFailurePreservesEnteredValues()
    {
        var store = new InMemoryIncidentStore { FailCreation = true };
        using var context = CreateContext(store: store);
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".create-incident").Click();

        CompleteRequiredFields(component, "Observed repeated tab switching.");
        component.Find(".submit-incident").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Observed repeated tab switching.", component.Find("#incident-description").GetAttribute("value"));
            Assert.AreEqual("Incident could not be saved. Review the details and try again.", component.Find(".incident-form-error").TextContent.Trim());
            Assert.HasCount(1, component.FindAll(".create-incident-dialog"));
        });
    }

    [TestMethod]
    public void EvidenceSelection_ShowsMultipleFilesAndSupportsRemoval()
    {
        using var context = CreateContext();
        var thumbnails = new[]
        {
            "data:image/jpeg;base64,bounded-thumbnail-one",
            "data:image/jpeg;base64,bounded-thumbnail-two"
        };
        context.JSInterop
            .Setup<string[]>("philslaIncidentEvidence.createThumbnails", _ => true)
            .SetResult(thumbnails);
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".create-incident").Click();
        var input = component.FindComponent<InputFile>();

        input.UploadFiles(
            InputFileContent.CreateFromBinary([137, 80, 78, 71, 13, 10, 26, 10], "first.png", contentType: "image/png"),
            InputFileContent.CreateFromBinary([255, 216, 255, 1], "second.jpg", contentType: "image/jpeg"));

        Assert.HasCount(2, component.FindAll(".selected-evidence"));
        CollectionAssert.AreEqual(
            thumbnails,
            component.FindAll(".selected-evidence img")
                .Select(image => image.GetAttribute("src"))
                .ToArray());
        var thumbnailInvocation = context.JSInterop.Invocations.Single(invocation =>
            invocation.Identifier == "philslaIncidentEvidence.createThumbnails");
        Assert.AreEqual(256, thumbnailInvocation.Arguments[1]);
        Assert.AreEqual(256, thumbnailInvocation.Arguments[2]);
        component.Find(".remove-evidence").Click();
        Assert.HasCount(1, component.FindAll(".selected-evidence"));
    }

    [TestMethod]
    public void EvidenceSelection_RejectsUnsupportedFilesAndMoreThanFiveImages()
    {
        using var context = CreateContext();
        var component = context.Render<IncidentsComponent>();
        component.WaitForElement(".create-incident").Click();
        var input = component.FindComponent<InputFile>();

        input.UploadFiles(InputFileContent.CreateFromText("notes", "notes.pdf", contentType: "application/pdf"));
        Assert.AreEqual("Only JPEG and PNG evidence is supported.", component.Find(".evidence-selection-error").TextContent.Trim());

        input.UploadFiles(Enumerable.Range(1, 6)
            .Select(index => InputFileContent.CreateFromBinary(
                [137, 80, 78, 71, 13, 10, 26, 10],
                $"evidence-{index}.png",
                contentType: "image/png"))
            .ToArray());
        Assert.AreEqual("You can attach up to 5 evidence images.", component.Find(".evidence-selection-error").TextContent.Trim());
        Assert.HasCount(0, component.FindAll(".selected-evidence"));
    }

    private static void CompleteRequiredFields(
        IRenderedComponent<IncidentsComponent> component,
        string description)
    {
        var candidateInput = component.Find("#incident-candidate-search");
        candidateInput.Input(IncidentTestData.Assignment.CandidateName);
        component.Find("[role='option']").Click();
        component.Find("#incident-category").Change(IncidentTestData.CategoryId.ToString());
        component.Find("#incident-severity").Change(nameof(IncidentSeverity.High));
        component.Find("#incident-description").Input(description);
    }

    private static BunitContext CreateContext(
        bool authenticated = true,
        IReadOnlyList<IncidentRecord>? records = null,
        InMemoryIncidentStore? store = null,
        IReadOnlyList<IncidentAssignment>? assignments = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var session = new ProctorSessionState();
        if (authenticated)
        {
            session.SignIn(new ProctorIdentity(
                IncidentTestData.ProctorId,
                "Santiago",
                "Reyes",
                "proctor@example.test",
                "PROCTOR"));
        }

        var incidentStore = store ?? new InMemoryIncidentStore(records);
        context.Services.AddSingleton(session);
        context.Services.AddSingleton<IIncidentStore>(incidentStore);
        context.Services.AddSingleton<IIncidentCategoryProvider>(new StubCategoryProvider());
        context.Services.AddSingleton<IIncidentAssignmentProvider>(
            new StubAssignmentProvider(assignments ?? [IncidentTestData.Assignment]));
        context.Services.AddSingleton(new TestTimeProvider(IncidentTestData.CreatedAtUtc));
        context.Services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<TestTimeProvider>());
        context.Services.AddSingleton<IncidentService>();
        return context;
    }

    private sealed class StubCategoryProvider : IIncidentCategoryProvider
    {
        public Task<IReadOnlyList<IncidentCategory>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IncidentCategory>>([IncidentTestData.Category]);
    }

    private sealed class StubAssignmentProvider(IReadOnlyList<IncidentAssignment> assignments) : IIncidentAssignmentProvider
    {
        public Task<IReadOnlyList<IncidentAssignment>> GetAssignedAsync(
            Guid proctorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(assignments);
    }
}
