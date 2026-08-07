using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Plenipo.Application.Authorization;
using Plenipo.Modules.Sdk;

namespace Hireworthy.Hiring;

/// <summary>
/// Supplies the hiring module's executable tools.
/// </summary>
/// <remarks>
/// A tool must be registered in <b>two</b> places — a <c>ToolDescriptor</c> in
/// <see cref="HiringModule"/>'s manifest and a <see cref="ModuleTool"/> here — carrying the
/// <b>same</b> permission string, always built with <c>Permissions.ForTool</c> rather than
/// hand-written. Miss either and the tool is silently never callable; mismatch the strings and it
/// 403s even for <c>system_admin</c>. Verify with <c>GET /api/admin/security/catalog</c>.
/// </remarks>
public sealed class HiringToolSource : IModuleToolSource
{
    public string ModuleId => HiringModule.Id;

    public IReadOnlyList<ModuleTool> GetTools(IServiceProvider scopedServices)
    {
        var tools = scopedServices.GetRequiredService<HiringTools>();

        return
        [
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "list_requisitions",
                Permission = Permissions.ForTool(ModuleId, "list_requisitions"),
                Function = AIFunctionFactory.Create(tools.ListRequisitionsAsync, name: "list_requisitions"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "get_requisition",
                Permission = Permissions.ForTool(ModuleId, "get_requisition"),
                Function = AIFunctionFactory.Create(tools.GetRequisitionAsync, name: "get_requisition"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "get_applicant",
                Permission = Permissions.ForTool(ModuleId, "get_applicant"),
                Function = AIFunctionFactory.Create(tools.GetApplicantAsync, name: "get_applicant"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "parse_cv",
                Permission = Permissions.ForTool(ModuleId, "parse_cv"),
                Function = AIFunctionFactory.Create(tools.ParseCvAsync, name: "parse_cv"),
                // NOT approval-gated, and that is an argued exception rather than an oversight —
                // see ADR-0004. It writes derived data that changes no candidate's outcome.
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "screen_applicant",
                Permission = Permissions.ForTool(ModuleId, "screen_applicant"),
                Function = AIFunctionFactory.Create(tools.ScreenApplicantAsync, name: "screen_applicant"),
                // Ungated (ADR-0004): writes a proposal, moves nobody's stage. The citation
                // containment check inside it is what makes the proposal trustworthy.
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "advance_candidates",
                Permission = Permissions.ForTool(ModuleId, "advance_candidates"),
                Function = AIFunctionFactory.Create(tools.AdvanceCandidatesAsync, name: "advance_candidates"),
                // A real person is told they progressed, and it cannot be un-told. Gated in BOTH
                // places; the runner unions the flags, so reviewing only one hides a broken gate.
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "reject_candidate",
                Permission = Permissions.ForTool(ModuleId, "reject_candidate"),
                Function = AIFunctionFactory.Create(tools.RejectCandidateAsync, name: "reject_candidate"),
                // The decision this product is ultimately judged on. Gated in BOTH places.
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "propose_rubric",
                Permission = Permissions.ForTool(ModuleId, "propose_rubric"),
                Function = AIFunctionFactory.Create(tools.ProposeRubricAsync, name: "propose_rubric"),
                // Kept in sync with the manifest descriptor deliberately: the runner unions both
                // sets, so setting one and reviewing only that one hides a broken gate.
                RequiresApproval = true,
            },
        ];
    }
}
