import type { FulfillmentMode, Order, OrderStatus, Product, StockLevel, Store, StoreAvailability } from './types';

const BASE = import.meta.env.VITE_API_URL ?? '/api';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  });

  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const problem = await response.json();
      message = problem.detail ?? problem.title ?? message;
    } catch {
      // not JSON
    }
    throw new Error(message);
  }

  return (await response.json()) as T;
}

const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: 'POST', body: JSON.stringify(body ?? {}) });

export const api = {
  products: () => request<Product[]>('/catalog/products'),
  stores: () => request<Store[]>('/inventory/stores'),
  availability: (skus: string[]) =>
    request<StoreAvailability[]>(`/inventory/availability?${skus.map((s) => `sku=${encodeURIComponent(s)}`).join('&')}`),
  storeStock: (storeId: string) => request<StockLevel[]>(`/inventory/stores/${storeId}/stock`),
  receive: (storeId: string, sku: string, quantity: number) =>
    post<StockLevel>(`/inventory/stores/${storeId}/stock/${sku}/receipts`, { quantity }),

  placeOrder: (body: {
    storeId: string;
    mode: FulfillmentMode;
    customerName: string;
    customerEmail: string;
    lines: { sku: string; quantity: number }[];
  }) => post<Order>('/ordering/orders', body),
  order: (id: string) => request<Order>(`/ordering/orders/${id}`),
  customerOrders: (email: string) => request<Order[]>(`/ordering/orders?customerEmail=${encodeURIComponent(email)}`),
  storeOrders: (storeId: string, status?: OrderStatus) =>
    request<Order[]>(`/ordering/orders?storeId=${storeId}${status ? `&status=${status}` : ''}`),
  markReady: (id: string) => post<Order>(`/ordering/orders/${id}/ready`),
  collect: (id: string) => post<Order>(`/ordering/orders/${id}/collect`),
  cancel: (id: string, reason: string) => post<Order>(`/ordering/orders/${id}/cancel`, { reason }),
};
