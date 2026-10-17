import { useEffect, useState } from 'react';
import { api } from '../api';
import { StatusBadge } from '../components/StatusBadge';
import { usePolling } from '../usePolling';
import { euro, finalStatuses, modeLabels, paymentLabels, type Order, type Product, type StockLevel, type Store } from '../types';

interface Props {
  products: Product[];
  stores: Store[];
}

const time = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'short', timeStyle: 'short' });

export function StaffSpace({ products, stores }: Props) {
  const [storeId, setStoreId] = useState('');
  const [orders, setOrders] = useState<Order[]>([]);
  const [stock, setStock] = useState<StockLevel[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!storeId && stores.length > 0) setStoreId(stores[0].id);
  }, [stores, storeId]);

  const refresh = async () => {
    if (!storeId) return;
    const [o, s] = await Promise.all([api.storeOrders(storeId), api.storeStock(storeId)]);
    setOrders(o);
    setStock(s);
  };

  usePolling(() => refresh().catch(() => undefined), 3000, storeId.length > 0);

  const act = async (operation: () => Promise<unknown>) => {
    setError(null);
    try {
      await operation();
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const columns: { title: string; hint: string; filter: (o: Order) => boolean }[] = [
    { title: 'À préparer', hint: 'stock réservé, à mettre de côté', filter: (o) => o.status === 'Confirmed' || o.status === 'Reserved' },
    { title: 'Prêtes au retrait', hint: 'en attente du client', filter: (o) => o.status === 'ReadyForPickup' },
    { title: 'En attente de stock', hint: 'réservation en cours', filter: (o) => o.status === 'Pending' },
    { title: 'Terminées', hint: 'retirées, annulées, expirées', filter: (o) => finalStatuses.includes(o.status) },
  ];

  const productName = (sku: string) => products.find((p) => p.sku === sku)?.name ?? sku;

  return (
    <div className="staff">
      <div className="toolbar">
        <label>
          Magasin
          <select value={storeId} onChange={(e) => setStoreId(e.target.value)}>
            {stores.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </label>
        {error && <span className="error">{error}</span>}
      </div>

      <div className="board">
        {columns.map((column) => {
          const items = orders.filter(column.filter).slice(0, 10);
          return (
            <section key={column.title} className="column">
              <h2>
                {column.title} <span className="count">{items.length}</span>
              </h2>
              <p className="muted">{column.hint}</p>
              {items.map((o) => (
                <article key={o.id} className="ticket">
                  <div className="order-head">
                    <strong>{o.number}</strong>
                    <span className={`mode mode-${o.mode}`}>{modeLabels[o.mode]}</span>
                  </div>
                  <p>{o.customerName}</p>
                  <ul>
                    {o.items.map((i) => (
                      <li key={i.sku}>
                        {i.quantity} × {i.productName}
                      </li>
                    ))}
                  </ul>
                  <p className="muted">
                    {euro.format(o.total)} · {paymentLabels[o.payment]}
                  </p>
                  {o.pickupDeadline && !finalStatuses.includes(o.status) && <p className="muted">Limite : {time.format(new Date(o.pickupDeadline))}</p>}
                  <StatusBadge status={o.status} />
                  <div className="ticket-actions">
                    {(o.status === 'Confirmed' || o.status === 'Reserved') && (
                      <button type="button" onClick={() => act(() => api.markReady(o.id))}>
                        Marquer prête
                      </button>
                    )}
                    {o.status === 'ReadyForPickup' && (
                      <button type="button" onClick={() => act(() => api.collect(o.id))}>
                        {o.payment === 'DueInStore' ? 'Encaisser et remettre' : 'Remettre au client'}
                      </button>
                    )}
                    {!finalStatuses.includes(o.status) && (
                      <button type="button" className="link danger" onClick={() => act(() => api.cancel(o.id, 'Annulée par le magasin'))}>
                        Annuler
                      </button>
                    )}
                  </div>
                </article>
              ))}
            </section>
          );
        })}
      </div>

      <section className="card">
        <h2>Stock du magasin</h2>
        <table className="stock">
          <thead>
            <tr>
              <th>Produit</th>
              <th>En stock</th>
              <th>Réservé</th>
              <th>Disponible</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {stock.map((s) => (
              <tr key={s.sku} className={s.available === 0 ? 'empty-row' : ''}>
                <td>
                  {productName(s.sku)} <span className="muted">{s.sku}</span>
                </td>
                <td>{s.onHand}</td>
                <td>{s.reserved}</td>
                <td>
                  <strong>{s.available}</strong>
                </td>
                <td>
                  <button type="button" className="link" onClick={() => act(() => api.receive(storeId, s.sku, 5))}>
                    +5 réassort
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
