using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Proctor.Authentication;
using HomeComponent = PhilSLA.ExamPlatform.Proctor.Components.Pages.Home;
using NavMenuComponent = PhilSLA.ExamPlatform.Proctor.Components.Layout.NavMenu;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class DashboardTests
{
    [TestMethod]
    public void UnauthenticatedVisitor_IsRedirectedToLogin()
    {
        using var context = CreateContext();

        context.Render<HomeComponent>();

        var navigation = context.Services.GetRequiredService<NavigationManager>();
        Assert.AreEqual("http://localhost/", navigation.Uri);
    }

    [TestMethod]
    public void AuthenticatedProctor_SeesAssignedSessions()
    {
        using var context = CreateContext(authenticated: true);

        var component = context.Render<HomeComponent>();

        Assert.AreEqual("Exam Schedule", component.Find("h1").TextContent);
        Assert.HasCount(3, component.FindAll(".session-card"));
        Assert.IsTrue(component.Find(".primary-button").HasAttribute("disabled"));
        Assert.IsTrue(component.Find(".session-actions button").HasAttribute("disabled"));
    }

    [TestMethod]
    public void SignOut_ClearsSessionAndReturnsToLogin()
    {
        using var context = CreateContext(authenticated: true);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/home");
        var component = context.Render<NavMenuComponent>();

        component.Find(".sign-out-button").Click();

        var session = context.Services.GetRequiredService<ProctorSessionState>();
        Assert.IsFalse(session.IsAuthenticated);
        Assert.AreEqual("http://localhost/", navigation.Uri);
    }

    private static BunitContext CreateContext(bool authenticated = false)
    {
        var context = new BunitContext();
        var session = new ProctorSessionState();
        if (authenticated)
        {
            session.SignIn(new ProctorIdentity(
                Guid.NewGuid(),
                "Santiago",
                "Reyes",
                "proctor@example.test",
                "PROCTOR"));
        }

        context.Services.AddSingleton(session);
        return context;
    }
}
