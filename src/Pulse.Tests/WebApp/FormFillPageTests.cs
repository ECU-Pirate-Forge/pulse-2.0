using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApp.Components.Pages;

namespace Pulse.Tests.Tests.WebApp;

public class FormFillPageTests : BunitContext
{
    private static readonly Guid FirstNameId = Guid.NewGuid();
    private static readonly Guid MajorId = Guid.NewGuid();
    private static readonly Guid OpenSourceId = Guid.NewGuid();

    private static PublicFormResponse SampleForm() => new(
        Guid.NewGuid(),
        "Club Signup",
        "Tell us about you",
        [
            new PublicFormField(FirstNameId, "First Name", FormFieldType.Text, true, null, []),
            new PublicFormField(MajorId, "Degree Major", FormFieldType.Dropdown, false, null, ["CS", "IT"]),
            new PublicFormField(OpenSourceId, "Interested in Open Source", FormFieldType.Checkbox, false, null, [])
        ]);

    private void UseHandler(FormHandler handler) =>
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public void Route_IsFormJoinCode()
    {
        Assert.Contains(typeof(FormFill).GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Components.RouteAttribute), false),
            a => ((Microsoft.AspNetCore.Components.RouteAttribute)a).Template.StartsWith("/form/{JoinCode"));
    }

    [Fact]
    public void OpenForm_RendersAllFieldTypes()
    {
        var handler = new FormHandler { Form = SampleForm() };
        UseHandler(handler);

        var cut = Render<FormFill>(p => p.Add(x => x.JoinCode, " abc123 "));

        cut.WaitForAssertion(() => Assert.Equal("Club Signup", cut.Find("[data-testid='form-title']").TextContent));
        Assert.Equal("/api/forms/join/ABC123", handler.LastGetPath);
        Assert.NotNull(cut.Find($"#field-{FirstNameId}"));
        Assert.Equal(3, cut.FindAll($"#field-{MajorId} option").Count);
        Assert.Equal("checkbox", cut.Find($"#field-{OpenSourceId}").GetAttribute("type"));
    }

    [Fact]
    public void OpenForm_RendersMarkdownDescriptionAndQuestions()
    {
        var form = SampleForm() with
        {
            Description = "## Welcome\n\n- Read **carefully**\n- Ask for help",
            Fields =
            [
                new PublicFormField(FirstNameId, "**First** name", FormFieldType.Text, true, "*plain placeholder*", []),
                new PublicFormField(MajorId, "Choose a *major*", FormFieldType.Dropdown, false, null, ["**CS**", "IT"]),
                new PublicFormField(OpenSourceId, "Like `open source`?", FormFieldType.Checkbox, false, null, [])
            ]
        };
        UseHandler(new FormHandler { Form = form });

        var cut = Render<FormFill>(p => p.Add(x => x.JoinCode, "ABC123"));

        Assert.Equal("Welcome", cut.WaitForElement(".pulse-markdown h2").TextContent);
        Assert.Equal(2, cut.FindAll(".pulse-markdown ul li").Count);
        Assert.Equal("First", cut.Find($"#field-{FirstNameId}-label strong").TextContent);
        Assert.Contains("*", cut.Find($"#field-{FirstNameId}-label").TextContent);
        Assert.Equal("major", cut.Find($"#field-{MajorId}-label em").TextContent);
        Assert.Equal("open source", cut.Find($"#field-{OpenSourceId}-label code").TextContent);
        foreach (var field in form.Fields)
        {
            Assert.Equal($"field-{field.Id}-label", cut.Find($"#field-{field.Id}").GetAttribute("aria-labelledby"));
        }
        Assert.Equal("*plain placeholder*", cut.Find($"#field-{FirstNameId}").GetAttribute("placeholder"));
        Assert.Equal("**CS**", cut.FindAll($"#field-{MajorId} option")[1].TextContent);
    }

    [Theory]
    [InlineData(FormFieldType.Text)]
    [InlineData(FormFieldType.TextArea)]
    [InlineData(FormFieldType.Email)]
    [InlineData(FormFieldType.Phone)]
    [InlineData(FormFieldType.Number)]
    [InlineData(FormFieldType.Date)]
    [InlineData(FormFieldType.Dropdown)]
    [InlineData(FormFieldType.YesNo)]
    [InlineData(FormFieldType.Checkbox)]
    public void EveryFieldType_HasAnAccessibleMarkdownQuestion(FormFieldType type)
    {
        UseHandler(new FormHandler
        {
            Form = SampleForm() with
            {
                Fields = [new PublicFormField(FirstNameId, "**Question**\n\n- Detail", type, false, null, [])]
            }
        });

        var cut = Render<FormFill>(p => p.Add(x => x.JoinCode, "ABC123"));

        Assert.Equal("Question", cut.WaitForElement($"#field-{FirstNameId}-label strong").TextContent);
        Assert.Equal("Detail", cut.Find($"#field-{FirstNameId}-label li").TextContent);
        Assert.Equal($"field-{FirstNameId}-label", cut.Find($"#field-{FirstNameId}").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void OpenForm_DoesNotExecuteMarkupInDescriptionsOrQuestions()
    {
        UseHandler(new FormHandler
        {
            Form = SampleForm() with
            {
                Description = "<script>alert(1)</script>\n\n[bad](javascript:alert%281%29)",
                Fields = [new PublicFormField(FirstNameId, "<img src=x onerror=alert(1)>", FormFieldType.Text, false, null, [])]
            }
        });
        var cut = Render<FormFill>(p => p.Add(x => x.JoinCode, "ABC123"));

        cut.WaitForElement("form");

        Assert.Empty(cut.FindAll("script, img, iframe, [onerror], a[href^='javascript:']"));
        Assert.Contains("<script>alert(1)</script>", cut.Find(".pulse-markdown").TextContent);
    }

    [Fact]
    public void Submit_SendsValuesAndShowsThanks()
    {
        var handler = new FormHandler { Form = SampleForm() };
        UseHandler(handler);
        var cut = Render<FormFill>(p => p.Add(x => x.JoinCode, "ABC123"));

        cut.WaitForElement($"#field-{FirstNameId}").Change("Ada");
        cut.Find($"#field-{MajorId}").Change("CS");
        cut.Find($"#field-{OpenSourceId}").Change(true);
        cut.Find("form").Submit();

        cut.WaitForElement("[data-testid='form-thanks']");
        Assert.NotNull(handler.Submitted);
        Assert.Equal("Ada", handler.Submitted!.Values![FirstNameId.ToString()]);
        Assert.Equal("CS", handler.Submitted.Values[MajorId.ToString()]);
        Assert.Equal("true", handler.Submitted.Values[OpenSourceId.ToString()]);
    }

    [Fact]
    public void Submit_ValidationErrors_ShowFieldErrors()
    {
        var handler = new FormHandler
        {
            Form = SampleForm(),
            SubmitStatus = HttpStatusCode.BadRequest,
            Validation = new FormValidationErrorResponse(
                "Please fix the highlighted fields.",
                new Dictionary<string, string> { [FirstNameId.ToString()] = "First Name is required." })
        };
        UseHandler(handler);
        var cut = Render<FormFill>(p => p.Add(x => x.JoinCode, "ABC123"));

        cut.WaitForElement("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Equal("First Name is required.", cut.Find("[data-testid='field-error']").TextContent));
        Assert.Contains("highlighted", cut.Find("[data-testid='submit-error']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='form-thanks']"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "not found")]
    [InlineData(HttpStatusCode.Conflict, "This form is closed.")]
    [InlineData(HttpStatusCode.InternalServerError, "could not load")]
    public void LoadFailure_ShowsMessage(HttpStatusCode status, string expected)
    {
        UseHandler(new FormHandler { GetStatus = status });

        var cut = Render<FormFill>(p => p.Add(x => x.JoinCode, "ABC123"));

        cut.WaitForAssertion(() =>
            Assert.Contains(expected, cut.Find("[data-testid='form-load-error']").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='submit-form']"));
    }

    private sealed class FormHandler : HttpMessageHandler
    {
        public PublicFormResponse? Form { get; init; }
        public HttpStatusCode GetStatus { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode SubmitStatus { get; init; } = HttpStatusCode.OK;
        public FormValidationErrorResponse? Validation { get; init; }
        public string? LastGetPath { get; private set; }
        public SubmitFormRequest? Submitted { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                LastGetPath = request.RequestUri!.AbsolutePath;
                if (GetStatus == HttpStatusCode.OK)
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Form) };
                if (GetStatus == HttpStatusCode.Conflict)
                    return new HttpResponseMessage(GetStatus) { Content = JsonContent.Create("This form is closed.") };
                return new HttpResponseMessage(GetStatus);
            }

            Submitted = await request.Content!.ReadFromJsonAsync<SubmitFormRequest>(cancellationToken);
            if (SubmitStatus == HttpStatusCode.BadRequest)
                return new HttpResponseMessage(SubmitStatus) { Content = JsonContent.Create(Validation) };
            return new HttpResponseMessage(SubmitStatus)
            {
                Content = JsonContent.Create(new SubmitFormResponse(Guid.NewGuid()))
            };
        }
    }
}
