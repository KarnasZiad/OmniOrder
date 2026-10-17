import { useEffect, useState } from 'react';
import { api } from '../api';
import { euro, modeLabels, type CartLine, type FulfillmentMode, type StoreAvailability } from '../types';

interface Props {
  cart: CartLine[];
  onQuantity: (sku: string, quantity: number) => void;
  defaultEmail: string;
  onOrdered: (orderId: string, email: string) => void;
}

const modeHelp: Record<FulfillmentMode, string> = {
  ClickAndCollect: 'Payez en ligne, le magasin prépare votre commande (réservée 3 jours).',
  ReserveInStore: 'Rien à payer maintenant : l’article vous attend 48 h, vous payez au retrait.',
};

export function Checkout({ cart, onQuantity, defaultEmail, onOrdered }: Props) {
  const [mode, setMode] = useState<FulfillmentMode>('ClickAndCollect');
  const [availability, setAvailability] = useState<StoreAvailability[]>([]);
  const [storeId, setStoreId] = useState('');
  const [name, setName] = useState('');
  const [email, setEmail] = useState(defaultEmail);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const skus = cart.map((l) => l.product.sku).join(',');
  useEffect(() => {
    if (!skus) {
      setAvailability([]);
      return;
    }
    api.availability(skus.split(',')).then(setAvailability).catch(() => setAvailability([]));
  }, [skus]);

  const canFulfil = (store: StoreAvailability) =>
    cart.every((line) => (store.items.find((i) => i.sku === line.product.sku)?.available ?? 0) >= line.quantity);

  const total = cart.reduce((sum, l) => sum + l.product.price * l.quantity, 0);

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const order = await api.placeOrder({
        storeId,
        mode,
        customerName: name,
        customerEmail: email,
        lines: cart.map((l) => ({ sku: l.product.sku, quantity: l.quantity })),
      });
      onOrdered(order.id, email);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (cart.length === 0) {
    return (
      <section className="card">
        <h2>Panier</h2>
        <p className="empty">Votre panier est vide. Ajoutez des articles depuis le catalogue.</p>
      </section>
    );
  }

  return (
    <section className="card">
      <h2>Panier</h2>
      <ul className="cart">
        {cart.map((l) => (
          <li key={l.product.sku}>
            <span>{l.product.name}</span>
            <span className="qty">
              <button type="button" className="ghost" onClick={() => onQuantity(l.product.sku, l.quantity - 1)} aria-label="Retirer un">
                −
              </button>
              {l.quantity}
              <button type="button" className="ghost" onClick={() => onQuantity(l.product.sku, Math.min(10, l.quantity + 1))} aria-label="Ajouter un">
                +
              </button>
            </span>
            <strong>{euro.format(l.product.price * l.quantity)}</strong>
          </li>
        ))}
      </ul>
      <div className="total">
        Total <strong>{euro.format(total)}</strong>
      </div>

      <fieldset className="modes">
        <legend>Mode de retrait</legend>
        {(Object.keys(modeLabels) as FulfillmentMode[]).map((m) => (
          <label key={m} className={mode === m ? 'mode active' : 'mode'}>
            <input type="radio" name="mode" checked={mode === m} onChange={() => setMode(m)} />
            <span>
              <strong>{modeLabels[m]}</strong>
              <small>{modeHelp[m]}</small>
            </span>
          </label>
        ))}
      </fieldset>

      <fieldset className="stores">
        <legend>Magasin de retrait</legend>
        {availability.map((s) => {
          const ok = canFulfil(s);
          return (
            <label key={s.storeId} className={`store ${ok ? '' : 'unavailable'} ${storeId === s.storeId ? 'active' : ''}`}>
              <input type="radio" name="store" disabled={!ok} checked={storeId === s.storeId} onChange={() => setStoreId(s.storeId)} />
              <span>
                <strong>{s.name}</strong>
                <small>{ok ? 'Tout est disponible' : 'Stock insuffisant pour ce panier'}</small>
              </span>
            </label>
          );
        })}
      </fieldset>

      <div className="form">
        <input placeholder="Nom" value={name} onChange={(e) => setName(e.target.value)} />
        <input placeholder="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
      </div>
      <button type="button" className="primary wide" disabled={busy || !storeId || !name || !email} onClick={submit}>
        {mode === 'ClickAndCollect' ? `Payer ${euro.format(total)} et commander` : 'Réserver en magasin'}
      </button>
      {error && <p className="error">{error}</p>}
    </section>
  );
}
