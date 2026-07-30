using MayaPro.WarehouseApi.Modules.Sales.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Tests;

/// <summary>
/// Unit tests for <see cref="SalePaymentPlan"/> — BE#15's qismən ödənişli satış resolution rule, shared by
/// CreateSale/UpdateSale's validator and handler.
/// </summary>
public sealed class SalePaymentPlanTests
{
    [Fact]
    public void TC1_Partial_Credit_Payment_Splits_Cash_And_Remaining()
    {
        // Total=500, Nisyə, paid 300 via Nağd → paid 300, remaining 200, stored type Nisyə, paidVia Nağd.
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Credit, total: 500m, paidAmount: 300m, paidViaCode: "Nağd");

        Assert.Equal(300m, plan.PaidAmount);
        Assert.Equal(200m, plan.Remaining);
        Assert.Equal(PaymentType.Credit, plan.PaymentType);
        Assert.Equal(PaymentType.Cash, plan.PaidVia);
    }

    [Fact]
    public void TC2_Cash_Sale_Without_PaidAmount_Defaults_To_Fully_Paid()
    {
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Cash, total: 500m, paidAmount: null, paidViaCode: null);

        Assert.Equal(500m, plan.PaidAmount);
        Assert.Equal(0m, plan.Remaining);
        Assert.Equal(PaymentType.Cash, plan.PaymentType);
        Assert.Equal(PaymentType.Cash, plan.PaidVia);
    }

    [Fact]
    public void TC3_Credit_Sale_Without_PaidAmount_Defaults_To_Zero_Paid()
    {
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Credit, total: 200m, paidAmount: null, paidViaCode: null);

        Assert.Equal(0m, plan.PaidAmount);
        Assert.Equal(200m, plan.Remaining);
        Assert.Equal(PaymentType.Credit, plan.PaymentType);
    }

    [Fact]
    public void TC4_Card_Down_Payment_Forces_Credit_And_Attributes_The_Paid_Portion_To_Card()
    {
        // Total=1000, requested Kart, paid 600 via Kart → stored Nisyə (remaining>0), Card gets the 600.
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Card, total: 1000m, paidAmount: 600m, paidViaCode: "Kart");

        Assert.Equal(600m, plan.PaidAmount);
        Assert.Equal(400m, plan.Remaining);
        Assert.Equal(PaymentType.Credit, plan.PaymentType); // forced to Nisyə regardless of the Kart request
        Assert.Equal(PaymentType.Card, plan.PaidVia);
    }

    [Fact]
    public void TC5_Zero_Paid_Cash_Request_Still_Leaves_A_Remaining_Balance()
    {
        // Requested Nağd but paidAmount explicitly 0 → the whole total remains owed; the validator (not the
        // plan) is what then demands a customer.
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Cash, total: 150m, paidAmount: 0m, paidViaCode: null);

        Assert.Equal(0m, plan.PaidAmount);
        Assert.Equal(150m, plan.Remaining);
        Assert.Equal(PaymentType.Credit, plan.PaymentType);
    }

    [Fact]
    public void TC9_Fully_Paying_Off_A_Formerly_Partial_Sale_Switches_Back_To_The_Requested_Type()
    {
        // Total=500, now paid in full via Nağd → remaining 0, stored type follows the request (Cash), not Credit.
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Cash, total: 500m, paidAmount: 500m, paidViaCode: null);

        Assert.Equal(500m, plan.PaidAmount);
        Assert.Equal(0m, plan.Remaining);
        Assert.Equal(PaymentType.Cash, plan.PaymentType);
        Assert.Equal(PaymentType.Cash, plan.PaidVia);
    }

    [Fact]
    public void TC10_Fully_Paid_Sale_Never_Reaches_Credit_Even_With_A_Customer()
    {
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Cash, total: 400m, paidAmount: 400m, paidViaCode: null);

        Assert.Equal(0m, plan.Remaining);
        Assert.NotEqual(PaymentType.Credit, plan.PaymentType);
    }

    [Fact]
    public void An_Unrecognised_PaidVia_Code_Falls_Back_To_Cash()
    {
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Credit, total: 100m, paidAmount: 40m, paidViaCode: "garbage");

        Assert.Equal(PaymentType.Cash, plan.PaidVia);
    }

    [Fact]
    public void PaidVia_Can_Never_Resolve_To_Credit_Itself()
    {
        // "Nisyə" is not a valid paid-via method — even if somehow supplied, it must fall back to Cash.
        SalePaymentPlan plan = SalePaymentPlan.Resolve(
            PaymentType.Credit, total: 100m, paidAmount: 40m, paidViaCode: "Nisyə");

        Assert.Equal(PaymentType.Cash, plan.PaidVia);
    }
}
