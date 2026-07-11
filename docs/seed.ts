/** Başlanğıc mock data — MVP-dəki realistik verilənlərdən köçürülüb. */
import { uid, todayISO, daysAgoISO, fmtDate } from "@/lib/format";
import { calcRealCost } from "@/features/products/lib";
import type {
  Product,
  Sale,
  Customer,
  Supplier,
  Employee,
  Expense,
  CustomerPayment,
  SupplierPayment,
  Activity,
  Closing,
  PaymentType,
} from "@/types";

/** Seed strukturu dəyişəndə bu nömrəni artırın → localStorage yenilənir. */
export const SEED_VERSION = 2;

export interface SeedDatabase {
  products: Product[];
  sales: Sale[];
  customers: Customer[];
  suppliers: Supplier[];
  expenses: Expense[];
  employees: Employee[];
  closings: Closing[];
  activity: Activity[];
  payments: CustomerPayment[];
  supplierPayments: SupplierPayment[];
}

const buildSuppliers = (): Supplier[] =>
  [
    {
      id: "sup_1",
      name: "İstanbul Tekstil (Laleli)",
      phone: "+994502223344",
      totalDebt: 8400,
      paidAmount: 5400,
      itemCount: 6,
    },
    {
      id: "sup_2",
      name: "Guangzhou Ayaqqabı MMC",
      phone: "+994515556677",
      totalDebt: 12200,
      paidAmount: 9000,
      itemCount: 4,
    },
    {
      id: "sup_3",
      name: "Bakı Toptan Aksesuar",
      phone: "+994703334455",
      totalDebt: 1500,
      paidAmount: 1500,
      itemCount: 3,
    },
    {
      id: "sup_4",
      name: "Merter Cins Toptan",
      phone: "+994554447788",
      totalDebt: 6300,
      paidAmount: 2300,
      itemCount: 2,
    },
  ].map((s) => ({
    ...s,
    remainingDebt: s.totalDebt - s.paidAmount,
    lastPaymentDate: daysAgoISO(6),
  }));

interface RawProduct {
  name: string;
  category: string;
  size: string;
  color: string;
  model: string;
  purchasePrice: number;
  salePrice: number;
  quantity: number;
  initialQuantity: number;
  minStock: number;
  supplierId: string;
  location: string;
  expenses: Product["expenses"];
  createdAt: string;
}

const rawProducts: RawProduct[] = [
  {
    name: "Kişi cins şalvar Slim",
    category: "Şalvar",
    size: "30-38",
    color: "Tünd göy",
    model: "MNG-armani",
    purchasePrice: 14,
    salePrice: 25,
    quantity: 84,
    initialQuantity: 120,
    minStock: 20,
    supplierId: "sup_4",
    location: "Anbar A / Rəf 3 / Qutu 12",
    expenses: { yol: 240, fehle: 60, yer: 50, paket: 30, diger: 0 },
    createdAt: daysAgoISO(24),
  },
  {
    name: "Qadın bluz ipək",
    category: "Bluz",
    size: "S-XL",
    color: "Bej",
    model: "Zara style",
    purchasePrice: 8,
    salePrice: 18,
    quantity: 12,
    initialQuantity: 80,
    minStock: 15,
    supplierId: "sup_1",
    location: "Anbar A / Rəf 1 / Qutu 4",
    expenses: { yol: 160, fehle: 40, yer: 30, paket: 20, diger: 0 },
    createdAt: daysAgoISO(18),
  },
  {
    name: "İdman ayaqqabısı AirMax",
    category: "Ayaqqabı",
    size: "40-45",
    color: "Qara/Ağ",
    model: "N-Air replika",
    purchasePrice: 22,
    salePrice: 45,
    quantity: 46,
    initialQuantity: 60,
    minStock: 10,
    supplierId: "sup_2",
    location: "Anbar B / Rəf 2 / Qutu 7",
    expenses: { yol: 300, fehle: 60, yer: 60, paket: 40, diger: 20 },
    createdAt: daysAgoISO(15),
  },
  {
    name: "Uşaq kombinzon qış",
    category: "Uşaq geyimi",
    size: "2-7 yaş",
    color: "Qırmızı",
    model: "WinterKids",
    purchasePrice: 16,
    salePrice: 32,
    quantity: 0,
    initialQuantity: 40,
    minStock: 8,
    supplierId: "sup_1",
    location: "Anbar A / Rəf 5 / Qutu 2",
    expenses: { yol: 120, fehle: 30, yer: 20, paket: 10, diger: 0 },
    createdAt: daysAgoISO(40),
  },
  {
    name: "Qadın çanta dəri",
    category: "Aksesuar",
    size: "Standart",
    color: "Qəhvəyi",
    model: "LV style",
    purchasePrice: 12,
    salePrice: 28,
    quantity: 34,
    initialQuantity: 50,
    minStock: 10,
    supplierId: "sup_3",
    location: "Mağaza / Vitrin 1",
    expenses: { yol: 90, fehle: 25, yer: 20, paket: 15, diger: 0 },
    createdAt: daysAgoISO(12),
  },
  {
    name: "Kişi köynək klassik",
    category: "Köynək",
    size: "M-XXL",
    color: "Ağ",
    model: "Classic-FIT",
    purchasePrice: 9,
    salePrice: 17,
    quantity: 95,
    initialQuantity: 100,
    minStock: 20,
    supplierId: "sup_1",
    location: "Anbar A / Rəf 2 / Qutu 9",
    expenses: { yol: 180, fehle: 45, yer: 30, paket: 25, diger: 0 },
    createdAt: daysAgoISO(65),
  },
  {
    name: "Qış gödəkçəsi kişi",
    category: "Gödəkçə",
    size: "L-XXL",
    color: "Qara",
    model: "NorthStyle",
    purchasePrice: 35,
    salePrice: 33,
    quantity: 28,
    initialQuantity: 35,
    minStock: 6,
    supplierId: "sup_1",
    location: "Anbar B / Rəf 4 / Qutu 1",
    expenses: { yol: 200, fehle: 50, yer: 40, paket: 30, diger: 0 },
    createdAt: daysAgoISO(70),
  },
  {
    name: "Qadın idman dəsti",
    category: "İdman",
    size: "S-L",
    color: "Çəhrayı",
    model: "FitSet",
    purchasePrice: 13,
    salePrice: 27,
    quantity: 8,
    initialQuantity: 45,
    minStock: 10,
    supplierId: "sup_4",
    location: "Anbar A / Rəf 6 / Qutu 3",
    expenses: { yol: 110, fehle: 30, yer: 25, paket: 15, diger: 0 },
    createdAt: daysAgoISO(9),
  },
  {
    name: "Uşaq krossovka LED",
    category: "Ayaqqabı",
    size: "25-34",
    color: "Göy",
    model: "KidsLight",
    purchasePrice: 10,
    salePrice: 22,
    quantity: 52,
    initialQuantity: 55,
    minStock: 12,
    supplierId: "sup_2",
    location: "Anbar B / Rəf 1 / Qutu 5",
    expenses: { yol: 140, fehle: 35, yer: 25, paket: 20, diger: 0 },
    createdAt: daysAgoISO(95),
  },
  {
    name: "Kəmər dəri kişi",
    category: "Aksesuar",
    size: "Universal",
    color: "Qara",
    model: "BeltPro",
    purchasePrice: 4,
    salePrice: 10,
    quantity: 140,
    initialQuantity: 150,
    minStock: 30,
    supplierId: "sup_3",
    location: "Mağaza / Vitrin 2",
    expenses: { yol: 45, fehle: 15, yer: 10, paket: 10, diger: 0 },
    createdAt: daysAgoISO(35),
  },
];

const parseLocation = (location: string) => {
  const [store = "", shelfPart = "", boxPart = ""] = location.split(" / ");
  return {
    store,
    warehouse: store,
    shelf: shelfPart.replace("Rəf ", ""),
    box: boxPart.replace("Qutu ", ""),
  };
};

const buildProducts = (): Product[] =>
  rawProducts.map((p, i) => {
    const realCostPerUnit = calcRealCost(
      p.purchasePrice,
      p.initialQuantity,
      p.expenses,
    );
    const loc = parseLocation(p.location);
    return {
      ...p,
      id: `prd_${i + 1}`,
      barcode: `SDK${String(1000 + i + 1)}`,
      currency: "AZN",
      image: "",
      note: "",
      realCostPerUnit,
      updatedAt: p.createdAt,
      ...loc,
    };
  });

const buildCustomers = (): Customer[] =>
  [
    {
      id: "cus_1",
      name: "Rəşad Məmmədov (Bina bazar)",
      phone: "994501112233",
      totalDebt: 1240,
      paidAmount: 800,
      lastPurchaseDate: daysAgoISO(2),
      lastPaymentDate: daysAgoISO(5),
    },
    {
      id: "cus_2",
      name: "Aygün Əliyeva",
      phone: "994552223344",
      totalDebt: 380,
      paidAmount: 380,
      lastPurchaseDate: daysAgoISO(11),
      lastPaymentDate: daysAgoISO(3),
    },
    {
      id: "cus_3",
      name: "Elvin Quliyev (8-ci km)",
      phone: "994703334455",
      totalDebt: 2150,
      paidAmount: 900,
      lastPurchaseDate: daysAgoISO(1),
      lastPaymentDate: daysAgoISO(14),
    },
    {
      id: "cus_4",
      name: "Nigar Həsənova",
      phone: "994514445566",
      totalDebt: 560,
      paidAmount: 100,
      lastPurchaseDate: daysAgoISO(28),
      lastPaymentDate: daysAgoISO(28),
    },
  ].map((c) => ({ ...c, remainingDebt: c.totalDebt - c.paidAmount }));

const buildEmployees = (): Employee[] => [
  {
    id: "emp_1",
    name: "Kamran Vəliyev",
    phone: "+994501234567",
    role: "Sahibkar",
    status: "Aktiv",
  },
  {
    id: "emp_2",
    name: "Səbinə Rüstəmova",
    phone: "+994557654321",
    role: "Menecer",
    status: "Aktiv",
  },
  {
    id: "emp_3",
    name: "Tural Abbasov",
    phone: "+994708889900",
    role: "Satıcı",
    status: "Aktiv",
  },
  {
    id: "emp_4",
    name: "Orxan Nəbiyev",
    phone: "+994515550011",
    role: "Satıcı",
    status: "Deaktiv",
  },
];

/** Son 30 günün + bugünkü satış tarixçəsi generatoru. */
const buildSales = (
  products: Product[],
  customers: Customer[],
  employees: Employee[],
): Sale[] => {
  const sales: Sale[] = [];
  const picks: [number, number][] = [
    [0, 3],
    [2, 1],
    [5, 4],
    [4, 2],
    [9, 6],
    [1, 2],
    [8, 2],
    [7, 1],
    [2, 2],
    [0, 2],
  ];
  const payCycle: PaymentType[] = ["Nağd", "Kart", "Nağd", "Nisyə", "Nağd", "Kart"];

  for (let d = 29; d >= 1; d--) {
    const n = 1 + ((d * 7) % 3);
    for (let k = 0; k < n; k++) {
      const [pi, q] = picks[(d + k) % picks.length];
      const p = products[pi];
      const pay = payCycle[(d + k) % 6];
      const price = p.salePrice;
      const subtotal = price * q;
      sales.push({
        id: uid("sal"),
        productId: p.id,
        productName: p.name,
        quantity: q,
        salePrice: price,
        subtotal,
        discount: 0,
        totalAmount: subtotal,
        paymentType: pay,
        customerId: pay === "Nisyə" ? customers[(d + k) % 3].id : null,
        profit: (price - p.realCostPerUnit) * q,
        createdAt: daysAgoISO(d),
        employeeId: employees[(d + k) % 3].id,
      });
    }
  }

  const today: {
    pi: number;
    q: number;
    pay: PaymentType;
    cus?: string;
    emp: string;
  }[] = [
    { pi: 0, q: 2, pay: "Nağd", emp: "emp_3" },
    { pi: 2, q: 1, pay: "Kart", emp: "emp_2" },
    { pi: 5, q: 3, pay: "Nağd", emp: "emp_3" },
    { pi: 4, q: 1, pay: "Nisyə", cus: "cus_1", emp: "emp_2" },
    { pi: 9, q: 4, pay: "Nağd", emp: "emp_3" },
  ];
  today.forEach(({ pi, q, pay, cus, emp }) => {
    const p = products[pi];
    const subtotal = p.salePrice * q;
    sales.push({
      id: uid("sal"),
      productId: p.id,
      productName: p.name,
      quantity: q,
      salePrice: p.salePrice,
      subtotal,
      discount: 0,
      totalAmount: subtotal,
      paymentType: pay,
      customerId: cus || null,
      profit: (p.salePrice - p.realCostPerUnit) * q,
      createdAt: todayISO(),
      employeeId: emp,
    });
  });
  return sales;
};

const buildExpenses = (): Expense[] => [
  {
    id: uid("exp"),
    title: "Sərnişin yükdaşıma (İstanbul karqo)",
    category: "Yol",
    amount: 240,
    productId: "prd_1",
    date: daysAgoISO(24),
    note: "120 ədəd şalvar partiyası",
  },
  {
    id: uid("exp"),
    title: "Hambal pulu",
    category: "Fəhlə",
    amount: 45,
    productId: null,
    date: daysAgoISO(4),
    note: "",
  },
  {
    id: uid("exp"),
    title: "Mağaza icarəsi (aylıq pay)",
    category: "Mağaza",
    amount: 600,
    productId: null,
    date: daysAgoISO(7),
    note: "İyul ayı",
  },
  {
    id: uid("exp"),
    title: "Sellofan paket 500 əd.",
    category: "Paket/Qutu",
    amount: 35,
    productId: null,
    date: daysAgoISO(3),
    note: "",
  },
  {
    id: uid("exp"),
    title: "Anbar yeri kirayəsi",
    category: "Anbar/Yer",
    amount: 180,
    productId: null,
    date: daysAgoISO(10),
    note: "Anbar B",
  },
  {
    id: uid("exp"),
    title: "Çay-su, təsərrüfat",
    category: "Digər",
    amount: 25,
    productId: null,
    date: todayISO(),
    note: "",
  },
  {
    id: uid("exp"),
    title: "Karqo çatdırılma",
    category: "Yol",
    amount: 60,
    productId: "prd_8",
    date: todayISO(),
    note: "İdman dəsti əlavə partiya",
  },
];

const buildPayments = (): CustomerPayment[] => [
  { id: uid("pay"), customerId: "cus_1", amount: 300, date: daysAgoISO(5), method: "Nağd" },
  { id: uid("pay"), customerId: "cus_2", amount: 380, date: daysAgoISO(3), method: "Kart" },
  { id: uid("pay"), customerId: "cus_3", amount: 500, date: daysAgoISO(14), method: "Nağd" },
  { id: uid("pay"), customerId: "cus_1", amount: 500, date: daysAgoISO(12), method: "Nağd" },
];

const buildSupplierPayments = (): SupplierPayment[] => [
  { id: uid("spy"), supplierId: "sup_1", amount: 2000, date: daysAgoISO(6) },
  { id: uid("spy"), supplierId: "sup_2", amount: 3000, date: daysAgoISO(9) },
];

const buildActivity = (): Activity[] => [
  {
    id: uid("act"),
    employeeId: "emp_3",
    action: "Satış etdi",
    detail: "Kəmər dəri kişi × 4 — 40.00 AZN",
    date: todayISO(),
  },
  {
    id: uid("act"),
    employeeId: "emp_2",
    action: "Nisyə satış etdi",
    detail: "Qadın çanta dəri × 1 — Rəşad Məmmədov",
    date: todayISO(),
  },
  {
    id: uid("act"),
    employeeId: "emp_2",
    action: "Mal əlavə etdi",
    detail: "Qadın idman dəsti — 45 ədəd",
    date: daysAgoISO(9),
  },
  {
    id: uid("act"),
    employeeId: "emp_1",
    action: "Gün sonu bağladı",
    detail: `${fmtDate(daysAgoISO(1))} — fərq: 0.00 AZN`,
    date: daysAgoISO(1),
  },
  {
    id: uid("act"),
    employeeId: "emp_3",
    action: "Endirim etdi",
    detail: "İdman ayaqqabısı — 5 AZN endirim",
    date: daysAgoISO(2),
  },
  {
    id: uid("act"),
    employeeId: "emp_2",
    action: "Stok dəyişdi",
    detail: "Kişi köynək klassik +20",
    date: daysAgoISO(6),
  },
];

const buildClosings = (): Closing[] => [
  {
    id: uid("cls"),
    date: daysAgoISO(2),
    openingCash: 350,
    cashSales: 412,
    cardSales: 145,
    creditSales: 90,
    expenses: 80,
    expectedCash: 682,
    actualCash: 680,
    difference: -2,
  },
  {
    id: uid("cls"),
    date: daysAgoISO(1),
    openingCash: 400,
    cashSales: 388,
    cardSales: 210,
    creditSales: 0,
    expenses: 45,
    expectedCash: 743,
    actualCash: 743,
    difference: 0,
  },
];

/** Bütün seed datanı bir obyektdə qaytarır. */
export const buildSeed = (): SeedDatabase => {
  const suppliers = buildSuppliers();
  const products = buildProducts();
  const customers = buildCustomers();
  const employees = buildEmployees();
  const sales = buildSales(products, customers, employees);
  return {
    products,
    sales,
    customers,
    suppliers,
    expenses: buildExpenses(),
    employees,
    closings: buildClosings(),
    activity: buildActivity(),
    payments: buildPayments(),
    supplierPayments: buildSupplierPayments(),
  };
};
