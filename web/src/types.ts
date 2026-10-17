export type FulfillmentMode = 'ClickAndCollect' | 'ReserveInStore';

export type OrderStatus =
  | 'Pending'
  | 'Confirmed'
  | 'Reserved'
  | 'ReadyForPickup'
  | 'Collected'
  | 'Rejected'
  | 'Cancelled'
  | 'Expired';

export type PaymentStatus = 'Captured' | 'Refunded' | 'DueInStore' | 'PaidInStore' | 'None';

export interface Product {
  id: string;
  sku: string;
  name: string;
  description: string;
  category: string;
  price: number;
}

export interface Store {
  id: string;
  code: string;
  name: string;
  city: string;
  address: string;
}

export interface StoreAvailability {
  storeId: string;
  code: string;
  name: string;
  city: string;
  items: { sku: string; available: number }[];
}

export interface OrderItem {
  sku: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  number: string;
  storeId: string;
  mode: FulfillmentMode;
  customerName: string;
  customerEmail: string;
  status: OrderStatus;
  payment: PaymentStatus;
  total: number;
  statusReason?: string | null;
  createdAt: string;
  updatedAt: string;
  pickupDeadline?: string | null;
  items: OrderItem[];
}

export interface StockLevel {
  sku: string;
  onHand: number;
  reserved: number;
  available: number;
}

export interface CartLine {
  product: Product;
  quantity: number;
}

export const statusLabels: Record<OrderStatus, string> = {
  Pending: 'Vérification du stock',
  Confirmed: 'Confirmée, en préparation',
  Reserved: 'Réservée en magasin',
  ReadyForPickup: 'Prête à retirer',
  Collected: 'Retirée',
  Rejected: 'Refusée (stock insuffisant)',
  Cancelled: 'Annulée',
  Expired: 'Expirée (non retirée)',
};

export const modeLabels: Record<FulfillmentMode, string> = {
  ClickAndCollect: 'Click & collect',
  ReserveInStore: 'Réserver en magasin',
};

export const paymentLabels: Record<PaymentStatus, string> = {
  Captured: 'Payée en ligne',
  Refunded: 'Remboursée',
  DueInStore: 'À payer en magasin',
  PaidInStore: 'Payée en magasin',
  None: 'Aucun paiement',
};

export const finalStatuses: OrderStatus[] = ['Collected', 'Rejected', 'Cancelled', 'Expired'];

export const euro = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR' });

export const categoryIcon = (category: string) =>
  ({ Textile: '👕', Chaussures: '👟', Accessoires: '👜' })[category] ?? '🛍️';
