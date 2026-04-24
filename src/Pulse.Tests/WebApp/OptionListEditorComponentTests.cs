using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Pulse.WebApp.Components;
using Xunit;

namespace Pulse.Tests.WebApp;

public class OptionListEditorComponentTests : BunitContext
{
    public OptionListEditorComponentTests()
    {
        Services.AddMudServices();
        
        // Configure JSInterop to allow all MudBlazor JS calls
        JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true);
        JSInterop.SetupVoid("mudElementRef.addOnFocusEvent", _ => true);
        JSInterop.SetupVoid("mudElementRef.removeOnBlurEvent", _ => true);
        JSInterop.SetupVoid("mudElementRef.removeOnFocusEvent", _ => true);
        JSInterop.SetupVoid("mudPopoverFactory.initialize", _ => true);
        JSInterop.SetupVoid("scrollManager.initialize", _ => true);
        JSInterop.SetupVoid("scrollManager.unlock", _ => true);
        JSInterop.SetupVoid("scrollManager.lock", _ => true);
    }
    [Fact]
    public void Renders_Initial_Empty_Option()
    {
        // Arrange
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, new List<string>()));

        // Act & Assert
        var optionInputs = component.FindAll("input[type='text']");
        Assert.NotEmpty(optionInputs);
    }

    [Fact]
    public void Renders_Existing_Options()
    {
        // Arrange
        var options = new List<string> { "Option 1", "Option 2", "Option 3" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        // Act & Assert
        var optionInputs = component.FindAll("input[type='text']");
        Assert.Equal(3, optionInputs.Count);
    }

    [Fact]
    public async Task AddOption_Appends_New_Empty_Option()
    {
        // Arrange
        var options = new List<string> { "Option 1" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        var addButton = component.FindAll("button").First(b => b.TextContent.Contains("Add"));

        // Act
        await addButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        component.WaitForAssertion(() =>
        {
            var optionInputs = component.FindAll("input[type='text']");
            Assert.Equal(2, optionInputs.Count);
        });

        // Assert
        var finalInputs = component.FindAll("input[type='text']");
        Assert.Equal(2, finalInputs.Count);
    }

    [Fact]
    public async Task RemoveOption_Deletes_Option_From_List()
    {
        // Arrange
        var options = new List<string> { "Option 1", "Option 2" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        var removeButtons = component.FindAll("button").Where(b => b.TextContent.Contains("Remove")).ToList();

        // Act
        await removeButtons.First().ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        component.WaitForAssertion(() =>
        {
            var optionInputs = component.FindAll("input[type='text']");
            Assert.Single(optionInputs);
        });

        // Assert
        var finalInputs = component.FindAll("input[type='text']");
        Assert.Single(finalInputs);
    }

    [Fact]
    public void RemoveOption_Disabled_When_Only_One_Option()
    {
        // Arrange
        var options = new List<string> { "Option 1" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        // Act & Assert
        var removeButton = component.FindAll("button").First(b => b.TextContent.Contains("Remove"));
        var disabled = removeButton.GetAttribute("disabled");
        Assert.NotNull(disabled);
    }

    [Fact]
    public async Task EditOption_Updates_Option_Value()
    {
        // Arrange
        var options = new List<string> { "Option 1", "Option 2" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        var optionInputs = component.FindAll("input[type='text']");

        // Act
        await optionInputs.First().ChangeAsync(new ChangeEventArgs { Value = "Updated Option 1" });
        component.WaitForAssertion(() =>
        {
            var updatedOptions = component.Instance.GetOptions();
            Assert.Equal("Updated Option 1", updatedOptions[0]);
        });

        // Assert
        var finalOptions = component.Instance.GetOptions();
        Assert.Equal("Updated Option 1", finalOptions[0]);
    }

    [Fact]
    public async Task OptionsChanged_Callback_Invoked_On_Add()
    {
        // Arrange
        var options = new List<string> { "Option 1" };
        var changedOptions = new List<string>();
        
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options)
            .Add(p => p.OptionsChanged, EventCallback.Factory.Create<List<string>>(
                this, (opts) => changedOptions = opts)));

        var addButton = component.FindAll("button").First(b => b.TextContent.Contains("Add"));

        // Act
        await addButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        component.WaitForAssertion(() => Assert.Equal(2, changedOptions.Count));

        // Assert
        Assert.Equal(2, changedOptions.Count);
    }

    [Fact]
    public async Task OptionsChanged_Callback_Invoked_On_Remove()
    {
        // Arrange
        var options = new List<string> { "Option 1", "Option 2" };
        var changedOptions = new List<string>();
        
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options)
            .Add(p => p.OptionsChanged, EventCallback.Factory.Create<List<string>>(
                this, (opts) => changedOptions = opts)));

        var removeButtons = component.FindAll("button").Where(b => b.TextContent.Contains("Remove")).ToList();

        // Act
        await removeButtons.Last().ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        component.WaitForAssertion(() => Assert.Single(changedOptions));

        // Assert
        Assert.Single(changedOptions);
    }

    [Fact]
    public async Task OptionsChanged_Callback_Invoked_On_Edit()
    {
        // Arrange
        var options = new List<string> { "Option 1", "Option 2" };
        var changedOptions = new List<string>();
        
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options)
            .Add(p => p.OptionsChanged, EventCallback.Factory.Create<List<string>>(
                this, (opts) => changedOptions = opts)));

        var optionInputs = component.FindAll("input[type='text']");

        // Act
        await optionInputs.First().ChangeAsync(new ChangeEventArgs { Value = "New Value" });
        component.WaitForAssertion(() => Assert.Equal("New Value", changedOptions[0]));

        // Assert
        Assert.Equal("New Value", changedOptions[0]);
    }

    [Fact]
    public void ValidateOptions_Returns_False_With_Less_Than_Two_Options()
    {
        // Arrange
        var options = new List<string> { "Option 1" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        // Act
        var isValid = component.Instance.ValidateOptions();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateOptions_Returns_False_With_Empty_Options()
    {
        // Arrange
        var options = new List<string> { "  ", "  " };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        // Act
        var isValid = component.Instance.ValidateOptions();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateOptions_Returns_True_With_Two_Valid_Options()
    {
        // Arrange
        var options = new List<string> { "Option 1", "Option 2" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        // Act
        var isValid = component.Instance.ValidateOptions();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateOptions_Returns_True_With_Multiple_Valid_Options()
    {
        // Arrange
        var options = new List<string> { "Option 1", "Option 2", "Option 3", "Option 4" };
        var component = Render<OptionListEditor>(parameters => parameters
            .Add(p => p.Options, options));

        // Act
        var isValid = component.Instance.ValidateOptions();

        // Assert
        Assert.True(isValid);
    }
}
