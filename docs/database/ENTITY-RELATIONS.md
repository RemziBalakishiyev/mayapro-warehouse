# Entity Relations

Modullararası referanslar **FK DEYİL** — sadəcə saxlanan Id-dir (navigation yoxdur, JOIN yoxdur). Ad/qiymət kimi məlumatlar snapshot kopyalanır (ADR-0004).

## Modullararası Id referansları

| Sahə | Hədəf | Qeyd |
|---|---|---|
| `Sale.ProductId` (Guid?) | products.Products | null = sərbəst satış |
| `Sale.CustomerId` (Guid?) | customers.Customers | hər ödəniş növündə ola bilər (nisyədə məcburi); borca təsir yalnız nisyədə |
| `Sale.SoldByUserId` (Guid?) | identity.Users | + `SoldByName` snapshot |
| `Expense.ProductId` (Guid?) | products.Products | + `ProductName` snapshot |
| `Product.SupplierId` (**string**) | suppliers.Suppliers | tarixi səbəbdən string (frontend `sup_1` formatı) |
| `Closing.ClosedByUserId` (Guid?) | identity.Users | |
| `ActivityLog.UserId` (Guid?) | identity.Users | + `UserName` snapshot |

Hədəf silinəndə referans qalır ("Silinmiş müştəri" davranışı) — zəncir geri sarmaları best-effort.

## Modul daxili münasibətlər (həqiqi FK-lar)

- `CustomerPayment.CustomerId` → Customer (eyni modul)
- `CustomerDebtAdjustment.CustomerId` → Customer (ilkin borc tarixçəsi)
- `SupplierPayment.SupplierId` → Supplier

## Entity-lərin qısa xəritəsi

- **User**: FullName, Phone (unique), Email, PasswordHash (BCrypt), Role (string), IsActive
- **Product**: ad/kateqoriya(string snapshot)/barcode/qiymətlər/Quantity/InitialQuantity(sabit)/MinStock/yerləşmə sahələri/Attributes(JSON)/Expenses(JSON)/RealCostPerUnit(hesablanan)
- **Category**: sadə ad siyahısı (məhsul kateqoriyaya FK ilə bağlanmır)
- **ExpenseType**: sadə ad siyahısı, unique (xərc `Category`-yə FK ilə bağlanmır — Category ilə eyni pattern)
- **Sale**: snapshot sahələri (ProductName, Category, CostPerUnit), Quantity, UnitPrice, Subtotal, TotalAmount(=Subtotal), Profit(null ola bilər), PaymentType, IsManual, ExpenseItems(JSON), Date(UTC), InvoiceToken(nullable, unique — açıq link, bir dəfə yaranır)
- **Customer**: Name, Phone, Note, Debt (0-dan aşağı düşmür)
- **CustomerPayment / CustomerDebtAdjustment**: məbləğ + tarix + qeyd
- **Supplier**: Name, ContactName, Phone, Note, Debt, ItemCount
- **Expense**: Title, Category(string snapshot, ExpenseType-a FK-sız), Source(enum: general/product), Amount, Date, ProductId?, ProductName?, Note
- **Closing**: gün totalları + ExpectedCash/Difference (constructor-da hesablanır), Date unique
- **ActivityLog**: Type(≤50), Message(≤1000), UserId?, UserName snapshot
- **StoreSettings**: singleton — StoreName, OwnerName?, Address?, Phone?, WhatsappTemplate, Currency, DefaultMinStock, Language

## Last Updated

2026-07-27 — BE#4: `ExpenseType` əlavə olundu; `Expense.Category` enum-dan sərbəst-string snapshot-a keçdi, `Expense.Source` (general/product) sahəsi əlavə olundu.

## Related Code

- `src/Modules/*/Domain/*.cs` (entity-lər)
- `src/Modules/*/Infrastructure/Configurations/` (mapping-lər)
