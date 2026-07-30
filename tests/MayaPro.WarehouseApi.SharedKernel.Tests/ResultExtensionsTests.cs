using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Http;

namespace MayaPro.WarehouseApi.SharedKernel.Tests;

/// <summary>
/// The error-code → HTTP status convention every module's endpoints lean on. Pinned here because the rules
/// are ordered: the import flow's <c>...TokenNotFound</c> is a 410 even though it also ends with
/// "NotFound", and adding a rule later must not silently re-map an existing code.
/// </summary>
public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData("Products.NotFound", StatusCodes.Status404NotFound)]
    [InlineData("Sales.Conflict", StatusCodes.Status409Conflict)]
    [InlineData("Products.BarcodeAlreadyExists", StatusCodes.Status409Conflict)]
    [InlineData("DayEnd.AlreadyClosed", StatusCodes.Status409Conflict)]
    [InlineData("General.Validation", StatusCodes.Status400BadRequest)]
    [InlineData("Imports.InvalidTemplate", StatusCodes.Status400BadRequest)]
    [InlineData("Imports.TokenExpired", StatusCodes.Status410Gone)]
    [InlineData("Imports.TokenNotFound", StatusCodes.Status410Gone)]
    public void Error_Code_Suffix_Drives_The_Status_Code(string code, int expectedStatus)
    {
        IResult response = Result.Failure(new Error(code, "mesaj")).ToHttpResult();

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
    }

    [Fact]
    public void A_Successful_Result_Is_A_200()
    {
        IResult response = Result.Success().ToHttpResult();

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
    }
}
