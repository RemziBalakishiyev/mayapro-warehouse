using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateEmployee;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateSalaryEntry;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.DeleteSalaryEntry;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetEmployees;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalaryEntries;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalarySummary;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeActive;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeSalary;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.UpdateEmployee;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Auth.Endpoints;

/// <summary>
/// <c>/api/employees</c> — the payroll register and its salary account (BE#28 + BE#57).
/// <para>
/// There is deliberately <b>no</b> <c>DELETE /api/employees/{id}</c>: an employee who has left is
/// deactivated, because deleting the row would take that person's salary history — and the cash that really
/// did leave the drawer — with it. <c>POST /{id}/deactivate</c> is the replacement.
/// </para>
/// </summary>
internal static class EmployeesEndpoints
{
    // Match the host's role policies: setting a salary and deleting a salary line are owner decisions;
    // maintaining the payroll register and recording a payment are day-to-day management work.
    private const string OwnerOnly = "OwnerOnly";
    private const string OwnerOrManager = "OwnerOrManager";

    public static void MapEmployeesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/employees")
            .WithTags("Employees")
            .RequireAuthorization(); // open to every role for now

        // Includes deactivated employees, flagged with isActive: false — see GetEmployeesHandler.
        group.MapGet("/", async (GetEmployeesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetEmployees");

        group.MapPost("/", async (
                CreateEmployeeCommand command,
                CreateEmployeeHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/employees/{result.Value.Id}" : "/api/employees";
                return result.ToCreatedResult(location);
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("CreateEmployee");

        // Literal segment; the sibling routes below are constrained to {id:guid}, so "salary-summary" can
        // never be read as an employee id (same approach as BE#21's /open-debts).
        group.MapGet("/salary-summary", async (
                string? month,
                GetSalarySummaryHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(month, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("GetSalarySummary");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateEmployeeCommand command,
                UpdateEmployeeHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(command with { Id = id }, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("UpdateEmployee");

        // Idempotent state statements, not toggles: repeating either one is still a 200.
        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                SetEmployeeActiveHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, isActive: false, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("DeactivateEmployee");

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                SetEmployeeActiveHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, isActive: true, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("ActivateEmployee");

        group.MapPut("/{id:guid}/salary", async (
                Guid id,
                SetSalaryRequest request,
                SetEmployeeSalaryHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(new SetEmployeeSalaryCommand(id, request.MonthlySalary), ct)).ToHttpResult())
            .RequireAuthorization(OwnerOnly)
            .WithName("SetEmployeeSalary");

        group.MapPost("/{id:guid}/salary-entries", async (
                Guid id,
                CreateSalaryEntryRequest request,
                CreateSalaryEntryHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(
                    new CreateSalaryEntryCommand(id, request.Type, request.Amount, request.Note, request.Month), ct);
                string location = result.IsSuccess
                    ? $"/api/employees/{id}/salary-entries/{result.Value.Id}"
                    : $"/api/employees/{id}/salary-entries";
                return result.ToCreatedResult(location);
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("CreateSalaryEntry");

        group.MapGet("/{id:guid}/salary-entries", async (
                Guid id,
                string? month,
                GetSalaryEntriesHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, month, ct)).ToHttpResult())
            .WithName("GetSalaryEntries");

        group.MapDelete("/{id:guid}/salary-entries/{entryId:guid}", async (
                Guid id,
                Guid entryId,
                DeleteSalaryEntryHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, entryId, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOnly)
            .WithName("DeleteSalaryEntry");
    }

    /// <summary>Body of <c>PUT /api/employees/{id}/salary</c> — the id lives in the route.</summary>
    private sealed record SetSalaryRequest(decimal MonthlySalary);

    /// <summary>Body of <c>POST /api/employees/{id}/salary-entries</c> — the employee id lives in the route.</summary>
    private sealed record CreateSalaryEntryRequest(string Type, decimal Amount, string? Note, string? Month);
}
