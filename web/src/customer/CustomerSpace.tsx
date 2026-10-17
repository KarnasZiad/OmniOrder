import { useMemo, useState } from 'react';
import { Catalog } from './Catalog';
import { Checkout } from './Checkout';
import { MyOrders } from './MyOrders';
import type { CartLine, Product, Store } from '../types';

interface Props {
  products: Product[];
  stores: Store[];
}

const EMAIL_KEY = 'omniorder.email';

const readEmail = () => {
  try {
    return localStorage.getItem(EMAIL_KEY) ?? '';
  } catch {
    return '';
  }
};

export function CustomerSpace({ products, stores }: Props) {
  const [cart, setCart] = useState<CartLine[]>([]);
  const [email, setEmail] = useState(readEmail);
  const [lastOrderId, setLastOrderId] = useState<string | null>(null);

  const add = (product: Product) =>
    setCart((lines) => {
      const existing = lines.find((l) => l.product.sku === product.sku);
      return existing
        ? lines.map((l) => (l === existing ? { ...l, quantity: Math.min(10, l.quantity + 1) } : l))
        : [...lines, { product, quantity: 1 }];
    });

  const setQuantity = (sku: string, quantity: number) =>
    setCart((lines) => (quantity <= 0 ? lines.filter((l) => l.product.sku !== sku) : lines.map((l) => (l.product.sku === sku ? { ...l, quantity } : l))));

  const storeById = useMemo(() => new Map(stores.map((s) => [s.id, s])), [stores]);

  const ordered = (orderId: string, customerEmail: string) => {
    setCart([]);
    setLastOrderId(orderId);
    setEmail(customerEmail);
    try {
      localStorage.setItem(EMAIL_KEY, customerEmail);
    } catch {
      // storage unavailable: the order list simply won't be remembered
    }
  };

  return (
    <div className="customer">
      <Catalog products={products} onAdd={add} />
      <aside className="side">
        <Checkout cart={cart} onQuantity={setQuantity} defaultEmail={email} onOrdered={ordered} />
        <MyOrders email={email} highlight={lastOrderId} storeById={storeById} />
      </aside>
    </div>
  );
}
