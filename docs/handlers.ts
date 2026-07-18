/** Biznes məntiqli mock əməliyyatlar. */
import { db } from "./db";
import { uid, todayISO, fmtMoney } from "@/lib/format";
import { useAuthStore } from "@/features/auth/store";
import type {
  Product,
  Sale,
  PaymentType,
  CustomerPayment,
  Supplier,
  SupplierPayment,
  Expense,
  ExpenseCategory,
  Closing,
  ProductExpenseItem,
} from "@/types";

/** Real maya: purchasePrice + Σ amounts / initialQuantity. */
function calcRealCostFromLines(
  purchasePrice: number,
  initialQuantity: number,
  expenses: ProductExpenseItem[],
): number {
  const total = expenses.reduce((sum, e) => sum + e.amount, 0);
  if (initialQuantity <= 0) return purchasePrice;
  return Math.round((purchasePrice + total / initialQuantity) * 100) / 100;
}

/** Yeni mal üçün giriş — hesablanan/avtomatik sahələr xaric. */
export type NewProduct = Omit<
  Product,
  "id" | "realCostPerUnit" | "initialQuantity" | "createdAt" | "updatedAt"
>;

/** Mövcud malın yenilənməsi üçün giriş (initialQuantity yaradılışda sabitlənir). */
export type ProductUpdate = NewProduct;

async function logActivity(action: string, detail: string): Promise<void> {
  const employeeId = useAuthStore.getState().user?.id ?? "emp_1";
  await db.activity.create({
    id: uid("act"),
    employeeId,
    action,
    detail,
    date: todayISO(),
  });
}

export const productHandlers = {
  list: () => db.products.list(),

  get: (id: string) => db.products.get(id),

  async create(input: NewProduct): Promise<Product> {
    const realCostPerUnit = calcRealCostFromLines(
      input.purchasePrice,
      input.quantity,
      input.expenses,
    );
    const product: Product = {
      ...input,
      id: uid("prd"),
      realCostPerUnit,
      initialQuantity: input.quantity,
      createdAt: todayISO(),
      updatedAt: todayISO(),
    };
    await db.products.create(product);
    await logActivity("Mal əlavə etdi", `${product.name} — ${product.quantity} ədəd`);
    return product;
  },

  async update(id: string, input: ProductUpdate): Promise<Product> {
    const realCostPerUnit = calcRealCostFromLines(
      input.purchasePrice,
      input.quantity,
      input.expenses,
    );
    return db.products.update(id, {
      ...input,
      realCostPerUnit,
      updatedAt: todayISO(),
    });
  },

  async adjustStock(
    id: string,
    delta: number,
    reason?: string,
  ): Promise<Product> {
    const current = await db.products.get(id);
    if (!current) throw new Error("Mal tapılmadı");
    const quantity = Math.max(0, current.quantity + delta);
    const updated = await db.products.update(id, {
      quantity,
      updatedAt: todayISO(),
    });
    const suffix = reason ? ` (${reason})` : "";
    await logActivity(
      "Stok dəyişdi",
      `${current.name} ${delta > 0 ? "+" : ""}${delta}${suffix}`,
    );
    return updated;
  },
};

/** Yeni satış üçün giriş. */
export interface CreateSaleInput {
  productId: string;
  quantity: number;
  salePrice: number;
  discount: number;
  paymentType: PaymentType;
  customerId: string | null;
  note?: string;
}

export const saleHandlers = {
  list: () => db.sales.list(),

  /**
   * Satış biznes zənciri:
   * 1) stok yoxlaması, 2) stok azalır,
   * 3) Nisyədirsə müştəri borcu artır, 4) satış yazılır, 5) activity log.
   */
  async createSale(input: CreateSaleInput): Promise<Sale> {
    const product = await db.products.get(input.productId);
    if (!product) throw new Error("Mal tapılmadı");

    const qty = Math.max(1, Math.floor(input.quantity));
    if (qty > product.quantity) {
      throw new Error("Stokda kifayət qədər mal yoxdur");
    }

    const employeeId = useAuthStore.getState().user?.id ?? "emp_1";
    const subtotal = input.salePrice * qty;
    const discount = Math.max(0, input.discount);
    const net = Math.max(0, subtotal - discount);
    const profit = net - product.realCostPerUnit * qty;
    const isCredit = input.paymentType === "Nisyə";

    const sale: Sale = {
      id: uid("sal"),
      productId: product.id,
      productName: product.name,
      category: product.category,
      quantity: qty,
      salePrice: input.salePrice,
      subtotal,
      discount,
      totalAmount: net,
      paymentType: input.paymentType,
      customerId: isCredit ? input.customerId : null,
      costPerUnit: product.realCostPerUnit,
      profit,
      isManual: false,
      createdAt: todayISO(),
      employeeId,
    };

    // 2) Stok azalır
    await db.products.update(product.id, {
      quantity: Math.max(0, product.quantity - qty),
      updatedAt: todayISO(),
    });

    // 3) Nisyə → müştəri borcu yekun (endirimdən sonrakı) məbləğ qədər artır
    if (isCredit && sale.customerId) {
      const c = await db.customers.get(sale.customerId);
      if (c) {
        await db.customers.update(c.id, {
          totalDebt: c.totalDebt + net,
          remainingDebt: c.remainingDebt + net,
          lastPurchaseDate: todayISO(),
        });
      }
    }

    // 4) Satış yazılır
    await db.sales.create(sale);

    // 5) Activity log (endirim varsa ayrıca qeyd)
    await logActivity(
      isCredit ? "Nisyə satış etdi" : "Satış etdi",
      `${product.name} × ${qty} — ${fmtMoney(net)}`,
    );
    if (discount > 0) {
      await logActivity(
        "Endirim etdi",
        `${product.name} — ${fmtMoney(discount)} endirim`,
      );
    }

    return sale;
  },

  /** Müştəri ödənişi: borc azalır (0-dan aşağı düşmür) + qeyd + activity. */
  async addCustomerPayment(
    customerId: string,
    amount: number,
    note?: string,
  ): Promise<CustomerPayment> {
    const c = await db.customers.get(customerId);
    if (!c) throw new Error("Müştəri tapılmadı");

    await db.customers.update(customerId, {
      paidAmount: c.paidAmount + amount,
      remainingDebt: Math.max(0, c.remainingDebt - amount),
      lastPaymentDate: todayISO(),
    });

    const payment: CustomerPayment = {
      id: uid("pay"),
      customerId,
      amount,
      date: todayISO(),
      method: "Nağd",
      note,
    };
    await db.payments.create(payment);
    await logActivity(
      "Ödəniş aldı",
      `${c.name} — ${fmtMoney(amount)}${note ? ` (${note})` : ""}`,
    );
    return payment;
  },
};

/** Yeni təchizatçı üçün giriş. */
export interface NewSupplier {
  name: string;
  phone: string;
  note?: string;
}

export const supplierHandlers = {
  list: () => db.suppliers.list(),

  listPayments: async (supplierId: string): Promise<SupplierPayment[]> => {
    const all = await db.supplierPayments.list();
    return all.filter((p) => p.supplierId === supplierId);
  },

  async create(input: NewSupplier): Promise<Supplier> {
    const supplier: Supplier = {
      id: uid("sup"),
      name: input.name.trim(),
      phone: input.phone.trim(),
      totalDebt: 0,
      paidAmount: 0,
      remainingDebt: 0,
      itemCount: 0,
      lastPaymentDate: "",
    };
    await db.suppliers.create(supplier);
    await logActivity("Təchizatçı əlavə etdi", supplier.name);
    return supplier;
  },

  /** Mal alışı → mənim təchizatçıya borcum artır. */
  async addDebt(supplierId: string, amount: number): Promise<Supplier> {
    const s = await db.suppliers.get(supplierId);
    if (!s) throw new Error("Təchizatçı tapılmadı");
    const updated = await db.suppliers.update(supplierId, {
      totalDebt: s.totalDebt + amount,
      remainingDebt: s.remainingDebt + amount,
    });
    await logActivity(
      "Təchizatçı borcu artdı",
      `${s.name} — ${fmtMoney(amount)}`,
    );
    return updated;
  },

  /** Təchizatçıya ödəniş → borcum azalır (0-dan aşağı düşmür). */
  async addPayment(
    supplierId: string,
    amount: number,
  ): Promise<SupplierPayment> {
    const s = await db.suppliers.get(supplierId);
    if (!s) throw new Error("Təchizatçı tapılmadı");
    await db.suppliers.update(supplierId, {
      paidAmount: s.paidAmount + amount,
      remainingDebt: Math.max(0, s.remainingDebt - amount),
      lastPaymentDate: todayISO(),
    });
    const payment: SupplierPayment = {
      id: uid("spy"),
      supplierId,
      amount,
      date: todayISO(),
    };
    await db.supplierPayments.create(payment);
    await logActivity(
      "Təchizatçıya ödəniş etdi",
      `${s.name} — ${fmtMoney(amount)}`,
    );
    return payment;
  },
};

/** Yeni xərc üçün giriş. */
export interface NewExpense {
  title: string;
  category: ExpenseCategory;
  amount: number;
  date: string;
  productId: string | null;
  note?: string;
}

export const expenseHandlers = {
  list: () => db.expenses.list(),

  /**
   * Xərc yazılır. ƏSAS QAYDA: xərc bir mala bağlıdırsa (productId),
   * uyğun kateqoriya breakdown-una əlavə olunur və real maya YENİDƏN
   * hesablanır — beləcə malın real mayası artır.
   */
  async createExpense(input: NewExpense): Promise<Expense> {
    const expense: Expense = {
      id: uid("exp"),
      title: input.title.trim(),
      category: input.category,
      amount: input.amount,
      productId: input.productId || null,
      date: input.date,
      note: input.note ?? "",
    };
    await db.expenses.create(expense);

    if (expense.productId) {
      const p = await db.products.get(expense.productId);
      if (p) {
        // Kateqoriya adı birbaşa sərbəst xərc sətiri adına çevrilir (eyni ad cəmlənir).
        const name = expense.category;
        const existing = p.expenses.find(
          (e) => e.name.toLowerCase() === name.toLowerCase(),
        );
        const expenses = existing
          ? p.expenses.map((e) =>
              e.name.toLowerCase() === name.toLowerCase()
                ? { ...e, amount: e.amount + expense.amount }
                : e,
            )
          : [...p.expenses, { name, amount: expense.amount }];
        const realCostPerUnit = calcRealCostFromLines(
          p.purchasePrice,
          p.initialQuantity,
          expenses,
        );
        await db.products.update(p.id, {
          expenses,
          realCostPerUnit,
          updatedAt: todayISO(),
        });
      }
    }

    await logActivity(
      "Xərc əlavə etdi",
      `${expense.title} — ${fmtMoney(expense.amount)}`,
    );
    return expense;
  },
};

export const closingHandlers = {
  list: () => db.closings.list(),

  /** Günü bağlayır. Eyni tarix üçün ikinci bağlanışa icazə vermir. */
  async closeDay(input: Omit<Closing, "id">): Promise<Closing> {
    const existing = await db.closings.list();
    if (existing.some((c) => c.date === input.date)) {
      throw new Error("Bu gün artıq bağlanıb");
    }
    const closing: Closing = { ...input, id: uid("cls") };
    await db.closings.create(closing);
    await logActivity(
      "Gün sonu bağladı",
      `${input.date} — fərq: ${fmtMoney(input.difference)}`,
    );
    return closing;
  },
};
