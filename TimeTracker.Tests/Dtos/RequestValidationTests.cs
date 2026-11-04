using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TimeTracker.Dtos;
using Xunit;

namespace TimeTracker.Tests.Dtos;

public class RequestValidationTests
{
    [Fact]
    public void CreateProjectRequest_IsValidatedWithoutThrowing()
    {
        Assert.True(IsValid(new CreateProjectRequest("Roadmap Meeting", 4)));
        Assert.False(IsValid(new CreateProjectRequest("", 0)));
    }

    [Fact]
    public void CreateCustomerRequest_IsValidatedWithoutThrowing()
    {
        Assert.True(IsValid(new CreateCustomerRequest("Contoso")));
        Assert.False(IsValid(new CreateCustomerRequest("")));
    }

    [Fact]
    public void StartTimeEntryRequest_IsValidatedWithoutThrowing()
    {
        Assert.True(IsValid(new StartTimeEntryRequest(1)));
        Assert.False(IsValid(new StartTimeEntryRequest(0)));
    }

    // Runs the same MVC validation that API controllers apply to request bodies.
    private static bool IsValid(object model)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();

        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = provider },
            new RouteData(),
            new ActionDescriptor());

        provider.GetRequiredService<IObjectModelValidator>()
            .Validate(actionContext, null, string.Empty, model);

        return actionContext.ModelState.IsValid;
    }
}
