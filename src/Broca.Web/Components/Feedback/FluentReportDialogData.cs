using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Components;
using static Broca.ActivityPub.Components.ReportDialog;

namespace Broca.Web.Components.Feedback;

public class FluentReportDialogData
{
    public IObject? TargetObject { get; set; }
    public Actor? TargetActor { get; set; }
    public ReportTargetType ReportType { get; set; }
    public bool IsLocalActor { get; set; }
    public bool AllowForward { get; set; } = true;
    public bool AllowBlock { get; set; } = true;
    public int MaxNotesLength { get; set; } = 1000;
    public EventCallback<ReportSubmission> OnSubmit { get; set; }
    public EventCallback OnCancel { get; set; }
}
