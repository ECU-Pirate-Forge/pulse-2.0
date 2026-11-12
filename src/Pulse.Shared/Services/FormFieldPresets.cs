using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.Common.Services;

/// <summary>
/// Common field sets instructors can start a form with.
/// </summary>
public static class FormFieldPresets
{
    public static List<FormFieldRequest> Starter() =>
    [
        new(null, "First Name", FormFieldType.Text, Required: true),
        new(null, "Last Name", FormFieldType.Text, Required: true),
        new(null, "Email", FormFieldType.Email, Required: true, Placeholder: "you@example.edu"),
        new(null, "Degree Major", FormFieldType.Text),
        new(null, "Interested in Open Source Project", FormFieldType.YesNo)
    ];
}
