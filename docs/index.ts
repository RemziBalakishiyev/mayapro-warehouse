/** Modullar arası paylaşılan ortaq tiplər. */

export type PaymentType = "Nağd" | "Kart" | "Nisyə";

export type ProductStatus =
  | "Stokda var"
  | "Azalır"
  | "Bitib"
  | "Satılmır"
  | "Ziyana satılır";

export type ExpenseCategory =
  | "Yol"
  | "Fəhlə"
  | "Anbar/Yer"
  | "Paket/Qutu"
  | "Mağaza"
  | "Digər";

/** Auth istifadəçisinin rolu. */
export type Role = "sahib" | "menecer" | "satici";

/** Partiya xərc sətiri — sərbəst ad + məbləğ (köhnə yol/fehle obyekti əvəzinə). */
export interface ProductExpenseItem {
  name: string;
  amount: number;
}

/** Anbardakı mal. */
export interface Product {
  id: string;
  name: string;
  category: string;
  size: string;
  color: string;
  model: string;
  barcode: string;
  image: string;
  note: string;
  purchasePrice: number;
  salePrice: number;
  quantity: number;
  initialQuantity: number;
  minStock: number;
  currency: string;
  supplierId: string;
  /** Yığcam ünvan: "Anbar A / Rəf 3 / Qutu 12" */
  location: string;
  store: string;
  warehouse: string;
  shelf: string;
  box: string;
  expenses: ProductExpenseItem[];
  /** Hesablanmış 1 ədədin real mayası */
  realCostPerUnit: number;
  createdAt: string;
  updatedAt: string;
}

export interface Sale {
  id: string;
  /** Sərbəst (əl ilə) satışda null — mal kataloqda yoxdur */
  productId: string | null;
  productName: string;
  /** Satış anındakı kateqoriya snapshot-u. Kataloq satışında məhsuldan; sərbəst satışda optional; köhnə sətirlərdə null */
  category: string | null;
  quantity: number;
  salePrice: number;
  /** Endirimdən əvvəlki cəm (salePrice × quantity) */
  subtotal: number;
  discount: number;
  /** Endirimdən sonrakı yekun məbləğ (subtotal − discount) */
  totalAmount: number;
  paymentType: PaymentType;
  customerId: string | null;
  /** Satış anındakı real maya snapshot-u (1 ədəd). Sərbəst satışda maya bilinmirsə null */
  costPerUnit: number | null;
  /** Qazanc. Sərbəst satışda maya bilinmirsə null — yalançı qazanc yazılmır */
  profit: number | null;
  /** Sərbəst satış: mal əl ilə yazılıb, stok dəyişməyib */
  isManual: boolean;
  createdAt: string;
  employeeId: string;
}

export interface Customer {
  id: string;
  name: string;
  phone: string;
  totalDebt: number;
  paidAmount: number;
  remainingDebt: number;
  /** Sistemə keçid zamanı yazılan ilkin (açılış) borcu. Təmiz başlayan müştəridə 0 */
  initialDebt: number;
  lastPurchaseDate: string;
  lastPaymentDate: string;
}

/**
 * WIRE QEYDİ (backend dəyişikliyi):
 * - POST /api/customers artıq `initialDebt` qəbul edir (əvvəlki `debt` sahəsi bu ada dəyişdi).
 *   InitialDebt > 0 olduqda müştəri + CustomerDebtAdjustment + activity log bir transaction-da yazılır.
 * - CustomerDto cavabına `initialDebt` sahəsi əlavə olundu.
 * - Yeni GET /api/customers/{id}/history — müştərinin TAM borc tarixçəsi (xronoloji):
 *   ilkin borc + nisyə satışlar + ödənişlər. Köhnə GET /api/customers/{id}/payments toxunulmadı.
 */
export interface CustomerHistoryEntry {
  date: string;
  type: "initialDebt" | "sale" | "payment";
  amount: number;
  /** Satışda mal adı (× miqdar); ilkin borc / ödənişdə qeyd mətni */
  note: string | null;
}

export interface Supplier {
  id: string;
  name: string;
  phone: string;
  totalDebt: number;
  paidAmount: number;
  remainingDebt: number;
  itemCount: number;
  lastPaymentDate: string;
}

export interface Employee {
  id: string;
  name: string;
  phone: string;
  role: string;
  status: "Aktiv" | "Deaktiv";
}

export interface Expense {
  id: string;
  title: string;
  category: ExpenseCategory;
  amount: number;
  productId: string | null;
  date: string;
  note: string;
}

export interface CustomerPayment {
  id: string;
  customerId: string;
  amount: number;
  date: string;
  method: string;
  note?: string;
}

export interface SupplierPayment {
  id: string;
  supplierId: string;
  amount: number;
  date: string;
}

export interface Activity {
  id: string;
  employeeId: string;
  action: string;
  detail: string;
  date: string;
}

export interface Closing {
  id: string;
  date: string;
  openingCash: number;
  cashSales: number;
  cardSales: number;
  creditSales: number;
  expenses: number;
  expectedCash: number;
  actualCash: number;
  difference: number;
}
