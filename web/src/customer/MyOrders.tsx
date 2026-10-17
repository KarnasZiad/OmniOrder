import { useState } from 'react';
import { api } from '../api';
import { OrderTimeline } from '../components/OrderTimeline';
import { StatusBadge } from '../components/StatusBadge';
import { usePolling } from '../usePolling';
import { euro, modeLabels, paymentLabels, type Order, type Store } from '../types';

interface Props {
  email: string;
  highlight: string | null;
  storeById: Map<string, Store>;
}

const date = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'short', timeStyle: 'short' });

export function MyOrders({ email, highlight, storeById }: Props) {
  const [orders, setOrders] = useState<Order[]>([]);

  // The order status changes asynchronously (Inventory answers through RabbitMQ): poll.
  usePolling(async () => {
    try {
      setOrders(await api.customerOrders(email));
    } catch {
      // keep the last known list
    }
  }, 2500, email.length > 0);

  if (!email) return null;

  return (
    <section className="card">
      <h2>Mes commandes</h2>
      {orders.length === 0 && <p className="empty">Aucune commande pour {email}.</p>}
      <ul className="orders">
        {orders.slice(0, 6).map((o) => (
          <li key={o.id} className={o.id === highlight ? 'highlight' : ''}>
            <div className="order-head">
              <strong>{o.number}</strong>
              <StatusBadge status={o.status} />
            </div>
            <p className="muted">
              {modeLabels[o.mode]} · {storeById.get(o.storeId)?.name} · {euro.format(o.total)} · {paymentLabels[o.payment]}
            </p>
            <OrderTimeline value={o} />
            {o.pickupDeadline && !['Collected', 'Cancelled', 'Rejected', 'Expired'].includes(o.status) && (
              <p className="muted">À retirer avant le {date.format(new Date(o.pickupDeadline))}</p>
            )}
            {o.statusReason && <p className="muted">{o.statusReason}</p>}
            {['Pending', 'Confirmed', 'Reserved', 'ReadyForPickup'].includes(o.status) && (
              <button type="button" className="link danger" onClick={() => api.cancel(o.id, 'Annulée par le client')}>
                Annuler
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
