using System.ComponentModel.DataAnnotations;
using PhilSLA.ExamPlatform.Candidate.Components.Pages;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class LoginFormModelTests
{
    [TestMethod]
    public void Validate_MissingEmail_ReturnsRequiredError()
    {
        var errors = Validate(new LoginFormModel { Password = "password" });

        CollectionAssert.Contains(errors, "Email address is required.");
    }

    [TestMethod]
    public void Validate_MalformedEmail_ReturnsFormatError()
    {
        var errors = Validate(new LoginFormModel
        {
            Email = "not-an-email",
            Password = "password"
        });

        CollectionAssert.Contains(errors, "Enter a valid email address.");
    }

    [TestMethod]
    public void Validate_ValidEmail_DoesNotReturnEmailError()
    {
        var errors = Validate(new LoginFormModel
        {
            Email = "candidate@example.test",
            Password = "password"
        });

        Assert.IsFalse(errors.Any(error =>
            error.Contains("email", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Validate_MissingPassword_ReturnsRequiredError()
    {
        var errors = Validate(new LoginFormModel
        {
            Email = "candidate@example.test"
        });

        CollectionAssert.Contains(errors, "Password is required.");
    }

    [TestMethod]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var errors = Validate(new LoginFormModel
        {
            Email = "candidate@example.test",
            Password = "password"
        });

        Assert.IsEmpty(errors);
    }

    private static string[] Validate(LoginFormModel model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);

        Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        return results
            .Select(result => result.ErrorMessage ?? string.Empty)
            .ToArray();
    }
}
